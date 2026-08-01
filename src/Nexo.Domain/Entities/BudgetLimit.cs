using Nexo.Domain.Enums;

namespace Nexo.Domain.Entities;

public class BudgetLimit
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public TransactionCategory Category { get; private set; }
    public decimal MonthlyLimit { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private BudgetLimit() { }

    public BudgetLimit(Guid userId, TransactionCategory category, decimal monthlyLimit)
    {
        if (monthlyLimit <= 0)
            throw new ArgumentException("O limite mensal deve ser maior que zero.", nameof(monthlyLimit));

        Id = Guid.NewGuid();
        UserId = userId;
        Category = category;
        MonthlyLimit = monthlyLimit;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateLimit(decimal monthlyLimit)
    {
        if (monthlyLimit <= 0)
            throw new ArgumentException("O limite mensal deve ser maior que zero.", nameof(monthlyLimit));

        MonthlyLimit = monthlyLimit;
        UpdatedAt = DateTime.UtcNow;
    }
}
