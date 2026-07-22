using Nexo.Domain.Enums;

namespace Nexo.Domain.Entities;

public class InstallmentPlan
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Description { get; private set; } = default!;
    public string Category { get; private set; } = default!;
    public decimal TotalAmount { get; private set; }
    public int InstallmentCount { get; private set; }
    public DateTime PurchaseDate { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private readonly List<Transaction> _transactions = new();
    public IReadOnlyCollection<Transaction> Transactions => _transactions.AsReadOnly();

    private InstallmentPlan() { }

    public static InstallmentPlan Create(
        Guid userId,
        string description,
        string category,
        decimal totalAmount,
        int installmentCount,
        DateTime purchaseDate)
    {
        if (totalAmount <= 0)
            throw new ArgumentException("O valor total deve ser maior que zero.", nameof(totalAmount));
        if (installmentCount <= 0)
            throw new ArgumentException("O número de parcelas deve ser maior que zero.", nameof(installmentCount));

        var plan = new InstallmentPlan
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Description = description,
            Category = string.IsNullOrWhiteSpace(category) ? "Outros" : category,
            TotalAmount = totalAmount,
            InstallmentCount = installmentCount,
            PurchaseDate = purchaseDate,
            CreatedAt = DateTime.UtcNow
        };

        plan.GenerateInstallmentTransactions();
        return plan;
    }

    // Divide o total em parcelas iguais, absorvendo o resto do arredondamento na última parcela.
    private void GenerateInstallmentTransactions()
    {
        var baseInstallment = Math.Round(TotalAmount / InstallmentCount, 2, MidpointRounding.AwayFromZero);
        var accumulated = 0m;

        for (var i = 0; i < InstallmentCount; i++)
        {
            var isLast = i == InstallmentCount - 1;
            var installmentAmount = isLast ? TotalAmount - accumulated : baseInstallment;
            accumulated += installmentAmount;

            var transaction = new Transaction(
                UserId,
                TransactionType.Expense,
                installmentAmount,
                Category,
                $"{Description} (parcela {i + 1}/{InstallmentCount})",
                PurchaseDate.AddMonths(i),
                Id);

            _transactions.Add(transaction);
        }
    }
}
