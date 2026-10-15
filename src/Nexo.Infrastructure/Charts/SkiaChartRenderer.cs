using Nexo.Application.Charts;
using Nexo.Application.Messaging;
using SkiaSharp;

namespace Nexo.Infrastructure.Charts;

// Desenha os gráficos como PNG no próprio servidor, para os dados financeiros não saírem para nenhum serviço externo.
// O WhatsApp encolhe a imagem para a largura da tela do celular, então as fontes são grandes de propósito.
public class SkiaChartRenderer : IChartRenderer
{
    private const int Width = 1000;
    private const int CategoryHeight = 800;
    private const int MonthlyHeight = 760;
    private const int MaxLegendRows = 7;

    private static readonly SKColor Ink = SKColor.Parse("#1A2E2F");
    private static readonly SKColor Muted = SKColor.Parse("#5B6B6C");
    private static readonly SKColor GridLine = SKColor.Parse("#E3EAEA");
    private static readonly SKColor IncomeColor = SKColor.Parse("#00A896");
    private static readonly SKColor ExpenseColor = SKColor.Parse("#F4845F");

    private static readonly SKColor[] Palette =
    {
        SKColor.Parse("#028090"), SKColor.Parse("#02C39A"), SKColor.Parse("#F4845F"), SKColor.Parse("#F2C14E"),
        SKColor.Parse("#3D5A80"), SKColor.Parse("#8E7DBE"), SKColor.Parse("#E56B9C"), SKColor.Parse("#9AA5B1")
    };

    public byte[] RenderCategoryChart(string title, string centerLabel, IReadOnlyList<ChartSlice> slices)
    {
        var visible = slices.Where(s => s.Value > 0).Take(MaxLegendRows).ToList();
        var total = visible.Sum(s => s.Value);

        using var surface = SKSurface.Create(new SKImageInfo(Width, CategoryHeight));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        DrawText(canvas, title, 48, 84, 44, bold: true, Ink);

        const float cx = 270, cy = 460, outer = 230, ring = 96;
        using (var arc = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = ring })
        {
            var rect = new SKRect(cx - (outer - ring / 2), cy - (outer - ring / 2), cx + (outer - ring / 2), cy + (outer - ring / 2));
            float start = -90;
            for (var i = 0; i < visible.Count; i++)
            {
                var sweep = (float)(360.0 * (double)(visible[i].Value / total));
                arc.Color = Palette[i % Palette.Length];
                // Um espaço de 1 grau separa as fatias, a não ser que só exista uma.
                canvas.DrawArc(rect, start, visible.Count > 1 ? Math.Max(sweep - 1, 0.5f) : sweep, false, arc);
                start += sweep;
            }
        }

        DrawText(canvas, "Total", cx, cy - 8, 30, bold: false, Muted, TextAlign.Center);
        DrawText(canvas, centerLabel, cx, cy + 44, 40, bold: true, Ink, TextAlign.Center);

        var y = 210f;
        for (var i = 0; i < visible.Count; i++)
        {
            using var dot = new SKPaint { IsAntialias = true, Color = Palette[i % Palette.Length] };
            canvas.DrawRoundRect(new SKRect(540, y - 28, 574, y + 6), 8, 8, dot);
            DrawText(canvas, visible[i].Label, 594, y, 36, bold: true, Ink);
            DrawText(canvas, $"{MessageFormatter.Money(visible[i].Value)} · {MessageFormatter.Percent(visible[i].Value / total)}",
                594, y + 40, 30, bold: false, Muted);
            y += 80;
        }

        return Encode(surface);
    }

    public byte[] RenderMonthlyChart(string title, IReadOnlyList<MonthlyBar> months)
    {
        using var surface = SKSurface.Create(new SKImageInfo(Width, MonthlyHeight));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        DrawText(canvas, title, 48, 84, 44, bold: true, Ink);
        DrawLegendItem(canvas, 560, 150, IncomeColor, "Receitas");
        DrawLegendItem(canvas, 770, 150, ExpenseColor, "Despesas");

        const float left = 150, right = 960, top = 230, bottom = 630;
        var maxValue = months.Count == 0 ? 0m : months.Max(m => Math.Max(m.Income, m.Expense));
        var axisMax = NiceAxisMax(maxValue);

        using (var grid = new SKPaint { IsAntialias = true, Color = GridLine, StrokeWidth = 2 })
        {
            for (var i = 0; i <= 4; i++)
            {
                var gy = bottom - (bottom - top) * i / 4f;
                canvas.DrawLine(left, gy, right, gy, grid);
                DrawText(canvas, Compact(axisMax * i / 4), left - 16, gy + 10, 28, bold: false, Muted, TextAlign.Right);
            }
        }

        var groupWidth = (right - left) / Math.Max(months.Count, 1);
        var barWidth = Math.Min(groupWidth * 0.3f, 56f);

        for (var i = 0; i < months.Count; i++)
        {
            var centerX = left + groupWidth * (i + 0.5f);
            DrawBar(canvas, centerX - barWidth - 4, barWidth, bottom, top, months[i].Income, axisMax, IncomeColor);
            DrawBar(canvas, centerX + 4, barWidth, bottom, top, months[i].Expense, axisMax, ExpenseColor);
            DrawText(canvas, months[i].Label, centerX, bottom + 54, 34, bold: true, Ink, TextAlign.Center);
        }

        return Encode(surface);
    }

    private static void DrawBar(
        SKCanvas canvas, float x, float width, float bottom, float top, decimal value, decimal axisMax, SKColor color)
    {
        if (value <= 0)
            return;

        var barHeight = (float)((double)(value / axisMax) * (bottom - top));
        using var paint = new SKPaint { IsAntialias = true, Color = color };
        canvas.DrawRoundRect(new SKRect(x, bottom - barHeight, x + width, bottom + 6), 6, 6, paint);

        // O arredondamento de baixo fica por trás da linha de base: cobre com branco o que passou dela.
        using var cover = new SKPaint { Color = SKColors.White };
        canvas.DrawRect(new SKRect(x - 1, bottom, x + width + 1, bottom + 8), cover);

        DrawText(canvas, Compact(value), x + width / 2, bottom - barHeight - 12, 26, bold: true, Ink, TextAlign.Center);
    }

    private static void DrawLegendItem(SKCanvas canvas, float x, float y, SKColor color, string label)
    {
        using var dot = new SKPaint { IsAntialias = true, Color = color };
        canvas.DrawRoundRect(new SKRect(x, y - 26, x + 30, y + 4), 8, 8, dot);
        DrawText(canvas, label, x + 44, y, 32, bold: false, Ink);
    }

    // Arredonda o topo do eixo para um valor "redondo" (1, 2, 5 vezes uma potência de 10) acima do maior valor.
    private static decimal NiceAxisMax(decimal max)
    {
        if (max <= 0)
            return 100;

        var magnitude = (decimal)Math.Pow(10, Math.Floor(Math.Log10((double)max)));
        foreach (var factor in new[] { 1m, 2m, 2.5m, 5m, 10m })
        {
            if (magnitude * factor >= max * 1.05m)
                return magnitude * factor;
        }

        return magnitude * 10;
    }

    private static string Compact(decimal value) => value >= 1000
        ? $"{(value / 1000).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',')} mil"
        : value.ToString("0", System.Globalization.CultureInfo.InvariantCulture);

    private enum TextAlign { Left, Center, Right }

    private static void DrawText(
        SKCanvas canvas, string text, float x, float y, float size, bool bold, SKColor color, TextAlign align = TextAlign.Left)
    {
        using var typeface = SKTypeface.FromFamilyName("Arial", bold ? SKFontStyle.Bold : SKFontStyle.Normal)
                             ?? SKTypeface.Default;
        using var font = new SKFont(typeface, size);
        using var paint = new SKPaint { IsAntialias = true, Color = color };

        var width = font.MeasureText(text);
        var drawX = align switch
        {
            TextAlign.Center => x - width / 2,
            TextAlign.Right => x - width,
            _ => x
        };
        canvas.DrawText(text, drawX, y, font, paint);
    }

    private static byte[] Encode(SKSurface surface)
    {
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
