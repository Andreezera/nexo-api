using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexo.Application.Parsing;
using Nexo.Domain.Enums;

namespace Nexo.Infrastructure.Parsing;

public class GeminiTransactionMessageParser : ITransactionMessageParser
{
    private const string SystemPrompt = """
        Você é um interpretador de mensagens para o NEXO, um assistente financeiro via WhatsApp usado no Brasil.
        Analise a mensagem do usuário e extraia dados estruturados de uma transação financeira (gasto, receita ou compra parcelada), quando aplicável.

        Regras:
        - Se a mensagem descrever um gasto, defina "type" = "expense".
        - Se descrever um ganho/recebimento, defina "type" = "income".
        - Se mencionar parcelamento (ex: "em 10 parcelas", "parcelado em 3x"), preencha "installmentCount" com o número de parcelas
          e "amount" com o valor TOTAL da compra (não o valor de cada parcela).
        - "category" deve ser uma palavra curta em português (ex: Mercado, Transporte, Lazer, Saúde, Salário, Outros).
        - "description" deve ser um resumo curto do que foi comprado ou recebido.
        - Se a mensagem NÃO for sobre registrar uma transação financeira (pergunta, saudação, outro assunto),
          defina "isFinancialTransaction" = false. Mesmo assim, todos os campos são obrigatórios no JSON de saída:
          preencha "type" com "expense", "amount" com 0, "category" e "description" com uma string vazia.
        - Nunca invente valores: se não houver um valor numérico claro na mensagem, trate como
          "isFinancialTransaction" = false, seguindo a regra acima para os demais campos.
        """;

    private static readonly object ResponseSchema = new
    {
        type = "OBJECT",
        properties = new
        {
            isFinancialTransaction = new { type = "BOOLEAN" },
            type = new { type = "STRING", @enum = new[] { "income", "expense" } },
            amount = new { type = "NUMBER" },
            category = new { type = "STRING" },
            description = new { type = "STRING" },
            installmentCount = new { type = "INTEGER" }
        },
        required = new[] { "isFinancialTransaction", "type", "amount", "category", "description" }
    };

    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiTransactionMessageParser> _logger;

    public GeminiTransactionMessageParser(
        HttpClient httpClient, IOptions<GeminiOptions> options, ILogger<GeminiTransactionMessageParser> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ParsedTransaction?> ParseAsync(string message, CancellationToken cancellationToken = default)
    {
        var requestBody = new
        {
            systemInstruction = new { parts = new[] { new { text = SystemPrompt } } },
            contents = new[] { new { parts = new[] { new { text = message } } } },
            generationConfig = new
            {
                temperature = 0.1,
                responseMimeType = "application/json",
                responseSchema = ResponseSchema
            }
        };

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_options.Model}:generateContent?key={_options.ApiKey}";

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(url, requestBody, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Gemini API retornou {StatusCode}: {Body}", response.StatusCode, errorBody);
                return null;
            }

            var geminiResponse = await response.Content.ReadFromJsonAsync<GeminiGenerateContentResponse>(
                DeserializeOptions, cancellationToken);

            var jsonText = geminiResponse?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
            if (string.IsNullOrWhiteSpace(jsonText))
                return null;

            var extracted = JsonSerializer.Deserialize<ExtractedTransactionDto>(jsonText, DeserializeOptions);
            if (extracted is null || !extracted.IsFinancialTransaction || extracted.Amount is null or <= 0)
                return null;

            return new ParsedTransaction(
                Type: extracted.Type == "income" ? TransactionType.Income : TransactionType.Expense,
                Amount: extracted.Amount.Value,
                Category: string.IsNullOrWhiteSpace(extracted.Category) ? "Outros" : extracted.Category,
                Description: string.IsNullOrWhiteSpace(extracted.Description) ? message : extracted.Description,
                OccurredAt: DateTime.UtcNow,
                InstallmentCount: extracted.InstallmentCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao chamar a API do Gemini para interpretar a mensagem.");
            return null;
        }
    }
}
