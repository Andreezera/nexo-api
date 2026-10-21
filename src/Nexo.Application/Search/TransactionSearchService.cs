using Nexo.Application.Abstractions;
using Nexo.Application.Messaging;
using Nexo.Domain.Entities;
using Nexo.Domain.Enums;

namespace Nexo.Application.Search;

// Start/End nulos: Start sem limite, End até hoje (gasto de uma parcela futura ainda não aconteceu).
public record TransactionSearchQuery(
    string? Text, TransactionCategory? Category, TransactionType? Kind, DateTime? Start, DateTime? End);

public record TransactionSearchResult(
    IReadOnlyList<Transaction> Matches, decimal Total, int Count, decimal Average, Transaction? Largest);

public interface ITransactionSearchService
{
    Task<TransactionSearchResult> SearchAsync(
        Guid userId, TransactionSearchQuery query, DateTime today, CancellationToken cancellationToken = default);
}

public class TransactionSearchService : ITransactionSearchService
{
    private readonly ITransactionRepository _transactionRepository;

    public TransactionSearchService(ITransactionRepository transactionRepository) =>
        _transactionRepository = transactionRepository;

    public async Task<TransactionSearchResult> SearchAsync(
        Guid userId, TransactionSearchQuery query, DateTime today, CancellationToken cancellationToken = default)
    {
        var all = await _transactionRepository.GetByUserIdAsync(userId, cancellationToken);
        var endExclusive = (query.End ?? today).Date.AddDays(1);

        var matches = all
            .Where(t => query.Start is null || t.OccurredAt >= query.Start.Value.Date)
            .Where(t => t.OccurredAt < endExclusive)
            .Where(t => query.Kind is null || t.Type == query.Kind)
            .Where(t => query.Category is null || t.Category == query.Category)
            .Where(t => string.IsNullOrWhiteSpace(query.Text)
                        || TextMatch.Contains(t.Description, query.Text)
                        || TextMatch.Contains(t.Category.ToDisplayName(), query.Text))
            .OrderByDescending(t => t.OccurredAt)
            .ToList();

        var total = matches.Sum(t => t.Amount);
        var average = matches.Count == 0 ? 0m : Math.Round(total / matches.Count, 2);
        var largest = matches.OrderByDescending(t => t.Amount).FirstOrDefault();

        return new TransactionSearchResult(matches, total, matches.Count, average, largest);
    }
}
