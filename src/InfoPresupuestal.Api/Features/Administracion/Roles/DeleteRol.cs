using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Features.Administracion.Usuarios;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Administracion.Roles;

public sealed record DeleteRolCommand(Guid Id) : ICommand<Done>;

internal sealed class DeleteRolHandler(AppDbContext db) : ICommandHandler<DeleteRolCommand, Done>
{
    public async Task<Result<Done>> HandleAsync(DeleteRolCommand command, CancellationToken cancellationToken)
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

        if (await db.Set<Usuario>().AnyAsync(u => u.RolId == rol.Id, cancellationToken))
        {
            return Error.Conflict("Roles.EnUso", "El rol tiene usuarios asignados. Cámbialos de rol antes de eliminarlo.");
        }

        db.Remove(rol);
        await db.SaveChangesAsync(cancellationToken);
        return new Done();
    }
}

internal sealed class DeleteRolEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete($"{RolesApi.Route}/{{id:guid}}", async (Guid id, ICommandHandler<DeleteRolCommand, Done> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new DeleteRolCommand(id), ct)).ToNoContent())
            .WithName("DeleteRol")
            .WithTags(AdministracionApi.Tag)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(Modulos.Roles, Accion.Eliminar);
}
