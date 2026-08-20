using Microsoft.AspNetCore.Mvc;
using Nexo.Api.Contracts;
using Nexo.Application.Conversation;

namespace Nexo.Api.Controllers;

[ApiController]
[Route("api/webhook/whatsapp")]
public class WhatsAppWebhookController : ControllerBase
{
    public const long MaxMediaBytes = 15 * 1024 * 1024;

    private readonly ConversationService _conversationService;

    public WhatsAppWebhookController(ConversationService conversationService) =>
        _conversationService = conversationService;

    /// <summary>
    /// Simula o recebimento de uma mensagem de texto do WhatsApp. A mensagem passa pela IA, que decide se é
    /// um lançamento (gasto, receita, compra parcelada) ou uma pergunta/comando sobre os dados do usuário,
    /// levando em conta as últimas mensagens da conversa.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<WhatsAppWebhookResponse>> ReceiveMessage(
        IncomingWhatsAppMessage message, CancellationToken cancellationToken)
    {
        var result = await _conversationService.HandleAsync(
            message.From, MessageContent.FromText(message.Text.Body), cancellationToken);
        return Ok(new WhatsAppWebhookResponse(result.Understood, result.ReplyMessage));
    }

    /// <summary>
    /// Simula o recebimento de um áudio (ex: "gastei 50 no mercado") ou de uma foto (ex: nota fiscal ou
    /// comprovante). O arquivo vai direto para o Gemini, que entende o conteúdo e segue o mesmo fluxo das
    /// mensagens de texto.
    /// </summary>
    [HttpPost("media")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxMediaBytes)]
    public async Task<ActionResult<WhatsAppWebhookResponse>> ReceiveMedia(
        [FromForm] MediaMessageForm form, CancellationToken cancellationToken)
    {
        if (form.File.Length == 0)
            return BadRequest("O arquivo está vazio.");
        if (form.File.Length > MaxMediaBytes)
            return BadRequest("O arquivo passa de 15 MB.");

        var mimeType = NormalizeMimeType(form.File.ContentType);
        if (!mimeType.StartsWith("audio/") && !mimeType.StartsWith("image/"))
            return BadRequest("Envie um áudio ou uma imagem.");

        using var buffer = new MemoryStream();
        await form.File.CopyToAsync(buffer, cancellationToken);

        var content = new MessageContent(form.Caption, buffer.ToArray(), mimeType);
        var result = await _conversationService.HandleAsync(form.From, content, cancellationToken);
        return Ok(new WhatsAppWebhookResponse(result.Understood, result.ReplyMessage));
    }

    // "audio/ogg; codecs=opus" vira "audio/ogg", que é o que o Gemini aceita.
    private static string NormalizeMimeType(string? contentType) =>
        (contentType ?? string.Empty).Split(';')[0].Trim().ToLowerInvariant();
}
