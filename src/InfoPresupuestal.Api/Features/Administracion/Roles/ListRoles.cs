using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Features.Administracion.Usuarios;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Administracion.Roles;

public sealed record ListRolesQuery(PageRequest Page) : IQuery<PagedResponse<RolResumenResponse>>;

internal sealed class ListRolesHandler(AppDbContext db) : IQueryHandler<ListRolesQuery, PagedResponse<RolResumenResponse>>
{
    public async Task<Result<PagedResponse<RolResumenResponse>>> HandleAsync(ListRolesQuery query, CancellationToken cancellationToken)
    {
        var roles = db.Set<Rol>().AsNoTracking();
        if (query.Page.SearchPattern is { } pattern)
        {
            roles = roles.Where(r => EF.Functions.ILike(r.Nombre, pattern));
        }

        return await roles
            .OrderByDescending(r => r.EsSistema).ThenBy(r => r.Nombre)
            .Select(r => new RolResumenResponse(r.Id, r.Nombre, r.Descripcion, r.EsSistema, db.Set<Usuario>().Count(u => u.RolId == r.Id)))
            .ToPagedAsync(query.Page, cancellationToken);
    }
}

internal sealed class ListRolesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(RolesApi.Route, async ([AsParameters] PageRequest page, IQueryHandler<ListRolesQuery, PagedResponse<RolResumenResponse>> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new ListRolesQuery(page), ct)).ToOk())
            .WithName("ListRoles")
            .WithTags(AdministracionApi.Tag)
            .RequirePermission(Modulos.Roles, Accion.Ver);
}
