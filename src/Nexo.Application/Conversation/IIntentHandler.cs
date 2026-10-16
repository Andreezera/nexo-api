using Nexo.Application.Transactions;
using Nexo.Domain.Entities;

namespace Nexo.Application.Conversation;

// Um recurso completo da conversa (contas recorrentes, metas, perguntas livres...). O ConversationService
// escolhe o tratador pelo tipo da intenção, então um recurso novo não mexe no serviço.
public interface IIntentHandler
{
    IReadOnlyCollection<IntentType> Handles { get; }

    Task<RegisterMessageResult> HandleAsync(User user, MessageIntent intent, CancellationToken cancellationToken = default);
}
