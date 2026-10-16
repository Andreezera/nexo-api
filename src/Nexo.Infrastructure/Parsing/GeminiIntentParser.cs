using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexo.Application.Conversation;
using Nexo.Application.Parsing;
using Nexo.Domain.Entities;
using Nexo.Domain.Enums;

namespace Nexo.Infrastructure.Parsing;

public class GeminiIntentParser : IMessageIntentParser
{
    // "Todas" só existe no schema: é como a IA diz que a pergunta não é sobre uma categoria específica.
    private const string AllCategories = "Todas";

    private static string BuildSystemPrompt(DateTime today) => $"""
        Você é o interpretador de mensagens do NEXO, um assistente financeiro via WhatsApp usado no Brasil.
        A data de hoje é {today:yyyy-MM-dd}.
        Você recebe a mensagem atual do usuário (texto, áudio ou foto) e, antes dela, as mensagens recentes da conversa.
        Use a conversa como contexto para entender referências como "e no mês passado?", "e com lazer?" ou "esse mesmo valor".

        Classifique a mensagem em "intent":
        - RegisterTransaction: o usuário relata um gasto, uma receita ou uma compra parcelada. Vale também para áudio contando
          um gasto e para foto de nota fiscal ou comprovante (use o valor total e o nome do estabelecimento como descrição).
        - QueryBalance: pergunta o saldo, ou quanto tem.
        - QuerySpending: pergunta quanto gastou, no total ou em uma categoria. Em "monthOffset" use 0 para o mês atual,
          -1 para o mês passado, -2 para dois meses atrás, e assim por diante.
        - QueryRecentTransactions: pede os últimos lançamentos. Em "recentCount" coloque quantos (padrão 5).
        - QueryPurchaseProjection: pergunta se consegue comprar algo ou quando terá o dinheiro. "amount" é o preço
          e "installmentCount" o número de parcelas, se mencionar.
        - QueryBudgets: pergunta como estão os limites de gasto.
        - SetBudgetLimit: pede para definir ou mudar o limite mensal de uma categoria ("category" e "amount").
        - ShowMenu: cumprimento (oi, olá, bom dia), pedido de ajuda, "menu", "o que você faz", "como funciona" ou "opções".
        - QueryMonthlySummary: pede um resumo, balanço ou panorama de um mês ("resumo do mês", "como foi outubro?").
          Em "monthOffset" use 0 para o mês atual, -1 para o mês passado, e assim por diante.
        - QueryChart: só quando a mensagem pede explicitamente um gráfico, imagem, figura ou visual. Em "chart" use
          "Categorias" para gastos por categoria (e "monthOffset" para o mês) ou "Mensal" para a evolução de receitas e
          despesas nos últimos meses. Perguntar "gastos por categoria" ou "quanto gastei em cada categoria" sem pedir
          gráfico é QuerySpending, não QueryChart.
        - UndoLastTransaction: pede para apagar, desfazer ou cancelar o último lançamento.
        - EditLastTransaction: corrige o último lançamento. Em "amount" vai o valor novo (0 se o valor não muda), em
          "category" a categoria nova ("Todas" se não muda) e em "description" o texto novo (vazio se não muda).
        - CreateRecurringTransaction: conta que se repete todo mês ("netflix 55,90 todo dia 10", "aluguel de 1200 todo dia 5",
          "meu salário de 3000 cai todo dia 5"). Preencha "type", "amount", "category", "description" e "dayOfMonth" (1 a 31).
          Compra parcelada NÃO é recorrente.
        - QueryRecurringTransactions: pergunta ou pede a lista das contas recorrentes, fixas ou assinaturas.
        - CancelRecurringTransaction: pede para cancelar uma conta recorrente ("cancela a netflix"). Em "description" vai o nome.
        - Unknown: qualquer outra coisa. Em "reply" escreva uma resposta curta e simpática em português, ou uma pergunta
          pedindo o que faltou. Não invente dados financeiros.

        Regras:
        - Despesa: "type" = "expense". Receita: "type" = "income".
        - Compra parcelada ("em 10x", "parcelado em 3 vezes"): "installmentCount" com o número de parcelas e "amount"
          com o valor TOTAL da compra, nunca o valor de cada parcela.
        - "category" deve ser exatamente uma das opções: Mercado, Alimentacao, Transporte, Moradia, Saude, Educacao, Lazer,
          Vestuario, Assinaturas, Eletronicos, Salario, Freelance, Investimentos, Presente, Outros. Use "Outros" quando nenhuma
          se aplicar claramente. Quando a mensagem não for sobre uma categoria específica, use "Todas".
        - "description" é um resumo curto do que foi comprado ou recebido.
        - "occurredAtDate" é a data da transação (AAAA-MM-DD), calculada a partir de referências como "ontem" ou
          "sexta-feira passada", tendo a data de hoje como base. Sem referência, use a data de hoje.
        - Todos os campos são obrigatórios no JSON. Quando não se aplicarem, preencha "type" com "expense", "amount" com 0,
          "category" com "Todas", "description" e "reply" com string vazia, "occurredAtDate" com a data de hoje e
          "monthOffset" com 0.
        - Nunca invente valores. Se for um registro e não houver um valor claro, use Unknown e peça o valor em "reply".
        - O assunto escrito na mensagem atual vale mais que a conversa anterior. Use a conversa só quando a mensagem atual
          for uma continuação clara (como "e no mês passado?"). Se ela trouxer um assunto novo, ignore o que veio antes.
        - "chart" só é preenchido quando a intenção for QueryChart. Nos outros casos, não inclua "chart".
        """;

    private static readonly object ResponseSchema = new
    {
        type = "OBJECT",
        properties = new
        {
            intent = new { type = "STRING", @enum = Enum.GetNames<IntentType>() },
            type = new { type = "STRING", @enum = new[] { "income", "expense" } },
            amount = new { type = "NUMBER" },
            category = new
            {
                type = "STRING",
                @enum = Enum.GetNames<TransactionCategory>().Append(AllCategories).ToArray()
            },
            description = new { type = "STRING" },
            occurredAtDate = new { type = "STRING" },
            installmentCount = new { type = "INTEGER" },
            monthOffset = new { type = "INTEGER" },
            recentCount = new { type = "INTEGER" },
            chart = new { type = "STRING", @enum = new[] { "Categorias", "Mensal" } },
            dayOfMonth = new { type = "INTEGER" },
            reply = new { type = "STRING" }
        },
        required = new[] { "intent", "type", "amount", "category", "description", "occurredAtDate", "monthOffset", "reply" }
    };

    private static readonly JsonSerializerOptions DeserializeOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiIntentParser> _logger;

    public GeminiIntentParser(HttpClient httpClient, IOptions<GeminiOptions> options, ILogger<GeminiIntentParser> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<MessageIntent?> ParseAsync(
        MessageContent content, IReadOnlyList<ConversationMessage> recentHistory, CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow;

        var requestBody = new
        {
            systemInstruction = new { parts = new[] { new { text = BuildSystemPrompt(today) } } },
            contents = BuildContents(content, recentHistory),
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

            var dto = JsonSerializer.Deserialize<ExtractedIntentDto>(jsonText, DeserializeOptions);
            return dto is null ? null : Map(dto, today, content.Text);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao chamar a API do Gemini para interpretar a mensagem.");
            return null;
        }
    }

    public static MessageIntent Map(ExtractedIntentDto dto, DateTime today, string? fallbackDescription)
    {
        var type = Enum.TryParse<IntentType>(dto.Intent, ignoreCase: true, out var parsedType) ? parsedType : IntentType.Unknown;

        TransactionCategory? category = null;
        if (!string.IsNullOrWhiteSpace(dto.Category)
            && !string.Equals(dto.Category, AllCategories, StringComparison.OrdinalIgnoreCase)
            && Enum.TryParse<TransactionCategory>(dto.Category, ignoreCase: true, out var parsedCategory))
        {
            category = parsedCategory;
        }

        if (type == IntentType.RegisterTransaction)
        {
            if (dto.Amount is null or <= 0)
            {
                return new MessageIntent(IntentType.Unknown,
                    Reply: "Não consegui identificar o valor. Pode repetir informando quanto foi?");
            }

            var transaction = new ParsedTransaction(
                Type: dto.Type == "income" ? TransactionType.Income : TransactionType.Expense,
                Amount: dto.Amount.Value,
                Category: category ?? TransactionCategory.Outros,
                Description: string.IsNullOrWhiteSpace(dto.Description) ? fallbackDescription ?? "Lançamento" : dto.Description,
                OccurredAt: GeminiTransactionMessageParser.ResolveOccurredAt(dto.OccurredAtDate, today),
                InstallmentCount: dto.InstallmentCount);

            return new MessageIntent(IntentType.RegisterTransaction, Transaction: transaction);
        }

        if (type == IntentType.CreateRecurringTransaction)
        {
            // Sem valor, o tratador pede o que falta. Com valor, a conta já vai montada.
            var recurring = dto.Amount is > 0
                ? new ParsedTransaction(
                    Type: dto.Type == "income" ? TransactionType.Income : TransactionType.Expense,
                    Amount: dto.Amount.Value,
                    Category: category ?? TransactionCategory.Outros,
                    Description: string.IsNullOrWhiteSpace(dto.Description) ? fallbackDescription ?? "Conta recorrente" : dto.Description,
                    OccurredAt: today,
                    InstallmentCount: null)
                : null;

            return new MessageIntent(IntentType.CreateRecurringTransaction, Transaction: recurring, DayOfMonth: dto.DayOfMonth);
        }

        return new MessageIntent(
            type,
            Category: category,
            Amount: dto.Amount,
            InstallmentCount: dto.InstallmentCount,
            MonthOffset: dto.MonthOffset ?? 0,
            RecentCount: dto.RecentCount,
            Reply: string.IsNullOrWhiteSpace(dto.Reply) ? null : dto.Reply,
            Chart: type == IntentType.QueryChart ? ParseChart(dto.Chart) : null,
            Description: string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description);
    }

    private static ChartKind ParseChart(string? chart) =>
        string.Equals(chart, "Mensal", StringComparison.OrdinalIgnoreCase) ? ChartKind.Monthly : ChartKind.Categories;

    private static List<object> BuildContents(MessageContent content, IReadOnlyList<ConversationMessage> history)
    {
        var contents = new List<object>();

        // A conversa para o Gemini precisa começar por uma mensagem do usuário.
        foreach (var message in history.SkipWhile(m => m.Role != ConversationRole.User))
        {
            contents.Add(new
            {
                role = message.Role == ConversationRole.User ? "user" : "model",
                parts = new[] { new { text = message.Text } }
            });
        }

        var parts = new List<object>();
        if (!string.IsNullOrWhiteSpace(content.Text))
            parts.Add(new { text = content.Text });
        if (content.HasMedia)
        {
            parts.Add(new { inlineData = new { mimeType = content.MediaMimeType, data = Convert.ToBase64String(content.MediaData!) } });
            if (string.IsNullOrWhiteSpace(content.Text))
                parts.Add(new { text = "O usuário enviou este arquivo em vez de texto. Interprete o conteúdo." });
        }

        contents.Add(new { role = "user", parts });
        return contents;
    }
}
