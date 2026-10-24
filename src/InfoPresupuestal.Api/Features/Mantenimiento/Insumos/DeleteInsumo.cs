using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Mantenimiento.Insumos;

public sealed record DeleteInsumoCommand(Guid Id) : ICommand<Done>;

/// <summary>Borra el insumo y su historial de precios. Para conservarlo, se desactiva con PUT.</summary>
internal sealed class DeleteInsumoHandler(AppDbContext db) : ICommandHandler<DeleteInsumoCommand, Done>
{
    public async Task<Result<Done>> HandleAsync(DeleteInsumoCommand command, CancellationToken cancellationToken)
    {
        var insumo = await db.Set<Insumo>().FirstOrDefaultAsync(i => i.Id == command.Id, cancellationToken);
        if (insumo is null)
        {
            return InsumosApi.NotFound(command.Id);
        }

        db.Remove(insumo);
        await db.SaveChangesAsync(cancellationToken);
        return new Done();
    }
}

internal sealed class DeleteInsumoEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete($"{InsumosApi.Route}/{{id:guid}}", async (Guid id, ICommandHandler<DeleteInsumoCommand, Done> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new DeleteInsumoCommand(id), ct)).ToNoContent())
            .WithName("DeleteInsumo")
            .WithTags(MantenimientoApi.Tag)
            .Produces(StatusCodes.Status204NoContent)
            .RequirePermission(Modulos.Insumos, Accion.Eliminar);
}
