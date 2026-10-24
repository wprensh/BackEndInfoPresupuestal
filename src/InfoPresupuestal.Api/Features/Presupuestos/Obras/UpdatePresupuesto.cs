using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Common.Validation;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Presupuestos.Obras;

public sealed record UpdatePresupuestoCommand(Guid Id, PresupuestoRequest Datos) : ICommand<PresupuestoResponse>;

/// <summary>Reemplaza el presupuesto completo y renumera capítulos e ítems. Solo en borrador.</summary>
internal sealed class UpdatePresupuestoHandler(AppDbContext db, TimeProvider timeProvider) : ICommandHandler<UpdatePresupuestoCommand, PresupuestoResponse>
{
    public async Task<Result<PresupuestoResponse>> HandleAsync(UpdatePresupuestoCommand command, CancellationToken cancellationToken)
    {
        var presupuesto = await db.Set<Presupuesto>()
            .Include(p => p.Capitulos).ThenInclude(c => c.Items)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == command.Id, cancellationToken);
        if (presupuesto is null)
        {
            return PresupuestosApi.NotFound(command.Id);
        }

        if (!presupuesto.EsEditable)
        {
            return PresupuestosApi.NoEditable(presupuesto.Estado);
        }

        var capitulos = await PresupuestosApi.ConstruirCapitulosAsync(db, command.Datos.Capitulos, cancellationToken);
        if (!capitulos.IsSuccess)
        {
            return capitulos.Error!;
        }

        presupuesto.Update(PresupuestosApi.Encabezado(command.Datos, presupuesto.Fecha), capitulos.Value, timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        return (await PresupuestosApi.LeerAsync(db, presupuesto.Id, cancellationToken))!;
    }
}

internal sealed class UpdatePresupuestoEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut($"{PresupuestosApi.Route}/{{id:guid}}", async (Guid id, PresupuestoRequest datos, ICommandHandler<UpdatePresupuestoCommand, PresupuestoResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new UpdatePresupuestoCommand(id, datos), ct)).ToOk())
            .WithName("UpdatePresupuesto")
            .WithTags(PresupuestosModulo.Tag)
            .WithRequestValidation<PresupuestoRequest>()
            .Produces<PresupuestoResponse>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(Modulos.Presupuestos, Accion.Editar);
}
