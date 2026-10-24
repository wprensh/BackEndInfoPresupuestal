using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Mantenimiento.Insumos;

public sealed record ListInsumosRequest(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    short? TipoInsumoId = null,
    int? ClasificacionId = null,
    bool? Activo = null) : PageRequest(Page, PageSize, Search);

public sealed record ListInsumosQuery(ListInsumosRequest Filtro) : IQuery<PagedResponse<InsumoResponse>>;

internal sealed class ListInsumosHandler(AppDbContext db) : IQueryHandler<ListInsumosQuery, PagedResponse<InsumoResponse>>
{
    public async Task<Result<PagedResponse<InsumoResponse>>> HandleAsync(ListInsumosQuery query, CancellationToken cancellationToken)
    {
        var f = query.Filtro;
        var insumos = db.Set<Insumo>().AsNoTracking();
        if (f.SearchPattern is { } pattern)
        {
            // El índice trigram de descripcion acelera este ILIKE.
            insumos = insumos.Where(i => EF.Functions.ILike(i.Descripcion, pattern) || EF.Functions.ILike(i.Codigo, pattern));
        }

        if (f.TipoInsumoId is { } tipo)
        {
            insumos = insumos.Where(i => i.TipoInsumoId == tipo);
        }

        if (f.ClasificacionId is { } clasificacion)
        {
            insumos = insumos.Where(i => i.ClasificacionId == clasificacion);
        }

        if (f.Activo is { } activo)
        {
            insumos = insumos.Where(i => i.Activo == activo);
        }

        return await insumos.OrderBy(i => i.Codigo).Select(InsumoMapping.ToResponse(db)).ToPagedAsync(f, cancellationToken);
    }
}

internal sealed class ListInsumosEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(InsumosApi.Route, async ([AsParameters] ListInsumosRequest filtro, IQueryHandler<ListInsumosQuery, PagedResponse<InsumoResponse>> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new ListInsumosQuery(filtro), ct)).ToOk())
            .WithName("ListInsumos")
            .WithTags(MantenimientoApi.Tag)
            .RequirePermission(Modulos.Insumos, Accion.Ver);
}
