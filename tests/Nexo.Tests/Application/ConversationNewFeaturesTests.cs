using Moq;
using Nexo.Application.Abstractions;
using Nexo.Application.Budgets;
using Nexo.Application.Charts;
using Nexo.Application.Conversation;
using Nexo.Application.Projections;
using Nexo.Application.Reports;
using Nexo.Application.Transactions;
using Nexo.Domain.Entities;
using Nexo.Domain.Enums;

namespace Nexo.Tests.Application;

// Menu, resumo mensal, gráficos, desfazer e corrigir o último lançamento.
public class ConversationNewFeaturesTests
{
    private const string Phone = "5511988887777";

    private readonly Mock<IMessageIntentParser> _intentParser = new();
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<ITransactionRepository> _transactionRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IChartRenderer> _chartRenderer = new();
    private readonly User _user = new(Phone);
    private static readonly IEnumerable<IIntentHandler> Handlers = Array.Empty<IIntentHandler>();

    public ConversationNewFeaturesTests()
    {
        _userRepository.Setup(u => u.GetByPhoneNumberAsync(Phone, It.IsAny<CancellationToken>())).ReturnsAsync(_user);
        _transactionRepository.Setup(t => t.GetByUserIdAsync(_user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Transaction>());
        _chartRenderer.Setup(r => r.RenderCategoryChart(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<ChartSlice>>()))
            .Returns(new byte[] { 1, 2, 3 });
        _chartRenderer.Setup(r => r.RenderMonthlyChart(It.IsAny<string>(), It.IsAny<IReadOnlyList<MonthlyBar>>()))
            .Returns(new byte[] { 4, 5, 6 });
    }

    private ConversationService CreateService(IMessageIntentParser? parser = null)
    {
        var history = new Mock<IConversationHistoryRepository>();
        history.Setup(h => h.GetRecentAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ConversationMessage>());
        var report = new ReportService(_transactionRepository.Object);

        return new ConversationService(
            parser ?? _intentParser.Object, Mock.Of<ITransactionRegistrar>(), _userRepository.Object, _transactionRepository.Object,
            Mock.Of<IBudgetLimitRepository>(), Mock.Of<IBudgetAlertService>(), Mock.Of<IPurchaseProjectionService>(),
            report, new ChartService(report, _chartRenderer.Object), history.Object, _unitOfWork.Object, Handlers);
    }

    private void SetupIntent(MessageIntent intent) =>
        _intentParser.Setup(p => p.ParseAsync(It.IsAny<MessageContent>(), It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(intent);

    private void SetupTransactions(params Transaction[] transactions) =>
        _transactionRepository.Setup(t => t.GetByUserIdAsync(_user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(transactions);

    private Task<RegisterMessageResult> Handle(string text = "mensagem") =>
        CreateService().HandleAsync(Phone, MessageContent.FromText(text));

    private static DateTime ThisMonth(int day) =>
        new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, Math.Min(day, 28), 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ShowMenu_RepliesWithTheMenu()
    {
        SetupIntent(new MessageIntent(IntentType.ShowMenu));

        var result = await Handle("menu");

        Assert.True(result.Understood);
        Assert.Contains("*NEXO*", result.ReplyMessage);
        Assert.Contains("Gráficos", result.ReplyMessage);
    }

    [Fact]
    public async Task FirstContact_AddsTheMenuHintToTheReply()
    {
        _userRepository.Setup(u => u.GetByPhoneNumberAsync("novo", It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        _transactionRepository.Setup(t => t.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Transaction>());
        SetupIntent(new MessageIntent(IntentType.QueryBalance));

        var result = await CreateService().HandleAsync("novo", MessageContent.FromText("saldo"));

        Assert.Contains("*menu*", result.ReplyMessage);
    }

    [Fact]
    public async Task ReturningUser_DoesNotGetTheHintAgain()
    {
        SetupIntent(new MessageIntent(IntentType.QueryBalance));

        var result = await Handle("saldo");

        Assert.DoesNotContain("*menu*", result.ReplyMessage);
    }

    [Fact]
    public async Task MonthlySummary_ShowsTotalsCategoriesAndComparison()
    {
        var previousMonth = ThisMonth(10).AddMonths(-1);
        SetupTransactions(
            new Transaction(_user.Id, TransactionType.Income, 3000m, TransactionCategory.Salario, "salário", ThisMonth(5)),
            new Transaction(_user.Id, TransactionType.Expense, 600m, TransactionCategory.Mercado, "mercado", ThisMonth(6)),
            new Transaction(_user.Id, TransactionType.Expense, 400m, TransactionCategory.Lazer, "cinema", ThisMonth(7)),
            new Transaction(_user.Id, TransactionType.Expense, 500m, TransactionCategory.Mercado, "mercado", previousMonth));
        SetupIntent(new MessageIntent(IntentType.QueryMonthlySummary));

        var result = await Handle("resumo do mês");

        Assert.Contains("Resumo de", result.ReplyMessage);
        Assert.Contains("R$ 3.000,00", result.ReplyMessage);
        Assert.Contains("R$ 1.000,00", result.ReplyMessage);
        Assert.Contains("R$ 2.000,00", result.ReplyMessage);
        Assert.Contains("Mercado", result.ReplyMessage);
        Assert.Contains("60%", result.ReplyMessage);
        Assert.Contains("maiores que em", result.ReplyMessage);
    }

    [Fact]
    public async Task MonthlySummary_OfAnEmptyMonth_SaysThereIsNothing()
    {
        SetupIntent(new MessageIntent(IntentType.QueryMonthlySummary, MonthOffset: -3));

        var result = await Handle("resumo de agosto");

        Assert.Contains("Não encontrei lançamentos", result.ReplyMessage);
    }

    [Fact]
    public async Task Spending_WithoutCategory_ListsCategoriesWithBars()
    {
        SetupTransactions(
            new Transaction(_user.Id, TransactionType.Expense, 80m, TransactionCategory.Mercado, "mercado", ThisMonth(6)),
            new Transaction(_user.Id, TransactionType.Expense, 20m, TransactionCategory.Lazer, "cinema", ThisMonth(7)));
        SetupIntent(new MessageIntent(IntentType.QuerySpending));

        var result = await Handle("gastos por categoria");

        Assert.Contains("Mercado", result.ReplyMessage);
        Assert.Contains("80%", result.ReplyMessage);
        Assert.Contains("🟦", result.ReplyMessage);
    }

    [Fact]
    public async Task Chart_ByCategory_ReturnsAnImageWithCaption()
    {
        SetupTransactions(new Transaction(_user.Id, TransactionType.Expense, 80m, TransactionCategory.Mercado, "mercado", ThisMonth(6)));
        SetupIntent(new MessageIntent(IntentType.QueryChart, Chart: ChartKind.Categories));

        var result = await Handle("gráfico por categoria");

        Assert.NotNull(result.Image);
        Assert.Equal(new byte[] { 1, 2, 3 }, result.Image!.Data);
        Assert.Contains("Gastos por categoria", result.ReplyMessage);
    }

    [Fact]
    public async Task Chart_Monthly_UsesTheMonthlyRenderer()
    {
        SetupTransactions(new Transaction(_user.Id, TransactionType.Income, 500m, TransactionCategory.Salario, "salário", ThisMonth(2)));
        SetupIntent(new MessageIntent(IntentType.QueryChart, Chart: ChartKind.Monthly));

        var result = await Handle("gráfico dos meses");

        Assert.Equal(new byte[] { 4, 5, 6 }, result.Image!.Data);
    }

    [Fact]
    public async Task Chart_WithoutData_RepliesWithTextOnly()
    {
        SetupIntent(new MessageIntent(IntentType.QueryChart, Chart: ChartKind.Categories));

        var result = await Handle("gráfico");

        Assert.Null(result.Image);
        Assert.Contains("dados suficientes", result.ReplyMessage);
    }

    [Fact]
    public async Task Undo_RemovesTheMostRecentlyCreatedTransaction()
    {
        var older = new Transaction(_user.Id, TransactionType.Expense, 10m, TransactionCategory.Lazer, "antigo", ThisMonth(9));
        await Task.Delay(20);
        var newest = new Transaction(_user.Id, TransactionType.Expense, 20m, TransactionCategory.Alimentacao, "padaria", ThisMonth(2));
        SetupTransactions(older, newest);
        SetupIntent(new MessageIntent(IntentType.UndoLastTransaction));

        var result = await Handle("apaga o último");

        Assert.True(result.Understood);
        Assert.True(newest.IsDeleted);
        Assert.False(older.IsDeleted);
        Assert.Contains("padaria", result.ReplyMessage);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Undo_WithoutTransactions_ExplainsThereIsNothing()
    {
        SetupIntent(new MessageIntent(IntentType.UndoLastTransaction));

        var result = await Handle("desfaz");

        Assert.False(result.Understood);
        Assert.Contains("não tem lançamentos", result.ReplyMessage);
    }

    [Fact]
    public async Task Undo_OfAnInstallmentPurchase_IsNotAllowed()
    {
        var plan = InstallmentPlan.Create(_user.Id, "geladeira", TransactionCategory.Eletronicos, 1200m, 3, ThisMonth(3));
        SetupTransactions(plan.Transactions.ToArray());
        SetupIntent(new MessageIntent(IntentType.UndoLastTransaction));

        var result = await Handle("apaga o último");

        Assert.False(result.Understood);
        Assert.Contains("parcelada", result.ReplyMessage);
        Assert.All(plan.Transactions, t => Assert.False(t.IsDeleted));
    }

    [Fact]
    public async Task Edit_ChangesAmountAndCategoryOfTheLastTransaction()
    {
        var last = new Transaction(_user.Id, TransactionType.Expense, 20m, TransactionCategory.Alimentacao, "padaria", ThisMonth(2));
        SetupTransactions(last);
        SetupIntent(new MessageIntent(IntentType.EditLastTransaction, Category: TransactionCategory.Lazer, Amount: 25m));

        var result = await Handle("corrige para 25 em lazer");

        Assert.True(result.Understood);
        Assert.Equal(25m, last.Amount);
        Assert.Equal(TransactionCategory.Lazer, last.Category);
        Assert.Equal("padaria", last.Description);
        Assert.Contains("R$ 25,00", result.ReplyMessage);
    }

    [Fact]
    public async Task Edit_WithNothingToChange_AsksWhatToCorrect()
    {
        var last = new Transaction(_user.Id, TransactionType.Expense, 20m, TransactionCategory.Alimentacao, "padaria", ThisMonth(2));
        SetupTransactions(last);
        SetupIntent(new MessageIntent(IntentType.EditLastTransaction, Amount: 0m));

        var result = await Handle("corrige");

        Assert.False(result.Understood);
        Assert.Equal(20m, last.Amount);
        Assert.Contains("corrige para 25", result.ReplyMessage);
    }
}
