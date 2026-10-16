using Nexo.Application.Abstractions;
using Nexo.Application.Conversation;
using Nexo.Application.Transactions;
using Nexo.Domain.Entities;
using Nexo.Domain.Enums;
using static Nexo.Application.Messaging.MessageFormatter;
using Nexo.Application.Messaging;

namespace Nexo.Application.Recurring;

// Criar, listar e cancelar contas recorrentes ("netflix 55,90 todo dia 10").
public class RecurringIntentHandler : IIntentHandler
{
    private readonly IRecurringTransactionRepository _repository;
    private readonly IRecurringTransactionService _service;
    private readonly IUnitOfWork _unitOfWork;

    public RecurringIntentHandler(
        IRecurringTransactionRepository repository, IRecurringTransactionService service, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _service = service;
        _unitOfWork = unitOfWork;
    }

    public IReadOnlyCollection<IntentType> Handles { get; } = new[]
    {
        IntentType.CreateRecurringTransaction,
        IntentType.QueryRecurringTransactions,
        IntentType.CancelRecurringTransaction
    };

    public Task<RegisterMessageResult> HandleAsync(User user, MessageIntent intent, CancellationToken cancellationToken = default) =>
        intent.Type switch
        {
            IntentType.CreateRecurringTransaction => CreateAsync(user, intent, cancellationToken),
            IntentType.CancelRecurringTransaction => CancelAsync(user, intent, cancellationToken),
            _ => ListAsync(user, cancellationToken)
        };

    private async Task<RegisterMessageResult> CreateAsync(User user, MessageIntent intent, CancellationToken cancellationToken)
    {
        if (intent.Transaction is null || intent.DayOfMonth is not (>= 1 and <= 31))
        {
            return new RegisterMessageResult(false,
                "🔁 Para criar uma conta recorrente preciso do valor e do dia do mês, por exemplo: \"netflix 55,90 todo dia 10\".");
        }

        var parsed = intent.Transaction;
        var today = DateTime.UtcNow;
        var recurring = new RecurringTransaction(
            user.Id, parsed.Type, parsed.Amount, parsed.Category, parsed.Description, intent.DayOfMonth.Value, today);

        await _repository.AddAsync(recurring, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Se o dia é hoje, o lançamento deste mês já sai agora.
        var generatedNow = await _service.GenerateDueAsync(today, user.Id, cancellationToken) > 0;

        var text =
            $"🔁 *Conta recorrente criada*\n{parsed.Category.ToEmoji()} {parsed.Category.ToDisplayName()} · {Money(parsed.Amount)}\n"
            + $"📝 {parsed.Description}\n📅 Todo dia {intent.DayOfMonth} · {(generatedNow ? "este mês já foi lançado" : $"próximo: {recurring.NextOccurrence(today):dd/MM}")}";

        return new RegisterMessageResult(true, text);
    }

    private async Task<RegisterMessageResult> ListAsync(User user, CancellationToken cancellationToken)
    {
        var items = await _repository.GetActiveByUserIdAsync(user.Id, cancellationToken);
        if (items.Count == 0)
        {
            return new RegisterMessageResult(true,
                "🔁 Você ainda não tem contas recorrentes. Experimente: \"netflix 55,90 todo dia 10\".");
        }

        var lines = items.Select(r =>
            $"{(r.Type == TransactionType.Income ? "📥" : "📤")} {r.Category.ToEmoji()} *{r.Description}* · {Money(r.Amount)} · todo dia {r.DayOfMonth}");
        var fixedExpense = items.Where(r => r.Type == TransactionType.Expense).Sum(r => r.Amount);

        var text = "🔁 *Suas contas recorrentes*\n\n" + string.Join("\n", lines);
        if (fixedExpense > 0)
            text += $"\n\n📤 Despesas fixas por mês: *{Money(fixedExpense)}*";

        return new RegisterMessageResult(true, text);
    }

    private async Task<RegisterMessageResult> CancelAsync(User user, MessageIntent intent, CancellationToken cancellationToken)
    {
        var items = await _repository.GetActiveByUserIdAsync(user.Id, cancellationToken);
        if (items.Count == 0)
            return new RegisterMessageResult(false, "🔁 Você não tem contas recorrentes para cancelar.");

        var matches = items.Where(r => TextMatch.Matches(r.Description, intent.Description)).ToList();
        if (matches.Count != 1)
        {
            var names = string.Join(", ", items.Select(r => r.Description));
            var question = matches.Count == 0
                ? "🔁 Não achei uma conta recorrente com esse nome."
                : "🔁 Mais de uma conta combina com esse nome.";
            return new RegisterMessageResult(false, $"{question} As suas são: {names}. Qual você quer cancelar?");
        }

        matches[0].Cancel();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new RegisterMessageResult(true,
            $"🗑️ Cancelei a conta recorrente *{matches[0].Description}* ({Money(matches[0].Amount)}). Os lançamentos que já existem continuam no histórico.");
    }
}
