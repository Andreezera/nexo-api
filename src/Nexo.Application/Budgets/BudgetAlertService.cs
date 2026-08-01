using Nexo.Application.Abstractions;
using Nexo.Domain.Entities;
using Nexo.Domain.Enums;

namespace Nexo.Application.Budgets;

public class BudgetAlertService : IBudgetAlertService
{
    public const decimal WarningThreshold = 0.8m;

    private readonly IBudgetLimitRepository _limitRepository;
    private readonly ITransactionRepository _transactionRepository;

    public BudgetAlertService(IBudgetLimitRepository limitRepository, ITransactionRepository transactionRepository)
    {
        _limitRepository = limitRepository;
        _transactionRepository = transactionRepository;
    }

    public async Task<BudgetStatus?> GetStatusAsync(
        Guid userId, TransactionCategory category, DateTime referenceDate, CancellationToken cancellationToken = default)
    {
        var limit = await _limitRepository.GetAsync(userId, category, cancellationToken);
        if (limit is null)
            return null;

        var transactions = await _transactionRepository.GetByUserIdAsync(userId, cancellationToken);
        return BuildStatus(limit, transactions, referenceDate);
    }

    public async Task<IReadOnlyList<BudgetStatus>> GetAllStatusesAsync(
        Guid userId, DateTime referenceDate, CancellationToken cancellationToken = default)
    {
        var limits = await _limitRepository.GetByUserIdAsync(userId, cancellationToken);
        if (limits.Count == 0)
            return Array.Empty<BudgetStatus>();

        var transactions = await _transactionRepository.GetByUserIdAsync(userId, cancellationToken);
        return limits
            .Select(limit => BuildStatus(limit, transactions, referenceDate))
            .OrderBy(status => status.Category)
            .ToList();
    }

    public async Task<string?> BuildAlertAsync(
        Guid userId, TransactionCategory category, DateTime referenceDate, CancellationToken cancellationToken = default)
    {
        var status = await GetStatusAsync(userId, category, referenceDate, cancellationToken);
        if (status is null)
            return null;

        if (status.PercentUsed >= 1m)
            return $"Atenção, você estourou o limite de {status.Category} do mês. Gastou R$ {status.Spent:F2} de R$ {status.MonthlyLimit:F2}.";

        if (status.PercentUsed >= WarningThreshold)
            return $"Atenção, você já usou {status.PercentUsed * 100:F0}% do limite de {status.Category} do mês. Gastou R$ {status.Spent:F2} de R$ {status.MonthlyLimit:F2}.";

        return null;
    }

    private static BudgetStatus BuildStatus(BudgetLimit limit, IReadOnlyList<Transaction> transactions, DateTime referenceDate)
    {
        var spent = transactions
            .Where(t => t.Type == TransactionType.Expense
                        && t.Category == limit.Category
                        && t.OccurredAt.Year == referenceDate.Year
                        && t.OccurredAt.Month == referenceDate.Month)
            .Sum(t => t.Amount);

        return new BudgetStatus(limit.Category, limit.MonthlyLimit, spent, spent / limit.MonthlyLimit);
    }
}
