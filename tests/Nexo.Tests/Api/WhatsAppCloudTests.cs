using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexo.Api.Controllers;
using Nexo.Api.WhatsApp;
using Nexo.Application.WhatsApp;
using Nexo.Infrastructure.WhatsApp;

namespace Nexo.Tests.Api;

public class WhatsAppCloudTests
{
    private const string Secret = "segredo-do-app";

    private static string Sign(byte[] body) =>
        "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), body)).ToLowerInvariant();

    private static byte[] TextPayload(string id, string from, string text) => Encoding.UTF8.GetBytes("""
        {"object":"whatsapp_business_account","entry":[{"id":"1","changes":[{"field":"messages","value":{
          "messaging_product":"whatsapp",
          "messages":[{"from":"@FROM","id":"@ID","timestamp":"1","type":"text","text":{"body":"@TEXT"}}]}}]}]}
        """.Replace("@FROM", from).Replace("@ID", id).Replace("@TEXT", text));

    // ---- assinatura ----

    [Fact]
    public void SignatureValidator_WithCorrectSignature_IsValid()
    {
        var body = Encoding.UTF8.GetBytes("{\"a\":1}");

        Assert.True(WhatsAppSignatureValidator.IsValid(Secret, body, Sign(body)));
    }

    [Fact]
    public void SignatureValidator_WhenBodyWasChanged_IsInvalid()
    {
        var body = Encoding.UTF8.GetBytes("{\"a\":1}");
        var signature = Sign(body);

        Assert.False(WhatsAppSignatureValidator.IsValid(Secret, Encoding.UTF8.GetBytes("{\"a\":2}"), signature));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sha256=nao-e-hexadecimal")]
    [InlineData("md5=abcdef")]
    public void SignatureValidator_WithMissingOrMalformedHeader_IsInvalid(string? header)
    {
        Assert.False(WhatsAppSignatureValidator.IsValid(Secret, new byte[] { 1 }, header));
    }

    [Fact]
    public void SignatureValidator_WithoutConfiguredSecret_IsAlwaysInvalid()
    {
        var body = new byte[] { 1, 2 };

        Assert.False(WhatsAppSignatureValidator.IsValid("", body, Sign(body)));
    }

    // ---- payload ----

    [Fact]
    public void PayloadParser_ReadsTextMessage()
    {
        var messages = WhatsAppCloudPayloadParser.Parse(TextPayload("wamid.1", "5511999999999", "gastei 30 no mercado"));

        var message = Assert.Single(messages);
        Assert.Equal("wamid.1", message.MessageId);
        Assert.Equal("5511999999999", message.From);
        Assert.Equal("text", message.Type);
        Assert.Equal("gastei 30 no mercado", message.Text);
    }

    [Fact]
    public void PayloadParser_ReadsAudioAndImageWithMediaIds()
    {
        var payload = Encoding.UTF8.GetBytes("""
            {"entry":[{"changes":[{"value":{"messages":[
              {"from":"5511","id":"a1","type":"audio","audio":{"id":"media-audio","mime_type":"audio/ogg; codecs=opus"}},
              {"from":"5511","id":"i1","type":"image","image":{"id":"media-img","mime_type":"image/jpeg","caption":"nota"}}
            ]}}]}]}
            """);

        var messages = WhatsAppCloudPayloadParser.Parse(payload);

        Assert.Equal(2, messages.Count);
        Assert.Equal("media-audio", messages[0].MediaId);
        Assert.Equal("audio/ogg; codecs=opus", messages[0].MediaMimeType);
        Assert.Equal("media-img", messages[1].MediaId);
        Assert.Equal("nota", messages[1].Caption);
    }

    [Fact]
    public void PayloadParser_IgnoresStatusEventsAndInvalidJson()
    {
        var statusOnly = Encoding.UTF8.GetBytes("{\"entry\":[{\"changes\":[{\"value\":{\"statuses\":[{\"id\":\"x\",\"status\":\"read\"}]}}]}]}");

        Assert.Empty(WhatsAppCloudPayloadParser.Parse(statusOnly));
        Assert.Empty(WhatsAppCloudPayloadParser.Parse(Encoding.UTF8.GetBytes("isso não é json")));
    }

    [Fact]
    public void PayloadParser_KeepsUnsupportedTypesSoTheUserGetsAnAnswer()
    {
        var payload = Encoding.UTF8.GetBytes("{\"entry\":[{\"changes\":[{\"value\":{\"messages\":[{\"from\":\"5511\",\"id\":\"s1\",\"type\":\"sticker\"}]}}]}]}");

        var message = Assert.Single(WhatsAppCloudPayloadParser.Parse(payload));

        Assert.Equal("sticker", message.Type);
    }

    // ---- controller ----

    private static (WhatsAppCloudWebhookController Controller, WhatsAppMessageQueue Queue) CreateController(byte[]? body = null, string? signature = null)
    {
        var queue = new WhatsAppMessageQueue();
        var options = Options.Create(new WhatsAppOptions { VerifyToken = "meu-token", AppSecret = Secret });
        var controller = new WhatsAppCloudWebhookController(
            options, queue, new MemoryCache(new MemoryCacheOptions()), NullLogger<WhatsAppCloudWebhookController>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(body ?? Array.Empty<byte>());
        if (signature is not null)
            context.Request.Headers["X-Hub-Signature-256"] = signature;
        controller.ControllerContext = new ControllerContext { HttpContext = context };

        return (controller, queue);
    }

    private static async Task<WhatsAppInboundMessage?> TryDequeueAsync(WhatsAppMessageQueue queue)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        try
        {
            await foreach (var message in queue.ReadAllAsync(cts.Token))
                return message;
        }
        catch (OperationCanceledException) { }
        return null;
    }

    [Fact]
    public void Verify_WithCorrectToken_EchoesTheChallenge()
    {
        var (controller, _) = CreateController();

        var result = controller.Verify("subscribe", "meu-token", "desafio-123");

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal("desafio-123", content.Content);
    }

    [Theory]
    [InlineData("subscribe", "token-errado")]
    [InlineData("unsubscribe", "meu-token")]
    [InlineData(null, null)]
    public void Verify_WithWrongTokenOrMode_IsForbidden(string? mode, string? token)
    {
        var (controller, _) = CreateController();

        var result = Assert.IsType<StatusCodeResult>(controller.Verify(mode, token, "x"));
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    [Fact]
    public async Task Receive_WithInvalidSignature_ReturnsUnauthorizedAndQueuesNothing()
    {
        var body = TextPayload("wamid.1", "5511", "oi");
        var (controller, queue) = CreateController(body, "sha256=00");

        var result = await controller.Receive(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
        Assert.Null(await TryDequeueAsync(queue));
    }

    [Fact]
    public async Task Receive_WithValidSignature_QueuesTheMessageAndReturnsOk()
    {
        var body = TextPayload("wamid.2", "5511", "oi");
        var (controller, queue) = CreateController(body, Sign(body));

        var result = await controller.Receive(CancellationToken.None);

        Assert.IsType<OkResult>(result);
        var queued = await TryDequeueAsync(queue);
        Assert.Equal("wamid.2", queued?.MessageId);
    }

    [Fact]
    public async Task Receive_WhenMetaResendsTheSameMessage_QueuesItOnlyOnce()
    {
        var body = TextPayload("wamid.3", "5511", "oi");
        var queue = new WhatsAppMessageQueue();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new WhatsAppOptions { VerifyToken = "t", AppSecret = Secret });

        for (var i = 0; i < 2; i++)
        {
            var controller = new WhatsAppCloudWebhookController(options, queue, cache, NullLogger<WhatsAppCloudWebhookController>.Instance);
            var context = new DefaultHttpContext();
            context.Request.Body = new MemoryStream(body);
            context.Request.Headers["X-Hub-Signature-256"] = Sign(body);
            controller.ControllerContext = new ControllerContext { HttpContext = context };
            await controller.Receive(CancellationToken.None);
        }

        Assert.NotNull(await TryDequeueAsync(queue));
        Assert.Null(await TryDequeueAsync(queue));
    }
}
