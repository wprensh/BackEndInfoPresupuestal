using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Products.GetProductById;

/// <summary>Lee de PostgreSQL. La caché la agrega el decorador, no este handler.</summary>
internal sealed class GetProductByIdHandler(AppDbContext db)
    : IQueryHandler<GetProductByIdQuery, GetProductByIdResponse>
{
    public async Task<Result<GetProductByIdResponse>> HandleAsync(GetProductByIdQuery query, CancellationToken cancellationToken)
    {
        var product = await db.Set<Product>()
            .AsNoTracking()
            .Where(p => p.Id == query.Id)
            .Select(GetProductByIdMapping.ToResponse)
            .FirstOrDefaultAsync(cancellationToken);

        return product is null ? ProductErrors.NotFound(query.Id) : product;
    }
}
