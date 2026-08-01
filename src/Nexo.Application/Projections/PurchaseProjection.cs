namespace Nexo.Application.Projections;

// AffordableMonth é o primeiro dia do mês em que o saldo projetado passa a cobrir o valor da compra.
public record PurchaseProjection(
    decimal Price,
    decimal CurrentBalance,
    decimal AverageMonthlyIncome,
    decimal AverageMonthlyExpense,
    decimal MonthlyMargin,
    bool CanAffordNow,
    int? MonthsUntilAffordable,
    DateTime? AffordableMonth,
    int InstallmentCount,
    decimal InstallmentValue,
    bool FitsMonthlyMargin);
