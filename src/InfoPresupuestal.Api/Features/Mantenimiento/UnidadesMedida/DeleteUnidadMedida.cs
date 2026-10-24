using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Features.Mantenimiento.Insumos;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Mantenimiento.UnidadesMedida;

public sealed record DeleteUnidadMedidaCommand(short Id) : ICommand<Done>;

internal sealed class DeleteUnidadMedidaHandler(AppDbContext db) : ICommandHandler<DeleteUnidadMedidaCommand, Done>
{
    public async Task<Result<Done>> HandleAsync(DeleteUnidadMedidaCommand command, CancellationToken cancellationToken)
    {
        var unidad = await db.Set<UnidadMedida>().FirstOrDefaultAsync(u => u.Id == command.Id, cancellationToken);
        if (unidad is null)
        {
            return UnidadesMedidaApi.NotFound(command.Id);
        }

        // Catálogo global: se revisa el uso en todas las empresas, no solo en la actual.
        if (await db.Set<Insumo>().IgnoreQueryFilters().AnyAsync(i => i.UnidadMedidaId == unidad.Id, cancellationToken))
        {
            return Error.Conflict("UnidadesMedida.EnUso", $"La unidad '{unidad.Codigo}' la usan insumos y no se puede eliminar.");
        }

        db.Remove(unidad);
        await db.SaveChangesAsync(cancellationToken);
        return new Done();
    }
}

internal sealed class DeleteUnidadMedidaEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete($"{UnidadesMedidaApi.Route}/{{id:int}}", async (short id, ICommandHandler<DeleteUnidadMedidaCommand, Done> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new DeleteUnidadMedidaCommand(id), ct)).ToNoContent())
            .WithName("DeleteUnidadMedida")
            .WithTags(MantenimientoApi.Tag)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(Modulos.UnidadesMedida, Accion.Eliminar);
}
