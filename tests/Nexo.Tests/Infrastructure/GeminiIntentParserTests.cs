using Nexo.Application.Conversation;
using Nexo.Domain.Enums;
using Nexo.Infrastructure.Parsing;

namespace Nexo.Tests.Infrastructure;

public class GeminiIntentParserTests
{
    private static readonly DateTime Today = new(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("ShowMenu", IntentType.ShowMenu)]
    [InlineData("QueryMonthlySummary", IntentType.QueryMonthlySummary)]
    [InlineData("UndoLastTransaction", IntentType.UndoLastTransaction)]
    [InlineData("EditLastTransaction", IntentType.EditLastTransaction)]
    public void Map_NewIntents_AreRecognized(string name, IntentType expected)
    {
        var intent = GeminiIntentParser.Map(new ExtractedIntentDto { Intent = name, Category = "Todas" }, Today, null);

        Assert.Equal(expected, intent.Type);
    }

    [Theory]
    [InlineData("Mensal", ChartKind.Monthly)]
    [InlineData("Categorias", ChartKind.Categories)]
    [InlineData(null, ChartKind.Categories)]
    public void Map_QueryChart_ReadsTheChartKind(string? chart, ChartKind expected)
    {
        var dto = new ExtractedIntentDto { Intent = "QueryChart", Chart = chart, Category = "Todas", MonthOffset = -1 };

        var intent = GeminiIntentParser.Map(dto, Today, null);

        Assert.Equal(IntentType.QueryChart, intent.Type);
        Assert.Equal(expected, intent.Chart);
        Assert.Equal(-1, intent.MonthOffset);
    }

    [Fact]
    public void Map_EditLastTransaction_CarriesAmountCategoryAndDescription()
    {
        var dto = new ExtractedIntentDto
        {
            Intent = "EditLastTransaction", Amount = 25m, Category = "Lazer", Description = "cinema"
        };

        var intent = GeminiIntentParser.Map(dto, Today, null);

        Assert.Equal(25m, intent.Amount);
        Assert.Equal(TransactionCategory.Lazer, intent.Category);
        Assert.Equal("cinema", intent.Description);
        Assert.Null(intent.Chart);
    }

    [Fact]
    public void Map_RegisterTransaction_BuildsParsedTransaction()
    {
        var dto = new ExtractedIntentDto
        {
            Intent = "RegisterTransaction", Type = "expense", Amount = 45m, Category = "Mercado",
            Description = "Mercado", OccurredAtDate = "2026-11-22", InstallmentCount = null
        };

        var intent = GeminiIntentParser.Map(dto, Today, "texto original");

        Assert.Equal(IntentType.RegisterTransaction, intent.Type);
        Assert.NotNull(intent.Transaction);
        Assert.Equal(TransactionCategory.Mercado, intent.Transaction!.Category);
        Assert.Equal(TransactionType.Expense, intent.Transaction.Type);
        Assert.Equal(new DateTime(2026, 11, 22), intent.Transaction.OccurredAt);
    }

    [Fact]
    public void Map_RegisterTransactionWithIncomeAndInstallments_KeepsTypeAndInstallments()
    {
        var dto = new ExtractedIntentDto
        {
            Intent = "RegisterTransaction", Type = "income", Amount = 3000m, Category = "Eletronicos",
            Description = "Notebook", OccurredAtDate = "2026-11-23", InstallmentCount = 10
        };

        var intent = GeminiIntentParser.Map(dto, Today, null);

        Assert.Equal(TransactionType.Income, intent.Transaction!.Type);
        Assert.Equal(10, intent.Transaction.InstallmentCount);
    }

    [Fact]
    public void Map_RegisterTransactionWithoutAmount_BecomesUnknownAskingForValue()
    {
        var dto = new ExtractedIntentDto { Intent = "RegisterTransaction", Type = "expense", Amount = 0m, Category = "Mercado" };

        var intent = GeminiIntentParser.Map(dto, Today, "gastei no mercado");

        Assert.Equal(IntentType.Unknown, intent.Type);
        Assert.Contains("valor", intent.Reply);
    }

    [Fact]
    public void Map_RegisterTransactionWithoutDescription_UsesOriginalText()
    {
        var dto = new ExtractedIntentDto { Intent = "RegisterTransaction", Type = "expense", Amount = 10m, Category = "Outros", Description = "" };

        var intent = GeminiIntentParser.Map(dto, Today, "paguei o flanelinha");

        Assert.Equal("paguei o flanelinha", intent.Transaction!.Description);
    }

    [Fact]
    public void Map_CategoryTodas_MeansNoSpecificCategory()
    {
        var dto = new ExtractedIntentDto { Intent = "QuerySpending", Category = "Todas", MonthOffset = -1 };

        var intent = GeminiIntentParser.Map(dto, Today, null);

        Assert.Equal(IntentType.QuerySpending, intent.Type);
        Assert.Null(intent.Category);
        Assert.Equal(-1, intent.MonthOffset);
    }

    [Fact]
    public void Map_SetBudgetLimit_CarriesCategoryAndAmount()
    {
        var dto = new ExtractedIntentDto { Intent = "SetBudgetLimit", Category = "Lazer", Amount = 300m };

        var intent = GeminiIntentParser.Map(dto, Today, null);

        Assert.Equal(TransactionCategory.Lazer, intent.Category);
        Assert.Equal(300m, intent.Amount);
    }

    [Fact]
    public void Map_QueryProjection_CarriesAmountAndInstallments()
    {
        var dto = new ExtractedIntentDto { Intent = "QueryPurchaseProjection", Amount = 4000m, InstallmentCount = 12 };

        var intent = GeminiIntentParser.Map(dto, Today, null);

        Assert.Equal(4000m, intent.Amount);
        Assert.Equal(12, intent.InstallmentCount);
    }

    [Theory]
    [InlineData("algo que nao existe")]
    [InlineData("")]
    [InlineData(null)]
    public void Map_UnrecognizedIntent_FallsBackToUnknown(string? rawIntent)
    {
        var dto = new ExtractedIntentDto { Intent = rawIntent, Reply = "Não entendi." };

        var intent = GeminiIntentParser.Map(dto, Today, null);

        Assert.Equal(IntentType.Unknown, intent.Type);
        Assert.Equal("Não entendi.", intent.Reply);
    }

    [Fact]
    public void Map_CategoryOutsideTheEnum_IsIgnored()
    {
        var dto = new ExtractedIntentDto { Intent = "QuerySpending", Category = "Roupas" };

        var intent = GeminiIntentParser.Map(dto, Today, null);

        Assert.Null(intent.Category);
    }
}
