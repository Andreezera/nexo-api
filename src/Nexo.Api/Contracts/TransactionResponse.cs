using Nexo.Domain.Enums;

namespace Nexo.Api.Contracts;

public record TransactionResponse(
    Guid Id,
    TransactionType Type,
    decimal Amount,
    TransactionCategory Category,
    string Description,
    DateTime OccurredAt);
