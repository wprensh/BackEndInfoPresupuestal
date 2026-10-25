using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Proveedores.Catalogo;

public sealed record ListProductosRequest(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    short? TipoInsumoId = null,
    bool? Activo = null) : PageRequest(Page, PageSize, Search);

public sealed record ListProductosQuery(ListProductosRequest Filtro) : IQuery<PagedResponse<ProductoProveedorResponse>>;

/// <summary>Productos del proveedor actual con el precio vigente de cada ciudad.</summary>
internal sealed class ListProductosHandler(AppDbContext db) : IQueryHandler<ListProductosQuery, PagedResponse<ProductoProveedorResponse>>
{
    public async Task<Result<PagedResponse<ProductoProveedorResponse>>> HandleAsync(ListProductosQuery query, CancellationToken cancellationToken)
    {
        var f = query.Filtro;
        var productos = db.Set<ProveedorProducto>().AsNoTracking();
        if (f.SearchPattern is { } pattern)
        {
            productos = productos.Where(p =>
                EF.Functions.ILike(p.Descripcion, pattern) || EF.Functions.ILike(p.Codigo, pattern) || EF.Functions.ILike(p.Marca ?? "", pattern));
        }

        if (f.TipoInsumoId is { } tipo)
        {
            productos = productos.Where(p => p.TipoInsumoId == tipo);
        }

        if (f.Activo is { } activo)
        {
            productos = productos.Where(p => p.Activo == activo);
        }

        var pagina = await productos.OrderBy(p => p.Codigo)
            .Select(p => new { p.Id, p.Codigo, p.Descripcion, p.TipoInsumoId, Tipo = p.TipoInsumo!.Nombre, p.UnidadMedidaId, Unidad = p.UnidadMedida!.Codigo, p.Marca, p.Categoria, p.Activo, p.ActualizadoEn })
            .ToPagedAsync(f, cancellationToken);

        var precios = await CatalogoApi.PreciosVigentesAsync(db.Set<ProveedorPrecio>(), pagina.Items.Select(p => p.Id).ToList(), cancellationToken);
        var items = pagina.Items
            .Select(p => new ProductoProveedorResponse(
                p.Id, p.Codigo, p.Descripcion, p.TipoInsumoId, p.Tipo, p.UnidadMedidaId, p.Unidad, p.Marca, p.Categoria, p.Activo, p.ActualizadoEn,
                precios.GetValueOrDefault(p.Id) ?? []))
            .ToList();

        return new PagedResponse<ProductoProveedorResponse>(items, pagina.Page, pagina.PageSize, pagina.Total);
    }
}

internal sealed class ListProductosEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(ProveedoresApi.CatalogoRoute, async ([AsParameters] ListProductosRequest filtro, IQueryHandler<ListProductosQuery, PagedResponse<ProductoProveedorResponse>> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new ListProductosQuery(filtro), ct)).ToOk())
            .WithName("ListProductosProveedor")
            .WithTags(ProveedoresApi.Tag)
            .RequireTipoEmpresa(TipoEmpresa.Proveedor)
            .RequirePermission(Modulos.CatalogoProveedor, Accion.Ver);
}
