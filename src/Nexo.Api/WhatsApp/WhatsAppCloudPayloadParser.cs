using System.Text.Json;
using Nexo.Application.WhatsApp;

namespace Nexo.Api.WhatsApp;

// Extrai as mensagens do formato aninhado do webhook: entry[].changes[].value.messages[].
// Eventos de status (entregue, lido) vêm em value.statuses e são ignorados.
public static class WhatsAppCloudPayloadParser
{
    public static IReadOnlyList<WhatsAppInboundMessage> Parse(byte[] body)
    {
        var messages = new List<WhatsAppInboundMessage>();

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            return messages;
        }

        using (document)
        {
            if (!document.RootElement.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
                return messages;

            foreach (var entry in entries.EnumerateArray())
            {
                if (!entry.TryGetProperty("changes", out var changes) || changes.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var change in changes.EnumerateArray())
                {
                    if (!change.TryGetProperty("value", out var value)
                        || !value.TryGetProperty("messages", out var rawMessages)
                        || rawMessages.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (var raw in rawMessages.EnumerateArray())
                    {
                        var message = ParseMessage(raw);
                        if (message is not null)
                            messages.Add(message);
                    }
                }
            }
        }

        return messages;
    }

    private static WhatsAppInboundMessage? ParseMessage(JsonElement raw)
    {
        var id = GetString(raw, "id");
        var from = GetString(raw, "from");
        var type = GetString(raw, "type");
        if (id is null || from is null || type is null)
            return null;

        return type switch
        {
            "text" when raw.TryGetProperty("text", out var text) =>
                new WhatsAppInboundMessage(id, from, type, Text: GetString(text, "body")),

            "audio" or "image" when raw.TryGetProperty(type, out var media) =>
                new WhatsAppInboundMessage(
                    id, from, type,
                    MediaId: GetString(media, "id"),
                    MediaMimeType: GetString(media, "mime_type"),
                    Caption: GetString(media, "caption")),

            _ => new WhatsAppInboundMessage(id, from, type)
        };
    }

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
