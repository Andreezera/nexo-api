namespace Nexo.Domain.Entities;

public class User
{
    public Guid Id { get; private set; }
    public string PhoneNumber { get; private set; } = default!;
    public string? Name { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private User() { }

    public User(string phoneNumber, string? name = null)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new ArgumentException("Número de telefone é obrigatório.", nameof(phoneNumber));

        Id = Guid.NewGuid();
        PhoneNumber = phoneNumber;
        Name = name;
        CreatedAt = DateTime.UtcNow;
    }
}
