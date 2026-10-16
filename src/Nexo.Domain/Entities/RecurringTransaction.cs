using Nexo.Domain.Enums;

namespace Nexo.Domain.Entities;

// Conta que se repete todo mês no mesmo dia (assinatura, aluguel, salário). A cada mês vira uma Transaction.
public class RecurringTransaction
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public TransactionType Type { get; private set; }
    public decimal Amount { get; private set; }
    public TransactionCategory Category { get; private set; }
    public string Description { get; private set; } = default!;
    public int DayOfMonth { get; private set; }
    public bool IsActive { get; private set; }

    // Primeiro dia do último mês em que o lançamento foi gerado. Evita gerar duas vezes no mesmo mês.
    public DateTime? LastGeneratedMonth { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private RecurringTransaction() { }

    // Se o dia deste mês já passou quando a conta é criada, ela só começa a valer no mês seguinte,
    // porque não dá para saber se o usuário já lançou o pagamento deste mês.
    public RecurringTransaction(
        Guid userId,
        TransactionType type,
        decimal amount,
        TransactionCategory category,
        string description,
        int dayOfMonth,
        DateTime today)
    {
        if (amount <= 0)
            throw new ArgumentException("O valor da conta recorrente deve ser maior que zero.", nameof(amount));
        if (dayOfMonth is < 1 or > 31)
            throw new ArgumentException("O dia do mês deve estar entre 1 e 31.", nameof(dayOfMonth));

        Id = Guid.NewGuid();
        UserId = userId;
        Type = type;
        Amount = amount;
        Category = category;
        Description = description;
        DayOfMonth = dayOfMonth;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;

        if (today.Day > EffectiveDay(today.Year, today.Month))
            LastGeneratedMonth = FirstOfMonth(today);
    }

    // Dia 31 em fevereiro vira o último dia do mês.
    public int EffectiveDay(int year, int month) => Math.Min(DayOfMonth, DateTime.DaysInMonth(year, month));

    public DateTime OccurrenceIn(int year, int month) =>
        new(year, month, EffectiveDay(year, month), 0, 0, 0, DateTimeKind.Utc);

    public bool IsDue(DateTime today) =>
        IsActive
        && (LastGeneratedMonth is null || LastGeneratedMonth < FirstOfMonth(today))
        && today.Day >= EffectiveDay(today.Year, today.Month);

    public Transaction GenerateTransaction(DateTime today)
    {
        if (!IsDue(today))
            throw new InvalidOperationException("Esta conta recorrente não vence agora.");

        LastGeneratedMonth = FirstOfMonth(today);
        return new Transaction(UserId, Type, Amount, Category, Description, OccurrenceIn(today.Year, today.Month));
    }

    // Próxima data em que a conta vai gerar um lançamento.
    public DateTime NextOccurrence(DateTime today)
    {
        var generatedThisMonth = LastGeneratedMonth is not null && LastGeneratedMonth >= FirstOfMonth(today);
        if (!generatedThisMonth)
            return OccurrenceIn(today.Year, today.Month);

        var next = FirstOfMonth(today).AddMonths(1);
        return OccurrenceIn(next.Year, next.Month);
    }

    public void Cancel() => IsActive = false;

    private static DateTime FirstOfMonth(DateTime date) => new(date.Year, date.Month, 1, 0, 0, 0, DateTimeKind.Utc);
}
