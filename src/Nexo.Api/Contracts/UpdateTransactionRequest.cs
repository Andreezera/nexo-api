using Nexo.Domain.Enums;

namespace Nexo.Api.Contracts;

public record UpdateTransactionRequest(
    TransactionType Type,
    decimal Amount,
    TransactionCategory Category,
    string Description,
    DateTime OccurredAt);
