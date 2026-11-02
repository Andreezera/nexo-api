using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexo.Application.Messaging;
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

    [Fact]
    public async Task SendImageAsync_UploadsTheFileThenSendsTheMessageWithItsId()
    {
        var handler = new FakeHandler();
        handler.Respond = request => request.RequestUri!.AbsolutePath.EndsWith("/media")
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"img-42\"}", Encoding.UTF8, "application/json") }
            : new HttpResponseMessage(HttpStatusCode.OK);

        await Create(handler).SendImageAsync("5511999999999", new byte[] { 1, 2, 3 }, "image/png", "Gastos de novembro");

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("https://graph.test/v23.0/12345/media", handler.Requests[0].RequestUri!.ToString());
        Assert.StartsWith("multipart/form-data", handler.Requests[0].Content!.Headers.ContentType!.MediaType + ";");
        Assert.Equal("https://graph.test/v23.0/12345/messages", handler.Requests[1].RequestUri!.ToString());
        Assert.Contains("\"type\":\"image\"", handler.Bodies[1]);
        Assert.Contains("\"id\":\"img-42\"", handler.Bodies[1]);
        Assert.Contains("Gastos de novembro", handler.Bodies[1]);
        Assert.All(handler.Requests, r => Assert.Equal("token-abc", r.Headers.Authorization!.Parameter));
    }

    [Fact]
    public async Task SendImageAsync_WhenTheUploadFails_ThrowsAndSendsNoMessage()
    {
        var handler = new FakeHandler { Respond = _ => new HttpResponseMessage(HttpStatusCode.BadRequest) };

        await Assert.ThrowsAsync<HttpRequestException>(
            () => Create(handler).SendImageAsync("5511", new byte[] { 1 }, "image/png", null));

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task SendButtonsAsync_PostsAnInteractiveButtonMessage()
    {
        var handler = new FakeHandler();

        await Create(handler).SendButtonsAsync("5511999999999", "Despesa registrada.", new[]
        {
            new ReplyButton("cmd_undo", "↩️ Desfazer"), new ReplyButton("cmd_saldo", "💼 Saldo")
        });

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://graph.test/v23.0/12345/messages", request.RequestUri!.ToString());
        Assert.Contains("\"type\":\"interactive\"", handler.Bodies[0]);
        Assert.Contains("\"type\":\"button\"", handler.Bodies[0]);
        Assert.Contains("\"id\":\"cmd_undo\"", handler.Bodies[0]);
        Assert.Contains("Despesa registrada.", handler.Bodies[0]);
    }

    [Fact]
    public async Task SendButtonsAsync_SendsAtMostThreeButtonsWithTitlesWithinTheLimit()
    {
        var handler = new FakeHandler();
        var buttons = Enumerable.Range(1, 5).Select(i => new ReplyButton($"b{i}", new string('x', 40))).ToArray();

        await Create(handler).SendButtonsAsync("5511", "oi", buttons);

        Assert.Contains("\"id\":\"b3\"", handler.Bodies[0]);
        Assert.DoesNotContain("\"id\":\"b4\"", handler.Bodies[0]);
        Assert.DoesNotContain(new string('x', 21), handler.Bodies[0]);
    }

    [Fact]
    public async Task SendListAsync_PostsSectionsAndRows()
    {
        var handler = new FakeHandler();
        var list = new InteractiveList("Escolha", "Ver opções", new[]
        {
            new ListSection("Consultar", new[] { new ListRow("cmd_saldo", "Saldo", "Quanto você tem") })
        }, Footer: "Digite menu");

        await Create(handler).SendListAsync("5511999999999", list);

        Assert.Contains("\"type\":\"list\"", handler.Bodies[0]);
        // O JSON escapa letras acentuadas (\u00E7), então a verificação usa só o começo do texto.
        Assert.Contains("\"button\":\"Ver op", handler.Bodies[0]);
        Assert.Contains("\"title\":\"Consultar\"", handler.Bodies[0]);
        Assert.Contains("\"id\":\"cmd_saldo\"", handler.Bodies[0]);
        Assert.Contains("\"description\":\"Quanto voc", handler.Bodies[0]);
        Assert.Contains("Digite menu", handler.Bodies[0]);
    }

    [Fact]
    public async Task SendListAsync_OmitsMissingFooterAndDescriptionInsteadOfSendingNull()
    {
        var handler = new FakeHandler();
        var list = new InteractiveList("Escolha", "Ver", new[] { new ListSection("A", new[] { new ListRow("r1", "Linha") }) });

        await Create(handler).SendListAsync("5511", list);

        Assert.DoesNotContain("null", handler.Bodies[0]);
        Assert.DoesNotContain("footer", handler.Bodies[0]);
        Assert.DoesNotContain("description", handler.Bodies[0]);
    }

    [Fact]
    public async Task SendListAsync_ClipsTitlesLongerThanTheMetaLimit()
    {
        var handler = new FakeHandler();
        var list = new InteractiveList("Escolha", "Ver", new[]
        {
            new ListSection("A", new[] { new ListRow("r1", new string('y', 60), new string('z', 120)) })
        });

        await Create(handler).SendListAsync("5511", list);

        Assert.DoesNotContain(new string('y', 25), handler.Bodies[0]);
        Assert.DoesNotContain(new string('z', 73), handler.Bodies[0]);
    }

    [Fact]
    public async Task SendListAsync_WhenMetaRejects_Throws()
    {
        var handler = new FakeHandler { Respond = _ => new HttpResponseMessage(HttpStatusCode.BadRequest) };
        var list = new InteractiveList("x", "y", new[] { new ListSection("A", new[] { new ListRow("r", "t") }) });

        await Assert.ThrowsAsync<HttpRequestException>(() => Create(handler).SendListAsync("5511", list));
    }
}
