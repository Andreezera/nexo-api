using Nexo.Domain.Entities;

namespace Nexo.Application.Abstractions;

public interface IRecurringTransactionRepository
{
    Task AddAsync(RecurringTransaction recurring, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecurringTransaction>> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecurringTransaction>> GetAllActiveAsync(CancellationToken cancellationToken = default);
}
