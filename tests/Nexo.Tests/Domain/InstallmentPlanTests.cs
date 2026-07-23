using Nexo.Domain.Entities;
using Nexo.Domain.Enums;
using Xunit;

namespace Nexo.Tests.Domain;

public class InstallmentPlanTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Create_WhenDivisionIsExact_SplitsAmountEvenly()
    {
        var plan = InstallmentPlan.Create(UserId, "Notebook", "Eletrônicos", 3000m, 10, new DateTime(2026, 7, 23));

        Assert.Equal(10, plan.Transactions.Count);
        Assert.All(plan.Transactions, t => Assert.Equal(300m, t.Amount));
    }

    [Fact]
    public void Create_WhenDivisionHasRemainder_LastInstallmentAbsorbsIt()
    {
        // 100 / 3 = 33.33 (x2) + resto -> soma das parcelas deve bater exatamente com o total
        var plan = InstallmentPlan.Create(UserId, "Compra", "Outros", 100m, 3, new DateTime(2026, 7, 23));

        var amounts = plan.Transactions.Select(t => t.Amount).ToList();
        Assert.Equal(100m, amounts.Sum());
        Assert.Equal(33.33m, amounts[0]);
        Assert.Equal(33.33m, amounts[1]);
        Assert.Equal(33.34m, amounts[2]);
    }

    [Fact]
    public void Create_GeneratesInstallmentsOneMonthApart()
    {
        var purchaseDate = new DateTime(2026, 7, 23);
        var plan = InstallmentPlan.Create(UserId, "Notebook", "Eletrônicos", 300m, 3, purchaseDate);

        var dates = plan.Transactions.OrderBy(t => t.OccurredAt).Select(t => t.OccurredAt).ToList();
        Assert.Equal(purchaseDate, dates[0]);
        Assert.Equal(purchaseDate.AddMonths(1), dates[1]);
        Assert.Equal(purchaseDate.AddMonths(2), dates[2]);
    }

    [Fact]
    public void Create_MarksAllGeneratedTransactionsAsExpense()
    {
        var plan = InstallmentPlan.Create(UserId, "Notebook", "Eletrônicos", 300m, 3, DateTime.UtcNow);

        Assert.All(plan.Transactions, t => Assert.Equal(TransactionType.Expense, t.Type));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void Create_WhenTotalAmountIsNotPositive_Throws(decimal invalidAmount)
    {
        Assert.Throws<ArgumentException>(() =>
            InstallmentPlan.Create(UserId, "Compra", "Outros", invalidAmount, 3, DateTime.UtcNow));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WhenInstallmentCountIsNotPositive_Throws(int invalidCount)
    {
        Assert.Throws<ArgumentException>(() =>
            InstallmentPlan.Create(UserId, "Compra", "Outros", 300m, invalidCount, DateTime.UtcNow));
    }
}
