using Nexo.Application.Abstractions;
using Nexo.Application.Budgets;
using Nexo.Application.Charts;
using Nexo.Application.Messaging;
using Nexo.Application.Projections;
using Nexo.Application.Reports;
using Nexo.Application.Transactions;
using Nexo.Domain.Entities;
using Nexo.Domain.Enums;
using static Nexo.Application.Messaging.MessageFormatter;

namespace Nexo.Application.Conversation;

// Porta de entrada das mensagens do WhatsApp. Pede para a IA classificar a intenção (levando em conta
// as últimas mensagens da conversa), executa e responde. Registrar uma transação continua sendo
// responsabilidade do ITransactionRegistrar, o resto são consultas e ajustes sobre os dados do usuário.
public class ConversationService
{
    public const int HistoryTurns = 6;
    private const int TopCategories = 5;
    private const int ChartMonths = 6;

    private const string ProviderFailureMessage =
        "⚠️ Tive um problema para interpretar sua mensagem agora. Tente de novo em instantes.";

    private const string NotUnderstoodMessage =
        "🤔 Não consegui entender essa mensagem. Tente algo como \"gastei 30 reais no mercado\" ou \"quanto gastei esse mês?\".\n\n"
        + MenuHint;

    private const string InstallmentUndoMessage =
        "⚠️ O último lançamento faz parte de uma compra parcelada, e por enquanto eu não consigo desfazer ou corrigir esse tipo de compra por aqui.";

    private readonly IMessageIntentParser _intentParser;
    private readonly ITransactionRegistrar _registrar;
    private readonly IUserRepository _userRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IBudgetLimitRepository _budgetLimitRepository;
    private readonly IBudgetAlertService _budgetAlertService;
    private readonly IPurchaseProjectionService _projectionService;
    private readonly IReportService _reportService;
    private readonly IChartService _chartService;
    private readonly IConversationHistoryRepository _historyRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReadOnlyDictionary<IntentType, IIntentHandler> _handlers;

    public ConversationService(
        IMessageIntentParser intentParser,
        ITransactionRegistrar registrar,
        IUserRepository userRepository,
        ITransactionRepository transactionRepository,
        IBudgetLimitRepository budgetLimitRepository,
        IBudgetAlertService budgetAlertService,
        IPurchaseProjectionService projectionService,
        IReportService reportService,
        IChartService chartService,
        IConversationHistoryRepository historyRepository,
        IUnitOfWork unitOfWork,
        IEnumerable<IIntentHandler> intentHandlers)
    {
        _handlers = intentHandlers
            .SelectMany(handler => handler.Handles.Select(type => (type, handler)))
            .ToDictionary(pair => pair.type, pair => pair.handler);
        _intentParser = intentParser;
        _registrar = registrar;
        _userRepository = userRepository;
        _transactionRepository = transactionRepository;
        _budgetLimitRepository = budgetLimitRepository;
        _budgetAlertService = budgetAlertService;
        _projectionService = projectionService;
        _reportService = reportService;
        _chartService = chartService;
        _historyRepository = historyRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<RegisterMessageResult> HandleAsync(
        string phoneNumber, MessageContent content, CancellationToken cancellationToken = default)
    {
        var isNewUser = false;
        var user = await _userRepository.GetByPhoneNumberAsync(phoneNumber, cancellationToken);
        if (user is null)
        {
            // O histórico da conversa precisa de um usuário, então ele é criado já no primeiro contato.
            user = new User(phoneNumber);
            await _userRepository.AddAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            isNewUser = true;
        }

        var history = await _historyRepository.GetRecentAsync(user.Id, HistoryTurns, cancellationToken);
        var intent = await _intentParser.ParseAsync(content, history, cancellationToken);
        intent = intent is null ? null : intent with { OriginalText = content.Text };

        var result = intent is null
            ? new RegisterMessageResult(false, ProviderFailureMessage)
            : await ExecuteAsync(user, phoneNumber, intent, cancellationToken);

        // No primeiro contato o usuário ainda não sabe o que pode pedir, então a resposta leva a dica do menu.
        if (isNewUser && intent?.Type != IntentType.ShowMenu && result.Image is null && !result.ReplyMessage.Contains("*menu*"))
            result = result with { ReplyMessage = $"{result.ReplyMessage}\n\n{MenuHint}" };

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
        if (_handlers.TryGetValue(intent.Type, out var handler))
            return await handler.HandleAsync(user, intent, cancellationToken);

        switch (intent.Type)
        {
            case IntentType.RegisterTransaction when intent.Transaction is not null:
                return await _registrar.RegisterAsync(phoneNumber, intent.Transaction, cancellationToken);

            case IntentType.ShowMenu:
                return new RegisterMessageResult(true, Menu, List: InteractiveCommands.MainMenu);

            case IntentType.QueryBalance:
                return new RegisterMessageResult(true, await BalanceReplyAsync(user, cancellationToken));

            case IntentType.QuerySpending:
                return new RegisterMessageResult(true,
                    await SpendingReplyAsync(user, intent.Category, intent.MonthOffset, cancellationToken));

            case IntentType.QueryMonthlySummary:
                return new RegisterMessageResult(true,
                    await SummaryReplyAsync(user, intent.MonthOffset, cancellationToken));

            case IntentType.QueryChart:
                return await ChartReplyAsync(user, intent, cancellationToken);

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

            case IntentType.UndoLastTransaction:
                return await UndoLastAsync(user, cancellationToken);

            case IntentType.EditLastTransaction:
                return await EditLastAsync(user, intent, cancellationToken);

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
            return "📭 Você ainda não tem lançamentos registrados. Comece com \"gastei 30 no mercado\".";

        var income = transactions.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);
        var expense = transactions.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);
        return $"💼 *Seu saldo hoje*\n{Money(income - expense)}\n\n📥 Receitas: {Money(income)}\n📤 Despesas: {Money(expense)}";
    }

    private async Task<string> SpendingReplyAsync(
        User user, TransactionCategory? category, int monthOffset, CancellationToken cancellationToken)
    {
        var summary = await _reportService.GetMonthlySummaryAsync(user.Id, monthOffset, DateTime.UtcNow, cancellationToken);
        var monthLabel = MonthLabel(summary.Month);

        if (category is not null)
        {
            var inCategory = summary.ExpensesByCategory.Where(c => c.Category == category).Sum(c => c.Total);
            var emoji = category.Value.ToEmoji();
            return inCategory == 0
                ? $"{emoji} Não encontrei gastos em {category.Value.ToDisplayName()} em {monthLabel}."
                : $"{emoji} Em {monthLabel}, você gastou *{Money(inCategory)}* em {category.Value.ToDisplayName()}.";
        }

        if (summary.Expense <= 0)
            return $"📭 Não encontrei gastos em {monthLabel}.";

        return $"📤 *Gastos de {monthLabel}*\nTotal: *{Money(summary.Expense)}*\n\n{CategoryLines(summary)}";
    }

    private async Task<string> SummaryReplyAsync(User user, int monthOffset, CancellationToken cancellationToken)
    {
        var summary = await _reportService.GetMonthlySummaryAsync(user.Id, monthOffset, DateTime.UtcNow, cancellationToken);
        var monthLabel = MonthLabel(summary.Month);

        if (!summary.HasTransactions)
            return $"📭 Não encontrei lançamentos em {monthLabel}.";

        var text =
            $"📊 *Resumo de {monthLabel}*\n\n"
            + $"📥 Receitas: {Money(summary.Income)}\n"
            + $"📤 Despesas: {Money(summary.Expense)}\n"
            + $"💼 Saldo do mês: *{Money(summary.Balance)}*";

        if (summary.Expense > 0)
            text += $"\n\n🏷️ *Onde você mais gastou*\n{CategoryLines(summary)}";

        var comparison = ComparisonLine(summary);
        return comparison is null ? text : $"{text}\n\n{comparison}";
    }

    private static string CategoryLines(MonthlySummary summary)
    {
        var lines = summary.ExpensesByCategory.Take(TopCategories).Select(c =>
        {
            var share = c.Total / summary.Expense;
            return $"{c.Category.ToEmoji()} *{c.Category.ToDisplayName()}*  {Money(c.Total)} · {Percent(share)}\n{Bar(share)}";
        }).ToList();

        var others = summary.ExpensesByCategory.Skip(TopCategories).Sum(c => c.Total);
        if (others > 0)
            lines.Add($"📦 *Outras*  {Money(others)} · {Percent(others / summary.Expense)}");

        return string.Join("\n", lines);
    }

    private static string? ComparisonLine(MonthlySummary summary)
    {
        if (summary.PreviousExpense is not > 0)
            return null;

        var previousMonth = MonthName(summary.Month.AddMonths(-1));
        var change = (summary.Expense - summary.PreviousExpense.Value) / summary.PreviousExpense.Value;

        if (Math.Abs(change) < 0.005m)
            return $"➡️ Despesas iguais às de {previousMonth}.";

        return change > 0
            ? $"📈 Despesas {Percent(change)} maiores que em {previousMonth} ({Money(summary.PreviousExpense.Value)})."
            : $"📉 Despesas {Percent(-change)} menores que em {previousMonth} ({Money(summary.PreviousExpense.Value)}).";
    }

    private async Task<RegisterMessageResult> ChartReplyAsync(
        User user, MessageIntent intent, CancellationToken cancellationToken)
    {
        var chart = intent.Chart == ChartKind.Monthly
            ? await _chartService.BuildMonthlyChartAsync(user.Id, ChartMonths, DateTime.UtcNow, cancellationToken)
            : await _chartService.BuildCategoryChartAsync(user.Id, intent.MonthOffset, DateTime.UtcNow, cancellationToken);

        return chart is null
            ? new RegisterMessageResult(true, "📭 Ainda não tenho dados suficientes para montar esse gráfico.")
            : new RegisterMessageResult(true, chart.Caption, new ReplyImage(chart.Png));
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
            return "📭 Você ainda não tem lançamentos registrados. Comece com \"gastei 30 no mercado\".";

        var lines = recent.Select(t =>
            $"{(t.Type == TransactionType.Income ? "📥" : "📤")} {t.OccurredAt:dd/MM} · {t.Description} · {(t.Type == TransactionType.Income ? "+" : "-")}{Money(t.Amount)}");
        return "🧾 *Últimos lançamentos*\n" + string.Join("\n", lines);
    }

    private async Task<string> ProjectionReplyAsync(
        User user, decimal? price, int? installmentCount, CancellationToken cancellationToken)
    {
        if (price is null or <= 0)
            return "🛍️ Qual o valor da compra que você quer simular?";

        var projection = await _projectionService.ProjectAsync(
            user.Id, price.Value, Math.Max(installmentCount ?? 1, 1), DateTime.UtcNow, cancellationToken);

        var text = projection.CanAffordNow
            ? $"✅ Dá para comprar agora! Seu saldo é {Money(projection.CurrentBalance)} e a compra é de {Money(projection.Price)}."
            : projection.AffordableMonth is { } month
                ? $"⏳ Hoje seu saldo é {Money(projection.CurrentBalance)}. Pelo seu histórico, você chega em {Money(projection.Price)} em {MonthLabel(month)}, daqui a {projection.MonthsUntilAffordable} mês(es)."
                : $"⚠️ Hoje seu saldo é {Money(projection.CurrentBalance)} e, pelo seu histórico, não consigo prever quando você chega em {Money(projection.Price)}.";

        if (projection.InstallmentCount > 1)
        {
            text += projection.FitsMonthlyMargin
                ? $"\n💳 Parcelado em {projection.InstallmentCount}x de {Money(projection.InstallmentValue)}, cabe na sua sobra mensal de {Money(projection.MonthlyMargin)}."
                : $"\n💳 Parcelado em {projection.InstallmentCount}x de {Money(projection.InstallmentValue)}, a parcela passa da sua sobra mensal de {Money(projection.MonthlyMargin)}.";
        }

        return text;
    }

    private async Task<string> BudgetsReplyAsync(User user, CancellationToken cancellationToken)
    {
        var statuses = await _budgetAlertService.GetAllStatusesAsync(user.Id, DateTime.UtcNow, cancellationToken);
        if (statuses.Count == 0)
            return "🎯 Você ainda não definiu limites de gasto. Pode pedir assim: \"define limite de 500 para mercado\".";

        var blocks = statuses.Select(s =>
            $"{StatusDot(s.PercentUsed)} {s.Category.ToEmoji()} *{s.Category.ToDisplayName()}*  {Money(s.Spent)} de {Money(s.MonthlyLimit)}\n"
            + $"{Bar(s.PercentUsed, 10, StatusSquare(s.PercentUsed))} {Percent(s.PercentUsed)}{(s.PercentUsed >= 1m ? " · estourado" : "")}");
        return "🎯 *Seus limites deste mês*\n\n" + string.Join("\n\n", blocks);
    }

    private async Task<RegisterMessageResult> SetBudgetLimitAsync(
        User user, TransactionCategory? category, decimal? amount, CancellationToken cancellationToken)
    {
        if (category is null || amount is null or <= 0)
        {
            return new RegisterMessageResult(false,
                "🎯 Para definir um limite preciso da categoria e do valor, por exemplo: \"limite de 500 para mercado\".");
        }

        var limit = await _budgetLimitRepository.GetAsync(user.Id, category.Value, cancellationToken);
        if (limit is null)
            await _budgetLimitRepository.AddAsync(new BudgetLimit(user.Id, category.Value, amount.Value), cancellationToken);
        else
            limit.UpdateLimit(amount.Value);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new RegisterMessageResult(true,
            $"🎯 Limite de {category.Value.ToEmoji()} {category.Value.ToDisplayName()} definido em {Money(amount.Value)} por mês.");
    }

    // "Último lançamento" é o criado mais recentemente, e não o de data mais recente.
    private async Task<Transaction?> LatestAsync(User user, CancellationToken cancellationToken) =>
        (await _transactionRepository.GetByUserIdAsync(user.Id, cancellationToken))
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefault();

    private async Task<RegisterMessageResult> UndoLastAsync(User user, CancellationToken cancellationToken)
    {
        var last = await LatestAsync(user, cancellationToken);
        if (last is null)
            return new RegisterMessageResult(false, "📭 Você ainda não tem lançamentos para desfazer.");

        if (last.InstallmentPlanId is not null)
            return new RegisterMessageResult(false, InstallmentUndoMessage);

        last.Delete();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new RegisterMessageResult(true, $"↩️ Removi o último lançamento: {last.Description}, {Money(last.Amount)}.");
    }

    private async Task<RegisterMessageResult> EditLastAsync(
        User user, MessageIntent intent, CancellationToken cancellationToken)
    {
        var last = await LatestAsync(user, cancellationToken);
        if (last is null)
            return new RegisterMessageResult(false, "📭 Você ainda não tem lançamentos para corrigir.");

        if (last.InstallmentPlanId is not null)
            return new RegisterMessageResult(false, InstallmentUndoMessage);

        var newAmount = intent.Amount is > 0 ? intent.Amount.Value : last.Amount;
        var newCategory = intent.Category ?? last.Category;
        var newDescription = string.IsNullOrWhiteSpace(intent.Description) ? last.Description : intent.Description;

        if (newAmount == last.Amount && newCategory == last.Category && newDescription == last.Description)
        {
            return new RegisterMessageResult(false,
                "✏️ O que você quer corrigir no último lançamento? Por exemplo: \"corrige para 25\" ou \"muda a categoria para lazer\".");
        }

        last.Update(last.Type, newAmount, newCategory, newDescription, last.OccurredAt);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var repeatsCategory = string.Equals(newDescription, newCategory.ToDisplayName(), StringComparison.OrdinalIgnoreCase);
        return new RegisterMessageResult(true,
            $"✏️ *Lançamento corrigido*\n{newCategory.ToEmoji()} {newCategory.ToDisplayName()} · {Money(newAmount)}"
            + (repeatsCategory ? "" : $"\n📝 {newDescription}"));
    }
}
