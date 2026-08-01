using Nexo.Domain.Entities;
using Nexo.Domain.Enums;

namespace Nexo.Tests.Domain;

public class BudgetLimitTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Constructor_SetsFieldsAndLeavesUpdatedAtEmpty()
    {
        var limit = new BudgetLimit(UserId, TransactionCategory.Mercado, 800m);

        Assert.Equal(UserId, limit.UserId);
        Assert.Equal(TransactionCategory.Mercado, limit.Category);
        Assert.Equal(800m, limit.MonthlyLimit);
        Assert.Null(limit.UpdatedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Constructor_WhenLimitIsNotPositive_Throws(decimal invalidLimit)
    {
        Assert.Throws<ArgumentException>(() => new BudgetLimit(UserId, TransactionCategory.Lazer, invalidLimit));
    }

    [Fact]
    public void UpdateLimit_ChangesValueAndSetsUpdatedAt()
    {
        var limit = new BudgetLimit(UserId, TransactionCategory.Lazer, 300m);

        limit.UpdateLimit(450m);

        Assert.Equal(450m, limit.MonthlyLimit);
        Assert.NotNull(limit.UpdatedAt);
    }

    [Fact]
    public void UpdateLimit_WhenLimitIsNotPositive_Throws()
    {
        var limit = new BudgetLimit(UserId, TransactionCategory.Lazer, 300m);

        Assert.Throws<ArgumentException>(() => limit.UpdateLimit(0m));
    }
}
