namespace Nexo.Application.Conversation;

// O que o usuário mandou: texto, ou mídia (áudio/foto) com texto opcional.
public record MessageContent(string? Text, byte[]? MediaData = null, string? MediaMimeType = null)
{
    public bool HasMedia => MediaData is { Length: > 0 };

    public static MessageContent FromText(string text) => new(text);

    // Texto que fica salvo no histórico da conversa.
    public string Describe()
    {
        if (!string.IsNullOrWhiteSpace(Text))
            return Text;
        if (MediaMimeType?.StartsWith("audio", StringComparison.OrdinalIgnoreCase) == true)
            return "[mensagem de áudio]";
        if (MediaMimeType?.StartsWith("image", StringComparison.OrdinalIgnoreCase) == true)
            return "[foto]";
        return "[arquivo]";
    }
}
