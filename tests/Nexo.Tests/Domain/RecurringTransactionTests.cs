using Nexo.Domain.Entities;
using Nexo.Domain.Enums;

namespace Nexo.Tests.Domain;

public class RecurringTransactionTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static DateTime Day(int year, int month, int day) => new(year, month, day, 12, 0, 0, DateTimeKind.Utc);

    private static RecurringTransaction Create(int dayOfMonth, DateTime today) =>
        new(UserId, TransactionType.Expense, 55.90m, TransactionCategory.Assinaturas, "Netflix", dayOfMonth, today);

    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    public void Constructor_WithInvalidDay_Throws(int day)
    {
        Assert.Throws<ArgumentException>(() => Create(day, Day(2026, 11, 25)));
    }

    [Fact]
    public void Constructor_WithNonPositiveAmount_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new RecurringTransaction(UserId, TransactionType.Expense, 0m, TransactionCategory.Outros, "x", 10, Day(2026, 11, 1)));
    }

    [Fact]
    public void CreatedBeforeTheDay_IsNotDueYetAndBecomesDueOnTheDay()
    {
        var recurring = Create(10, Day(2026, 11, 3));

        Assert.False(recurring.IsDue(Day(2026, 11, 9)));
        Assert.True(recurring.IsDue(Day(2026, 11, 10)));
        Assert.True(recurring.IsDue(Day(2026, 11, 28)));
        Assert.Equal(new DateTime(2026, 11, 10), recurring.NextOccurrence(Day(2026, 11, 3)).Date);
    }

    [Fact]
    public void CreatedAfterTheDay_DoesNotBackfillAndStartsNextMonth()
    {
        var recurring = Create(10, Day(2026, 11, 25));

        Assert.False(recurring.IsDue(Day(2026, 11, 25)));
        Assert.False(recurring.IsDue(Day(2026, 11, 30)));
        Assert.True(recurring.IsDue(Day(2026, 12, 10)));
        Assert.Equal(new DateTime(2026, 12, 10), recurring.NextOccurrence(Day(2026, 11, 25)).Date);
    }

    [Fact]
    public void GenerateTransaction_CreatesTheTransactionOnTheDayAndOnlyOncePerMonth()
    {
        var recurring = Create(10, Day(2026, 11, 3));

        var transaction = recurring.GenerateTransaction(Day(2026, 11, 14));

        Assert.Equal(new DateTime(2026, 11, 10), transaction.OccurredAt.Date);
        Assert.Equal(55.90m, transaction.Amount);
        Assert.Equal(TransactionCategory.Assinaturas, transaction.Category);
        Assert.Equal("Netflix", transaction.Description);
        Assert.False(recurring.IsDue(Day(2026, 11, 20)));
        Assert.Throws<InvalidOperationException>(() => recurring.GenerateTransaction(Day(2026, 11, 20)));
        Assert.True(recurring.IsDue(Day(2026, 12, 10)));
    }

    [Fact]
    public void Day31_InAShortMonth_FallsOnTheLastDay()
    {
        var recurring = Create(31, Day(2026, 1, 5));

        Assert.Equal(28, recurring.EffectiveDay(2026, 2));
        Assert.False(recurring.IsDue(Day(2026, 2, 27)));
        Assert.True(recurring.IsDue(Day(2026, 2, 28)));
        Assert.Equal(31, recurring.EffectiveDay(2026, 3));
    }

    [Fact]
    public void Cancel_StopsItFromBeingDue()
    {
        var recurring = Create(10, Day(2026, 11, 3));

        recurring.Cancel();

        Assert.False(recurring.IsActive);
        Assert.False(recurring.IsDue(Day(2026, 11, 20)));
    }
}
