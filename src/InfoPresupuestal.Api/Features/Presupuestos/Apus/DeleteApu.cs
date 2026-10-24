using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Features.Presupuestos.Obras;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Presupuestos.Apus;

public sealed record DeleteApuCommand(Guid Id) : ICommand<Done>;

/// <summary>Un APU usado en algún presupuesto no se borra (la base lo impide): se desactiva con PUT.</summary>
internal sealed class DeleteApuHandler(AppDbContext db) : ICommandHandler<DeleteApuCommand, Done>
{
    public async Task<Result<Done>> HandleAsync(DeleteApuCommand command, CancellationToken cancellationToken)
    {
        var apu = await db.Set<Apu>().Include(a => a.Detalles).FirstOrDefaultAsync(a => a.Id == command.Id, cancellationToken);
        if (apu is null)
        {
            return ApusApi.NotFound(command.Id);
        }

        var usos = await db.Set<PresupuestoItem>().CountAsync(i => i.ApuId == apu.Id, cancellationToken);
        if (usos > 0)
        {
            return Error.Conflict("Apus.EnUso", $"El APU se usa en {usos} ítem(s) de presupuestos. Desactívalo en lugar de eliminarlo.");
        }

        db.Remove(apu);
        await db.SaveChangesAsync(cancellationToken);
        return new Done();
    }
}

internal sealed class DeleteApuEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete($"{ApusApi.Route}/{{id:guid}}", async (Guid id, ICommandHandler<DeleteApuCommand, Done> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new DeleteApuCommand(id), ct)).ToNoContent())
            .WithName("DeleteApu")
            .WithTags(PresupuestosModulo.Tag)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(Modulos.Apu, Accion.Eliminar);
}
