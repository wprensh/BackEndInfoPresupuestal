using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Features.Administracion.Empresas;
using InfoPresupuestal.Api.Features.Proveedores.Catalogo;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Proveedores.Consulta;

/// <param name="Proveedor">Identificador (tenant) de un proveedor; vacío = todos.</param>
/// <param name="Ciudad">Solo productos con precio en esa ciudad o nacional; vacía = todas.</param>
public sealed record BuscarProductosRequest(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? Proveedor = null,
    short? TipoInsumoId = null,
    string? Ciudad = null) : PageRequest(Page, PageSize, Search);

public sealed record ProductoCatalogoResponse(
    Guid Id,
    string Proveedor,
    string RazonSocial,
    string Codigo,
    string Descripcion,
    short TipoInsumoId,
    string Tipo,
    short UnidadMedidaId,
    string Unidad,
    string? Marca,
    string? Categoria,
    IReadOnlyList<PrecioProveedorResponse> Precios);

public sealed record BuscarProductosQuery(BuscarProductosRequest Filtro) : IQuery<PagedResponse<ProductoCatalogoResponse>>;

/// <summary>
/// Productos activos de todos los proveedores activos, con el precio vigente de cada ciudad. Las listas de
/// precios son públicas para las constructoras, por eso la consulta cruza tenants (solo lectura).
/// </summary>
internal sealed class BuscarProductosHandler(AppDbContext db) : IQueryHandler<BuscarProductosQuery, PagedResponse<ProductoCatalogoResponse>>
{
    public async Task<Result<PagedResponse<ProductoCatalogoResponse>>> HandleAsync(BuscarProductosQuery query, CancellationToken cancellationToken)
    {
        var f = query.Filtro;
        var ciudad = ProveedorPrecio.NormalizeCiudad(f.Ciudad);
        var empresas = db.Set<Empresa>().AsNoTracking().Where(e => e.Tipo == TipoEmpresa.Proveedor && e.Activo);
        var precios = db.Set<ProveedorPrecio>().IgnoreQueryFilters();

        var productos = db.Set<ProveedorProducto>().IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.Activo && empresas.Any(e => e.TenantId == p.TenantId));
        if (f.SearchPattern is { } pattern)
        {
            productos = productos.Where(p =>
                EF.Functions.ILike(p.Descripcion, pattern) || EF.Functions.ILike(p.Codigo, pattern)
                || EF.Functions.ILike(p.Marca ?? "", pattern) || EF.Functions.ILike(p.Categoria ?? "", pattern));
        }

        if (!string.IsNullOrWhiteSpace(f.Proveedor))
        {
            var proveedor = f.Proveedor.Trim().ToLowerInvariant();
            productos = productos.Where(p => p.TenantId == proveedor);
        }

        if (f.TipoInsumoId is { } tipo)
        {
            productos = productos.Where(p => p.TipoInsumoId == tipo);
        }

        if (ciudad is not null)
        {
            productos = productos.Where(p => precios.Any(x => x.ProductoId == p.Id && (x.Ciudad == null || x.Ciudad == ciudad)));
        }

        var pagina = await productos.OrderBy(p => p.Descripcion).ThenBy(p => p.TenantId)
            .Select(p => new
            {
                p.Id, p.TenantId,
                RazonSocial = empresas.Where(e => e.TenantId == p.TenantId).Select(e => e.RazonSocial).First(),
                p.Codigo, p.Descripcion, p.TipoInsumoId, Tipo = p.TipoInsumo!.Nombre, p.UnidadMedidaId, Unidad = p.UnidadMedida!.Codigo, p.Marca, p.Categoria,
            })
            .ToPagedAsync(f, cancellationToken);

        var vigentes = await CatalogoApi.PreciosVigentesAsync(precios, pagina.Items.Select(p => p.Id).ToList(), cancellationToken);
        var items = pagina.Items
            .Select(p => new ProductoCatalogoResponse(
                p.Id, p.TenantId, p.RazonSocial, p.Codigo, p.Descripcion, p.TipoInsumoId, p.Tipo, p.UnidadMedidaId, p.Unidad, p.Marca, p.Categoria,
                (vigentes.GetValueOrDefault(p.Id) ?? []).Where(x => ciudad is null || x.Ciudad is null || x.Ciudad == ciudad).ToList()))
            .ToList();

        return new PagedResponse<ProductoCatalogoResponse>(items, pagina.Page, pagina.PageSize, pagina.Total);
    }
}

internal sealed class BuscarProductosEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(ConsultaApi.ProductosRoute, async ([AsParameters] BuscarProductosRequest filtro, IQueryHandler<BuscarProductosQuery, PagedResponse<ProductoCatalogoResponse>> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new BuscarProductosQuery(filtro), ct)).ToOk())
            .WithName("BuscarProductosProveedores")
            .WithTags(ProveedoresApi.Tag)
            .RequireTipoEmpresa(TipoEmpresa.Constructora)
            .RequirePermission(Modulos.Proveedores, Accion.Ver);
}
