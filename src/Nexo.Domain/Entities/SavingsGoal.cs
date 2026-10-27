namespace Nexo.Domain.Entities;

// Meta de economia ("juntar 3000 até junho"). O valor guardado é acompanhado à parte do saldo das transações:
// guardar dinheiro numa meta não gera despesa nem receita.
public class SavingsGoal
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Name { get; private set; } = default!;
    public decimal TargetAmount { get; private set; }
    public decimal SavedAmount { get; private set; }
    public DateTime? Deadline { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private SavingsGoal() { }

    public SavingsGoal(Guid userId, string name, decimal targetAmount, DateTime? deadline)
    {
        if (targetAmount <= 0)
            throw new ArgumentException("O valor da meta deve ser maior que zero.", nameof(targetAmount));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A meta precisa de um nome.", nameof(name));

        Id = Guid.NewGuid();
        UserId = userId;
        Name = name.Trim();
        TargetAmount = targetAmount;
        Deadline = deadline?.Date;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    public decimal Remaining => Math.Max(0m, TargetAmount - SavedAmount);

    public bool IsReached => SavedAmount >= TargetAmount;

    public decimal Progress => Math.Min(1m, SavedAmount / TargetAmount);

    public void AddSaved(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("O valor guardado deve ser maior que zero.", nameof(amount));

        SavedAmount += amount;
    }

    public void Update(decimal targetAmount, DateTime? deadline)
    {
        if (targetAmount <= 0)
            throw new ArgumentException("O valor da meta deve ser maior que zero.", nameof(targetAmount));

        TargetAmount = targetAmount;
        Deadline = deadline?.Date;
    }

    public void Cancel() => IsActive = false;
}
