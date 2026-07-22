using Microsoft.EntityFrameworkCore;
using Nexo.Application.Abstractions;
using Nexo.Domain.Entities;
using Nexo.Infrastructure.Persistence;

namespace Nexo.Infrastructure.Repositories;

public class TransactionRepository : ITransactionRepository
{
    private readonly NexoDbContext _context;

    public TransactionRepository(NexoDbContext context) => _context = context;

    public async Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default) =>
        await _context.Transactions.AddAsync(transaction, cancellationToken);

    public async Task<IReadOnlyList<Transaction>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _context.Transactions
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.OccurredAt)
            .ToListAsync(cancellationToken);
}
