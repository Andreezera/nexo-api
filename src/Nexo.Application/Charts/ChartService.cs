using Nexo.Application.Messaging;
using Nexo.Application.Reports;
using Nexo.Domain.Enums;

namespace Nexo.Application.Charts;

public record ChartSlice(string Label, decimal Value);

public record MonthlyBar(string Label, decimal Income, decimal Expense);

// Desenha as imagens. A implementação fica na camada de infraestrutura.
public interface IChartRenderer
{
    byte[] RenderCategoryChart(string title, string centerLabel, IReadOnlyList<ChartSlice> slices);

    byte[] RenderMonthlyChart(string title, IReadOnlyList<MonthlyBar> months);
}

public record ChartResult(byte[] Png, string Caption);

public interface IChartService
{
    // Retorna null quando não há dados para desenhar.
    Task<ChartResult?> BuildCategoryChartAsync(
        Guid userId, int monthOffset, DateTime today, CancellationToken cancellationToken = default);

    Task<ChartResult?> BuildMonthlyChartAsync(
        Guid userId, int months, DateTime today, CancellationToken cancellationToken = default);
}

public class ChartService : IChartService
{
    private const int MaxSlices = 6;
    private const int DefaultMonths = 6;

    private readonly IReportService _reportService;
    private readonly IChartRenderer _renderer;

    public ChartService(IReportService reportService, IChartRenderer renderer)
    {
        _reportService = reportService;
        _renderer = renderer;
    }

    public async Task<ChartResult?> BuildCategoryChartAsync(
        Guid userId, int monthOffset, DateTime today, CancellationToken cancellationToken = default)
    {
        var summary = await _reportService.GetMonthlySummaryAsync(userId, monthOffset, today, cancellationToken);
        if (summary.Expense <= 0)
            return null;

        var slices = summary.ExpensesByCategory
            .Take(MaxSlices)
            .Select(c => new ChartSlice(c.Category.ToDisplayName(), c.Total))
            .ToList();

        // O que passa de MaxSlices vira uma fatia só, para o gráfico continuar legível.
        var others = summary.ExpensesByCategory.Skip(MaxSlices).Sum(c => c.Total);
        if (others > 0)
            slices.Add(new ChartSlice("Outras", others));

        var month = MessageFormatter.MonthLabel(summary.Month);
        var png = _renderer.RenderCategoryChart(
            $"Gastos por categoria · {month}", MessageFormatter.Money(summary.Expense), slices);

        var caption = $"📊 *Gastos por categoria*\n{month}\nTotal: {MessageFormatter.Money(summary.Expense)}";
        return new ChartResult(png, caption);
    }

    public async Task<ChartResult?> BuildMonthlyChartAsync(
        Guid userId, int months, DateTime today, CancellationToken cancellationToken = default)
    {
        var count = months <= 0 ? DefaultMonths : months;
        var series = await _reportService.GetMonthlySeriesAsync(userId, count, today, cancellationToken);
        if (series.All(p => p.Income == 0 && p.Expense == 0))
            return null;

        var bars = series
            .Select(p => new MonthlyBar(MessageFormatter.MonthName(p.Month)[..3], p.Income, p.Expense))
            .ToList();

        var png = _renderer.RenderMonthlyChart($"Receitas e despesas · últimos {series.Count} meses", bars);

        var income = series.Sum(p => p.Income);
        var expense = series.Sum(p => p.Expense);
        var caption =
            $"📈 *Últimos {series.Count} meses*\n📥 Receitas: {MessageFormatter.Money(income)}\n📤 Despesas: {MessageFormatter.Money(expense)}";
        return new ChartResult(png, caption);
    }
}
