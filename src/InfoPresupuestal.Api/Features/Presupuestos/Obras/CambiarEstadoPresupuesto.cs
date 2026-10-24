using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Common.Validation;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Presupuestos.Obras;

public sealed record CambiarEstadoPresupuestoCommand(Guid Id, string Estado) : ICommand<PresupuestoResponse>;

/// <summary>Aprobar o anular congela el presupuesto; volver a borrador lo deja editar otra vez.</summary>
internal sealed class CambiarEstadoPresupuestoHandler(AppDbContext db, TimeProvider timeProvider) : ICommandHandler<CambiarEstadoPresupuestoCommand, PresupuestoResponse>
{
    public async Task<Result<PresupuestoResponse>> HandleAsync(CambiarEstadoPresupuestoCommand command, CancellationToken cancellationToken)
    {
        var presupuesto = await db.Set<Presupuesto>().FirstOrDefaultAsync(p => p.Id == command.Id, cancellationToken);
        if (presupuesto is null)
        {
            return PresupuestosApi.NotFound(command.Id);
        }

        if (command.Estado == EstadoPresupuesto.Aprobado
            && !await db.Set<PresupuestoCapitulo>().AnyAsync(c => c.PresupuestoId == presupuesto.Id && c.Items.Any(), cancellationToken))
        {
            return Error.Validation("Presupuestos.Vacio", "No se puede aprobar un presupuesto sin ítems.");
        }

        presupuesto.CambiarEstado(command.Estado, timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return (await PresupuestosApi.LeerAsync(db, presupuesto.Id, cancellationToken))!;
    }
}

internal sealed class CambiarEstadoPresupuestoEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut($"{PresupuestosApi.Route}/{{id:guid}}/estado", async (Guid id, CambiarEstadoRequest datos, ICommandHandler<CambiarEstadoPresupuestoCommand, PresupuestoResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new CambiarEstadoPresupuestoCommand(id, datos.Estado), ct)).ToOk())
            .WithName("CambiarEstadoPresupuesto")
            .WithTags(PresupuestosModulo.Tag)
            .WithRequestValidation<CambiarEstadoRequest>()
            .Produces<PresupuestoResponse>()
            .RequirePermission(Modulos.Presupuestos, Accion.Editar);
}
