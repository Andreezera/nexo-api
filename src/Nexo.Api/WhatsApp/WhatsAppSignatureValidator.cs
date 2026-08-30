using System.Security.Cryptography;
using System.Text;

namespace Nexo.Api.WhatsApp;

// A Meta assina cada webhook com HMAC-SHA256 (usando o App Secret) no header X-Hub-Signature-256,
// no formato "sha256=<hex>". Sem essa checagem qualquer pessoa poderia mandar mensagens falsas.
public static class WhatsAppSignatureValidator
{
    private const string Prefix = "sha256=";

    public static bool IsValid(string appSecret, byte[] body, string? signatureHeader)
    {
        if (string.IsNullOrEmpty(appSecret) || string.IsNullOrEmpty(signatureHeader))
            return false;
        if (!signatureHeader.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        byte[] received;
        try
        {
            received = Convert.FromHexString(signatureHeader[Prefix.Length..]);
        }
        catch (FormatException)
        {
            return false;
        }

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), body);
        return CryptographicOperations.FixedTimeEquals(expected, received);
    }
}
