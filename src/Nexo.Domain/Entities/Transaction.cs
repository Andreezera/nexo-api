using Nexo.Domain.Enums;

namespace Nexo.Domain.Entities;

public class Transaction
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public TransactionType Type { get; private set; }
    public decimal Amount { get; private set; }
    public string Category { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public DateTime OccurredAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public Guid? InstallmentPlanId { get; private set; }
    public InstallmentPlan? InstallmentPlan { get; private set; }

    private Transaction() { }

    public Transaction(
        Guid userId,
        TransactionType type,
        decimal amount,
        string category,
        string description,
        DateTime occurredAt,
        Guid? installmentPlanId = null)
    {
        if (amount <= 0)
            throw new ArgumentException("O valor da transação deve ser maior que zero.", nameof(amount));

        Id = Guid.NewGuid();
        UserId = userId;
        Type = type;
        Amount = amount;
        Category = string.IsNullOrWhiteSpace(category) ? "Outros" : category;
        Description = description;
        OccurredAt = occurredAt;
        InstallmentPlanId = installmentPlanId;
        CreatedAt = DateTime.UtcNow;
    }
}
