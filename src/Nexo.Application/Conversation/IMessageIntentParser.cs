using Nexo.Domain.Entities;

namespace Nexo.Application.Conversation;

public interface IMessageIntentParser
{
    // Retorna null quando o provedor de IA falhou (erro de rede, cota etc.). Mensagem que a IA
    // entendeu mas não é sobre finanças volta como IntentType.Unknown.
    Task<MessageIntent?> ParseAsync(
        MessageContent content, IReadOnlyList<ConversationMessage> recentHistory, CancellationToken cancellationToken = default);
}
