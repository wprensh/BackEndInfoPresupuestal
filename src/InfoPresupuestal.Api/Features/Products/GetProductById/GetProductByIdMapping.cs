using System.Linq.Expressions;

namespace InfoPresupuestal.Api.Features.Products.GetProductById;

internal static class GetProductByIdMapping
{
    /// <summary>Proyección traducible a SQL: solo se leen las columnas de la respuesta.</summary>
    public static readonly Expression<Func<Product, GetProductByIdResponse>> ToResponse = product =>
        new GetProductByIdResponse(product.Id, product.Name, product.Description, product.Price, product.Stock, product.CreatedAtUtc);
}
