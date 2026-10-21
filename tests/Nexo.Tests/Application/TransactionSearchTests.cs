using Moq;
using Nexo.Application.Abstractions;
using Nexo.Application.Conversation;
using Nexo.Application.Search;
using Nexo.Domain.Entities;
using Nexo.Domain.Enums;

namespace Nexo.Tests.Application;

public class TransactionSearchTests
{
    private static readonly DateTime Today = new(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);
    private readonly User _user = new("5511955554444");
    private readonly Mock<ITransactionRepository> _repository = new();
    private readonly Mock<IReplyComposer> _composer = new();

    private static DateTime D(int month, int day) => new(2026, month, day, 0, 0, 0, DateTimeKind.Utc);

    private Transaction Expense(decimal amount, string description, DateTime when, TransactionCategory category = TransactionCategory.Transporte) =>
        new(_user.Id, TransactionType.Expense, amount, category, description, when);

    private Transaction Income(decimal amount, string description, DateTime when) =>
        new(_user.Id, TransactionType.Income, amount, TransactionCategory.Salario, description, when);

    private TransactionSearchService CreateService(params Transaction[] transactions)
    {
        _repository.Setup(r => r.GetByUserIdAsync(_user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(transactions);
        return new TransactionSearchService(_repository.Object);
    }

    private Transaction[] Sample() => new[]
    {
        Expense(30m, "Uber", D(11, 3)),
        Expense(58m, "uber eats", D(10, 20), TransactionCategory.Alimentacao),
        Expense(20m, "Açaí", D(9, 1), TransactionCategory.Alimentacao),
        Expense(400m, "Compras", D(11, 10), TransactionCategory.Mercado),
        Income(3000m, "Salário", D(11, 5)),
        Expense(100m, "Notebook parcela", D(12, 5), TransactionCategory.Eletronicos)
    };

    [Fact]
    public async Task Text_IgnoresCaseAndAccents()
    {
        var service = CreateService(Sample());

        var uber = await service.SearchAsync(_user.Id, new TransactionSearchQuery("UBER", null, TransactionType.Expense, null, null), Today);
        var acai = await service.SearchAsync(_user.Id, new TransactionSearchQuery("acai", null, TransactionType.Expense, null, null), Today);

        Assert.Equal(2, uber.Count);
        Assert.Equal(88m, uber.Total);
        Assert.Equal(1, acai.Count);
    }

    [Fact]
    public async Task Text_AlsoMatchesTheCategoryName()
    {
        var result = await CreateService(Sample()).SearchAsync(
            _user.Id, new TransactionSearchQuery("mercado", null, TransactionType.Expense, null, null), Today);

        Assert.Equal(400m, result.Total);
    }

    [Fact]
    public async Task Period_FiltersByStartAndEndInclusive()
    {
        var result = await CreateService(Sample()).SearchAsync(
            _user.Id, new TransactionSearchQuery("uber", null, null, D(11, 1), D(11, 3)), Today);

        Assert.Equal(1, result.Count);
        Assert.Equal(30m, result.Total);
    }

    [Fact]
    public async Task WithoutAnEndDate_FutureTransactionsAreLeftOut()
    {
        var result = await CreateService(Sample()).SearchAsync(
            _user.Id, new TransactionSearchQuery("notebook", null, TransactionType.Expense, null, null), Today);

        Assert.Equal(0, result.Count);
    }

    [Fact]
    public async Task Kind_SeparatesIncomeFromExpenses()
    {
        var service = CreateService(Sample());

        var income = await service.SearchAsync(_user.Id, new TransactionSearchQuery(null, null, TransactionType.Income, null, null), Today);
        var both = await service.SearchAsync(_user.Id, new TransactionSearchQuery(null, null, null, D(11, 1), D(11, 30)), Today);

        Assert.Equal(3000m, income.Total);
        Assert.Equal(3430m, both.Total);
    }

    [Fact]
    public async Task Category_FiltersAndTotalsAreComputed()
    {
        var result = await CreateService(Sample()).SearchAsync(
            _user.Id, new TransactionSearchQuery(null, TransactionCategory.Alimentacao, TransactionType.Expense, null, null), Today);

        Assert.Equal(2, result.Count);
        Assert.Equal(78m, result.Total);
        Assert.Equal(39m, result.Average);
        Assert.Equal(58m, result.Largest!.Amount);
    }

    [Fact]
    public async Task Matches_AreOrderedFromTheNewest()
    {
        var result = await CreateService(Sample()).SearchAsync(
            _user.Id, new TransactionSearchQuery("uber", null, null, null, null), Today);

        Assert.Equal("Uber", result.Matches[0].Description);
    }

    private TransactionSearchIntentHandler CreateHandler(params Transaction[] transactions) =>
        new(CreateService(transactions), _composer.Object);

    private static MessageIntent Intent(SearchAggregation aggregation, string text = "uber") =>
        new(IntentType.QueryTransactions, SearchText: text, Kind: TransactionType.Expense,
            Aggregation: aggregation, OriginalText: "quanto gastei com uber?");

    private static Transaction[] AllInThePast(User user) => new[]
    {
        new Transaction(user.Id, TransactionType.Expense, 30m, TransactionCategory.Transporte, "Uber", DateTime.UtcNow.AddDays(-5)),
        new Transaction(user.Id, TransactionType.Expense, 58m, TransactionCategory.Transporte, "Uber", DateTime.UtcNow.AddDays(-9))
    };

    [Fact]
    public async Task Handler_UsesTheComposedTextWhenItCarriesTheComputedFigure()
    {
        _composer.Setup(c => c.ComposeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Foram *R$ 88,00* de Uber até agora 🚗");
        var handler = CreateHandler(AllInThePast(_user));

        var result = await handler.HandleAsync(_user, Intent(SearchAggregation.Sum));

        Assert.Equal("Foram *R$ 88,00* de Uber até agora 🚗", result.ReplyMessage);
        _composer.Verify(c => c.ComposeAsync("quanto gastei com uber?", It.Is<string>(f => f.Contains("R$ 88,00") && f.Contains("Quantidade de lançamentos: 2")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handler_FallsBackWhenTheComposedTextInventsADifferentNumber()
    {
        _composer.Setup(c => c.ComposeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Você gastou *R$ 999,00* com Uber.");

        var result = await CreateHandler(AllInThePast(_user)).HandleAsync(_user, Intent(SearchAggregation.Sum));

        Assert.DoesNotContain("999", result.ReplyMessage);
        Assert.Contains("R$ 88,00", result.ReplyMessage);
    }

    [Fact]
    public async Task Handler_FallsBackWhenTheComposerFails()
    {
        _composer.Setup(c => c.ComposeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await CreateHandler(AllInThePast(_user)).HandleAsync(_user, Intent(SearchAggregation.Sum));

        Assert.True(result.Understood);
        Assert.Contains("R$ 88,00", result.ReplyMessage);
        Assert.Contains("2 lançamento", result.ReplyMessage);
    }

    [Fact]
    public async Task Handler_WithNoResults_DoesNotCallTheComposer()
    {
        var result = await CreateHandler(AllInThePast(_user)).HandleAsync(_user, Intent(SearchAggregation.Sum, "ifood"));

        Assert.Contains("Não encontrei", result.ReplyMessage);
        _composer.Verify(c => c.ComposeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(SearchAggregation.Count, "*2* lançamento")]
    [InlineData(SearchAggregation.Average, "R$ 44,00")]
    [InlineData(SearchAggregation.Largest, "R$ 58,00")]
    [InlineData(SearchAggregation.List, "📤")]
    public async Task Handler_FallbackAnswersEachKindOfQuestion(SearchAggregation aggregation, string expected)
    {
        _composer.Setup(c => c.ComposeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await CreateHandler(AllInThePast(_user)).HandleAsync(_user, Intent(aggregation));

        Assert.Contains(expected, result.ReplyMessage);
    }

    [Fact]
    public async Task Handler_ForIncomeUsesTheRightVerb()
    {
        _composer.Setup(c => c.ComposeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        var salary = new[] { new Transaction(_user.Id, TransactionType.Income, 3000m, TransactionCategory.Salario, "Salário", DateTime.UtcNow.AddDays(-2)) };
        var intent = new MessageIntent(IntentType.QueryTransactions, Kind: TransactionType.Income, Aggregation: SearchAggregation.Sum);

        var result = await CreateHandler(salary).HandleAsync(_user, intent);

        Assert.Contains("Você recebeu", result.ReplyMessage);
        Assert.Contains("R$ 3.000,00", result.ReplyMessage);
    }
}
