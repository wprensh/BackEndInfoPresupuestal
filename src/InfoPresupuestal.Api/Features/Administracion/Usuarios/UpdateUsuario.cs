using FluentValidation;
using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Common.Validation;
using InfoPresupuestal.Api.Features.Administracion.Roles;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Administracion.Usuarios;

/// <summary>El correo no se cambia: es el usuario de inicio de sesión.</summary>
public sealed record UpdateUsuarioRequest(string Nombre, Guid RolId, bool Activo);

public sealed record UpdateUsuarioCommand(Guid Id, UpdateUsuarioRequest Datos) : ICommand<UsuarioResponse>;

internal sealed class UpdateUsuarioValidator : AbstractValidator<UpdateUsuarioRequest>
{
    public UpdateUsuarioValidator()
    {
        RuleFor(c => c.Nombre).NotEmpty().MaximumLength(UsuarioConfiguration.NombreMaxLength);
        RuleFor(c => c.RolId).NotEmpty();
    }
}

internal sealed class UpdateUsuarioHandler(AppDbContext db, ICurrentUser currentUser) : ICommandHandler<UpdateUsuarioCommand, UsuarioResponse>
{
    public async Task<Result<UsuarioResponse>> HandleAsync(UpdateUsuarioCommand command, CancellationToken cancellationToken)
    {
        var usuario = await db.Set<Usuario>().FirstOrDefaultAsync(u => u.Id == command.Id, cancellationToken);
        if (usuario is null)
        {
            return UsuariosApi.NotFound(command.Id);
        }

        var d = command.Datos;
        if (usuario.Id == currentUser.Id && (!d.Activo || d.RolId != usuario.RolId))
        {
            return Error.Conflict("Usuarios.Propio", "No puedes desactivarte ni cambiar tu propio rol.");
        }

        var rol = await db.Set<Rol>().AsNoTracking().FirstOrDefaultAsync(r => r.Id == d.RolId, cancellationToken);
        if (rol is null)
        {
            return UsuariosApi.RolInvalido(d.RolId);
        }

        usuario.Update(d.Nombre, rol.Id, d.Activo);
        await db.SaveChangesAsync(cancellationToken);

        return new UsuarioResponse(usuario.Id, usuario.Email, usuario.Nombre, rol.Id, rol.Nombre, usuario.Activo, usuario.EsSuperAdmin, usuario.UltimoAcceso, usuario.CreadoEn);
    }
}

internal sealed class UpdateUsuarioEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut($"{UsuariosApi.Route}/{{id:guid}}", async (Guid id, UpdateUsuarioRequest datos, ICommandHandler<UpdateUsuarioCommand, UsuarioResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new UpdateUsuarioCommand(id, datos), ct)).ToOk())
            .WithName("UpdateUsuario")
            .WithTags(AdministracionApi.Tag)
            .WithRequestValidation<UpdateUsuarioRequest>()
            .Produces<UsuarioResponse>()
            .RequirePermission(Modulos.Usuarios, Accion.Editar);
}
