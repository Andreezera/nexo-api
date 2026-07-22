namespace Nexo.Api.Contracts;

// Formato simplificado, inspirado no payload de mensagens do WhatsApp Cloud API
// (entry[].changes[].value.messages[].from / .text.body). Simula a chegada de uma
// mensagem até que a integração real com a Meta seja conectada a este mesmo endpoint.
public record IncomingWhatsAppMessage(string From, WhatsAppText Text);

public record WhatsAppText(string Body);

public record WhatsAppWebhookResponse(bool Understood, string ReplyMessage);
