using Nexo.Application.Messaging;

namespace Nexo.Tests.Application;

public class MessageFormatterTests
{
    [Theory]
    [InlineData(20, "R$ 20,00")]
    [InlineData(1538.8, "R$ 1.538,80")]
    [InlineData(1234567.89, "R$ 1.234.567,89")]
    [InlineData(-1490.22, "-R$ 1.490,22")]
    [InlineData(0, "R$ 0,00")]
    public void Money_UsesBrazilianFormat(double value, string expected)
    {
        Assert.Equal(expected, MessageFormatter.Money((decimal)value));
    }

    [Theory]
    [InlineData(0, "░░░░░░░░░░")]
    [InlineData(0.5, "▓▓▓▓▓░░░░░")]
    [InlineData(1, "▓▓▓▓▓▓▓▓▓▓")]
    [InlineData(2.5, "▓▓▓▓▓▓▓▓▓▓")]
    [InlineData(-1, "░░░░░░░░░░")]
    public void Bar_FillsProportionallyAndClamps(double fraction, string expected)
    {
        Assert.Equal(expected, MessageFormatter.Bar((decimal)fraction));
    }

    [Theory]
    [InlineData(0.2, "🟢")]
    [InlineData(0.7, "🟡")]
    [InlineData(0.99, "🟡")]
    [InlineData(1.0, "🔴")]
    [InlineData(1.4, "🔴")]
    public void StatusDot_ChangesColorWithUsage(double fraction, string expected)
    {
        Assert.Equal(expected, MessageFormatter.StatusDot((decimal)fraction));
    }

    [Fact]
    public void MonthLabel_UsesPortugueseNamesRegardlessOfCulture()
    {
        Assert.Equal("março de 2026", MessageFormatter.MonthLabel(new DateTime(2026, 3, 10)));
        Assert.Equal("novembro", MessageFormatter.MonthName(new DateTime(2026, 11, 1)));
    }

    [Fact]
    public void Percent_RoundsToWholeNumber()
    {
        Assert.Equal("92%", MessageFormatter.Percent(0.9234m));
    }

    [Fact]
    public void Menu_MentionsTheMainFeatures()
    {
        Assert.Contains("Registrar", MessageFormatter.Menu);
        Assert.Contains("Gráficos", MessageFormatter.Menu);
        Assert.Contains("Corrigir", MessageFormatter.Menu);
    }
}
