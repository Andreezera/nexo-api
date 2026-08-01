namespace Nexo.Api.Contracts;

public record PurchaseProjectionResponse(
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
