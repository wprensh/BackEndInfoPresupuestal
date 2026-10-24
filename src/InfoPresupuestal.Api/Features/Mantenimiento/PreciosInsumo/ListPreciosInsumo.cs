using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Features.Mantenimiento.Insumos;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Mantenimiento.PreciosInsumo;

/// <summary>Historial de precios del insumo, el más reciente primero.</summary>
public sealed record ListPreciosInsumoQuery(Guid InsumoId) : IQuery<IReadOnlyList<PrecioInsumoResponse>>;

internal sealed class ListPreciosInsumoHandler(AppDbContext db) : IQueryHandler<ListPreciosInsumoQuery, IReadOnlyList<PrecioInsumoResponse>>
{
    public async Task<Result<IReadOnlyList<PrecioInsumoResponse>>> HandleAsync(ListPreciosInsumoQuery query, CancellationToken cancellationToken)
    {
        if (!await db.Set<Insumo>().AnyAsync(i => i.Id == query.InsumoId, cancellationToken))
        {
            return InsumosApi.NotFound(query.InsumoId);
        }

        return await db.Set<InsumoPrecio>().AsNoTracking()
            .Where(p => p.InsumoId == query.InsumoId)
            .OrderByDescending(p => p.FechaVigencia)
            .Select(p => new PrecioInsumoResponse(p.Id, p.InsumoId, p.ValorUnitario, p.Moneda, p.FechaVigencia, p.Fuente))
            .ToListAsync(cancellationToken);
    }
}

internal sealed class ListPreciosInsumoEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(PreciosInsumoApi.Route, async (Guid insumoId, IQueryHandler<ListPreciosInsumoQuery, IReadOnlyList<PrecioInsumoResponse>> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new ListPreciosInsumoQuery(insumoId), ct)).ToOk())
            .WithName("ListPreciosInsumo")
            .WithTags(MantenimientoApi.Tag)
            .RequirePermission(Modulos.PreciosInsumo, Accion.Ver);
}
