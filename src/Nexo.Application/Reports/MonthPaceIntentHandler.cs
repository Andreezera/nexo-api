using Nexo.Application.Conversation;
using Nexo.Application.Transactions;
using Nexo.Domain.Entities;
using Nexo.Domain.Enums;
using static Nexo.Application.Messaging.MessageFormatter;

namespace Nexo.Application.Reports;

// "Como estou no mês?": quanto ainda dá para gastar por dia e como o mês deve fechar.
public class MonthPaceIntentHandler : IIntentHandler
{
    private readonly IMonthPaceService _paceService;

    public MonthPaceIntentHandler(IMonthPaceService paceService) => _paceService = paceService;

    public IReadOnlyCollection<IntentType> Handles { get; } = new[] { IntentType.QueryMonthPace };

    public async Task<RegisterMessageResult> HandleAsync(User user, MessageIntent intent, CancellationToken cancellationToken = default)
    {
        var pace = await _paceService.GetAsync(user.Id, DateTime.UtcNow, cancellationToken);
        return new RegisterMessageResult(true, BuildReply(pace));
    }

    public static string BuildReply(MonthPace pace)
    {
        var lines = new List<string> { $"🧭 *Como você está em {MonthName(pace.Today)}*", "" };

        lines.Add($"📥 Receitas: {Money(pace.IncomeSoFar)}");
        lines.Add($"📤 Gastos até hoje: {Money(pace.ExpenseSoFar)}");
        if (pace.UpcomingExpenses > 0)
            lines.Add($"📌 Ainda vão cair neste mês: {Money(pace.UpcomingExpenses)}");
        lines.Add("");

        if (pace.ExpectedIncome <= 0)
        {
            lines.Add("💡 Ainda não tenho receita registrada, nem histórico para estimar, então não consigo calcular quanto você pode gastar por dia.");
        }
        else
        {
            if (pace.IncomeIsEstimated)
                lines.Add($"ℹ️ Ainda não entrou receita neste mês, então estimei pela sua média dos últimos meses ({Money(pace.ExpectedIncome)}).");

            lines.Add(pace.AvailableToSpend > 0
                ? $"💡 *Você ainda pode gastar {Money(pace.AvailablePerDay)} por dia* ({pace.DaysLeft} {(pace.DaysLeft == 1 ? "dia" : "dias")} até o fim do mês)."
                : $"⚠️ Você já passou do que entrou este mês em {Money(-pace.AvailableToSpend)}.");
        }

        if (pace.TooEarlyToProject)
        {
            lines.Add("");
            lines.Add("⏳ Ainda é cedo no mês para prever como ele fecha.");
        }
        else
        {
            lines.Add("");
            lines.Add(pace.ExpectedIncome > 0
                ? $"🔮 No ritmo atual, os gastos do mês fecham em *{Money(pace.ProjectedExpense)}*, {(pace.ExpectedIncome >= pace.ProjectedExpense ? $"e sobram {Money(pace.ExpectedIncome - pace.ProjectedExpense)}" : $"{Money(pace.ProjectedExpense - pace.ExpectedIncome)} acima da receita")}."
                : $"🔮 No ritmo atual, os gastos do mês fecham em *{Money(pace.ProjectedExpense)}*.");
        }

        if (pace.BiggestIncrease is { } change)
        {
            lines.Add("");
            lines.Add($"📈 *O que mudou:* {change.Category.ToEmoji()} {change.Category.ToDisplayName()} subiu {Money(change.Delta)} em relação a {MonthName(pace.Today.AddMonths(-1))}.");
        }

        return string.Join("\n", lines);
    }
}
