using Nexo.Application.Messaging;

namespace Nexo.Application.Transactions;

// Image: quando existe, a resposta é uma imagem (gráfico) e ReplyMessage vai como legenda.
// Buttons e List: a resposta ganha botões de toque ou um menu em lista. ReplyMessage continua sendo o texto de
// reserva, usado quando o envio interativo não é possível, e é o que fica no histórico.
public record ReplyImage(byte[] Data, string MimeType = "image/png");

public record RegisterMessageResult(
    bool Understood,
    string ReplyMessage,
    ReplyImage? Image = null,
    IReadOnlyList<ReplyButton>? Buttons = null,
    InteractiveList? List = null);
