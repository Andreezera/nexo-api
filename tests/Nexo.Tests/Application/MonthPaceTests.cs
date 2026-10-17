using Moq;
using Nexo.Application.Abstractions;
using Nexo.Application.Conversation;
using Nexo.Application.Reports;
using Nexo.Domain.Entities;
using Nexo.Domain.Enums;

namespace Nexo.Tests.Application;

public class MonthPaceTests
{
    private readonly User _user = new("5511966665555");
    private readonly Mock<ITransactionRepository> _transactions = new();
    private readonly Mock<IRecurringTransactionRepository> _recurring = new();

    // 20/11/2026: o mês tem 30 dias, restam 11 contando hoje.
    private static readonly DateTime Today = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

    private static DateTime D(int month, int day) => new(2026, month, day, 0, 0, 0, DateTimeKind.Utc);

    private Transaction Income(decimal amount, DateTime when) =>
        new(_user.Id, TransactionType.Income, amount, TransactionCategory.Salario, "salário", when);

    private Transaction Expense(decimal amount, DateTime when, TransactionCategory category = TransactionCategory.Mercado) =>
        new(_user.Id, TransactionType.Expense, amount, category, "gasto", when);

    private MonthPaceService Create(Transaction[] transactions, params RecurringTransaction[] recurring)
    {
        _transactions.Setup(t => t.GetByUserIdAsync(_user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(transactions);
        _recurring.Setup(r => r.GetActiveByUserIdAsync(_user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(recurring);
        return new MonthPaceService(_transactions.Object, _recurring.Object);
    }

    [Fact]
    public async Task ComputesAvailablePerDayAndProjectedClose()
    {
        var service = Create(new[] { Income(3000m, D(11, 5)), Expense(600m, D(11, 10)), Expense(400m, D(11, 15)) });

        var pace = await service.GetAsync(_user.Id, Today);

        Assert.Equal(3000m, pace.ExpectedIncome);
        Assert.Equal(1000m, pace.ExpenseSoFar);
        Assert.Equal(2000m, pace.AvailableToSpend);
        Assert.Equal(11, pace.DaysLeft);
        Assert.Equal(181.82m, pace.AvailablePerDay);
        // 1000 em 20 dias = 50 por dia, e restam 10 dias depois de hoje.
        Assert.Equal(1500m, pace.ProjectedExpense);
        Assert.False(pace.TooEarlyToProject);
    }

    [Fact]
    public async Task FutureDatedExpensesAreSubtractedFromWhatIsAvailable()
    {
        var service = Create(new[] { Income(3000m, D(11, 5)), Expense(1000m, D(11, 10)), Expense(300m, D(11, 25)) });

        var pace = await service.GetAsync(_user.Id, Today);

        Assert.Equal(300m, pace.UpcomingExpenses);
        Assert.Equal(1700m, pace.AvailableToSpend);
    }

    [Fact]
    public async Task RecurringBillsNotYetGeneratedCountAsUpcoming()
    {
        var pending = new RecurringTransaction(_user.Id, TransactionType.Expense, 100m, TransactionCategory.Moradia, "Internet", 28, D(11, 3));
        var service = Create(new[] { Income(3000m, D(11, 5)) }, pending);

        var pace = await service.GetAsync(_user.Id, Today);

        Assert.Equal(100m, pace.UpcomingExpenses);
        Assert.Equal(2900m, pace.AvailableToSpend);
    }

    [Fact]
    public async Task RecurringBillsAlreadyGeneratedAreNotCountedTwice()
    {
        var generated = new RecurringTransaction(_user.Id, TransactionType.Expense, 100m, TransactionCategory.Moradia, "Aluguel", 10, D(11, 3));
        var transaction = generated.GenerateTransaction(Today);
        var service = Create(new[] { Income(3000m, D(11, 5)), transaction }, generated);

        var pace = await service.GetAsync(_user.Id, Today);

        Assert.Equal(0m, pace.UpcomingExpenses);
        Assert.Equal(100m, pace.ExpenseSoFar);
    }

    [Fact]
    public async Task PendingRecurringIncomeRaisesTheExpectedIncome()
    {
        var salary = new RecurringTransaction(_user.Id, TransactionType.Income, 3000m, TransactionCategory.Salario, "Salário", 28, D(11, 3));
        var service = Create(new[] { Expense(500m, D(11, 10)) }, salary);

        var pace = await service.GetAsync(_user.Id, Today);

        Assert.Equal(3000m, pace.ExpectedIncome);
        Assert.False(pace.IncomeIsEstimated);
    }

    [Fact]
    public async Task WithoutIncomeThisMonth_EstimatesFromThePastMonths()
    {
        var service = Create(new[] { Income(2000m, D(10, 5)), Expense(500m, D(10, 8)), Expense(100m, D(11, 10)) });

        var pace = await service.GetAsync(_user.Id, Today);

        Assert.True(pace.IncomeIsEstimated);
        Assert.Equal(2000m, pace.ExpectedIncome);
    }

    [Fact]
    public async Task WithoutAnyIncomeInformation_ExpectedIncomeIsZero()
    {
        var service = Create(new[] { Expense(100m, D(11, 10)) });

        var pace = await service.GetAsync(_user.Id, Today);

        Assert.Equal(0m, pace.ExpectedIncome);
        Assert.Equal(0m, pace.AvailablePerDay);
    }

    [Fact]
    public async Task WhenSpendingExceedsIncome_AvailablePerDayIsZero()
    {
        var service = Create(new[] { Income(500m, D(11, 5)), Expense(800m, D(11, 10)) });

        var pace = await service.GetAsync(_user.Id, Today);

        Assert.Equal(-300m, pace.AvailableToSpend);
        Assert.Equal(0m, pace.AvailablePerDay);
    }

    [Fact]
    public async Task InTheFirstDaysOfTheMonth_DoesNotProject()
    {
        var service = Create(new[] { Income(3000m, D(11, 1)), Expense(50m, D(11, 2)) });

        var pace = await service.GetAsync(_user.Id, new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc));

        Assert.True(pace.TooEarlyToProject);
    }

    [Fact]
    public async Task BiggestIncrease_PicksTheCategoryThatGrewTheMostAndIgnoresSmallChanges()
    {
        var service = Create(new[]
        {
            Expense(100m, D(10, 5), TransactionCategory.Lazer), Expense(200m, D(10, 6), TransactionCategory.Mercado),
            Expense(400m, D(11, 5), TransactionCategory.Lazer), Expense(205m, D(11, 6), TransactionCategory.Mercado)
        });

        var pace = await service.GetAsync(_user.Id, Today);

        Assert.NotNull(pace.BiggestIncrease);
        Assert.Equal(TransactionCategory.Lazer, pace.BiggestIncrease!.Category);
        Assert.Equal(300m, pace.BiggestIncrease.Delta);
    }

    [Fact]
    public async Task BiggestIncrease_IsNullWithoutAPreviousMonth()
    {
        var service = Create(new[] { Expense(400m, D(11, 5), TransactionCategory.Lazer) });

        var pace = await service.GetAsync(_user.Id, Today);

        Assert.Null(pace.BiggestIncrease);
    }

    [Fact]
    public async Task Handler_RepliesWithTheFriendlyText()
    {
        var service = Create(new[] { Income(3000m, D(11, 5)), Expense(1000m, D(11, 10)) });
        var handler = new MonthPaceIntentHandler(service);

        var result = await handler.HandleAsync(_user, new MessageIntent(IntentType.QueryMonthPace));

        Assert.True(result.Understood);
        Assert.Contains("Você ainda pode gastar", result.ReplyMessage);
        Assert.Contains("por dia", result.ReplyMessage);
        Assert.Contains("fecham em", result.ReplyMessage);
    }

    [Fact]
    public void BuildReply_WhenOverBudget_WarnsInsteadOfShowingADailyAmount()
    {
        var pace = new MonthPace(Today, 11, 500m, 800m, 500m, false, 0m, -300m, 0m, 1200m, false, null);

        var reply = MonthPaceIntentHandler.BuildReply(pace);

        Assert.Contains("já passou do que entrou", reply);
        Assert.Contains("R$ 300,00", reply);
        Assert.DoesNotContain("por dia", reply);
    }

    [Fact]
    public void BuildReply_WhenEstimatingIncome_SaysSo()
    {
        var pace = new MonthPace(Today, 11, 0m, 100m, 2000m, true, 0m, 1900m, 172.73m, 150m, false, null);

        Assert.Contains("estimei", MonthPaceIntentHandler.BuildReply(pace));
    }

    [Fact]
    public void BuildReply_WithoutIncomeInformation_ExplainsWhy()
    {
        var pace = new MonthPace(Today, 11, 0m, 100m, 0m, false, 0m, -100m, 0m, 150m, false, null);

        Assert.Contains("Ainda não tenho receita", MonthPaceIntentHandler.BuildReply(pace));
    }

    [Fact]
    public void BuildReply_TooEarly_DoesNotPromiseAProjection()
    {
        var pace = new MonthPace(new DateTime(2026, 11, 2), 29, 3000m, 50m, 3000m, false, 0m, 2950m, 101.72m, 50m, true, null);

        var reply = MonthPaceIntentHandler.BuildReply(pace);

        Assert.Contains("Ainda é cedo", reply);
        Assert.DoesNotContain("fecham em", reply);
    }

    [Fact]
    public void BuildReply_MentionsWhatChanged()
    {
        var change = new CategoryChange(TransactionCategory.Lazer, 400m, 100m);
        var pace = new MonthPace(Today, 11, 3000m, 1000m, 3000m, false, 0m, 2000m, 181.82m, 1500m, false, change);

        var reply = MonthPaceIntentHandler.BuildReply(pace);

        Assert.Contains("O que mudou", reply);
        Assert.Contains("Lazer subiu R$ 300,00", reply);
        Assert.Contains("outubro", reply);
    }
}
