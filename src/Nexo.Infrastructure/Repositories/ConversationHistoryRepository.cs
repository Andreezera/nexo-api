using Microsoft.EntityFrameworkCore;
using Nexo.Application.Abstractions;
using Nexo.Domain.Entities;
using Nexo.Infrastructure.Persistence;

namespace Nexo.Infrastructure.Repositories;

public class ConversationHistoryRepository : IConversationHistoryRepository
{
    private readonly NexoDbContext _context;

    public ConversationHistoryRepository(NexoDbContext context) => _context = context;

    public async Task<IReadOnlyList<ConversationMessage>> GetRecentAsync(
        Guid userId, int count, CancellationToken cancellationToken = default)
    {
        var latest = await _context.ConversationMessages
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);

        latest.Reverse();
        return latest;
    }

    public async Task AddAsync(ConversationMessage message, CancellationToken cancellationToken = default) =>
        await _context.ConversationMessages.AddAsync(message, cancellationToken);
}
