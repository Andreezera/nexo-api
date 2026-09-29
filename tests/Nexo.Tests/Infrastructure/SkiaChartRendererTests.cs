using Nexo.Application.Charts;
using Nexo.Infrastructure.Charts;

namespace Nexo.Tests.Infrastructure;

public class SkiaChartRendererTests
{
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private static void AssertIsPng(byte[] data)
    {
        Assert.True(data.Length > 1000);
        Assert.Equal(PngSignature, data.Take(8).ToArray());
    }

    [Fact]
    public void RenderCategoryChart_ProducesAPng()
    {
        var png = new SkiaChartRenderer().RenderCategoryChart(
            "Gastos por categoria · novembro de 2026", "R$ 1.538,80",
            new[] { new ChartSlice("Vestuário", 1422.8m), new ChartSlice("Lazer", 50m), new ChartSlice("Alimentação", 46m) });

        AssertIsPng(png);
    }

    [Fact]
    public void RenderCategoryChart_WithASingleSlice_ProducesAPng()
    {
        AssertIsPng(new SkiaChartRenderer().RenderCategoryChart("Gastos", "R$ 20,00", new[] { new ChartSlice("Mercado", 20m) }));
    }

    [Fact]
    public void RenderMonthlyChart_ProducesAPng()
    {
        var png = new SkiaChartRenderer().RenderMonthlyChart("Receitas e despesas", new[]
        {
            new MonthlyBar("jun", 0m, 0m), new MonthlyBar("jul", 1500m, 800m), new MonthlyBar("ago", 3200m, 2100m),
            new MonthlyBar("set", 2800m, 3500m), new MonthlyBar("out", 0m, 120m), new MonthlyBar("nov", 175m, 1538.8m)
        });

        AssertIsPng(png);
    }

    [Fact]
    public void RenderMonthlyChart_WithOnlyZeros_DoesNotThrow()
    {
        AssertIsPng(new SkiaChartRenderer().RenderMonthlyChart("Vazio", new[] { new MonthlyBar("nov", 0m, 0m) }));
    }
}
