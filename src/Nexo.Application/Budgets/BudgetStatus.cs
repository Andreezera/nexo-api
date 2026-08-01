using Nexo.Domain.Enums;

namespace Nexo.Application.Budgets;

// PercentUsed é uma fração (0,85 = 85% do limite).
public record BudgetStatus(TransactionCategory Category, decimal MonthlyLimit, decimal Spent, decimal PercentUsed);
