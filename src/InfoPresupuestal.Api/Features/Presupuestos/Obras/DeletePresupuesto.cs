using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Presupuestos.Obras;

public sealed record DeletePresupuestoCommand(Guid Id) : ICommand<Done>;

/// <summary>Un presupuesto aprobado no se borra: primero se anula o vuelve a borrador.</summary>
internal sealed class DeletePresupuestoHandler(AppDbContext db) : ICommandHandler<DeletePresupuestoCommand, Done>
{
    public async Task<Result<Done>> HandleAsync(DeletePresupuestoCommand command, CancellationToken cancellationToken)
    {
        var presupuesto = await db.Set<Presupuesto>()
            .Include(p => p.Capitulos).ThenInclude(c => c.Items)
            .FirstOrDefaultAsync(p => p.Id == command.Id, cancellationToken);
        if (presupuesto is null)
        {
            return PresupuestosApi.NotFound(command.Id);
        }

        if (presupuesto.Estado == EstadoPresupuesto.Aprobado)
        {
            return Error.Conflict("Presupuestos.Aprobado", "Un presupuesto aprobado no se puede eliminar.");
        }

        db.Remove(presupuesto);
        await db.SaveChangesAsync(cancellationToken);
        return new Done();
    }
}

internal sealed class DeletePresupuestoEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete($"{PresupuestosApi.Route}/{{id:guid}}", async (Guid id, ICommandHandler<DeletePresupuestoCommand, Done> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new DeletePresupuestoCommand(id), ct)).ToNoContent())
            .WithName("DeletePresupuesto")
            .WithTags(PresupuestosModulo.Tag)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(Modulos.Presupuestos, Accion.Eliminar);
}
