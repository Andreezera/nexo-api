using Nexo.Domain.Entities;
using Nexo.Domain.Enums;

namespace Nexo.Application.Abstractions;

public interface IBudgetLimitRepository
{
    Task<BudgetLimit?> GetAsync(Guid userId, TransactionCategory category, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BudgetLimit>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(BudgetLimit limit, CancellationToken cancellationToken = default);
    void Remove(BudgetLimit limit);
}
