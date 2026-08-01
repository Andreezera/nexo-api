using Microsoft.EntityFrameworkCore;
using Nexo.Application.Abstractions;
using Nexo.Domain.Entities;
using Nexo.Domain.Enums;
using Nexo.Infrastructure.Persistence;

namespace Nexo.Infrastructure.Repositories;

public class BudgetLimitRepository : IBudgetLimitRepository
{
    private readonly NexoDbContext _context;

    public BudgetLimitRepository(NexoDbContext context) => _context = context;

    public Task<BudgetLimit?> GetAsync(Guid userId, TransactionCategory category, CancellationToken cancellationToken = default) =>
        _context.BudgetLimits.FirstOrDefaultAsync(b => b.UserId == userId && b.Category == category, cancellationToken);

    public async Task<IReadOnlyList<BudgetLimit>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _context.BudgetLimits.Where(b => b.UserId == userId).ToListAsync(cancellationToken);

    public async Task AddAsync(BudgetLimit limit, CancellationToken cancellationToken = default) =>
        await _context.BudgetLimits.AddAsync(limit, cancellationToken);

    public void Remove(BudgetLimit limit) => _context.BudgetLimits.Remove(limit);
}
