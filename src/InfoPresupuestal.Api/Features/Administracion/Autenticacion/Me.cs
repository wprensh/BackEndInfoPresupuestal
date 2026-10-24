using System.Security.Claims;
using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Features.Administracion.Empresas;
using InfoPresupuestal.Api.Features.Administracion.Usuarios;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Administracion.Autenticacion;

public sealed record MeQuery(Guid UsuarioId, string TenantId) : IQuery<MeResponse>;

public sealed record MeResponse(SesionUsuario Usuario, IReadOnlyCollection<string> Permisos);

internal sealed class MeHandler(AppDbContext db) : IQueryHandler<MeQuery, MeResponse>
{
    public async Task<Result<MeResponse>> HandleAsync(MeQuery query, CancellationToken cancellationToken)
    {
        var empresa = await db.Set<Empresa>().AsNoTracking()
            .FirstOrDefaultAsync(e => e.TenantId == query.TenantId && e.Activo, cancellationToken);

        // Administrativo explícito: el tenant viene del token ya validado, no del grupo de endpoints.
        var usuario = empresa is null
            ? null
            : await db.Set<Usuario>().IgnoreQueryFilters().AsNoTracking()
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(u => u.Id == query.UsuarioId && u.TenantId == query.TenantId && u.Activo, cancellationToken);
        if (empresa is null || usuario is null)
        {
            return Error.Unauthorized("Auth.SesionInvalida", "La sesión ya no es válida.");
        }

        var sesion = new SesionUsuario(usuario.Id, usuario.Email, usuario.Nombre, empresa.TenantId, empresa.RazonSocial, usuario.Rol?.Nombre ?? string.Empty, usuario.EsSuperAdmin);
        return new MeResponse(sesion, AutenticacionApi.Permisos(usuario));
    }
}

internal sealed class MeEndpoint : IPlatformEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet($"{AutenticacionApi.Route}/me", async (ClaimsPrincipal user, IQueryHandler<MeQuery, MeResponse> handler, CancellationToken ct) =>
            {
                if (!Guid.TryParse(user.FindFirstValue(AppClaims.UserId), out var id) || user.FindFirstValue(AppClaims.Tenant) is not { Length: > 0 } tenant)
                {
                    return Results.Unauthorized();
                }

                return (await handler.HandleAsync(new MeQuery(id, tenant), ct)).ToOk();
            })
            .WithName("Me")
            .WithTags(AutenticacionApi.Tag)
            .Produces<MeResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
}
