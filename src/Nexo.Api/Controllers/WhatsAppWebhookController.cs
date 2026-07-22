using Microsoft.AspNetCore.Mvc;
using Nexo.Api.Contracts;
using Nexo.Application.Transactions;

namespace Nexo.Api.Controllers;

[ApiController]
[Route("api/webhook/whatsapp")]
public class WhatsAppWebhookController : ControllerBase
{
    private readonly MessageProcessingService _messageProcessingService;

    public WhatsAppWebhookController(MessageProcessingService messageProcessingService) =>
        _messageProcessingService = messageProcessingService;

    /// <summary>
    /// Simula o recebimento de uma mensagem do WhatsApp. Enquanto a integração real com o
    /// WhatsApp Cloud API não está conectada, este endpoint pode ser chamado diretamente
    /// (via Swagger/Postman) para demonstrar o fluxo completo de registro de transações.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<WhatsAppWebhookResponse>> ReceiveMessage(
        IncomingWhatsAppMessage message, CancellationToken cancellationToken)
    {
        var result = await _messageProcessingService.ProcessAsync(message.From, message.Text.Body, cancellationToken);
        return Ok(new WhatsAppWebhookResponse(result.Understood, result.ReplyMessage));
    }
}
