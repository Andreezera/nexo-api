using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexo.Application.WhatsApp;

namespace Nexo.Infrastructure.WhatsApp;

public class WhatsAppCloudMessenger : IWhatsAppMessenger
{
    private readonly HttpClient _httpClient;
    private readonly WhatsAppOptions _options;
    private readonly ILogger<WhatsAppCloudMessenger> _logger;

    public WhatsAppCloudMessenger(
        HttpClient httpClient, IOptions<WhatsAppOptions> options, ILogger<WhatsAppCloudMessenger> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendTextAsync(string to, string text, CancellationToken cancellationToken = default)
    {
        var url = $"{_options.GraphApiBaseUrl}/{_options.GraphApiVersion}/{_options.PhoneNumberId}/messages";

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new
            {
                messaging_product = "whatsapp",
                to,
                type = "text",
                text = new { body = text }
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("WhatsApp Cloud API recusou o envio ({StatusCode}): {Body}", response.StatusCode, body);
            throw new HttpRequestException($"Falha ao enviar mensagem pelo WhatsApp ({(int)response.StatusCode}).");
        }
    }

    // O download tem dois passos: primeiro a Meta informa a URL temporária do arquivo, depois o arquivo
    // é baixado dessa URL (os dois pedidos precisam do token).
    public async Task<MediaFile?> DownloadMediaAsync(string mediaId, CancellationToken cancellationToken = default)
    {
        try
        {
            var infoUrl = $"{_options.GraphApiBaseUrl}/{_options.GraphApiVersion}/{mediaId}";
            using var infoRequest = AuthorizedGet(infoUrl);
            using var infoResponse = await _httpClient.SendAsync(infoRequest, cancellationToken);
            if (!infoResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Não consegui obter os dados da mídia {MediaId} ({StatusCode}).", mediaId, infoResponse.StatusCode);
                return null;
            }

            var info = await infoResponse.Content.ReadFromJsonAsync<MediaInfo>(cancellationToken);
            if (string.IsNullOrWhiteSpace(info?.Url))
                return null;

            using var fileRequest = AuthorizedGet(info.Url);
            using var fileResponse = await _httpClient.SendAsync(fileRequest, cancellationToken);
            if (!fileResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Não consegui baixar a mídia {MediaId} ({StatusCode}).", mediaId, fileResponse.StatusCode);
                return null;
            }

            var data = await fileResponse.Content.ReadAsByteArrayAsync(cancellationToken);
            return new MediaFile(data, info.MimeType ?? fileResponse.Content.Headers.ContentType?.MediaType ?? "application/octet-stream");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao baixar a mídia {MediaId} do WhatsApp.", mediaId);
            return null;
        }
    }

    private HttpRequestMessage AuthorizedGet(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
        return request;
    }

    private class MediaInfo
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("mime_type")]
        public string? MimeType { get; set; }
    }
}
