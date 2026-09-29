using Moq;
using Nexo.Application.Abstractions;
using Nexo.Application.Charts;
using Nexo.Application.Reports;
using Nexo.Domain.Entities;
using Nexo.Domain.Enums;

namespace Nexo.Tests.Application;

public class ReportAndChartServiceTests
{
    private static readonly DateTime Today = new(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Mock<ITransactionRepository> _repository = new();
    private readonly Mock<IChartRenderer> _renderer = new();

    private Transaction Tx(TransactionType type, decimal amount, TransactionCategory category, DateTime when) =>
        new(_userId, type, amount, category, "teste", when);

    private ReportService CreateReport(params Transaction[] transactions)
    {
        _repository.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync(transactions);
        return new ReportService(_repository.Object);
    }

    [Fact]
    public async Task Summary_SumsIncomeAndExpenseAndOrdersCategories()
    {
        var report = CreateReport(
            Tx(TransactionType.Income, 3000m, TransactionCategory.Salario, new DateTime(2026, 11, 5, 0, 0, 0, DateTimeKind.Utc)),
            Tx(TransactionType.Expense, 100m, TransactionCategory.Lazer, new DateTime(2026, 11, 6, 0, 0, 0, DateTimeKind.Utc)),
            Tx(TransactionType.Expense, 400m, TransactionCategory.Mercado, new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc)),
            Tx(TransactionType.Expense, 50m, TransactionCategory.Lazer, new DateTime(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc)));

        var summary = await report.GetMonthlySummaryAsync(_userId, 0, Today);

        Assert.Equal(3000m, summary.Income);
        Assert.Equal(550m, summary.Expense);
        Assert.Equal(2450m, summary.Balance);
        Assert.Equal(TransactionCategory.Mercado, summary.ExpensesByCategory[0].Category);
        Assert.Equal(150m, summary.ExpensesByCategory[1].Total);
        Assert.True(summary.HasTransactions);
    }

    [Fact]
    public async Task Summary_WithoutPreviousMonth_HasNoComparison()
    {
        var report = CreateReport(
            Tx(TransactionType.Expense, 100m, TransactionCategory.Lazer, new DateTime(2026, 11, 6, 0, 0, 0, DateTimeKind.Utc)));

        var summary = await report.GetMonthlySummaryAsync(_userId, 0, Today);

        Assert.Null(summary.PreviousExpense);
    }

    [Fact]
    public async Task Summary_WithPreviousMonth_ExposesItsExpense()
    {
        var report = CreateReport(
            Tx(TransactionType.Expense, 100m, TransactionCategory.Lazer, new DateTime(2026, 11, 6, 0, 0, 0, DateTimeKind.Utc)),
            Tx(TransactionType.Expense, 80m, TransactionCategory.Lazer, new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc)));

        var summary = await report.GetMonthlySummaryAsync(_userId, 0, Today);

        Assert.Equal(80m, summary.PreviousExpense);
    }

    [Fact]
    public async Task Summary_WithMonthOffset_LooksAtThatMonth()
    {
        var report = CreateReport(
            Tx(TransactionType.Expense, 100m, TransactionCategory.Lazer, new DateTime(2026, 11, 6, 0, 0, 0, DateTimeKind.Utc)),
            Tx(TransactionType.Expense, 80m, TransactionCategory.Mercado, new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc)));

        var summary = await report.GetMonthlySummaryAsync(_userId, -1, Today);

        Assert.Equal(new DateTime(2026, 10, 1), summary.Month.Date);
        Assert.Equal(80m, summary.Expense);
    }

    [Fact]
    public async Task Summary_OfAnEmptyMonth_HasNoTransactions()
    {
        var summary = await CreateReport().GetMonthlySummaryAsync(_userId, 0, Today);

        Assert.False(summary.HasTransactions);
        Assert.Empty(summary.ExpensesByCategory);
    }

    [Fact]
    public async Task Series_ReturnsRequestedMonthsOldestFirst()
    {
        var report = CreateReport(
            Tx(TransactionType.Income, 500m, TransactionCategory.Salario, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)),
            Tx(TransactionType.Expense, 200m, TransactionCategory.Mercado, new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc)));

        var series = await report.GetMonthlySeriesAsync(_userId, 3, Today);

        Assert.Equal(3, series.Count);
        Assert.Equal(9, series[0].Month.Month);
        Assert.Equal(500m, series[0].Income);
        Assert.Equal(11, series[2].Month.Month);
        Assert.Equal(200m, series[2].Expense);
    }

    [Fact]
    public async Task CategoryChart_WithoutExpenses_ReturnsNull()
    {
        var chart = new ChartService(CreateReport(), _renderer.Object);

        Assert.Null(await chart.BuildCategoryChartAsync(_userId, 0, Today));
    }

    [Fact]
    public async Task CategoryChart_GroupsCategoriesBeyondSixAsOthers()
    {
        var categories = new[]
        {
            TransactionCategory.Mercado, TransactionCategory.Lazer, TransactionCategory.Saude, TransactionCategory.Moradia,
            TransactionCategory.Transporte, TransactionCategory.Vestuario, TransactionCategory.Presente, TransactionCategory.Educacao
        };
        var transactions = categories
            .Select((c, i) => Tx(TransactionType.Expense, 100m + i, c, new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc)))
            .ToArray();
        IReadOnlyList<ChartSlice>? captured = null;
        _renderer.Setup(r => r.RenderCategoryChart(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<ChartSlice>>()))
            .Callback<string, string, IReadOnlyList<ChartSlice>>((_, _, slices) => captured = slices)
            .Returns(new byte[] { 1 });

        var result = await new ChartService(CreateReport(transactions), _renderer.Object).BuildCategoryChartAsync(_userId, 0, Today);

        Assert.NotNull(result);
        Assert.Equal(7, captured!.Count);
        Assert.Equal("Outras", captured[^1].Label);
        Assert.Contains("Gastos por categoria", result!.Caption);
    }

    [Fact]
    public async Task MonthlyChart_WithoutAnyTransaction_ReturnsNull()
    {
        var chart = new ChartService(CreateReport(), _renderer.Object);

        Assert.Null(await chart.BuildMonthlyChartAsync(_userId, 6, Today));
    }

    [Fact]
    public async Task MonthlyChart_UsesThreeLetterMonthLabels()
    {
        IReadOnlyList<MonthlyBar>? captured = null;
        _renderer.Setup(r => r.RenderMonthlyChart(It.IsAny<string>(), It.IsAny<IReadOnlyList<MonthlyBar>>()))
            .Callback<string, IReadOnlyList<MonthlyBar>>((_, bars) => captured = bars)
            .Returns(new byte[] { 1 });
        var report = CreateReport(
            Tx(TransactionType.Expense, 90m, TransactionCategory.Lazer, new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc)));

        var result = await new ChartService(report, _renderer.Object).BuildMonthlyChartAsync(_userId, 6, Today);

        Assert.NotNull(result);
        Assert.Equal(6, captured!.Count);
        Assert.Equal("nov", captured[^1].Label);
        Assert.Equal("jun", captured[0].Label);
    }
}
