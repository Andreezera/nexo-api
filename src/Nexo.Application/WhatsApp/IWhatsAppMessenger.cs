using Nexo.Application.Messaging;

namespace Nexo.Application.WhatsApp;

public record MediaFile(byte[] Data, string MimeType);

public interface IWhatsAppMessenger
{
    Task SendTextAsync(string to, string text, CancellationToken cancellationToken = default);

    // Envia uma imagem (por exemplo, um gráfico) com legenda opcional.
    Task SendImageAsync(
        string to, byte[] image, string mimeType, string? caption, CancellationToken cancellationToken = default);

    // Mensagem com até 3 botões de resposta rápida embaixo do texto.
    Task SendButtonsAsync(
        string to, string body, IReadOnlyList<ReplyButton> buttons, CancellationToken cancellationToken = default);

    // Mensagem com um botão que abre uma lista de opções.
    Task SendListAsync(string to, InteractiveList list, CancellationToken cancellationToken = default);

    // Baixa um áudio ou imagem recebido. Retorna null se não foi possível baixar.
    Task<MediaFile?> DownloadMediaAsync(string mediaId, CancellationToken cancellationToken = default);
}
