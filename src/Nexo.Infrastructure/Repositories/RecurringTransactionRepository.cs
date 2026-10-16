using Microsoft.EntityFrameworkCore;
using Nexo.Application.Abstractions;
using Nexo.Domain.Entities;
using Nexo.Infrastructure.Persistence;

namespace Nexo.Infrastructure.Repositories;

public class RecurringTransactionRepository : IRecurringTransactionRepository
{
    private readonly NexoDbContext _context;

    public RecurringTransactionRepository(NexoDbContext context) => _context = context;

    public async Task AddAsync(RecurringTransaction recurring, CancellationToken cancellationToken = default) =>
        await _context.RecurringTransactions.AddAsync(recurring, cancellationToken);

    public async Task<IReadOnlyList<RecurringTransaction>> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _context.RecurringTransactions
            .Where(r => r.UserId == userId && r.IsActive)
            .OrderBy(r => r.DayOfMonth)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RecurringTransaction>> GetAllActiveAsync(CancellationToken cancellationToken = default) =>
        await _context.RecurringTransactions.Where(r => r.IsActive).ToListAsync(cancellationToken);
}
