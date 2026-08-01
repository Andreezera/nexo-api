using Nexo.Domain.Enums;

namespace Nexo.Api.Contracts;

public record SetBudgetLimitRequest(decimal MonthlyLimit);

// PercentUsed em porcentagem (85,5 = 85,5% do limite), considerando o mês atual.
public record BudgetLimitResponse(
    TransactionCategory Category,
    decimal MonthlyLimit,
    decimal Spent,
    decimal PercentUsed,
    bool Exceeded);
