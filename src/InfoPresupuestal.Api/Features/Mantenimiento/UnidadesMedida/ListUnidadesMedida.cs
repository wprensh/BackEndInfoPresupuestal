using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Mantenimiento.UnidadesMedida;

public sealed record ListUnidadesMedidaQuery(PageRequest Page) : IQuery<PagedResponse<UnidadMedidaResponse>>;

internal sealed class ListUnidadesMedidaHandler(AppDbContext db) : IQueryHandler<ListUnidadesMedidaQuery, PagedResponse<UnidadMedidaResponse>>
{
    public async Task<Result<PagedResponse<UnidadMedidaResponse>>> HandleAsync(ListUnidadesMedidaQuery query, CancellationToken cancellationToken)
    {
        var unidades = db.Set<UnidadMedida>().AsNoTracking();
        if (query.Page.SearchPattern is { } pattern)
        {
            unidades = unidades.Where(u => EF.Functions.ILike(u.Codigo, pattern) || EF.Functions.ILike(u.Nombre, pattern));
        }

        return await unidades.OrderBy(u => u.Codigo)
            .Select(u => new UnidadMedidaResponse(u.Id, u.Codigo, u.Nombre))
            .ToPagedAsync(query.Page, cancellationToken);
    }
}

internal sealed class ListUnidadesMedidaEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(UnidadesMedidaApi.Route, async ([AsParameters] PageRequest page, IQueryHandler<ListUnidadesMedidaQuery, PagedResponse<UnidadMedidaResponse>> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new ListUnidadesMedidaQuery(page), ct)).ToOk())
            .WithName("ListUnidadesMedida")
            .WithTags(MantenimientoApi.Tag)
            .RequirePermission(Modulos.UnidadesMedida, Accion.Ver);
}
