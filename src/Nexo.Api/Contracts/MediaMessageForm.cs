using System.ComponentModel.DataAnnotations;

namespace Nexo.Api.Contracts;

// Formulário do endpoint que simula o recebimento de áudio ou foto pelo WhatsApp.
public class MediaMessageForm
{
    [Required]
    public string From { get; set; } = default!;

    [Required]
    public IFormFile File { get; set; } = default!;

    // Legenda que o usuário pode mandar junto da foto. Opcional.
    public string? Caption { get; set; }
}
