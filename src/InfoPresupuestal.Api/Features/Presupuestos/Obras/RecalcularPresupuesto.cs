using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Features.Presupuestos.Apus;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Presupuestos.Obras;

public sealed record RecalcularPresupuestoCommand(Guid Id) : ICommand<PresupuestoResponse>;

/// <summary>Trae a los ítems el costo directo actual de sus APU (los ítems manuales no cambian).</summary>
internal sealed class RecalcularPresupuestoHandler(AppDbContext db, TimeProvider timeProvider) : ICommandHandler<RecalcularPresupuestoCommand, PresupuestoResponse>
{
    public async Task<Result<PresupuestoResponse>> HandleAsync(RecalcularPresupuestoCommand command, CancellationToken cancellationToken)
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

        var items = presupuesto.Capitulos.SelectMany(c => c.Items).Where(i => i.ApuId is not null).ToList();
        var costos = await ApusApi.CostosAsync(db, items.Select(i => i.ApuId!.Value).Distinct().ToList(), cancellationToken);
        foreach (var item in items)
        {
            item.ActualizarValorUnitario(costos.GetValueOrDefault(item.ApuId!.Value, ApuCostos.Cero).CostoDirecto);
        }

        presupuesto.Tocar(timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return (await PresupuestosApi.LeerAsync(db, presupuesto.Id, cancellationToken))!;
    }
}

internal sealed class RecalcularPresupuestoEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost($"{PresupuestosApi.Route}/{{id:guid}}/recalcular", async (Guid id, ICommandHandler<RecalcularPresupuestoCommand, PresupuestoResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new RecalcularPresupuestoCommand(id), ct)).ToOk())
            .WithName("RecalcularPresupuesto")
            .WithTags(PresupuestosModulo.Tag)
            .Produces<PresupuestoResponse>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(Modulos.Presupuestos, Accion.Editar);
}
