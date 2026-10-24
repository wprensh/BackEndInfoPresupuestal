using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Common.Validation;

namespace InfoPresupuestal.Api.Features.Presupuestos.Obras;

public sealed record CreatePresupuestoCommand(PresupuestoRequest Datos) : ICommand<PresupuestoResponse>;

internal sealed class CreatePresupuestoHandler(AppDbContext db, TimeProvider timeProvider) : ICommandHandler<CreatePresupuestoCommand, PresupuestoResponse>
{
    public async Task<Result<PresupuestoResponse>> HandleAsync(CreatePresupuestoCommand command, CancellationToken cancellationToken)
    {
        var capitulos = await PresupuestosApi.ConstruirCapitulosAsync(db, command.Datos.Capitulos, cancellationToken);
        if (!capitulos.IsSuccess)
        {
            return capitulos.Error!;
        }

        var now = timeProvider.GetUtcNow();
        var presupuesto = Presupuesto.Create(
            await PresupuestosApi.SiguienteCodigoAsync(db, cancellationToken),
            PresupuestosApi.Encabezado(command.Datos, DateOnly.FromDateTime(now.UtcDateTime)),
            capitulos.Value,
            now);
        db.Add(presupuesto);
        await db.SaveChangesAsync(cancellationToken);

        return (await PresupuestosApi.LeerAsync(db, presupuesto.Id, cancellationToken))!;
    }
}

internal sealed class CreatePresupuestoEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(PresupuestosApi.Route, async (PresupuestoRequest datos, ICommandHandler<CreatePresupuestoCommand, PresupuestoResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new CreatePresupuestoCommand(datos), ct)).ToHttp(p => Results.Created($"{PresupuestosApi.Route}/{p.Id}", p)))
            .WithName("CreatePresupuesto")
            .WithTags(PresupuestosModulo.Tag)
            .WithRequestValidation<PresupuestoRequest>()
            .Produces<PresupuestoResponse>(StatusCodes.Status201Created)
            .RequirePermission(Modulos.Presupuestos, Accion.Insertar);
}
