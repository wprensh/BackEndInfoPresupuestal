namespace InfoPresupuestal.Api.Features.Products;

/// <summary>Entidad de la feature Products. Se crea solo con <see cref="Create"/>, que protege sus invariantes.</summary>
public sealed class Product
{
    private Product()
    {
        // Requerido por EF Core.
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public decimal Price { get; private set; }

    public int Stock { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Product Create(string name, string? description, decimal price, int stock, DateTimeOffset createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(price);
        ArgumentOutOfRangeException.ThrowIfNegative(stock);

        return new Product
        {
            Id = Guid.CreateVersion7(createdAtUtc),
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Price = price,
            Stock = stock,
            CreatedAtUtc = createdAtUtc,
        };
    }
}
