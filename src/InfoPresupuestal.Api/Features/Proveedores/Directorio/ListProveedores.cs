using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Features.Administracion.Empresas;
using InfoPresupuestal.Api.Features.Proveedores.Catalogo;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Proveedores.Directorio;

public sealed record ProveedorResponse(
    Guid Id,
    string TenantId,
    string RazonSocial,
    string? Nit,
    string? Email,
    string? Telefono,
    int Productos,
    DateTimeOffset CreadoEn);

public sealed record ListProveedoresQuery(PageRequest Page) : IQuery<PagedResponse<ProveedorResponse>>;

/// <summary>
/// Directorio de proveedores activos. Las listas de precios son públicas para todas las constructoras,
/// por eso este listado cruza tenants a propósito (solo lectura y solo datos de proveedores).
/// </summary>
internal sealed class ListProveedoresHandler(AppDbContext db) : IQueryHandler<ListProveedoresQuery, PagedResponse<ProveedorResponse>>
{
    public async Task<Result<PagedResponse<ProveedorResponse>>> HandleAsync(ListProveedoresQuery query, CancellationToken cancellationToken)
    {
        var empresas = db.Set<Empresa>().AsNoTracking().Where(e => e.Tipo == TipoEmpresa.Proveedor && e.Activo);
        if (query.Page.SearchPattern is { } pattern)
        {
            empresas = empresas.Where(e => EF.Functions.ILike(e.RazonSocial, pattern) || EF.Functions.ILike(e.Nit ?? "", pattern));
        }

        var productos = db.Set<ProveedorProducto>().IgnoreQueryFilters().AsNoTracking().Where(p => p.Activo);
        return await empresas
            .OrderBy(e => e.RazonSocial)
            .Select(e => new ProveedorResponse(
                e.Id, e.TenantId, e.RazonSocial, e.Nit, e.Email, e.Telefono,
                productos.Count(p => p.TenantId == e.TenantId),
                e.CreadoEn))
            .ToPagedAsync(query.Page, cancellationToken);
    }
}

internal sealed class ListProveedoresEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(ProveedoresApi.DirectorioRoute, async ([AsParameters] PageRequest page, IQueryHandler<ListProveedoresQuery, PagedResponse<ProveedorResponse>> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new ListProveedoresQuery(page), ct)).ToOk())
            .WithName("ListProveedores")
            .WithTags(ProveedoresApi.Tag)
            .RequireTipoEmpresa(TipoEmpresa.Constructora)
            .RequirePermission(Modulos.Proveedores, Accion.Ver);
}
