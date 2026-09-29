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

    public Task SendTextAsync(string to, string text, CancellationToken cancellationToken = default) =>
        PostMessageAsync(new
        {
            messaging_product = "whatsapp",
            to,
            type = "text",
            text = new { body = text }
        }, cancellationToken);

    // Enviar uma imagem tem dois passos: primeiro o arquivo é enviado para a Meta, que devolve um id,
    // e depois a mensagem é enviada referenciando esse id.
    public async Task SendImageAsync(
        string to, byte[] image, string mimeType, string? caption, CancellationToken cancellationToken = default)
    {
        var uploadUrl = $"{_options.GraphApiBaseUrl}/{_options.GraphApiVersion}/{_options.PhoneNumberId}/media";

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("whatsapp"), "messaging_product");
        form.Add(new StringContent(mimeType), "type");
        var file = new ByteArrayContent(image);
        file.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
        form.Add(file, "file", "grafico.png");

        using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, uploadUrl) { Content = form };
        uploadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);

        using var uploadResponse = await _httpClient.SendAsync(uploadRequest, cancellationToken);
        if (!uploadResponse.IsSuccessStatusCode)
        {
            var body = await uploadResponse.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("WhatsApp Cloud API recusou o envio da imagem ({StatusCode}): {Body}", uploadResponse.StatusCode, body);
            throw new HttpRequestException($"Falha ao enviar a imagem pelo WhatsApp ({(int)uploadResponse.StatusCode}).");
        }

        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<UploadedMedia>(cancellationToken);
        if (string.IsNullOrWhiteSpace(uploaded?.Id))
            throw new HttpRequestException("A Meta não devolveu o id da imagem enviada.");

        await PostMessageAsync(new
        {
            messaging_product = "whatsapp",
            to,
            type = "image",
            image = new { id = uploaded.Id, caption }
        }, cancellationToken);
    }

    private async Task PostMessageAsync(object payload, CancellationToken cancellationToken)
    {
        var url = $"{_options.GraphApiBaseUrl}/{_options.GraphApiVersion}/{_options.PhoneNumberId}/messages";

        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(payload) };
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

    private class UploadedMedia
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }

    private class MediaInfo
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("mime_type")]
        public string? MimeType { get; set; }
    }
}
