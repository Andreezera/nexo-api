using Nexo.Application.Abstractions;
using Nexo.Domain.Entities;
using Nexo.Domain.Enums;

namespace Nexo.Application.Projections;

public class PurchaseProjectionService : IPurchaseProjectionService
{
    public const int HistoryMonths = 3;
    public const int HorizonMonths = 24;

    private readonly ITransactionRepository _transactionRepository;

    public PurchaseProjectionService(ITransactionRepository transactionRepository) =>
        _transactionRepository = transactionRepository;

    public async Task<PurchaseProjection> ProjectAsync(
        Guid userId, decimal price, int installmentCount, DateTime referenceDate, CancellationToken cancellationToken = default)
    {
        if (price <= 0)
            throw new ArgumentException("O valor da compra deve ser maior que zero.", nameof(price));
        if (installmentCount <= 0)
            throw new ArgumentException("O número de parcelas deve ser maior que zero.", nameof(installmentCount));

        var transactions = await _transactionRepository.GetByUserIdAsync(userId, cancellationToken);
        var monthStart = new DateTime(referenceDate.Year, referenceDate.Month, 1, 0, 0, 0, referenceDate.Kind);

        // Saldo de hoje considera só o que já aconteceu. Parcelas com data futura ainda não saíram do bolso.
        var currentBalance = transactions
            .Where(t => t.OccurredAt <= referenceDate)
            .Sum(SignedAmount);

        var (averageIncome, averageExpense) = AverageMonthlyFlow(transactions, monthStart);
        var netPerMonth = averageIncome - averageExpense;

        // Parcelas já comprometidas (datas futuras) reduzem o saldo nos meses em que vencem.
        var committed = transactions
            .Where(t => t.OccurredAt > referenceDate && t.Type == TransactionType.Expense)
            .ToList();

        var committedNextMonth = committed
            .Where(t => t.OccurredAt >= monthStart.AddMonths(1) && t.OccurredAt < monthStart.AddMonths(2))
            .Sum(t => t.Amount);

        var installmentValue = Math.Round(price / installmentCount, 2, MidpointRounding.AwayFromZero);
        var monthlyMargin = netPerMonth - committedNextMonth;

        int? monthsUntil = null;
        DateTime? affordableMonth = null;

        if (currentBalance >= price)
        {
            monthsUntil = 0;
            affordableMonth = monthStart;
        }
        else
        {
            for (var k = 1; k <= HorizonMonths; k++)
            {
                var committedUpToMonth = committed
                    .Where(t => t.OccurredAt < monthStart.AddMonths(k + 1))
                    .Sum(t => t.Amount);

                var projectedBalance = currentBalance + k * netPerMonth - committedUpToMonth;
                if (projectedBalance >= price)
                {
                    monthsUntil = k;
                    affordableMonth = monthStart.AddMonths(k);
                    break;
                }
            }
        }

        return new PurchaseProjection(
            price,
            currentBalance,
            averageIncome,
            averageExpense,
            monthlyMargin,
            currentBalance >= price,
            monthsUntil,
            affordableMonth,
            installmentCount,
            installmentValue,
            installmentValue <= monthlyMargin);
    }

    private static decimal SignedAmount(Transaction t) =>
        t.Type == TransactionType.Income ? t.Amount : -t.Amount;

    // Média mensal de receita e despesa nos últimos meses fechados, sem contar parcelas
    // (elas entram à parte, como compromissos futuros). Sem histórico fechado, usa o mês atual.
    public static (decimal Income, decimal Expense) AverageMonthlyFlow(IReadOnlyList<Transaction> transactions, DateTime monthStart)
    {
        var regular = transactions.Where(t => t.InstallmentPlanId is null).ToList();

        var history = regular
            .Where(t => t.OccurredAt >= monthStart.AddMonths(-HistoryMonths) && t.OccurredAt < monthStart)
            .ToList();

        if (history.Count == 0)
            history = regular.Where(t => t.OccurredAt >= monthStart && t.OccurredAt < monthStart.AddMonths(1)).ToList();

        var months = history.Select(t => (t.OccurredAt.Year, t.OccurredAt.Month)).Distinct().Count();
        if (months == 0)
            return (0m, 0m);

        var income = history.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount) / months;
        var expense = history.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount) / months;
        return (Math.Round(income, 2), Math.Round(expense, 2));
    }
}
