using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Features.Mantenimiento.Insumos;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Mantenimiento.PreciosInsumo;

public sealed record DeletePrecioInsumoCommand(Guid InsumoId, long Id) : ICommand<Done>;

internal sealed class DeletePrecioInsumoHandler(AppDbContext db) : ICommandHandler<DeletePrecioInsumoCommand, Done>
{
    public async Task<Result<Done>> HandleAsync(DeletePrecioInsumoCommand command, CancellationToken cancellationToken)
    {
        var precio = await db.Set<InsumoPrecio>().FirstOrDefaultAsync(p => p.Id == command.Id && p.InsumoId == command.InsumoId, cancellationToken);
        if (precio is null)
        {
            return PreciosInsumoApi.NotFound(command.Id);
        }

        db.Remove(precio);
        await db.SaveChangesAsync(cancellationToken);
        return new Done();
    }
}

internal sealed class DeletePrecioInsumoEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete($"{PreciosInsumoApi.Route}/{{id:long}}", async (Guid insumoId, long id, ICommandHandler<DeletePrecioInsumoCommand, Done> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new DeletePrecioInsumoCommand(insumoId, id), ct)).ToNoContent())
            .WithName("DeletePrecioInsumo")
            .WithTags(MantenimientoApi.Tag)
            .Produces(StatusCodes.Status204NoContent)
            .RequirePermission(Modulos.PreciosInsumo, Accion.Eliminar);
}
