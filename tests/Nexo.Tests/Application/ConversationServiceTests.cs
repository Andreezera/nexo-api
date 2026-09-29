using Moq;
using Nexo.Application.Abstractions;
using Nexo.Application.Budgets;
using Nexo.Application.Charts;
using Nexo.Application.Conversation;
using Nexo.Application.Parsing;
using Nexo.Application.Projections;
using Nexo.Application.Reports;
using Nexo.Application.Transactions;
using Nexo.Domain.Entities;
using Nexo.Domain.Enums;

namespace Nexo.Tests.Application;

public class ConversationServiceTests
{
    private const string Phone = "5511999999999";

    private readonly Mock<IMessageIntentParser> _intentParser = new();
    private readonly Mock<ITransactionRegistrar> _registrar = new();
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<ITransactionRepository> _transactionRepository = new();
    private readonly Mock<IBudgetLimitRepository> _budgetLimitRepository = new();
    private readonly Mock<IBudgetAlertService> _budgetAlertService = new();
    private readonly Mock<IPurchaseProjectionService> _projectionService = new();
    private readonly Mock<IConversationHistoryRepository> _historyRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly User _user = new(Phone);

    public ConversationServiceTests()
    {
        _userRepository.Setup(u => u.GetByPhoneNumberAsync(Phone, It.IsAny<CancellationToken>())).ReturnsAsync(_user);
        _historyRepository.Setup(h => h.GetRecentAsync(_user.Id, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ConversationMessage>());
        _transactionRepository.Setup(t => t.GetByUserIdAsync(_user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Transaction>());
    }

    private readonly Mock<IChartRenderer> _chartRenderer = new();

    private ConversationService CreateService()
    {
        var reportService = new ReportService(_transactionRepository.Object);
        return new ConversationService(
            _intentParser.Object, _registrar.Object, _userRepository.Object, _transactionRepository.Object,
            _budgetLimitRepository.Object, _budgetAlertService.Object, _projectionService.Object,
            reportService, new ChartService(reportService, _chartRenderer.Object),
            _historyRepository.Object, _unitOfWork.Object);
    }

    private void SetupIntent(MessageIntent? intent) =>
        _intentParser.Setup(p => p.ParseAsync(It.IsAny<MessageContent>(), It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(intent);

    private void SetupTransactions(params Transaction[] transactions) =>
        _transactionRepository.Setup(t => t.GetByUserIdAsync(_user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(transactions);

    private Task<RegisterMessageResult> Handle(string text = "mensagem") =>
        CreateService().HandleAsync(Phone, MessageContent.FromText(text));

    [Fact]
    public async Task HandleAsync_WhenIntentIsRegister_DelegatesToRegistrar()
    {
        var parsed = new ParsedTransaction(TransactionType.Expense, 30m, TransactionCategory.Mercado, "Mercado", DateTime.UtcNow, null);
        SetupIntent(new MessageIntent(IntentType.RegisterTransaction, Transaction: parsed));
        _registrar.Setup(r => r.RegisterAsync(Phone, parsed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegisterMessageResult(true, "Despesa registrada."));

        var result = await Handle("gastei 30 no mercado");

        Assert.True(result.Understood);
        Assert.Equal("Despesa registrada.", result.ReplyMessage);
    }

    [Fact]
    public async Task HandleAsync_SavesUserMessageAndReplyInHistory()
    {
        SetupIntent(new MessageIntent(IntentType.QueryBalance));

        await Handle("qual meu saldo?");

        _historyRepository.Verify(h => h.AddAsync(
            It.Is<ConversationMessage>(m => m.Role == ConversationRole.User && m.Text == "qual meu saldo?"),
            It.IsAny<CancellationToken>()), Times.Once);
        _historyRepository.Verify(h => h.AddAsync(
            It.Is<ConversationMessage>(m => m.Role == ConversationRole.Assistant), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenProviderFails_RepliesWithProblemMessageAndDoesNotRegister()
    {
        SetupIntent(null);

        var result = await Handle();

        Assert.False(result.Understood);
        Assert.Contains("problema", result.ReplyMessage);
        _registrar.Verify(r => r.RegisterAsync(It.IsAny<string>(), It.IsAny<ParsedTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenPhoneIsUnknown_CreatesUserBeforeAnythingElse()
    {
        _userRepository.Setup(u => u.GetByPhoneNumberAsync("0000", It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        _historyRepository.Setup(h => h.GetRecentAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ConversationMessage>());
        SetupIntent(new MessageIntent(IntentType.Unknown, Reply: "Oi!"));

        await CreateService().HandleAsync("0000", MessageContent.FromText("oi"));

        _userRepository.Verify(u => u.AddAsync(It.Is<User>(x => x.PhoneNumber == "0000"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenIntentIsUnknown_ReturnsModelReply()
    {
        SetupIntent(new MessageIntent(IntentType.Unknown, Reply: "Oi! Posso te ajudar com seus gastos."));

        var result = await Handle("oi");

        Assert.False(result.Understood);
        Assert.Equal("Oi! Posso te ajudar com seus gastos.", result.ReplyMessage);
    }

    [Fact]
    public async Task HandleAsync_QueryBalance_SumsIncomeMinusExpense()
    {
        SetupTransactions(
            new Transaction(_user.Id, TransactionType.Income, 1000m, TransactionCategory.Salario, "salário", DateTime.UtcNow.AddDays(-3)),
            new Transaction(_user.Id, TransactionType.Expense, 250m, TransactionCategory.Mercado, "mercado", DateTime.UtcNow.AddDays(-2)));
        SetupIntent(new MessageIntent(IntentType.QueryBalance));

        var result = await Handle();

        Assert.True(result.Understood);
        Assert.Contains($"{750m:F2}", result.ReplyMessage);
    }

    [Fact]
    public async Task HandleAsync_QueryBalance_IgnoresFutureInstallments()
    {
        SetupTransactions(
            new Transaction(_user.Id, TransactionType.Income, 1000m, TransactionCategory.Salario, "salário", DateTime.UtcNow.AddDays(-3)),
            new Transaction(_user.Id, TransactionType.Expense, 400m, TransactionCategory.Eletronicos, "parcela futura", DateTime.UtcNow.AddMonths(1)));
        SetupIntent(new MessageIntent(IntentType.QueryBalance));

        var result = await Handle();

        Assert.Contains("R$ 1.000,00", result.ReplyMessage);
    }

    [Fact]
    public async Task HandleAsync_QuerySpendingByCategory_ReturnsTotalForCategoryAndMonth()
    {
        SetupTransactions(
            new Transaction(_user.Id, TransactionType.Expense, 120m, TransactionCategory.Mercado, "a", DateTime.UtcNow),
            new Transaction(_user.Id, TransactionType.Expense, 80m, TransactionCategory.Mercado, "b", DateTime.UtcNow),
            new Transaction(_user.Id, TransactionType.Expense, 500m, TransactionCategory.Lazer, "c", DateTime.UtcNow));
        SetupIntent(new MessageIntent(IntentType.QuerySpending, Category: TransactionCategory.Mercado));

        var result = await Handle();

        Assert.Contains("Mercado", result.ReplyMessage);
        Assert.Contains($"{200m:F2}", result.ReplyMessage);
    }

    [Fact]
    public async Task HandleAsync_QuerySpendingWithoutCategory_ListsTopCategories()
    {
        SetupTransactions(
            new Transaction(_user.Id, TransactionType.Expense, 120m, TransactionCategory.Mercado, "a", DateTime.UtcNow),
            new Transaction(_user.Id, TransactionType.Expense, 500m, TransactionCategory.Lazer, "c", DateTime.UtcNow));
        SetupIntent(new MessageIntent(IntentType.QuerySpending));

        var result = await Handle();

        Assert.Contains($"{620m:F2}", result.ReplyMessage);
        Assert.Contains("Lazer", result.ReplyMessage);
    }

    [Fact]
    public async Task HandleAsync_QuerySpendingPreviousMonth_OnlyCountsThatMonth()
    {
        SetupTransactions(
            new Transaction(_user.Id, TransactionType.Expense, 100m, TransactionCategory.Mercado, "este mês", DateTime.UtcNow),
            new Transaction(_user.Id, TransactionType.Expense, 300m, TransactionCategory.Mercado, "mês passado", DateTime.UtcNow.AddMonths(-1)));
        SetupIntent(new MessageIntent(IntentType.QuerySpending, Category: TransactionCategory.Mercado, MonthOffset: -1));

        var result = await Handle();

        Assert.Contains($"{300m:F2}", result.ReplyMessage);
    }

    [Fact]
    public async Task HandleAsync_QueryRecent_ListsMostRecentFirst()
    {
        SetupTransactions(
            new Transaction(_user.Id, TransactionType.Expense, 10m, TransactionCategory.Outros, "antigo", DateTime.UtcNow.AddDays(-10)),
            new Transaction(_user.Id, TransactionType.Expense, 20m, TransactionCategory.Outros, "novo", DateTime.UtcNow.AddDays(-1)));
        SetupIntent(new MessageIntent(IntentType.QueryRecentTransactions, RecentCount: 5));

        var result = await Handle();

        Assert.True(result.ReplyMessage.IndexOf("novo", StringComparison.Ordinal) < result.ReplyMessage.IndexOf("antigo", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HandleAsync_QueryProjectionWithoutAmount_AsksForTheValue()
    {
        SetupIntent(new MessageIntent(IntentType.QueryPurchaseProjection));

        var result = await Handle();

        Assert.Contains("valor", result.ReplyMessage);
        _projectionService.Verify(p => p.ProjectAsync(
            It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_QueryProjection_UsesProjectionService()
    {
        SetupIntent(new MessageIntent(IntentType.QueryPurchaseProjection, Amount: 3000m));
        _projectionService.Setup(p => p.ProjectAsync(_user.Id, 3000m, 1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PurchaseProjection(3000m, 5000m, 3000m, 2000m, 1000m, true, 0, DateTime.UtcNow, 1, 3000m, false));

        var result = await Handle();

        Assert.Contains("comprar agora", result.ReplyMessage);
    }

    [Fact]
    public async Task HandleAsync_QueryBudgetsWithoutLimits_ExplainsHowToCreateOne()
    {
        _budgetAlertService.Setup(b => b.GetAllStatusesAsync(_user.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<BudgetStatus>());
        SetupIntent(new MessageIntent(IntentType.QueryBudgets));

        var result = await Handle();

        Assert.Contains("limite", result.ReplyMessage);
    }

    [Fact]
    public async Task HandleAsync_SetBudgetLimit_CreatesLimit()
    {
        _budgetLimitRepository.Setup(b => b.GetAsync(_user.Id, TransactionCategory.Mercado, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BudgetLimit?)null);
        SetupIntent(new MessageIntent(IntentType.SetBudgetLimit, Category: TransactionCategory.Mercado, Amount: 500m));

        var result = await Handle();

        Assert.True(result.Understood);
        _budgetLimitRepository.Verify(b => b.AddAsync(
            It.Is<BudgetLimit>(l => l.Category == TransactionCategory.Mercado && l.MonthlyLimit == 500m),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_SetBudgetLimit_UpdatesWhenLimitAlreadyExists()
    {
        var existing = new BudgetLimit(_user.Id, TransactionCategory.Lazer, 100m);
        _budgetLimitRepository.Setup(b => b.GetAsync(_user.Id, TransactionCategory.Lazer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        SetupIntent(new MessageIntent(IntentType.SetBudgetLimit, Category: TransactionCategory.Lazer, Amount: 250m));

        await Handle();

        Assert.Equal(250m, existing.MonthlyLimit);
        _budgetLimitRepository.Verify(b => b.AddAsync(It.IsAny<BudgetLimit>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SetBudgetLimitWithoutCategory_AsksForIt()
    {
        SetupIntent(new MessageIntent(IntentType.SetBudgetLimit, Amount: 500m));

        var result = await Handle();

        Assert.False(result.Understood);
        _budgetLimitRepository.Verify(b => b.AddAsync(It.IsAny<BudgetLimit>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PassesRecentHistoryToTheParser()
    {
        var history = new[] { new ConversationMessage(_user.Id, ConversationRole.User, "quanto gastei com mercado?") };
        _historyRepository.Setup(h => h.GetRecentAsync(_user.Id, ConversationService.HistoryTurns, It.IsAny<CancellationToken>()))
            .ReturnsAsync(history);
        SetupIntent(new MessageIntent(IntentType.Unknown, Reply: "ok"));

        await Handle("e com lazer?");

        _intentParser.Verify(p => p.ParseAsync(
            It.IsAny<MessageContent>(), It.Is<IReadOnlyList<ConversationMessage>>(h => h.Count == 1), It.IsAny<CancellationToken>()), Times.Once);
    }
}
