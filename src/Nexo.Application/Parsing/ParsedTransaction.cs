using Nexo.Domain.Enums;

namespace Nexo.Application.Parsing;

public record ParsedTransaction(
    TransactionType Type,
    decimal Amount,
    string Category,
    string Description,
    DateTime OccurredAt,
    int? InstallmentCount);
