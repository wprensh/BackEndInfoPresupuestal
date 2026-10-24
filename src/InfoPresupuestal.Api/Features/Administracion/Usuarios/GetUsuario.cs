using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Administracion.Usuarios;

public sealed record GetUsuarioQuery(Guid Id) : IQuery<UsuarioResponse>;

internal sealed class GetUsuarioHandler(AppDbContext db) : IQueryHandler<GetUsuarioQuery, UsuarioResponse>
{
    public async Task<Result<UsuarioResponse>> HandleAsync(GetUsuarioQuery query, CancellationToken cancellationToken)
    {
        var usuario = await db.Set<Usuario>().AsNoTracking()
            .Where(u => u.Id == query.Id)
            .Select(UsuarioMapping.ToResponse)
            .FirstOrDefaultAsync(cancellationToken);

        return usuario is null ? UsuariosApi.NotFound(query.Id) : usuario;
    }
}

internal sealed class GetUsuarioEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet($"{UsuariosApi.Route}/{{id:guid}}", async (Guid id, IQueryHandler<GetUsuarioQuery, UsuarioResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new GetUsuarioQuery(id), ct)).ToOk())
            .WithName("GetUsuario")
            .WithTags(AdministracionApi.Tag)
            .Produces<UsuarioResponse>()
            .RequirePermission(Modulos.Usuarios, Accion.Ver);
}
