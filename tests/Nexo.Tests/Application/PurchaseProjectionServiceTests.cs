using Moq;
using Nexo.Application.Abstractions;
using Nexo.Application.Projections;
using Nexo.Domain.Entities;
using Nexo.Domain.Enums;

namespace Nexo.Tests.Application;

public class PurchaseProjectionServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTime Reference = new(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<ITransactionRepository> _transactions = new();

    private PurchaseProjectionService CreateService(params Transaction[] transactions)
    {
        _transactions.Setup(t => t.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(transactions);
        return new PurchaseProjectionService(_transactions.Object);
    }

    private static Transaction Income(decimal amount, DateTime date) =>
        new(UserId, TransactionType.Income, amount, TransactionCategory.Salario, "receita", date);

    private static Transaction Expense(decimal amount, DateTime date, Guid? installmentPlanId = null) =>
        new(UserId, TransactionType.Expense, amount, TransactionCategory.Outros, "despesa", date, installmentPlanId);

    // Ago, set e out: receita de 3000 e despesa de 2000 por mês (sobra 1000). Saldo de hoje = 3000.
    private static List<Transaction> ThreeMonthsOfHistory()
    {
        var list = new List<Transaction>();
        foreach (var month in new[] { 8, 9, 10 })
        {
            list.Add(Income(3000m, new DateTime(2026, month, 5, 0, 0, 0, DateTimeKind.Utc)));
            list.Add(Expense(2000m, new DateTime(2026, month, 15, 0, 0, 0, DateTimeKind.Utc)));
        }
        return list;
    }

    [Fact]
    public async Task ProjectAsync_WhenBalanceCoversPrice_CanAffordNow()
    {
        var service = CreateService(ThreeMonthsOfHistory().ToArray());

        var result = await service.ProjectAsync(UserId, 2500m, 1, Reference);

        Assert.True(result.CanAffordNow);
        Assert.Equal(3000m, result.CurrentBalance);
        Assert.Equal(0, result.MonthsUntilAffordable);
    }

    [Fact]
    public async Task ProjectAsync_ComputesAveragesFromClosedMonths()
    {
        var service = CreateService(ThreeMonthsOfHistory().ToArray());

        var result = await service.ProjectAsync(UserId, 100m, 1, Reference);

        Assert.Equal(3000m, result.AverageMonthlyIncome);
        Assert.Equal(2000m, result.AverageMonthlyExpense);
        Assert.Equal(1000m, result.MonthlyMargin);
    }

    [Fact]
    public async Task ProjectAsync_WhenBalanceIsShort_FindsFirstMonthThatCoversPrice()
    {
        var service = CreateService(ThreeMonthsOfHistory().ToArray());

        // saldo 3000 + 1000 por mês: em 2 meses chega em 5000
        var result = await service.ProjectAsync(UserId, 5000m, 1, Reference);

        Assert.False(result.CanAffordNow);
        Assert.Equal(2, result.MonthsUntilAffordable);
        Assert.Equal(new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc), result.AffordableMonth);
    }

    [Fact]
    public async Task ProjectAsync_FutureInstallmentsDelayTheProjection()
    {
        var history = ThreeMonthsOfHistory();
        var planId = Guid.NewGuid();
        history.Add(Expense(300m, new DateTime(2026, 12, 10, 0, 0, 0, DateTimeKind.Utc), planId));

        var withInstallment = await CreateService(history.ToArray()).ProjectAsync(UserId, 4000m, 1, Reference);
        var withoutInstallment = await CreateService(ThreeMonthsOfHistory().ToArray()).ProjectAsync(UserId, 4000m, 1, Reference);

        Assert.Equal(1, withoutInstallment.MonthsUntilAffordable);
        Assert.Equal(2, withInstallment.MonthsUntilAffordable);
        Assert.Equal(700m, withInstallment.MonthlyMargin);
    }

    [Fact]
    public async Task ProjectAsync_WhenMonthlyFlowIsNegative_NeverReachesPrice()
    {
        var transactions = new List<Transaction>
        {
            Income(1000m, new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc)),
            Expense(1500m, new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc))
        };

        var result = await CreateService(transactions.ToArray()).ProjectAsync(UserId, 3000m, 1, Reference);

        Assert.False(result.CanAffordNow);
        Assert.Null(result.MonthsUntilAffordable);
        Assert.Null(result.AffordableMonth);
    }

    [Fact]
    public async Task ProjectAsync_InstallmentValueMustFitMonthlyMargin()
    {
        var service = CreateService(ThreeMonthsOfHistory().ToArray());

        var fits = await service.ProjectAsync(UserId, 6000m, 10, Reference);
        var doesNotFit = await service.ProjectAsync(UserId, 6000m, 3, Reference);

        Assert.Equal(600m, fits.InstallmentValue);
        Assert.True(fits.FitsMonthlyMargin);
        Assert.Equal(2000m, doesNotFit.InstallmentValue);
        Assert.False(doesNotFit.FitsMonthlyMargin);
    }

    [Fact]
    public async Task ProjectAsync_WithoutClosedMonths_UsesCurrentMonthAsSample()
    {
        var transactions = new[]
        {
            Income(2000m, new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc)),
            Expense(500m, new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc))
        };

        var result = await CreateService(transactions).ProjectAsync(UserId, 100m, 1, Reference);

        Assert.Equal(2000m, result.AverageMonthlyIncome);
        Assert.Equal(500m, result.AverageMonthlyExpense);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(100, 0)]
    public async Task ProjectAsync_WithInvalidInput_Throws(decimal price, int installments)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() => service.ProjectAsync(UserId, price, installments, Reference));
    }
}
