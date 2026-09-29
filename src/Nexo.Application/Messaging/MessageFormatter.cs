using System.Globalization;

namespace Nexo.Application.Messaging;

// Texto das respostas do WhatsApp. Fica num lugar só para as mensagens terem o mesmo jeito em todo o sistema.
// Negrito usa *asteriscos*, que é a formatação do próprio WhatsApp.
public static class MessageFormatter
{
    private static readonly string[] MonthNames =
    {
        "janeiro", "fevereiro", "março", "abril", "maio", "junho",
        "julho", "agosto", "setembro", "outubro", "novembro", "dezembro"
    };

    // Formato brasileiro (R$ 1.234,56) sem depender da cultura instalada na máquina.
    public static string Money(decimal value)
    {
        var digits = Math.Abs(value).ToString("N2", CultureInfo.InvariantCulture);
        var brazilian = digits.Replace(",", "#").Replace(".", ",").Replace("#", ".");
        return (value < 0 ? "-R$ " : "R$ ") + brazilian;
    }

    public static string MonthName(DateTime date) => MonthNames[date.Month - 1];

    public static string MonthLabel(DateTime date) => $"{MonthName(date)} de {date.Year}";

    public static string Percent(decimal fraction) => $"{fraction * 100:F0}%";

    // Barra de progresso feita de caracteres, que o WhatsApp mostra em qualquer celular.
    public static string Bar(decimal fraction, int width = 10)
    {
        var filled = (int)Math.Round(Math.Clamp(fraction, 0m, 1m) * width, MidpointRounding.AwayFromZero);
        return new string('▓', filled) + new string('░', width - filled);
    }

    // Verde até 70% do limite, amarelo até 100%, vermelho depois.
    public static string StatusDot(decimal fraction) => fraction >= 1m ? "🔴" : fraction >= 0.7m ? "🟡" : "🟢";

    public const string Menu = """
        👋 Oi! Eu sou o *NEXO*, seu assistente financeiro.

        📝 *Registrar*
        • gastei 30 no mercado
        • recebi 2500 de salário
        • comprei um celular em 10x de 300
        • 🎤 um áudio ou 📸 a foto de uma nota fiscal

        📊 *Consultar*
        • saldo
        • resumo do mês (ou "resumo de outubro")
        • gastos por categoria
        • últimos lançamentos

        📈 *Gráficos*
        • gráfico por categoria
        • gráfico dos últimos meses

        🎯 *Planejar*
        • limite de 500 para mercado
        • como estão meus limites?
        • consigo comprar um notebook de 3000?

        ↩️ *Corrigir*
        • apaga o último
        • corrige para 25

        Digite *menu* a qualquer momento para ver esta lista.
        """;

    public const string MenuHint = "💡 Digite *menu* para ver tudo que eu sei fazer.";
}
