using Nexo.Domain.Enums;

namespace Nexo.Application.Reports;

public record CategoryTotal(TransactionCategory Category, decimal Total);

// PreviousIncome/PreviousExpense são nulos quando o mês anterior não tem nenhum lançamento,
// para a comparação não dizer "subiu 100%" a partir do zero.
public record MonthlySummary(
    DateTime Month,
    decimal Income,
    decimal Expense,
    IReadOnlyList<CategoryTotal> ExpensesByCategory,
    bool HasTransactions,
    decimal? PreviousIncome,
    decimal? PreviousExpense)
{
    public decimal Balance => Income - Expense;
}

public record MonthlyPoint(DateTime Month, decimal Income, decimal Expense);
