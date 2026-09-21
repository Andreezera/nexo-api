using System.Globalization;
using Nexo.Application.Abstractions;
using Nexo.Application.Budgets;
using Nexo.Application.Projections;
using Nexo.Application.Transactions;
using Nexo.Domain.Entities;
using Nexo.Domain.Enums;

namespace Nexo.Application.Conversation;

// Porta de entrada das mensagens do WhatsApp. Pede para a IA classificar a intenção (levando em conta
// as últimas mensagens da conversa), executa e responde. Registrar uma transação continua sendo
// responsabilidade do ITransactionRegistrar, o resto são consultas e ajustes sobre os dados do usuário.
public class ConversationService
{
    public const int HistoryTurns = 6;

    private const string ProviderFailureMessage =
        "Tive um problema para interpretar sua mensagem agora. Tente de novo em instantes.";

    private const string NotUnderstoodMessage =
        "Não consegui entender essa mensagem. Tente algo como \"gastei 30 reais no mercado\" ou \"quanto gastei esse mês?\".";

    private static readonly CultureInfo PtBr = CreatePtBr();

    private readonly IMessageIntentParser _intentParser;
    private readonly ITransactionRegistrar _registrar;
    private readonly IUserRepository _userRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IBudgetLimitRepository _budgetLimitRepository;
    private readonly IBudgetAlertService _budgetAlertService;
    private readonly IPurchaseProjectionService _projectionService;
    private readonly IConversationHistoryRepository _historyRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ConversationService(
        IMessageIntentParser intentParser,
        ITransactionRegistrar registrar,
        IUserRepository userRepository,
        ITransactionRepository transactionRepository,
        IBudgetLimitRepository budgetLimitRepository,
        IBudgetAlertService budgetAlertService,
        IPurchaseProjectionService projectionService,
        IConversationHistoryRepository historyRepository,
        IUnitOfWork unitOfWork)
    {
        _intentParser = intentParser;
        _registrar = registrar;
        _userRepository = userRepository;
        _transactionRepository = transactionRepository;
        _budgetLimitRepository = budgetLimitRepository;
        _budgetAlertService = budgetAlertService;
        _projectionService = projectionService;
        _historyRepository = historyRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<RegisterMessageResult> HandleAsync(
        string phoneNumber, MessageContent content, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByPhoneNumberAsync(phoneNumber, cancellationToken);
        if (user is null)
        {
            // O histórico da conversa precisa de um usuário, então ele é criado já no primeiro contato.
            user = new User(phoneNumber);
            await _userRepository.AddAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var history = await _historyRepository.GetRecentAsync(user.Id, HistoryTurns, cancellationToken);
        var intent = await _intentParser.ParseAsync(content, history, cancellationToken);

        var result = intent is null
            ? new RegisterMessageResult(false, ProviderFailureMessage)
            : await ExecuteAsync(user, phoneNumber, intent, cancellationToken);

        await _historyRepository.AddAsync(
            new ConversationMessage(user.Id, ConversationRole.User, content.Describe()), cancellationToken);
        await _historyRepository.AddAsync(
            new ConversationMessage(user.Id, ConversationRole.Assistant, result.ReplyMessage), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return result;
    }

    private async Task<RegisterMessageResult> ExecuteAsync(
        User user, string phoneNumber, MessageIntent intent, CancellationToken cancellationToken)
    {
        switch (intent.Type)
        {
            case IntentType.RegisterTransaction when intent.Transaction is not null:
                return await _registrar.RegisterAsync(phoneNumber, intent.Transaction, cancellationToken);

            case IntentType.QueryBalance:
                return new RegisterMessageResult(true, await BalanceReplyAsync(user, cancellationToken));

            case IntentType.QuerySpending:
                return new RegisterMessageResult(true,
                    await SpendingReplyAsync(user, intent.Category, intent.MonthOffset, cancellationToken));

            case IntentType.QueryRecentTransactions:
                return new RegisterMessageResult(true,
                    await RecentReplyAsync(user, intent.RecentCount, cancellationToken));

            case IntentType.QueryPurchaseProjection:
                return new RegisterMessageResult(true,
                    await ProjectionReplyAsync(user, intent.Amount, intent.InstallmentCount, cancellationToken));

            case IntentType.QueryBudgets:
                return new RegisterMessageResult(true, await BudgetsReplyAsync(user, cancellationToken));

            case IntentType.SetBudgetLimit:
                return await SetBudgetLimitAsync(user, intent.Category, intent.Amount, cancellationToken);

            default:
                return new RegisterMessageResult(false,
                    string.IsNullOrWhiteSpace(intent.Reply) ? NotUnderstoodMessage : intent.Reply);
        }
    }

    private async Task<string> BalanceReplyAsync(User user, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var transactions = (await _transactionRepository.GetByUserIdAsync(user.Id, cancellationToken))
            .Where(t => t.OccurredAt <= now)
            .ToList();

        if (transactions.Count == 0)
            return "Você ainda não tem lançamentos registrados.";

        var income = transactions.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);
        var expense = transactions.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);
        return $"Seu saldo hoje é {Money(income - expense)}. Receitas: {Money(income)}, despesas: {Money(expense)}.";
    }

    private async Task<string> SpendingReplyAsync(
        User user, TransactionCategory? category, int monthOffset, CancellationToken cancellationToken)
    {
        var offset = Math.Clamp(monthOffset, -12, 0);
        var reference = DateTime.UtcNow.AddMonths(offset);
        var monthLabel = MonthLabel(reference);

        var expenses = (await _transactionRepository.GetByUserIdAsync(user.Id, cancellationToken))
            .Where(t => t.Type == TransactionType.Expense
                        && t.OccurredAt.Year == reference.Year
                        && t.OccurredAt.Month == reference.Month)
            .ToList();

        if (category is not null)
        {
            var inCategory = expenses.Where(t => t.Category == category).Sum(t => t.Amount);
            return inCategory == 0
                ? $"Não encontrei gastos em {category.Value.ToDisplayName()} em {monthLabel}."
                : $"Em {monthLabel}, você gastou {Money(inCategory)} em {category.Value.ToDisplayName()}.";
        }

        if (expenses.Count == 0)
            return $"Não encontrei gastos em {monthLabel}.";

        var total = expenses.Sum(t => t.Amount);
        var top = expenses
            .GroupBy(t => t.Category)
            .Select(g => new { Category = g.Key, Total = g.Sum(t => t.Amount) })
            .OrderByDescending(g => g.Total)
            .Take(3)
            .Select(g => $"{g.Category.ToDisplayName()} {Money(g.Total)}");

        return $"Em {monthLabel}, você gastou {Money(total)} no total. Maiores categorias: {string.Join(", ", top)}.";
    }

    private async Task<string> RecentReplyAsync(User user, int? count, CancellationToken cancellationToken)
    {
        var take = Math.Clamp(count ?? 5, 1, 10);
        var now = DateTime.UtcNow;

        var recent = (await _transactionRepository.GetByUserIdAsync(user.Id, cancellationToken))
            .Where(t => t.OccurredAt <= now)
            .OrderByDescending(t => t.OccurredAt)
            .Take(take)
            .ToList();

        if (recent.Count == 0)
            return "Você ainda não tem lançamentos registrados.";

        var lines = recent.Select(t =>
            $"{t.OccurredAt:dd/MM} {t.Description}, {(t.Type == TransactionType.Income ? "+" : "-")}{Money(t.Amount)}");
        return "Seus últimos lançamentos:\n" + string.Join("\n", lines);
    }

    private async Task<string> ProjectionReplyAsync(
        User user, decimal? price, int? installmentCount, CancellationToken cancellationToken)
    {
        if (price is null or <= 0)
            return "Qual o valor da compra que você quer simular?";

        var projection = await _projectionService.ProjectAsync(
            user.Id, price.Value, Math.Max(installmentCount ?? 1, 1), DateTime.UtcNow, cancellationToken);

        var text = projection.CanAffordNow
            ? $"Dá para comprar agora. Seu saldo é {Money(projection.CurrentBalance)} e a compra é de {Money(projection.Price)}."
            : projection.AffordableMonth is { } month
                ? $"Hoje seu saldo é {Money(projection.CurrentBalance)}. Pelo seu histórico, você chega em {Money(projection.Price)} em {MonthLabel(month)}, daqui a {projection.MonthsUntilAffordable} mês(es)."
                : $"Hoje seu saldo é {Money(projection.CurrentBalance)} e, pelo seu histórico, não consigo prever quando você chega em {Money(projection.Price)}.";

        if (projection.InstallmentCount > 1)
        {
            text += projection.FitsMonthlyMargin
                ? $" Parcelado em {projection.InstallmentCount}x de {Money(projection.InstallmentValue)}, cabe na sua sobra mensal de {Money(projection.MonthlyMargin)}."
                : $" Parcelado em {projection.InstallmentCount}x de {Money(projection.InstallmentValue)}, a parcela passa da sua sobra mensal de {Money(projection.MonthlyMargin)}.";
        }

        return text;
    }

    private async Task<string> BudgetsReplyAsync(User user, CancellationToken cancellationToken)
    {
        var statuses = await _budgetAlertService.GetAllStatusesAsync(user.Id, DateTime.UtcNow, cancellationToken);
        if (statuses.Count == 0)
            return "Você ainda não definiu limites de gasto. Pode pedir assim: \"define limite de 500 para mercado\".";

        var lines = statuses.Select(s =>
            $"{s.Category.ToDisplayName()}: {Money(s.Spent)} de {Money(s.MonthlyLimit)} ({s.PercentUsed * 100:F0}%){(s.PercentUsed >= 1m ? ", estourado" : "")}");
        return "Seus limites deste mês:\n" + string.Join("\n", lines);
    }

    private async Task<RegisterMessageResult> SetBudgetLimitAsync(
        User user, TransactionCategory? category, decimal? amount, CancellationToken cancellationToken)
    {
        if (category is null || amount is null or <= 0)
        {
            return new RegisterMessageResult(false,
                "Para definir um limite preciso da categoria e do valor, por exemplo: \"limite de 500 para mercado\".");
        }

        var limit = await _budgetLimitRepository.GetAsync(user.Id, category.Value, cancellationToken);
        if (limit is null)
            await _budgetLimitRepository.AddAsync(new BudgetLimit(user.Id, category.Value, amount.Value), cancellationToken);
        else
            limit.UpdateLimit(amount.Value);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new RegisterMessageResult(true, $"Limite de {category.Value.ToDisplayName()} definido em {Money(amount.Value)} por mês.");
    }

    private static string Money(decimal value) => $"R$ {value:F2}";

    private static string MonthLabel(DateTime date) => date.ToString("MMMM 'de' yyyy", PtBr);

    private static CultureInfo CreatePtBr()
    {
        try { return CultureInfo.GetCultureInfo("pt-BR"); }
        catch (CultureNotFoundException) { return CultureInfo.InvariantCulture; }
    }
}
