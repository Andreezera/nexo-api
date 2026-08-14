using Microsoft.AspNetCore.Mvc;
using Nexo.Api.Contracts;
using Nexo.Application.Conversation;

namespace Nexo.Api.Controllers;

[ApiController]
[Route("api/webhook/whatsapp")]
public class WhatsAppWebhookController : ControllerBase
{
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
}
