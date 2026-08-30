using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Nexo.Api.WhatsApp;
using Nexo.Infrastructure.WhatsApp;

namespace Nexo.Api.Controllers;

// Endpoint que a Meta chama de verdade (WhatsApp Cloud API). É separado do endpoint simulado, que
// continua existindo para demonstrar o sistema pelo Swagger sem precisar de conta na Meta.
[ApiController]
[Route("api/webhook/whatsapp/cloud")]
public class WhatsAppCloudWebhookController : ControllerBase
{
    private static readonly TimeSpan DedupWindow = TimeSpan.FromHours(1);

    private readonly WhatsAppOptions _options;
    private readonly WhatsAppMessageQueue _queue;
    private readonly IMemoryCache _cache;
    private readonly ILogger<WhatsAppCloudWebhookController> _logger;

    public WhatsAppCloudWebhookController(
        IOptions<WhatsAppOptions> options,
        WhatsAppMessageQueue queue,
        IMemoryCache cache,
        ILogger<WhatsAppCloudWebhookController> logger)
    {
        _options = options.Value;
        _queue = queue;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Verificação do webhook. Ao cadastrar a URL no painel da Meta, ela faz um GET com um desafio e
    /// espera receber o mesmo desafio de volta, desde que o token de verificação confira.
    /// </summary>
    [HttpGet]
    public IActionResult Verify(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        var valid = mode == "subscribe"
                    && !string.IsNullOrEmpty(_options.VerifyToken)
                    && verifyToken == _options.VerifyToken;

        return valid ? Content(challenge ?? string.Empty, "text/plain") : StatusCode(StatusCodes.Status403Forbidden);
    }

    /// <summary>
    /// Recebe as mensagens enviadas pelos usuários no WhatsApp. Valida a assinatura da Meta, enfileira
    /// as mensagens novas e responde 200 na hora (o processamento acontece em segundo plano).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, cancellationToken);
        var body = buffer.ToArray();

        var signature = Request.Headers["X-Hub-Signature-256"].ToString();
        if (!WhatsAppSignatureValidator.IsValid(_options.AppSecret, body, signature))
        {
            _logger.LogWarning("Webhook do WhatsApp recusado: assinatura inválida ou AppSecret não configurado.");
            return Unauthorized();
        }

        foreach (var message in WhatsAppCloudPayloadParser.Parse(body))
        {
            // A Meta reenvia o webhook se demorar para receber o 200, então o mesmo id pode chegar mais de uma vez.
            if (!_cache.TryGetValue(message.MessageId, out _))
            {
                _cache.Set(message.MessageId, true, DedupWindow);
                await _queue.EnqueueAsync(message, cancellationToken);
            }
        }

        return Ok();
    }
}
