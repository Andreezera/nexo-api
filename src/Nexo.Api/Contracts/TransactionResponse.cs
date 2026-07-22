using Nexo.Domain.Enums;

namespace Nexo.Api.Contracts;

public record TransactionResponse(
    Guid Id,
    TransactionType Type,
    decimal Amount,
    string Category,
    string Description,
    DateTime OccurredAt);
