namespace InfoPresupuestal.Api.Features.Products.GetProductById;

public sealed record GetProductByIdResponse(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    int Stock,
    DateTimeOffset CreatedAtUtc);
