namespace InfoPresupuestal.Api.Features.Products;

/// <summary>Constantes compartidas por los slices de Products.</summary>
internal static class ProductsApi
{
    public const string Route = "/api/products";
    public const string Tag = "Products";

    public static string CacheKeyById(Guid id) => $"products:{id}";
}
