namespace Nexo.Infrastructure.Parsing;

internal class GeminiGenerateContentResponse
{
    public List<GeminiCandidate>? Candidates { get; set; }
}

internal class GeminiCandidate
{
    public GeminiContent? Content { get; set; }
}

internal class GeminiContent
{
    public List<GeminiPart>? Parts { get; set; }
}

internal class GeminiPart
{
    public string? Text { get; set; }
}

public class ExtractedIntentDto
{
    public string? Intent { get; set; }
    public string? Type { get; set; }
    public decimal? Amount { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }
    public string? OccurredAtDate { get; set; }
    public int? InstallmentCount { get; set; }
    public int? MonthOffset { get; set; }
    public int? RecentCount { get; set; }
    public string? Chart { get; set; }
    public int? DayOfMonth { get; set; }
    public string? Reply { get; set; }
}

internal class ExtractedTransactionDto
{
    public bool IsFinancialTransaction { get; set; }
    public string? Type { get; set; }
    public decimal? Amount { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }
    public string? OccurredAtDate { get; set; }
    public int? InstallmentCount { get; set; }
}
