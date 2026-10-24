using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Administracion.Roles;

public sealed record GetRolQuery(Guid Id) : IQuery<RolResponse>;

internal sealed class GetRolHandler(AppDbContext db) : IQueryHandler<GetRolQuery, RolResponse>
{
    public async Task<Result<RolResponse>> HandleAsync(GetRolQuery query, CancellationToken cancellationToken)
    {
        var rol = await db.Set<Rol>().AsNoTracking().FirstOrDefaultAsync(r => r.Id == query.Id, cancellationToken);
        return rol is null ? RolesApi.NotFound(query.Id) : rol.AsResponse();
    }
}

internal sealed class GetRolEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet($"{RolesApi.Route}/{{id:guid}}", async (Guid id, IQueryHandler<GetRolQuery, RolResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new GetRolQuery(id), ct)).ToOk())
            .WithName("GetRol")
            .WithTags(AdministracionApi.Tag)
            .Produces<RolResponse>()
            .RequirePermission(Modulos.Roles, Accion.Ver);
}
