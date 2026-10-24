using FluentValidation;
using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Common.Validation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Administracion.Usuarios;

/// <summary>Un administrador asigna una contraseña nueva a un usuario de su empresa.</summary>
public sealed record CambiarPasswordRequest(string Password);

public sealed record CambiarPasswordCommand(Guid Id, string Password) : ICommand<Done>;

internal sealed class CambiarPasswordValidator : AbstractValidator<CambiarPasswordRequest>
{
    public CambiarPasswordValidator() => RuleFor(c => c.Password).ContrasenaSegura();
}

internal sealed class CambiarPasswordHandler(AppDbContext db, IPasswordHasher<Usuario> passwordHasher) : ICommandHandler<CambiarPasswordCommand, Done>
{
    public async Task<Result<Done>> HandleAsync(CambiarPasswordCommand command, CancellationToken cancellationToken)
    {
        var usuario = await db.Set<Usuario>().FirstOrDefaultAsync(u => u.Id == command.Id, cancellationToken);
        if (usuario is null)
        {
            return UsuariosApi.NotFound(command.Id);
        }

        usuario.SetPasswordHash(passwordHasher.HashPassword(usuario, command.Password));
        await db.SaveChangesAsync(cancellationToken);
        return new Done();
    }
}

internal sealed class CambiarPasswordEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut($"{UsuariosApi.Route}/{{id:guid}}/password", async (Guid id, CambiarPasswordRequest datos, ICommandHandler<CambiarPasswordCommand, Done> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new CambiarPasswordCommand(id, datos.Password), ct)).ToNoContent())
            .WithName("CambiarPasswordUsuario")
            .WithTags(AdministracionApi.Tag)
            .WithRequestValidation<CambiarPasswordRequest>()
            .Produces(StatusCodes.Status204NoContent)
            .RequirePermission(Modulos.Usuarios, Accion.Editar);
}
