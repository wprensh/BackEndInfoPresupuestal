using InfoPresupuestal.Api.Common.Abstractions;

namespace InfoPresupuestal.Api.Features.Products.CreateProduct;

/// <summary>Cuerpo de <c>POST /api/products</c>.</summary>
public sealed record CreateProductCommand(
    string Name,
    string? Description,
    decimal Price,
    int Stock) : ICommand<CreateProductResponse>;
