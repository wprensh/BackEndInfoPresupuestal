using FluentValidation;
using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Common.Validation;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Administracion.Roles;

public sealed record UpdateRolRequest(string Nombre, string? Descripcion, IReadOnlyList<PermisoDto> Permisos);

public sealed record UpdateRolCommand(Guid Id, UpdateRolRequest Datos) : ICommand<RolResponse>;

internal sealed class UpdateRolValidator : AbstractValidator<UpdateRolRequest>
{
    public UpdateRolValidator()
    {
        RuleFor(c => c.Nombre).NotEmpty().MaximumLength(RolConfiguration.NombreMaxLength);
        RuleFor(c => c.Descripcion).MaximumLength(RolConfiguration.DescripcionMaxLength);
        RuleFor(c => c.Permisos).NotNull().SetValidator(new PermisosValidator());
    }
}

/// <summary>Los cambios de permisos aplican cuando los usuarios del rol vuelven a iniciar sesión.</summary>
internal sealed class UpdateRolHandler(AppDbContext db) : ICommandHandler<UpdateRolCommand, RolResponse>
{
    public async Task<Result<RolResponse>> HandleAsync(UpdateRolCommand command, CancellationToken cancellationToken)
    {
        var rol = await db.Set<Rol>().FirstOrDefaultAsync(r => r.Id == command.Id, cancellationToken);
        if (rol is null)
        {
            return RolesApi.NotFound(command.Id);
        }

        if (rol.EsSistema)
        {
            return RolesApi.EsSistema();
        }

        var nombre = command.Datos.Nombre.Trim();
        if (await db.Set<Rol>().AnyAsync(r => r.Id != rol.Id && EF.Functions.ILike(r.Nombre, LikePattern.Exact(nombre)), cancellationToken))
        {
            return RolesApi.NombreDuplicado(nombre);
        }

        rol.Update(nombre, command.Datos.Descripcion, command.Datos.Permisos.ToEntities());
        await db.SaveChangesAsync(cancellationToken);
        return rol.AsResponse();
    }
}

internal sealed class UpdateRolEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut($"{RolesApi.Route}/{{id:guid}}", async (Guid id, UpdateRolRequest datos, ICommandHandler<UpdateRolCommand, RolResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new UpdateRolCommand(id, datos), ct)).ToOk())
            .WithName("UpdateRol")
            .WithTags(AdministracionApi.Tag)
            .WithRequestValidation<UpdateRolRequest>()
            .Produces<RolResponse>()
            .RequirePermission(Modulos.Roles, Accion.Editar);
}
