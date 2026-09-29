using Nexo.Application.Abstractions;
using Nexo.Domain.Entities;
using Nexo.Domain.Enums;

namespace Nexo.Application.Reports;

public interface IReportService
{
    // monthOffset: 0 = mês atual, -1 = mês passado, e assim por diante.
    Task<MonthlySummary> GetMonthlySummaryAsync(
        Guid userId, int monthOffset, DateTime today, CancellationToken cancellationToken = default);

    // Os últimos "months" meses, do mais antigo para o atual.
    Task<IReadOnlyList<MonthlyPoint>> GetMonthlySeriesAsync(
        Guid userId, int months, DateTime today, CancellationToken cancellationToken = default);
}

public class ReportService : IReportService
{
    public const int MaxMonthsBack = 24;

    private readonly ITransactionRepository _transactionRepository;

    public ReportService(ITransactionRepository transactionRepository)
    {
        _transactionRepository = transactionRepository;
    }

    public async Task<MonthlySummary> GetMonthlySummaryAsync(
        Guid userId, int monthOffset, DateTime today, CancellationToken cancellationToken = default)
    {
        var month = FirstDayOfMonth(today).AddMonths(Math.Clamp(monthOffset, -MaxMonthsBack, 0));
        var all = await _transactionRepository.GetByUserIdAsync(userId, cancellationToken);

        var current = InMonth(all, month);
        var previous = InMonth(all, month.AddMonths(-1));

        var expenses = current.Where(t => t.Type == TransactionType.Expense).ToList();
        var byCategory = expenses
            .GroupBy(t => t.Category)
            .Select(g => new CategoryTotal(g.Key, g.Sum(t => t.Amount)))
            .OrderByDescending(c => c.Total)
            .ToList();

        return new MonthlySummary(
            month,
            Income(current),
            expenses.Sum(t => t.Amount),
            byCategory,
            current.Count > 0,
            previous.Count > 0 ? Income(previous) : null,
            previous.Count > 0 ? Expense(previous) : null);
    }

    public async Task<IReadOnlyList<MonthlyPoint>> GetMonthlySeriesAsync(
        Guid userId, int months, DateTime today, CancellationToken cancellationToken = default)
    {
        var count = Math.Clamp(months, 1, 12);
        var all = await _transactionRepository.GetByUserIdAsync(userId, cancellationToken);
        var currentMonth = FirstDayOfMonth(today);

        return Enumerable.Range(0, count)
            .Select(i => currentMonth.AddMonths(-(count - 1 - i)))
            .Select(month =>
            {
                var inMonth = InMonth(all, month);
                return new MonthlyPoint(month, Income(inMonth), Expense(inMonth));
            })
            .ToList();
    }

    private static DateTime FirstDayOfMonth(DateTime date) => new(date.Year, date.Month, 1, 0, 0, 0, DateTimeKind.Utc);

    private static List<Transaction> InMonth(IReadOnlyList<Transaction> all, DateTime month) =>
        all.Where(t => t.OccurredAt.Year == month.Year && t.OccurredAt.Month == month.Month).ToList();

    private static decimal Income(IEnumerable<Transaction> list) =>
        list.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);

    private static decimal Expense(IEnumerable<Transaction> list) =>
        list.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);
}
