using Nexo.Domain.Enums;

namespace Nexo.Application.Parsing;

public record ParsedTransaction(
    TransactionType Type,
    decimal Amount,
    TransactionCategory Category,
    string Description,
    DateTime OccurredAt,
    int? InstallmentCount);
