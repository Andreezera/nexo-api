using Nexo.Infrastructure.Parsing;
using Xunit;

namespace Nexo.Tests.Infrastructure;

public class GeminiTransactionMessageParserTests
{
    private static readonly DateTime Today = new(2026, 7, 23);

    [Fact]
    public void ResolveOccurredAt_WhenDateIsValid_ReturnsParsedDateAsUtc()
    {
        var result = GeminiTransactionMessageParser.ResolveOccurredAt("2026-07-20", Today);

        Assert.Equal(new DateTime(2026, 7, 20), result);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveOccurredAt_WhenDateIsMissing_FallsBackToToday(string? occurredAtDate)
    {
        var result = GeminiTransactionMessageParser.ResolveOccurredAt(occurredAtDate, Today);

        Assert.Equal(Today, result);
    }

    [Fact]
    public void ResolveOccurredAt_WhenDateIsInInvalidFormat_FallsBackToToday()
    {
        var result = GeminiTransactionMessageParser.ResolveOccurredAt("20/07/2026", Today);

        Assert.Equal(Today, result);
    }
}
