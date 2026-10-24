using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Features.Mantenimiento.Insumos;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Mantenimiento.Clasificaciones;

public sealed record ListClasificacionesRequest(int Page = 1, int PageSize = 20, string? Search = null, bool? Activo = null)
    : PageRequest(Page, PageSize, Search);

public sealed record ListClasificacionesQuery(ListClasificacionesRequest Filtro) : IQuery<PagedResponse<ClasificacionResponse>>;

internal sealed class ListClasificacionesHandler(AppDbContext db) : IQueryHandler<ListClasificacionesQuery, PagedResponse<ClasificacionResponse>>
{
    public async Task<Result<PagedResponse<ClasificacionResponse>>> HandleAsync(ListClasificacionesQuery query, CancellationToken cancellationToken)
    {
        var f = query.Filtro;
        var clasificaciones = db.Set<Clasificacion>().AsNoTracking();
        if (f.SearchPattern is { } pattern)
        {
            clasificaciones = clasificaciones.Where(c => EF.Functions.ILike(c.Nombre, pattern));
        }

        if (f.Activo is { } activo)
        {
            clasificaciones = clasificaciones.Where(c => c.Activo == activo);
        }

        return await clasificaciones.OrderBy(c => c.Nombre)
            .Select(c => new ClasificacionResponse(c.Id, c.Nombre, c.Activo, db.Set<Insumo>().Count(i => i.ClasificacionId == c.Id)))
            .ToPagedAsync(f, cancellationToken);
    }
}

internal sealed class ListClasificacionesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(ClasificacionesApi.Route, async ([AsParameters] ListClasificacionesRequest filtro, IQueryHandler<ListClasificacionesQuery, PagedResponse<ClasificacionResponse>> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new ListClasificacionesQuery(filtro), ct)).ToOk())
            .WithName("ListClasificaciones")
            .WithTags(MantenimientoApi.Tag)
            .RequirePermission(Modulos.Clasificaciones, Accion.Ver);
}
