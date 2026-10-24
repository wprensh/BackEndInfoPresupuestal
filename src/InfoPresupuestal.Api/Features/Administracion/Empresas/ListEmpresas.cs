using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Administracion.Empresas;

public sealed record ListEmpresasQuery(PageRequest Page) : IQuery<PagedResponse<EmpresaResponse>>;

internal sealed class ListEmpresasHandler(AppDbContext db) : IQueryHandler<ListEmpresasQuery, PagedResponse<EmpresaResponse>>
{
    public async Task<Result<PagedResponse<EmpresaResponse>>> HandleAsync(ListEmpresasQuery query, CancellationToken cancellationToken)
    {
        var empresas = db.Set<Empresa>().AsNoTracking();
        if (query.Page.SearchPattern is { } pattern)
        {
            empresas = empresas.Where(e =>
                EF.Functions.ILike(e.RazonSocial, pattern) || EF.Functions.ILike(e.TenantId, pattern) || EF.Functions.ILike(e.Nit ?? "", pattern));
        }

        return await empresas.OrderBy(e => e.RazonSocial).Select(EmpresaMapping.ToResponse).ToPagedAsync(query.Page, cancellationToken);
    }
}

internal sealed class ListEmpresasEndpoint : IPlatformEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(EmpresasApi.Route, async ([AsParameters] PageRequest page, IQueryHandler<ListEmpresasQuery, PagedResponse<EmpresaResponse>> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new ListEmpresasQuery(page), ct)).ToOk())
            .WithName("ListEmpresas")
            .WithTags(AdministracionApi.Tag)
            .RequireAuthorization(PermissionEndpointExtensions.SuperAdminPolicy);
}
