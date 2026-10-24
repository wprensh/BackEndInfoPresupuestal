using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Common.Validation;
using InfoPresupuestal.Api.Features.Mantenimiento.Insumos;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Presupuestos.Apus;

public sealed record UpdateApuCommand(Guid Id, ApuRequest Datos) : ICommand<ApuResponse>;

/// <summary>
/// Reemplaza el APU completo. Los presupuestos que ya lo usan conservan su valor unitario
/// hasta que se recalculen.
/// </summary>
internal sealed class UpdateApuHandler(AppDbContext db, TimeProvider timeProvider) : ICommandHandler<UpdateApuCommand, ApuResponse>
{
    public async Task<Result<ApuResponse>> HandleAsync(UpdateApuCommand command, CancellationToken cancellationToken)
    {
        var datos = command.Datos;
        var apu = await db.Set<Apu>().Include(a => a.Detalles).FirstOrDefaultAsync(a => a.Id == command.Id, cancellationToken);
        if (apu is null)
        {
            return ApusApi.NotFound(command.Id);
        }

        var descripcion = Insumo.NormalizeDescripcion(datos.Descripcion);
        if (await ApusApi.ExisteDuplicadoAsync(db, apu.Id, descripcion, datos.UnidadMedidaId, cancellationToken))
        {
            return ApusApi.Duplicado();
        }

        var detalles = await ApusApi.ConstruirDetallesAsync(db, datos, cancellationToken);
        if (!detalles.IsSuccess)
        {
            return detalles.Error!;
        }

        apu.Update(descripcion, datos.UnidadMedidaId, datos.Fecha ?? apu.Fecha, datos.Observaciones, datos.Activo ?? apu.Activo, detalles.Value!, timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        return (await ApusApi.LeerAsync(db, apu.Id, cancellationToken))!;
    }
}

internal sealed class UpdateApuEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut($"{ApusApi.Route}/{{id:guid}}", async (Guid id, ApuRequest datos, ICommandHandler<UpdateApuCommand, ApuResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new UpdateApuCommand(id, datos), ct)).ToOk())
            .WithName("UpdateApu")
            .WithTags(PresupuestosModulo.Tag)
            .WithRequestValidation<ApuRequest>()
            .Produces<ApuResponse>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(Modulos.Apu, Accion.Editar);
}
