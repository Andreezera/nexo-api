using Microsoft.EntityFrameworkCore;
using Nexo.Application.Abstractions;
using Nexo.Domain.Entities;
using Nexo.Infrastructure.Persistence;

namespace Nexo.Infrastructure.Repositories;

public class SavingsGoalRepository : ISavingsGoalRepository
{
    private readonly NexoDbContext _context;

    public SavingsGoalRepository(NexoDbContext context) => _context = context;

    public async Task AddAsync(SavingsGoal goal, CancellationToken cancellationToken = default) =>
        await _context.SavingsGoals.AddAsync(goal, cancellationToken);

    public async Task<IReadOnlyList<SavingsGoal>> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _context.SavingsGoals
            .Where(g => g.UserId == userId && g.IsActive)
            .OrderBy(g => g.CreatedAt)
            .ToListAsync(cancellationToken);
}
