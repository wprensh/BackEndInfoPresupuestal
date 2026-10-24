using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Presupuestos.Obras;

public sealed record ListPresupuestosRequest(int Page = 1, int PageSize = 20, string? Search = null, string? Estado = null) : PageRequest(Page, PageSize, Search);

public sealed record ListPresupuestosQuery(ListPresupuestosRequest Filtro) : IQuery<PagedResponse<PresupuestoResumenResponse>>;

internal sealed class ListPresupuestosHandler(AppDbContext db) : IQueryHandler<ListPresupuestosQuery, PagedResponse<PresupuestoResumenResponse>>
{
    public async Task<Result<PagedResponse<PresupuestoResumenResponse>>> HandleAsync(ListPresupuestosQuery query, CancellationToken cancellationToken)
    {
        var f = query.Filtro;
        var presupuestos = db.Set<Presupuesto>().AsNoTracking();
        if (f.SearchPattern is { } pattern)
        {
            presupuestos = presupuestos.Where(p =>
                EF.Functions.ILike(p.NombreObra, pattern) || EF.Functions.ILike(p.Codigo, pattern)
                || (p.Cliente != null && EF.Functions.ILike(p.Cliente, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(f.Estado))
        {
            var estado = f.Estado.Trim().ToUpperInvariant();
            presupuestos = presupuestos.Where(p => p.Estado == estado);
        }

        var pagina = await presupuestos.OrderByDescending(p => p.Fecha).ThenByDescending(p => p.Codigo)
            .Select(p => new
            {
                p.Id, p.Codigo, p.NombreObra, p.Cliente, p.Fecha, p.PropuestaNumero, p.Estado, p.ActualizadoEn,
                p.AdministracionPct, p.ImprevistosPct, p.UtilidadPct, p.IvaUtilidadPct,
                Capitulos = p.Capitulos.Count,
                Items = p.Capitulos.Sum(c => c.Items.Count),
            })
            .ToPagedAsync(f, cancellationToken);

        // Costo directo de la página en una sola consulta agrupada.
        var ids = pagina.Items.Select(p => p.Id).ToList();
        var costos = await db.Set<PresupuestoCapitulo>()
            .Where(c => ids.Contains(c.PresupuestoId))
            .SelectMany(c => c.Items, (c, i) => new { c.PresupuestoId, Valor = Math.Round(i.Cantidad * i.ValorUnitario, 2) })
            .GroupBy(x => x.PresupuestoId)
            .Select(g => new { PresupuestoId = g.Key, Total = g.Sum(x => x.Valor) })
            .ToDictionaryAsync(x => x.PresupuestoId, x => x.Total, cancellationToken);

        var items = pagina.Items.Select(p =>
        {
            var costoDirecto = costos.GetValueOrDefault(p.Id);
            var totales = PresupuestoCalculo.Totales(costoDirecto, new PorcentajesAiu(p.AdministracionPct, p.ImprevistosPct, p.UtilidadPct, p.IvaUtilidadPct), null);
            return new PresupuestoResumenResponse(
                p.Id, p.Codigo, p.NombreObra, p.Cliente, p.Fecha, p.PropuestaNumero, p.Estado, p.Capitulos, p.Items, costoDirecto, totales.ValorTotal, p.ActualizadoEn);
        }).ToList();
        return new PagedResponse<PresupuestoResumenResponse>(items, pagina.Page, pagina.PageSize, pagina.Total);
    }
}

internal sealed class ListPresupuestosEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(PresupuestosApi.Route, async ([AsParameters] ListPresupuestosRequest filtro, IQueryHandler<ListPresupuestosQuery, PagedResponse<PresupuestoResumenResponse>> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new ListPresupuestosQuery(filtro), ct)).ToOk())
            .WithName("ListPresupuestos")
            .WithTags(PresupuestosModulo.Tag)
            .RequirePermission(Modulos.Presupuestos, Accion.Ver);
}
