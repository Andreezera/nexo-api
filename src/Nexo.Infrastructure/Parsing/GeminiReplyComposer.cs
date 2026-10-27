using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexo.Application.Conversation;

namespace Nexo.Infrastructure.Parsing;

public class GeminiReplyComposer : IReplyComposer
{
    private const string SystemPrompt = """
        Você é o NEXO, um assistente financeiro no WhatsApp, simpático e direto, que fala como uma pessoa de confiança
        ajudando um amigo com o dinheiro. Responda à pergunta do usuário em português do Brasil.

        Regras:
        - Use SOMENTE os números e fatos fornecidos. Nunca invente valores, datas, quantidades ou lançamentos.
        - Responda em 1 a 4 linhas, em tom natural e amigável, sem frases de robô e sem repetir a pergunta.
        - Destaque o valor principal com *asteriscos* (negrito do WhatsApp). Use no máximo 2 emojis.
        - Escreva os valores exatamente como vieram nos fatos (ex: R$ 1.234,56).
        - Não presuma o gênero do usuário: evite "meu caro", "querida", "amigo" e parecidos. Fale de forma neutra.
        - Se o resultado for pequeno ou zero, diga isso com naturalidade. Se fizer sentido, comente de leve (sem julgar o usuário).
        """;

    private static readonly JsonSerializerOptions DeserializeOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiReplyComposer> _logger;

    public GeminiReplyComposer(HttpClient httpClient, IOptions<GeminiOptions> options, ILogger<GeminiReplyComposer> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string?> ComposeAsync(string question, string facts, CancellationToken cancellationToken = default)
    {
        var requestBody = new
        {
            systemInstruction = new { parts = new[] { new { text = SystemPrompt } } },
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = $"Pergunta do usuário: {question}\n\nFatos calculados pelo sistema:\n{facts}" } } }
            },
            generationConfig = new { temperature = 0.4 }
        };

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_options.Model}:generateContent?key={_options.ApiKey}";

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(url, requestBody, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini API retornou {StatusCode} ao redigir a resposta.", response.StatusCode);
                return null;
            }

            var gemini = await response.Content.ReadFromJsonAsync<GeminiGenerateContentResponse>(DeserializeOptions, cancellationToken);
            var text = gemini?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text?.Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Falha ao redigir a resposta com o Gemini.");
            return null;
        }
    }
}
