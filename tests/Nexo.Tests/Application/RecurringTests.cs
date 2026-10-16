using Moq;
using Nexo.Application.Abstractions;
using Nexo.Application.Conversation;
using Nexo.Application.Parsing;
using Nexo.Application.Recurring;
using Nexo.Domain.Entities;
using Nexo.Domain.Enums;

namespace Nexo.Tests.Application;

public class RecurringTests
{
    private readonly User _user = new("5511977776666");
    private readonly Mock<IRecurringTransactionRepository> _repository = new();
    private readonly Mock<ITransactionRepository> _transactions = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private static DateTime Today => DateTime.UtcNow;

    private RecurringTransaction Existing(string description, decimal amount, int day, TransactionType type = TransactionType.Expense) =>
        new(_user.Id, type, amount, TransactionCategory.Assinaturas, description, day, Today);

    private void SetupActive(params RecurringTransaction[] items) =>
        _repository.Setup(r => r.GetActiveByUserIdAsync(_user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(items);

    private RecurringIntentHandler CreateHandler()
    {
        var service = new RecurringTransactionService(_repository.Object, _transactions.Object, _unitOfWork.Object);
        return new RecurringIntentHandler(_repository.Object, service, _unitOfWork.Object);
    }

    private static ParsedTransaction Parsed(decimal amount, string description) =>
        new(TransactionType.Expense, amount, TransactionCategory.Assinaturas, description, Today, null);

    [Fact]
    public async Task Service_GeneratesOnlyTheDueOnesAndSavesOnce()
    {
        var created = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);
        // Dia 5, criada no dia 3: vence a partir do dia 5.
        var due = new RecurringTransaction(_user.Id, TransactionType.Expense, 50m, TransactionCategory.Moradia, "Aluguel", 5, created);
        // Dia 20: ainda não chegou.
        var notYet = new RecurringTransaction(_user.Id, TransactionType.Expense, 20m, TransactionCategory.Assinaturas, "Internet", 20, created);
        // Dia 1, criada no dia 3: o dia já tinha passado, então só começa em dezembro.
        var startsNextMonth = new RecurringTransaction(_user.Id, TransactionType.Expense, 30m, TransactionCategory.Assinaturas, "Netflix", 1, created);
        _repository.Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { due, notYet, startsNextMonth });
        var service = new RecurringTransactionService(_repository.Object, _transactions.Object, _unitOfWork.Object);
        var today = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

        var first = await service.GenerateDueAsync(today);
        var second = await service.GenerateDueAsync(today);

        Assert.Equal(1, first);
        Assert.Equal(0, second);
        _transactions.Verify(t => t.AddAsync(
            It.Is<Transaction>(x => x.Description == "Aluguel" && x.Amount == 50m && x.OccurredAt.Date == new DateTime(2026, 11, 5)),
            It.IsAny<CancellationToken>()), Times.Once);
        _transactions.Verify(t => t.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_WithValueAndDay_SavesAndConfirms()
    {
        SetupActive();
        var handler = CreateHandler();

        var result = await handler.HandleAsync(_user,
            new MessageIntent(IntentType.CreateRecurringTransaction, Transaction: Parsed(55.90m, "Netflix"), DayOfMonth: 10));

        Assert.True(result.Understood);
        Assert.Contains("Conta recorrente criada", result.ReplyMessage);
        Assert.Contains("R$ 55,90", result.ReplyMessage);
        Assert.Contains("Todo dia 10", result.ReplyMessage);
        _repository.Verify(r => r.AddAsync(
            It.Is<RecurringTransaction>(x => x.Amount == 55.90m && x.DayOfMonth == 10 && x.Description == "Netflix"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(40)]
    public async Task Create_WithoutAValidDay_AsksForIt(int? day)
    {
        var result = await CreateHandler().HandleAsync(_user,
            new MessageIntent(IntentType.CreateRecurringTransaction, Transaction: Parsed(55.90m, "Netflix"), DayOfMonth: day));

        Assert.False(result.Understood);
        Assert.Contains("dia do mês", result.ReplyMessage);
        _repository.Verify(r => r.AddAsync(It.IsAny<RecurringTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_WithoutAValue_AsksForIt()
    {
        var result = await CreateHandler().HandleAsync(_user,
            new MessageIntent(IntentType.CreateRecurringTransaction, Transaction: null, DayOfMonth: 10));

        Assert.False(result.Understood);
    }

    [Fact]
    public async Task List_ShowsEachOneAndTheMonthlyFixedTotal()
    {
        SetupActive(
            Existing("Netflix", 55.90m, 10),
            Existing("Aluguel", 1200m, 5),
            Existing("Salário", 3000m, 5, TransactionType.Income));

        var result = await CreateHandler().HandleAsync(_user, new MessageIntent(IntentType.QueryRecurringTransactions));

        Assert.Contains("Netflix", result.ReplyMessage);
        Assert.Contains("Aluguel", result.ReplyMessage);
        Assert.Contains("Salário", result.ReplyMessage);
        Assert.Contains("R$ 1.255,90", result.ReplyMessage);
    }

    [Fact]
    public async Task List_WhenThereAreNone_ExplainsHowToCreateOne()
    {
        SetupActive();

        var result = await CreateHandler().HandleAsync(_user, new MessageIntent(IntentType.QueryRecurringTransactions));

        Assert.True(result.Understood);
        Assert.Contains("todo dia 10", result.ReplyMessage);
    }

    [Fact]
    public async Task Cancel_WithAMatchingName_DeactivatesIt()
    {
        var netflix = Existing("Netflix", 55.90m, 10);
        SetupActive(netflix, Existing("Aluguel", 1200m, 5));

        var result = await CreateHandler().HandleAsync(_user,
            new MessageIntent(IntentType.CancelRecurringTransaction, Description: "netflix"));

        Assert.True(result.Understood);
        Assert.False(netflix.IsActive);
        Assert.Contains("Netflix", result.ReplyMessage);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Cancel_IgnoresAccentsAndCase()
    {
        var item = Existing("Açaí do mês", 30m, 3);
        SetupActive(item);

        await CreateHandler().HandleAsync(_user, new MessageIntent(IntentType.CancelRecurringTransaction, Description: "ACAI"));

        Assert.False(item.IsActive);
    }

    [Fact]
    public async Task Cancel_WithNoMatch_ListsTheOptions()
    {
        var item = Existing("Netflix", 55.90m, 10);
        SetupActive(item);

        var result = await CreateHandler().HandleAsync(_user,
            new MessageIntent(IntentType.CancelRecurringTransaction, Description: "spotify"));

        Assert.False(result.Understood);
        Assert.True(item.IsActive);
        Assert.Contains("Netflix", result.ReplyMessage);
    }

    [Fact]
    public async Task Cancel_WithAnAmbiguousName_AsksWhichOne()
    {
        var a = Existing("Netflix básico", 30m, 10);
        var b = Existing("Netflix premium", 55m, 12);
        SetupActive(a, b);

        var result = await CreateHandler().HandleAsync(_user,
            new MessageIntent(IntentType.CancelRecurringTransaction, Description: "netflix"));

        Assert.False(result.Understood);
        Assert.True(a.IsActive && b.IsActive);
        Assert.Contains("Mais de uma", result.ReplyMessage);
    }
}
