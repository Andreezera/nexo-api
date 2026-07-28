using Nexo.Domain.Enums;

namespace Nexo.Api.Contracts;

public record UpdateTransactionRequest(
    TransactionType Type,
    decimal Amount,
    string Category,
    string Description,
    DateTime OccurredAt);
