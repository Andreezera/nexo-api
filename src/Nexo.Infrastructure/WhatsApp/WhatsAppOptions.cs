namespace Nexo.Infrastructure.WhatsApp;

// Configuração do WhatsApp Cloud API. Os valores secretos ficam nos user-secrets, nunca no repositório.
public class WhatsAppOptions
{
    public string AccessToken { get; set; } = string.Empty;
    public string PhoneNumberId { get; set; } = string.Empty;
    public string VerifyToken { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
    public string GraphApiVersion { get; set; } = "v23.0";
    public string GraphApiBaseUrl { get; set; } = "https://graph.facebook.com";
}
