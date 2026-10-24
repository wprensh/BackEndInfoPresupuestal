using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Mantenimiento.Insumos;

public sealed record GetInsumoQuery(Guid Id) : IQuery<InsumoResponse>;

internal sealed class GetInsumoHandler(AppDbContext db) : IQueryHandler<GetInsumoQuery, InsumoResponse>
{
    public async Task<Result<InsumoResponse>> HandleAsync(GetInsumoQuery query, CancellationToken cancellationToken)
    {
        var insumo = await db.Set<Insumo>().AsNoTracking()
            .Where(i => i.Id == query.Id)
            .Select(InsumoMapping.ToResponse(db))
            .FirstOrDefaultAsync(cancellationToken);

        return insumo is null ? InsumosApi.NotFound(query.Id) : insumo;
    }
}

internal sealed class GetInsumoEndpoint : IEndpoint
{
    public const string Name = "GetInsumo";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet($"{InsumosApi.Route}/{{id:guid}}", async (Guid id, IQueryHandler<GetInsumoQuery, InsumoResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new GetInsumoQuery(id), ct)).ToOk())
            .WithName(Name)
            .WithTags(MantenimientoApi.Tag)
            .Produces<InsumoResponse>()
            .RequirePermission(Modulos.Insumos, Accion.Ver);
}
