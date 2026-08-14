using Nexo.Domain.Enums;

namespace Nexo.Domain.Entities;

// Uma mensagem da conversa (do usuário ou do assistente). O histórico recente vira contexto para a IA.
public class ConversationMessage
{
    public const int MaxTextLength = 2000;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public ConversationRole Role { get; private set; }
    public string Text { get; private set; } = default!;
    public DateTime CreatedAt { get; private set; }

    private ConversationMessage() { }

    public ConversationMessage(Guid userId, ConversationRole role, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("O texto da mensagem é obrigatório.", nameof(text));

        Id = Guid.NewGuid();
        UserId = userId;
        Role = role;
        Text = text.Length > MaxTextLength ? text[..MaxTextLength] : text;
        CreatedAt = DateTime.UtcNow;
    }
}
