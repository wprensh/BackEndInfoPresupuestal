using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Administracion.Usuarios;

public sealed record DeleteUsuarioCommand(Guid Id) : ICommand<Done>;

internal sealed class DeleteUsuarioHandler(AppDbContext db, ICurrentUser currentUser) : ICommandHandler<DeleteUsuarioCommand, Done>
{
    public async Task<Result<Done>> HandleAsync(DeleteUsuarioCommand command, CancellationToken cancellationToken)
    {
        var usuario = await db.Set<Usuario>().FirstOrDefaultAsync(u => u.Id == command.Id, cancellationToken);
        if (usuario is null)
        {
            return UsuariosApi.NotFound(command.Id);
        }

        if (usuario.Id == currentUser.Id || usuario.EsSuperAdmin)
        {
            return Error.Conflict("Usuarios.NoEliminable", "No puedes eliminar tu propio usuario ni al superadministrador.");
        }

        db.Remove(usuario);
        await db.SaveChangesAsync(cancellationToken);
        return new Done();
    }
}

internal sealed class DeleteUsuarioEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete($"{UsuariosApi.Route}/{{id:guid}}", async (Guid id, ICommandHandler<DeleteUsuarioCommand, Done> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new DeleteUsuarioCommand(id), ct)).ToNoContent())
            .WithName("DeleteUsuario")
            .WithTags(AdministracionApi.Tag)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(Modulos.Usuarios, Accion.Eliminar);
}
