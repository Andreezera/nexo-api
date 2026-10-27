using Nexo.Domain.Entities;

namespace Nexo.Tests.Domain;

public class SavingsGoalTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Constructor_StartsEmptyAndActive()
    {
        var goal = new SavingsGoal(UserId, "  Viagem ", 3000m, new DateTime(2027, 6, 30, 15, 0, 0));

        Assert.Equal("Viagem", goal.Name);
        Assert.Equal(0m, goal.SavedAmount);
        Assert.Equal(3000m, goal.Remaining);
        Assert.Equal(new DateTime(2027, 6, 30), goal.Deadline);
        Assert.True(goal.IsActive);
        Assert.False(goal.IsReached);
        Assert.Equal(0m, goal.Progress);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Constructor_WithNonPositiveTarget_Throws(double target)
    {
        Assert.Throws<ArgumentException>(() => new SavingsGoal(UserId, "Viagem", (decimal)target, null));
    }

    [Fact]
    public void Constructor_WithoutAName_Throws()
    {
        Assert.Throws<ArgumentException>(() => new SavingsGoal(UserId, "  ", 100m, null));
    }

    [Fact]
    public void AddSaved_AccumulatesProgressAndReachesTheGoal()
    {
        var goal = new SavingsGoal(UserId, "Viagem", 1000m, null);

        goal.AddSaved(250m);
        Assert.Equal(0.25m, goal.Progress);
        Assert.Equal(750m, goal.Remaining);

        goal.AddSaved(800m);
        Assert.True(goal.IsReached);
        Assert.Equal(0m, goal.Remaining);
        Assert.Equal(1m, goal.Progress);
    }

    [Fact]
    public void AddSaved_WithNonPositiveAmount_Throws()
    {
        var goal = new SavingsGoal(UserId, "Viagem", 1000m, null);

        Assert.Throws<ArgumentException>(() => goal.AddSaved(0m));
    }

    [Fact]
    public void Update_ChangesTargetAndDeadlineKeepingWhatWasSaved()
    {
        var goal = new SavingsGoal(UserId, "Viagem", 1000m, null);
        goal.AddSaved(300m);

        goal.Update(2000m, new DateTime(2027, 1, 31));

        Assert.Equal(2000m, goal.TargetAmount);
        Assert.Equal(300m, goal.SavedAmount);
        Assert.Equal(new DateTime(2027, 1, 31), goal.Deadline);
    }

    [Fact]
    public void Cancel_DeactivatesTheGoal()
    {
        var goal = new SavingsGoal(UserId, "Viagem", 1000m, null);

        goal.Cancel();

        Assert.False(goal.IsActive);
    }
}
