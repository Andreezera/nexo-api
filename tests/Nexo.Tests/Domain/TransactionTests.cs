using Nexo.Domain.Entities;
using Nexo.Domain.Enums;
using Xunit;

namespace Nexo.Tests.Domain;

public class TransactionTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static Transaction CreateTransaction() => new(
        UserId, TransactionType.Expense, 100m, TransactionCategory.Mercado, "Compra inicial", new DateTime(2026, 7, 20));

    [Fact]
    public void Constructor_WhenAmountIsNotPositive_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new Transaction(UserId, TransactionType.Expense, 0m, TransactionCategory.Outros, "Teste", DateTime.UtcNow));
    }

    [Fact]
    public void Update_ChangesFieldsAndSetsUpdatedAt()
    {
        var transaction = CreateTransaction();

        transaction.Update(TransactionType.Income, 250m, TransactionCategory.Salario, "Correção", new DateTime(2026, 7, 21));

        Assert.Equal(TransactionType.Income, transaction.Type);
        Assert.Equal(250m, transaction.Amount);
        Assert.Equal(TransactionCategory.Salario, transaction.Category);
        Assert.Equal("Correção", transaction.Description);
        Assert.Equal(new DateTime(2026, 7, 21), transaction.OccurredAt);
        Assert.NotNull(transaction.UpdatedAt);
    }

    [Fact]
    public void Update_WhenAmountIsNotPositive_Throws()
    {
        var transaction = CreateTransaction();

        Assert.Throws<ArgumentException>(() =>
            transaction.Update(TransactionType.Expense, 0m, TransactionCategory.Outros, "Teste", DateTime.UtcNow));
    }

    [Fact]
    public void Update_WhenTransactionIsDeleted_Throws()
    {
        var transaction = CreateTransaction();
        transaction.Delete();

        Assert.Throws<InvalidOperationException>(() =>
            transaction.Update(TransactionType.Expense, 50m, TransactionCategory.Outros, "Teste", DateTime.UtcNow));
    }

    [Fact]
    public void Delete_MarksTransactionAsDeletedAndSetsDeletedAt()
    {
        var transaction = CreateTransaction();

        transaction.Delete();

        Assert.True(transaction.IsDeleted);
        Assert.NotNull(transaction.DeletedAt);
    }

    [Fact]
    public void Delete_WhenAlreadyDeleted_Throws()
    {
        var transaction = CreateTransaction();
        transaction.Delete();

        Assert.Throws<InvalidOperationException>(() => transaction.Delete());
    }
}
