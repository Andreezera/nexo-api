using Nexo.Application.Abstractions;
using Nexo.Application.Projections;
using Nexo.Domain.Entities;
using Nexo.Domain.Enums;

namespace Nexo.Application.Reports;

public record CategoryChange(TransactionCategory Category, decimal Current, decimal Previous)
{
    public decimal Delta => Current - Previous;
}

// Retrato do mês até hoje. ExpectedIncome é a receita que entrou mais as recorrentes que ainda vão cair, ou a média
// dos últimos meses quando nada foi registrado ainda (nesse caso IncomeIsEstimated é verdadeiro).
public record MonthPace(
    DateTime Today,
    int DaysLeft,
    decimal IncomeSoFar,
    decimal ExpenseSoFar,
    decimal ExpectedIncome,
    bool IncomeIsEstimated,
    decimal UpcomingExpenses,
    decimal AvailableToSpend,
    decimal AvailablePerDay,
    decimal ProjectedExpense,
    bool TooEarlyToProject,
    CategoryChange? BiggestIncrease);

public interface IMonthPaceService
{
    Task<MonthPace> GetAsync(Guid userId, DateTime today, CancellationToken cancellationToken = default);
}

public class MonthPaceService : IMonthPaceService
{
    // Nos primeiros dias o ritmo de gasto ainda não diz nada sobre o mês.
    public const int MinDaysToProject = 4;

    private readonly ITransactionRepository _transactionRepository;
    private readonly IRecurringTransactionRepository _recurringRepository;

    public MonthPaceService(ITransactionRepository transactionRepository, IRecurringTransactionRepository recurringRepository)
    {
        _transactionRepository = transactionRepository;
        _recurringRepository = recurringRepository;
    }

    public async Task<MonthPace> GetAsync(Guid userId, DateTime today, CancellationToken cancellationToken = default)
    {
        var all = await _transactionRepository.GetByUserIdAsync(userId, cancellationToken);
        var recurring = await _recurringRepository.GetActiveByUserIdAsync(userId, cancellationToken);

        var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endOfToday = today.Date.AddDays(1);
        var daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);
        var daysLeft = daysInMonth - today.Day + 1;

        var thisMonth = all.Where(t => t.OccurredAt >= monthStart && t.OccurredAt < monthStart.AddMonths(1)).ToList();
        var sofar = thisMonth.Where(t => t.OccurredAt < endOfToday).ToList();

        var incomeSoFar = Sum(sofar, TransactionType.Income);
        var expenseSoFar = Sum(sofar, TransactionType.Expense);

        // Compromissos que ainda vão cair neste mês: lançamentos com data futura (parcelas) e contas recorrentes
        // que ainda não viraram lançamento.
        var scheduledExpense = Sum(thisMonth.Where(t => t.OccurredAt >= endOfToday).ToList(), TransactionType.Expense);
        var pendingRecurring = recurring.Where(r => IsPendingThisMonth(r, today)).ToList();
        var pendingRecurringExpense = pendingRecurring.Where(r => r.Type == TransactionType.Expense).Sum(r => r.Amount);
        var pendingRecurringIncome = pendingRecurring.Where(r => r.Type == TransactionType.Income).Sum(r => r.Amount);
        var upcoming = scheduledExpense + pendingRecurringExpense;

        var expectedIncome = incomeSoFar + pendingRecurringIncome;
        var estimated = false;
        if (expectedIncome <= 0)
        {
            expectedIncome = PurchaseProjectionService.AverageMonthlyFlow(all, monthStart).Income;
            estimated = expectedIncome > 0;
        }

        var available = expectedIncome - expenseSoFar - upcoming;
        var perDay = available > 0 ? Math.Round(available / daysLeft, 2) : 0m;

        var dailyRate = today.Day > 0 ? expenseSoFar / today.Day : 0m;
        var projected = expenseSoFar + upcoming + Math.Round(dailyRate * (daysInMonth - today.Day), 2);

        return new MonthPace(
            today, daysLeft, incomeSoFar, expenseSoFar, expectedIncome, estimated, upcoming,
            available, perDay, projected, today.Day < MinDaysToProject, BiggestIncrease(all, monthStart));
    }

    // A conta ainda não virou lançamento neste mês e a data dela é neste mês.
    private static bool IsPendingThisMonth(RecurringTransaction recurring, DateTime today)
    {
        var next = recurring.NextOccurrence(today);
        return next.Year == today.Year && next.Month == today.Month;
    }

    private static decimal Sum(IEnumerable<Transaction> transactions, TransactionType type) =>
        transactions.Where(t => t.Type == type).Sum(t => t.Amount);

    // Categoria cujo gasto mais cresceu em relação ao mês anterior. Só conta se a subida for relevante.
    private static CategoryChange? BiggestIncrease(IReadOnlyList<Transaction> all, DateTime monthStart)
    {
        Dictionary<TransactionCategory, decimal> ByCategory(DateTime start) => all
            .Where(t => t.Type == TransactionType.Expense && t.OccurredAt >= start && t.OccurredAt < start.AddMonths(1))
            .GroupBy(t => t.Category)
            .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));

        var current = ByCategory(monthStart);
        var previous = ByCategory(monthStart.AddMonths(-1));
        if (previous.Count == 0)
            return null;

        return current
            .Select(c => new CategoryChange(c.Key, c.Value, previous.GetValueOrDefault(c.Key)))
            .Where(c => c.Delta >= 20m && c.Delta >= c.Previous * 0.1m)
            .OrderByDescending(c => c.Delta)
            .FirstOrDefault();
    }
}
