using Nexo.Application.Conversation;
using Nexo.Application.Transactions;
using Nexo.Domain.Entities;
using Nexo.Domain.Enums;
using static Nexo.Application.Messaging.MessageFormatter;

namespace Nexo.Application.Search;

// Perguntas livres sobre os lançamentos ("quanto gastei com uber esse ano?"). O código calcula os números,
// e a IA só escreve a resposta em tom natural a partir deles.
public class TransactionSearchIntentHandler : IIntentHandler
{
    private const int MaxListed = 8;

    private readonly ITransactionSearchService _searchService;
    private readonly IReplyComposer _composer;

    public TransactionSearchIntentHandler(ITransactionSearchService searchService, IReplyComposer composer)
    {
        _searchService = searchService;
        _composer = composer;
    }

    public IReadOnlyCollection<IntentType> Handles { get; } = new[] { IntentType.QueryTransactions };

    public async Task<RegisterMessageResult> HandleAsync(User user, MessageIntent intent, CancellationToken cancellationToken = default)
    {
        var query = new TransactionSearchQuery(intent.SearchText, intent.Category, intent.Kind, intent.StartDate, intent.EndDate);
        var result = await _searchService.SearchAsync(user.Id, query, DateTime.UtcNow, cancellationToken);
        var aggregation = intent.Aggregation ?? SearchAggregation.Sum;

        var fallback = FallbackReply(intent, result, aggregation);
        if (result.Count == 0)
            return new RegisterMessageResult(true, fallback);

        var question = string.IsNullOrWhiteSpace(intent.OriginalText) ? "(pergunta enviada por áudio)" : intent.OriginalText;
        var composed = await _composer.ComposeAsync(question, BuildFacts(intent, result, aggregation), cancellationToken);

        // Trava contra invenção: a resposta precisa trazer o número que o código calculou. Senão, vale o texto de reserva.
        var reply = !string.IsNullOrWhiteSpace(composed) && ContainsKeyFigure(composed, result, aggregation) ? composed : fallback;
        return new RegisterMessageResult(true, reply);
    }

    private static bool ContainsKeyFigure(string text, TransactionSearchResult result, SearchAggregation aggregation) => aggregation switch
    {
        SearchAggregation.Sum => text.Contains(Money(result.Total)),
        SearchAggregation.Average => text.Contains(Money(result.Average)),
        SearchAggregation.Largest => result.Largest is not null && text.Contains(Money(result.Largest.Amount)),
        SearchAggregation.Count => text.Contains(result.Count.ToString()),
        _ => true
    };

    private static string BuildFacts(MessageIntent intent, TransactionSearchResult result, SearchAggregation aggregation)
    {
        var lines = new List<string>
        {
            $"Período: {PeriodText(intent)}",
            $"Filtro: {FilterDescription(intent)}",
            $"O usuário quer saber: {AggregationName(aggregation)}",
            $"Quantidade de lançamentos: {result.Count}",
            $"Total: {Money(result.Total)}",
            $"Média por lançamento: {Money(result.Average)}"
        };

        if (result.Largest is { } largest)
            lines.Add($"Maior lançamento: {Money(largest.Amount)} em {largest.Description} ({largest.OccurredAt:dd/MM/yyyy})");

        if (aggregation == SearchAggregation.List)
            lines.Add("Lançamentos mais recentes:\n" + string.Join("\n", result.Matches.Take(MaxListed).Select(ListLine)));

        return string.Join("\n", lines);
    }

    private static string FallbackReply(MessageIntent intent, TransactionSearchResult result, SearchAggregation aggregation)
    {
        var label = LabelText(intent);
        var period = PeriodText(intent);

        if (result.Count == 0)
            return $"🔎 Não encontrei lançamentos {label} {period}. Quer tentar outra palavra ou outro período?";

        return aggregation switch
        {
            SearchAggregation.Count => $"🔎 Foram *{result.Count}* lançamento(s) {label} {period}.",
            SearchAggregation.Average => $"🔎 A média {label} {period} é *{Money(result.Average)}* por lançamento, em {result.Count} lançamento(s).",
            SearchAggregation.Largest => $"🔎 O maior lançamento {label} {period} foi *{Money(result.Largest!.Amount)}* em {result.Largest.Description} ({result.Largest.OccurredAt:dd/MM/yyyy}).",
            SearchAggregation.List => $"🔎 *Lançamentos {label} {period}*\n" + string.Join("\n", result.Matches.Take(MaxListed).Select(ListLine))
                                       + (result.Count > MaxListed ? $"\n… e mais {result.Count - MaxListed}." : ""),
            _ => $"🔎 {(intent.Kind == TransactionType.Income ? "Você recebeu" : "Você gastou")} *{Money(result.Total)}* {label} {period}, em {result.Count} lançamento(s)."
        };
    }

    private static string ListLine(Transaction t) =>
        $"{(t.Type == TransactionType.Income ? "📥" : "📤")} {t.OccurredAt:dd/MM} · {t.Description} · {Money(t.Amount)}";

    private static string LabelText(MessageIntent intent)
    {
        var text = string.IsNullOrWhiteSpace(intent.SearchText) ? null : intent.SearchText.Trim();
        return (text, intent.Category) switch
        {
            (not null, not null) => $"com \"{text}\" ({intent.Category.Value.ToDisplayName()})",
            (not null, null) => $"com \"{text}\"",
            (null, not null) => $"em {intent.Category.Value.ToDisplayName()}",
            _ => "no total"
        };
    }

    private static string FilterDescription(MessageIntent intent)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(intent.SearchText)) parts.Add($"palavra \"{intent.SearchText.Trim()}\" na descrição");
        if (intent.Category is { } category) parts.Add($"categoria {category.ToDisplayName()}");
        parts.Add(intent.Kind switch
        {
            TransactionType.Income => "somente receitas",
            TransactionType.Expense => "somente gastos",
            _ => "gastos e receitas"
        });
        return string.Join("; ", parts);
    }

    private static string PeriodText(MessageIntent intent) => (intent.StartDate, intent.EndDate) switch
    {
        ({ } start, { } end) => $"de {start:dd/MM/yyyy} a {end:dd/MM/yyyy}",
        ({ } start, null) => $"desde {start:dd/MM/yyyy}",
        (null, { } end) => $"até {end:dd/MM/yyyy}",
        _ => "em todo o histórico"
    };

    private static string AggregationName(SearchAggregation aggregation) => aggregation switch
    {
        SearchAggregation.Count => "quantos lançamentos foram",
        SearchAggregation.Average => "a média",
        SearchAggregation.Largest => "o maior lançamento",
        SearchAggregation.List => "a lista dos lançamentos",
        _ => "quanto somou no total"
    };
}
