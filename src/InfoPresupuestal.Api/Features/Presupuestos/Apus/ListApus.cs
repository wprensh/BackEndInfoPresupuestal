using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Presupuestos.Apus;

public sealed record ListApusRequest(int Page = 1, int PageSize = 20, string? Search = null, bool? Activo = null) : PageRequest(Page, PageSize, Search);

public sealed record ListApusQuery(ListApusRequest Filtro) : IQuery<PagedResponse<ApuResumenResponse>>;

internal sealed class ListApusHandler(AppDbContext db) : IQueryHandler<ListApusQuery, PagedResponse<ApuResumenResponse>>
{
    public async Task<Result<PagedResponse<ApuResumenResponse>>> HandleAsync(ListApusQuery query, CancellationToken cancellationToken)
    {
        var f = query.Filtro;
        var apus = db.Set<Apu>().AsNoTracking();
        if (f.SearchPattern is { } pattern)
        {
            apus = apus.Where(a => EF.Functions.ILike(a.Descripcion, pattern) || EF.Functions.ILike(a.Codigo, pattern));
        }

        if (f.Activo is { } activo)
        {
            apus = apus.Where(a => a.Activo == activo);
        }

        var pagina = await apus.OrderBy(a => a.Codigo)
            .Select(a => new { a.Id, a.Codigo, a.Descripcion, a.UnidadMedidaId, Unidad = a.UnidadMedida!.Codigo, a.Fecha, a.Activo, a.ActualizadoEn })
            .ToPagedAsync(f, cancellationToken);

        var costos = await ApusApi.CostosAsync(db, pagina.Items.Select(a => a.Id).ToList(), cancellationToken);
        var items = pagina.Items
            .Select(a => new ApuResumenResponse(a.Id, a.Codigo, a.Descripcion, a.UnidadMedidaId, a.Unidad, a.Fecha, a.Activo, costos.GetValueOrDefault(a.Id, ApuCostos.Cero), a.ActualizadoEn))
            .ToList();
        return new PagedResponse<ApuResumenResponse>(items, pagina.Page, pagina.PageSize, pagina.Total);
    }
}

internal sealed class ListApusEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(ApusApi.Route, async ([AsParameters] ListApusRequest filtro, IQueryHandler<ListApusQuery, PagedResponse<ApuResumenResponse>> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new ListApusQuery(filtro), ct)).ToOk())
            .WithName("ListApus")
            .WithTags(PresupuestosModulo.Tag)
            .RequirePermission(Modulos.Apu, Accion.Ver);
}
