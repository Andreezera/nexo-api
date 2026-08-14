using Nexo.Domain.Entities;

namespace Nexo.Application.Abstractions;

public interface IConversationHistoryRepository
{
    // Últimas mensagens do usuário, da mais antiga para a mais recente.
    Task<IReadOnlyList<ConversationMessage>> GetRecentAsync(Guid userId, int count, CancellationToken cancellationToken = default);
    Task AddAsync(ConversationMessage message, CancellationToken cancellationToken = default);
}
