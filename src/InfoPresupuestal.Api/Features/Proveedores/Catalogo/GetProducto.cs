using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Proveedores.Catalogo;

/// <summary>Un producto con todo su historial de precios, el más reciente primero.</summary>
public sealed record GetProductoQuery(Guid Id) : IQuery<ProductoProveedorResponse>;

internal sealed class GetProductoHandler(AppDbContext db) : IQueryHandler<GetProductoQuery, ProductoProveedorResponse>
{
    public async Task<Result<ProductoProveedorResponse>> HandleAsync(GetProductoQuery query, CancellationToken cancellationToken) =>
        await CargarAsync(db, query.Id, cancellationToken) is { } producto ? producto : CatalogoApi.NotFound(query.Id);

    public static async Task<ProductoProveedorResponse?> CargarAsync(AppDbContext db, Guid id, CancellationToken cancellationToken)
    {
        var p = await db.Set<ProveedorProducto>().AsNoTracking()
            .Include(x => x.TipoInsumo).Include(x => x.UnidadMedida)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (p is null)
        {
            return null;
        }

        var precios = await db.Set<ProveedorPrecio>().AsNoTracking()
            .Where(x => x.ProductoId == id)
            .OrderByDescending(x => x.FechaVigencia).ThenBy(x => x.Ciudad)
            .ToListAsync(cancellationToken);

        return new ProductoProveedorResponse(
            p.Id, p.Codigo, p.Descripcion, p.TipoInsumoId, p.TipoInsumo!.Nombre, p.UnidadMedidaId, p.UnidadMedida!.Codigo,
            p.Marca, p.Categoria, p.Activo, p.ActualizadoEn, precios.Select(x => x.AsResponse()).ToList());
    }
}

internal sealed class GetProductoEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet($"{ProveedoresApi.CatalogoRoute}/{{id:guid}}", async (Guid id, IQueryHandler<GetProductoQuery, ProductoProveedorResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new GetProductoQuery(id), ct)).ToOk())
            .WithName("GetProductoProveedor")
            .WithTags(ProveedoresApi.Tag)
            .RequireTipoEmpresa(TipoEmpresa.Proveedor)
            .RequirePermission(Modulos.CatalogoProveedor, Accion.Ver);
}
