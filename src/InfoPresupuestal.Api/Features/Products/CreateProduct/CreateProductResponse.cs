namespace InfoPresupuestal.Api.Features.Products.CreateProduct;

public sealed record CreateProductResponse(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    int Stock,
    DateTimeOffset CreatedAtUtc);
