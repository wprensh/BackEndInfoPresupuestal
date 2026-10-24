using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Mantenimiento.TiposInsumo;

public sealed record ListTiposInsumoQuery(PageRequest Page) : IQuery<PagedResponse<TipoInsumoResponse>>;

internal sealed class ListTiposInsumoHandler(AppDbContext db) : IQueryHandler<ListTiposInsumoQuery, PagedResponse<TipoInsumoResponse>>
{
    public async Task<Result<PagedResponse<TipoInsumoResponse>>> HandleAsync(ListTiposInsumoQuery query, CancellationToken cancellationToken)
    {
        var tipos = db.Set<TipoInsumo>().AsNoTracking();
        if (query.Page.SearchPattern is { } pattern)
        {
            tipos = tipos.Where(t => EF.Functions.ILike(t.Codigo, pattern) || EF.Functions.ILike(t.Nombre, pattern));
        }

        return await tipos.OrderBy(t => t.Id)
            .Select(t => new TipoInsumoResponse(t.Id, t.Codigo, t.Nombre, t.Prefijo))
            .ToPagedAsync(query.Page, cancellationToken);
    }
}

internal sealed class ListTiposInsumoEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(TiposInsumoApi.Route, async ([AsParameters] PageRequest page, IQueryHandler<ListTiposInsumoQuery, PagedResponse<TipoInsumoResponse>> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new ListTiposInsumoQuery(page), ct)).ToOk())
            .WithName("ListTiposInsumo")
            .WithTags(MantenimientoApi.Tag)
            .RequirePermission(Modulos.TiposInsumo, Accion.Ver);
}
