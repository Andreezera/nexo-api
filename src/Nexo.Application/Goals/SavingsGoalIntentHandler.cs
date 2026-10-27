using Nexo.Application.Abstractions;
using Nexo.Application.Conversation;
using Nexo.Application.Messaging;
using Nexo.Application.Projections;
using Nexo.Application.Transactions;
using Nexo.Domain.Entities;
using static Nexo.Application.Messaging.MessageFormatter;

namespace Nexo.Application.Goals;

// Metas de economia: criar ("quero juntar 3000 até junho"), guardar dinheiro nelas e acompanhar o progresso.
public class SavingsGoalIntentHandler : IIntentHandler
{
    private const string DefaultName = "Minha meta";

    private readonly ISavingsGoalRepository _repository;
    private readonly IPurchaseProjectionService _projectionService;
    private readonly IUnitOfWork _unitOfWork;

    public SavingsGoalIntentHandler(
        ISavingsGoalRepository repository, IPurchaseProjectionService projectionService, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _projectionService = projectionService;
        _unitOfWork = unitOfWork;
    }

    public IReadOnlyCollection<IntentType> Handles { get; } = new[]
    {
        IntentType.CreateSavingsGoal,
        IntentType.AddToSavingsGoal,
        IntentType.QuerySavingsGoals
    };

    public Task<RegisterMessageResult> HandleAsync(User user, MessageIntent intent, CancellationToken cancellationToken = default) =>
        intent.Type switch
        {
            IntentType.CreateSavingsGoal => CreateAsync(user, intent, cancellationToken),
            IntentType.AddToSavingsGoal => DepositAsync(user, intent, cancellationToken),
            _ => ListAsync(user, cancellationToken)
        };

    private async Task<RegisterMessageResult> CreateAsync(User user, MessageIntent intent, CancellationToken cancellationToken)
    {
        if (intent.Amount is not > 0)
        {
            return new RegisterMessageResult(false,
                "🎯 Qual valor você quer juntar? Por exemplo: \"quero juntar 3000 até junho\".");
        }

        var today = DateTime.UtcNow;
        if (intent.TargetDate is { } deadline && deadline.Date < today.Date)
        {
            return new RegisterMessageResult(false, "📅 Essa data já passou. Até quando você quer juntar esse valor?");
        }

        var name = string.IsNullOrWhiteSpace(intent.Description) ? DefaultName : intent.Description.Trim();
        var goals = await _repository.GetActiveByUserIdAsync(user.Id, cancellationToken);
        var existing = goals.FirstOrDefault(g => TextMatch.Normalize(g.Name) == TextMatch.Normalize(name));

        SavingsGoal goal;
        string heading;
        if (existing is not null)
        {
            existing.Update(intent.Amount.Value, intent.TargetDate);
            goal = existing;
            heading = "🎯 *Meta atualizada*";
        }
        else
        {
            goal = new SavingsGoal(user.Id, name, intent.Amount.Value, intent.TargetDate);
            await _repository.AddAsync(goal, cancellationToken);
            heading = "🎯 *Meta criada*";
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new RegisterMessageResult(true, $"{heading}\n\n{await CardAsync(user, goal, today, cancellationToken)}");
    }

    private async Task<RegisterMessageResult> DepositAsync(User user, MessageIntent intent, CancellationToken cancellationToken)
    {
        if (intent.Amount is not > 0)
            return new RegisterMessageResult(false, "💰 Quanto você guardou? Por exemplo: \"guardei 200 para a viagem\".");

        var goals = await _repository.GetActiveByUserIdAsync(user.Id, cancellationToken);
        if (goals.Count == 0)
        {
            return new RegisterMessageResult(false,
                "🎯 Você ainda não tem nenhuma meta. Crie uma assim: \"quero juntar 3000 até junho\".");
        }

        var goal = PickGoal(goals, intent.Description);
        if (goal is null)
        {
            var names = string.Join(", ", goals.Select(g => g.Name));
            return new RegisterMessageResult(false, $"🎯 Em qual meta você quer guardar? As suas são: {names}.");
        }

        goal.AddSaved(intent.Amount.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var heading = goal.IsReached
            ? "🎉 *Meta batida! Parabéns!*"
            : $"💰 *Guardado!* +{Money(intent.Amount.Value)}";
        return new RegisterMessageResult(true, $"{heading}\n\n{await CardAsync(user, goal, DateTime.UtcNow, cancellationToken)}");
    }

    private async Task<RegisterMessageResult> ListAsync(User user, CancellationToken cancellationToken)
    {
        var goals = await _repository.GetActiveByUserIdAsync(user.Id, cancellationToken);
        if (goals.Count == 0)
        {
            return new RegisterMessageResult(true,
                "🎯 Você ainda não tem metas. Crie uma assim: \"quero juntar 3000 até junho\".");
        }

        var today = DateTime.UtcNow;
        var cards = new List<string>();
        foreach (var goal in goals)
            cards.Add(await CardAsync(user, goal, today, cancellationToken));

        return new RegisterMessageResult(true, "🎯 *Suas metas*\n\n" + string.Join("\n\n", cards));
    }

    // Sem nome, vale a única meta que existe. Com vários, o nome precisa casar com apenas uma.
    private static SavingsGoal? PickGoal(IReadOnlyList<SavingsGoal> goals, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return goals.Count == 1 ? goals[0] : null;

        var matches = goals.Where(g => TextMatch.Matches(g.Name, name)).ToList();
        return matches.Count == 1 ? matches[0] : goals.Count == 1 ? goals[0] : null;
    }

    private async Task<string> CardAsync(User user, SavingsGoal goal, DateTime today, CancellationToken cancellationToken)
    {
        var lines = new List<string>
        {
            $"🎯 *{goal.Name}*",
            $"{Money(goal.SavedAmount)} de {Money(goal.TargetAmount)} · {Percent(goal.Progress)}",
            Bar(goal.Progress, 10, "🟩")
        };

        if (goal.IsReached)
            return string.Join("\n", lines);

        var monthlyMargin = (await _projectionService.ProjectAsync(user.Id, goal.Remaining, 1, today, cancellationToken)).MonthlyMargin;

        if (goal.Deadline is { } deadline)
        {
            var monthsLeft = (deadline.Year - today.Year) * 12 + deadline.Month - today.Month;
            if (deadline.Date < today.Date)
            {
                lines.Add($"⏰ O prazo ({MonthLabel(deadline)}) já passou, e faltam {Money(goal.Remaining)}.");
                return string.Join("\n", lines);
            }

            monthsLeft = Math.Max(monthsLeft, 1);
            var perMonth = Math.Round(goal.Remaining / monthsLeft, 2);
            lines.Add($"📅 Prazo: {MonthLabel(deadline)} ({monthsLeft} {(monthsLeft == 1 ? "mês" : "meses")})");
            lines.Add($"💡 Para chegar lá, guarde *{Money(perMonth)} por mês*.");
            lines.Add(monthlyMargin <= 0
                ? $"⚠️ Hoje sua sobra mensal está em {Money(monthlyMargin)}, então vale rever os gastos."
                : monthlyMargin >= perMonth
                    ? $"✅ Pela sua sobra mensal de {Money(monthlyMargin)}, dá para chegar no prazo."
                    : $"⚠️ Sua sobra mensal é de {Money(monthlyMargin)}, e vai ficar apertado para chegar no prazo.");
            return string.Join("\n", lines);
        }

        lines.Add($"💡 Faltam {Money(goal.Remaining)}.");
        if (monthlyMargin > 0)
        {
            var months = (int)Math.Ceiling(goal.Remaining / monthlyMargin);
            lines.Add($"📊 Com a sua sobra mensal de {Money(monthlyMargin)}, você chega lá em cerca de {months} {(months == 1 ? "mês" : "meses")}.");
        }

        return string.Join("\n", lines);
    }
}
