using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Common.Validation;
using InfoPresupuestal.Api.Features.Mantenimiento.Insumos;

namespace InfoPresupuestal.Api.Features.Presupuestos.Apus;

public sealed record CreateApuCommand(ApuRequest Datos) : ICommand<ApuResponse>;

internal sealed class CreateApuHandler(AppDbContext db, TimeProvider timeProvider) : ICommandHandler<CreateApuCommand, ApuResponse>
{
    public async Task<Result<ApuResponse>> HandleAsync(CreateApuCommand command, CancellationToken cancellationToken)
    {
        var datos = command.Datos;
        var descripcion = Insumo.NormalizeDescripcion(datos.Descripcion);
        if (await ApusApi.ExisteDuplicadoAsync(db, null, descripcion, datos.UnidadMedidaId, cancellationToken))
        {
            return ApusApi.Duplicado();
        }

        var detalles = await ApusApi.ConstruirDetallesAsync(db, datos, cancellationToken);
        if (!detalles.IsSuccess)
        {
            return detalles.Error!;
        }

        var now = timeProvider.GetUtcNow();
        var apu = Apu.Create(
            await ApusApi.SiguienteCodigoAsync(db, cancellationToken), descripcion, datos.UnidadMedidaId,
            datos.Fecha ?? DateOnly.FromDateTime(now.UtcDateTime), datos.Observaciones, detalles.Value!, now);
        db.Add(apu);
        await db.SaveChangesAsync(cancellationToken);

        return (await ApusApi.LeerAsync(db, apu.Id, cancellationToken))!;
    }
}

internal sealed class CreateApuEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(ApusApi.Route, async (ApuRequest datos, ICommandHandler<CreateApuCommand, ApuResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new CreateApuCommand(datos), ct)).ToHttp(a => Results.Created($"{ApusApi.Route}/{a.Id}", a)))
            .WithName("CreateApu")
            .WithTags(PresupuestosModulo.Tag)
            .WithRequestValidation<ApuRequest>()
            .Produces<ApuResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(Modulos.Apu, Accion.Insertar);
}
