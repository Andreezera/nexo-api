using System.Globalization;
using System.Text;

namespace Nexo.Application.Messaging;

// Comparação de texto sem depender de acento nem de maiúscula ("açaí" casa com "acai", "Uber" com "uber").
public static class TextMatch
{
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var decomposed = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    // True se um dos textos contém o outro. Assim "netflix" casa com "Assinatura Netflix" e vice-versa.
    public static bool Matches(string? text, string? term)
    {
        var a = Normalize(text);
        var b = Normalize(term);
        return a.Length > 0 && b.Length > 0 && (a.Contains(b) || b.Contains(a));
    }
}
