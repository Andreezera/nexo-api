using Nexo.Domain.Entities;

namespace Nexo.Application.Abstractions;

public interface ISavingsGoalRepository
{
    Task AddAsync(SavingsGoal goal, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SavingsGoal>> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
