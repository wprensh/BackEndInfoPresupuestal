using InfoPresupuestal.Api.Common.Abstractions;

namespace InfoPresupuestal.Api.Features.Products.GetProductById;

/// <summary>Consulta cacheada en Redis por <see cref="CacheKey"/> durante <see cref="Expiration"/>.</summary>
public sealed record GetProductByIdQuery(Guid Id) : ICachedQuery<GetProductByIdResponse>
{
    public string CacheKey => ProductsApi.CacheKeyById(Id);

    public TimeSpan? Expiration => TimeSpan.FromMinutes(10);
}
