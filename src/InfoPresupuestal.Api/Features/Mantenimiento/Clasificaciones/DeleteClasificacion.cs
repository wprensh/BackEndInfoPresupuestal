using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Features.Mantenimiento.Insumos;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Mantenimiento.Clasificaciones;

public sealed record DeleteClasificacionCommand(int Id) : ICommand<Done>;

internal sealed class DeleteClasificacionHandler(AppDbContext db) : ICommandHandler<DeleteClasificacionCommand, Done>
{
    public async Task<Result<Done>> HandleAsync(DeleteClasificacionCommand command, CancellationToken cancellationToken)
    {
        var clasificacion = await db.Set<Clasificacion>().FirstOrDefaultAsync(c => c.Id == command.Id, cancellationToken);
        if (clasificacion is null)
        {
            return ClasificacionesApi.NotFound(command.Id);
        }

        if (await db.Set<Insumo>().AnyAsync(i => i.ClasificacionId == clasificacion.Id, cancellationToken))
        {
            return Error.Conflict("Clasificaciones.EnUso", "La clasificación tiene insumos. Desactívala o mueve sus insumos antes de eliminarla.");
        }

        db.Remove(clasificacion);
        await db.SaveChangesAsync(cancellationToken);
        return new Done();
    }
}

internal sealed class DeleteClasificacionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete($"{ClasificacionesApi.Route}/{{id:int}}", async (int id, ICommandHandler<DeleteClasificacionCommand, Done> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new DeleteClasificacionCommand(id), ct)).ToNoContent())
            .WithName("DeleteClasificacion")
            .WithTags(MantenimientoApi.Tag)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(Modulos.Clasificaciones, Accion.Eliminar);
}
