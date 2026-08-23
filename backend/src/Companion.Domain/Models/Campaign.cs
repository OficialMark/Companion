namespace Companion.Domain.Models;

public sealed class Campaign
{
    private Campaign() { }

    public Campaign(Guid id, string code, string name, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 80)
        {
            throw new ArgumentException("O nome da campanha deve ter entre 1 e 80 caracteres.", nameof(name));
        }

        Id = id;
        Code = code;
        Name = name.Trim();
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
}
