namespace Nexo.Application.Parsing;

// Abstrai a interpretação de uma mensagem em linguagem natural.
// Hoje implementada com uma LLM (Gemini); no futuro pode ganhar outras
// implementações (ex: suporte a áudio transcrito) sem alterar quem a consome.
public interface ITransactionMessageParser
{
    Task<ParsedTransaction?> ParseAsync(string message, CancellationToken cancellationToken = default);
}
