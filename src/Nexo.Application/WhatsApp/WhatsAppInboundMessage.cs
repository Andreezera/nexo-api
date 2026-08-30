namespace Nexo.Application.WhatsApp;

// Mensagem recebida do WhatsApp Cloud API, já separada do formato aninhado do webhook.
// Type é o tipo informado pela Meta: "text", "audio", "image" e outros que o sistema ainda não trata.
public record WhatsAppInboundMessage(
    string MessageId,
    string From,
    string Type,
    string? Text = null,
    string? MediaId = null,
    string? MediaMimeType = null,
    string? Caption = null);
