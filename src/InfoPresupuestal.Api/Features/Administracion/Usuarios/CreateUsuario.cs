using FluentValidation;
using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Common.Validation;
using InfoPresupuestal.Api.Features.Administracion.Roles;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Administracion.Usuarios;

public sealed record CreateUsuarioCommand(string Email, string Nombre, Guid RolId, string Password) : ICommand<UsuarioResponse>;

internal sealed class CreateUsuarioValidator : AbstractValidator<CreateUsuarioCommand>
{
    public CreateUsuarioValidator()
    {
        RuleFor(c => c.Email).NotEmpty().EmailAddress().MaximumLength(UsuarioConfiguration.EmailMaxLength);
        RuleFor(c => c.Nombre).NotEmpty().MaximumLength(UsuarioConfiguration.NombreMaxLength);
        RuleFor(c => c.RolId).NotEmpty();
        RuleFor(c => c.Password).ContrasenaSegura();
    }
}

internal sealed class CreateUsuarioHandler(AppDbContext db, IPasswordHasher<Usuario> passwordHasher, TimeProvider timeProvider)
    : ICommandHandler<CreateUsuarioCommand, UsuarioResponse>
{
    public async Task<Result<UsuarioResponse>> HandleAsync(CreateUsuarioCommand command, CancellationToken cancellationToken)
    {
        var email = Usuario.NormalizeEmail(command.Email);
        if (await db.Set<Usuario>().AnyAsync(u => u.Email == email, cancellationToken))
        {
            return Error.Conflict("Usuarios.EmailDuplicado", $"Ya existe un usuario con el correo '{email}' en la empresa.");
        }

        // El filtro global garantiza que el rol sea de la misma empresa.
        var rol = await db.Set<Rol>().AsNoTracking().FirstOrDefaultAsync(r => r.Id == command.RolId, cancellationToken);
        if (rol is null)
        {
            return UsuariosApi.RolInvalido(command.RolId);
        }

        var usuario = Usuario.Create(email, command.Nombre, rol.Id, timeProvider.GetUtcNow());
        usuario.SetPasswordHash(passwordHasher.HashPassword(usuario, command.Password));
        db.Add(usuario);
        await db.SaveChangesAsync(cancellationToken);

        return new UsuarioResponse(usuario.Id, usuario.Email, usuario.Nombre, rol.Id, rol.Nombre, usuario.Activo, false, null, usuario.CreadoEn);
    }
}

internal sealed class CreateUsuarioEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(UsuariosApi.Route, async (CreateUsuarioCommand command, ICommandHandler<CreateUsuarioCommand, UsuarioResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(command, ct)).ToHttp(u => Results.Created($"{UsuariosApi.Route}/{u.Id}", u)))
            .WithName("CreateUsuario")
            .WithTags(AdministracionApi.Tag)
            .WithRequestValidation<CreateUsuarioCommand>()
            .Produces<UsuarioResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(Modulos.Usuarios, Accion.Insertar);
}
