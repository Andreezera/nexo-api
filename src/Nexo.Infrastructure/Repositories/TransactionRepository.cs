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

    public Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Transactions.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public void Remove(Transaction transaction) => _context.Transactions.Remove(transaction);
}
