namespace Nexo.Application.WhatsApp;

public record MediaFile(byte[] Data, string MimeType);

public interface IWhatsAppMessenger
{
    Task SendTextAsync(string to, string text, CancellationToken cancellationToken = default);

    // Envia uma imagem (por exemplo, um gráfico) com legenda opcional.
    Task SendImageAsync(
        string to, byte[] image, string mimeType, string? caption, CancellationToken cancellationToken = default);

    // Baixa um áudio ou imagem recebido. Retorna null se não foi possível baixar.
    Task<MediaFile?> DownloadMediaAsync(string mediaId, CancellationToken cancellationToken = default);
}
