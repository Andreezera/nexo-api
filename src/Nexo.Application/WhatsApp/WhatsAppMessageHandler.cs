using Nexo.Application.Conversation;

namespace Nexo.Application.WhatsApp;

// Trata uma mensagem recebida do WhatsApp de ponta a ponta: baixa a mídia (se houver), passa pela
// conversa com a IA e envia a resposta de volta pelo WhatsApp.
public class WhatsAppMessageHandler
{
    private const string UnsupportedMessage =
        "Por enquanto eu só entendo texto, áudio e foto. Pode mandar um gasto, como \"gastei 30 no mercado\".";

    private const string MediaFailureMessage =
        "Não consegui abrir o arquivo que você mandou. Tente enviar de novo.";

    private readonly ConversationService _conversationService;
    private readonly IWhatsAppMessenger _messenger;

    public WhatsAppMessageHandler(ConversationService conversationService, IWhatsAppMessenger messenger)
    {
        _conversationService = conversationService;
        _messenger = messenger;
    }

    public async Task HandleAsync(WhatsAppInboundMessage message, CancellationToken cancellationToken = default)
    {
        var (content, failureReply) = await BuildContentAsync(message, cancellationToken);

        if (content is null)
        {
            await _messenger.SendTextAsync(message.From, failureReply!, cancellationToken);
            return;
        }

        var result = await _conversationService.HandleAsync(message.From, content, cancellationToken);
        await _messenger.SendTextAsync(message.From, result.ReplyMessage, cancellationToken);
    }

    private async Task<(MessageContent? Content, string? FailureReply)> BuildContentAsync(
        WhatsAppInboundMessage message, CancellationToken cancellationToken)
    {
        switch (message.Type)
        {
            case "text" when !string.IsNullOrWhiteSpace(message.Text):
                return (MessageContent.FromText(message.Text), null);

            case "audio" or "image" when !string.IsNullOrEmpty(message.MediaId):
                var media = await _messenger.DownloadMediaAsync(message.MediaId, cancellationToken);
                if (media is null || media.Data.Length == 0)
                    return (null, MediaFailureMessage);

                // A Meta manda o tipo com parâmetros (ex: "audio/ogg; codecs=opus"), o Gemini quer só "audio/ogg".
                var mimeType = media.MimeType.Split(';')[0].Trim().ToLowerInvariant();
                return (new MessageContent(message.Caption, media.Data, mimeType), null);

            default:
                return (null, UnsupportedMessage);
        }
    }
}
