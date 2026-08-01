using Moq;
using Nexo.Application.Abstractions;
using Nexo.Application.Budgets;
using Nexo.Domain.Entities;
using Nexo.Domain.Enums;

namespace Nexo.Tests.Application;

public class BudgetAlertServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTime Reference = new(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IBudgetLimitRepository> _limits = new();
    private readonly Mock<ITransactionRepository> _transactions = new();

    private BudgetAlertService CreateService() => new(_limits.Object, _transactions.Object);

    private void SetupLimit(TransactionCategory category, decimal amount) =>
        _limits.Setup(l => l.GetAsync(UserId, category, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BudgetLimit(UserId, category, amount));

    private void SetupTransactions(params Transaction[] transactions) =>
        _transactions.Setup(t => t.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactions);

    private static Transaction Expense(decimal amount, TransactionCategory category, DateTime date) =>
        new(UserId, TransactionType.Expense, amount, category, "teste", date);

    [Fact]
    public async Task BuildAlertAsync_WhenCategoryHasNoLimit_ReturnsNull()
    {
        _limits.Setup(l => l.GetAsync(UserId, TransactionCategory.Mercado, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BudgetLimit?)null);

        var alert = await CreateService().BuildAlertAsync(UserId, TransactionCategory.Mercado, Reference);

        Assert.Null(alert);
    }

    [Fact]
    public async Task BuildAlertAsync_WhenSpendingIsBelowWarningThreshold_ReturnsNull()
    {
        SetupLimit(TransactionCategory.Mercado, 1000m);
        SetupTransactions(Expense(500m, TransactionCategory.Mercado, Reference));

        var alert = await CreateService().BuildAlertAsync(UserId, TransactionCategory.Mercado, Reference);

        Assert.Null(alert);
    }

    [Fact]
    public async Task BuildAlertAsync_WhenSpendingReachesEightyPercent_ReturnsWarning()
    {
        SetupLimit(TransactionCategory.Mercado, 1000m);
        SetupTransactions(Expense(800m, TransactionCategory.Mercado, Reference));

        var alert = await CreateService().BuildAlertAsync(UserId, TransactionCategory.Mercado, Reference);

        Assert.NotNull(alert);
        Assert.Contains("80%", alert);
    }

    [Fact]
    public async Task BuildAlertAsync_WhenSpendingExceedsLimit_ReturnsExceededAlert()
    {
        SetupLimit(TransactionCategory.Mercado, 1000m);
        SetupTransactions(
            Expense(700m, TransactionCategory.Mercado, Reference),
            Expense(450m, TransactionCategory.Mercado, Reference));

        var alert = await CreateService().BuildAlertAsync(UserId, TransactionCategory.Mercado, Reference);

        Assert.NotNull(alert);
        Assert.Contains("estourou", alert);
    }

    [Fact]
    public async Task GetStatusAsync_IgnoresOtherMonthsOtherCategoriesAndIncome()
    {
        SetupLimit(TransactionCategory.Mercado, 1000m);
        SetupTransactions(
            Expense(300m, TransactionCategory.Mercado, Reference),
            Expense(999m, TransactionCategory.Mercado, Reference.AddMonths(-1)),
            Expense(999m, TransactionCategory.Lazer, Reference),
            new Transaction(UserId, TransactionType.Income, 999m, TransactionCategory.Mercado, "estorno", Reference));

        var status = await CreateService().GetStatusAsync(UserId, TransactionCategory.Mercado, Reference);

        Assert.NotNull(status);
        Assert.Equal(300m, status!.Spent);
        Assert.Equal(0.3m, status.PercentUsed);
    }

    [Fact]
    public async Task GetAllStatusesAsync_ReturnsOneStatusPerLimit()
    {
        _limits.Setup(l => l.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new BudgetLimit(UserId, TransactionCategory.Mercado, 1000m),
                new BudgetLimit(UserId, TransactionCategory.Lazer, 200m)
            });
        SetupTransactions(Expense(100m, TransactionCategory.Lazer, Reference));

        var statuses = await CreateService().GetAllStatusesAsync(UserId, Reference);

        Assert.Equal(2, statuses.Count);
        Assert.Equal(100m, statuses.Single(s => s.Category == TransactionCategory.Lazer).Spent);
        Assert.Equal(0m, statuses.Single(s => s.Category == TransactionCategory.Mercado).Spent);
    }
}
