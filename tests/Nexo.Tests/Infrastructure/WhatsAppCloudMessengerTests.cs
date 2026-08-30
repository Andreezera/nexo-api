using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexo.Infrastructure.WhatsApp;

namespace Nexo.Tests.Infrastructure;

public class WhatsAppCloudMessengerTests
{
    private class FakeHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();
        public List<string> Bodies { get; } = new();
        public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.OK);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
            return Respond(request);
        }
    }

    private static WhatsAppCloudMessenger Create(FakeHandler handler) => new(
        new HttpClient(handler),
        Options.Create(new WhatsAppOptions
        {
            AccessToken = "token-abc", PhoneNumberId = "12345", GraphApiVersion = "v23.0", GraphApiBaseUrl = "https://graph.test"
        }),
        NullLogger<WhatsAppCloudMessenger>.Instance);

    [Fact]
    public async Task SendTextAsync_PostsToTheMessagesEndpointWithBearerToken()
    {
        var handler = new FakeHandler();

        await Create(handler).SendTextAsync("5511999999999", "Despesa registrada.");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://graph.test/v23.0/12345/messages", request.RequestUri!.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("token-abc", request.Headers.Authorization.Parameter);
        Assert.Contains("\"messaging_product\":\"whatsapp\"", handler.Bodies[0]);
        Assert.Contains("\"to\":\"5511999999999\"", handler.Bodies[0]);
        Assert.Contains("Despesa registrada.", handler.Bodies[0]);
    }

    [Fact]
    public async Task SendTextAsync_WhenMetaRejects_Throws()
    {
        var handler = new FakeHandler { Respond = _ => new HttpResponseMessage(HttpStatusCode.Unauthorized) };

        await Assert.ThrowsAsync<HttpRequestException>(() => Create(handler).SendTextAsync("5511", "oi"));
    }

    [Fact]
    public async Task DownloadMediaAsync_FetchesTheUrlThenTheFile()
    {
        var handler = new FakeHandler();
        handler.Respond = request => request.RequestUri!.ToString() switch
        {
            "https://graph.test/v23.0/media-1" => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"url\":\"https://files.test/abc\",\"mime_type\":\"audio/ogg; codecs=opus\"}", Encoding.UTF8, "application/json")
            },
            "https://files.test/abc" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 7, 8, 9 }) },
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        };

        var file = await Create(handler).DownloadMediaAsync("media-1");

        Assert.NotNull(file);
        Assert.Equal(new byte[] { 7, 8, 9 }, file!.Data);
        Assert.Equal("audio/ogg; codecs=opus", file.MimeType);
        Assert.All(handler.Requests, r => Assert.Equal("token-abc", r.Headers.Authorization!.Parameter));
    }

    [Fact]
    public async Task DownloadMediaAsync_WhenMediaInfoFails_ReturnsNull()
    {
        var handler = new FakeHandler { Respond = _ => new HttpResponseMessage(HttpStatusCode.NotFound) };

        Assert.Null(await Create(handler).DownloadMediaAsync("media-x"));
    }

    [Fact]
    public async Task DownloadMediaAsync_WhenTheFileDownloadFails_ReturnsNull()
    {
        var handler = new FakeHandler();
        handler.Respond = request => request.RequestUri!.Host == "graph.test"
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"url\":\"https://files.test/abc\"}", Encoding.UTF8, "application/json") }
            : new HttpResponseMessage(HttpStatusCode.Forbidden);

        Assert.Null(await Create(handler).DownloadMediaAsync("media-1"));
    }
}
