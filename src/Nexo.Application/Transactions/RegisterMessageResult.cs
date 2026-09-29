namespace Nexo.Application.Transactions;

// Image: quando existe, a resposta é uma imagem (gráfico) e ReplyMessage vai como legenda.
public record ReplyImage(byte[] Data, string MimeType = "image/png");

public record RegisterMessageResult(bool Understood, string ReplyMessage, ReplyImage? Image = null);
