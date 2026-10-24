using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Common.Validation;
using InfoPresupuestal.Api.Features.Mantenimiento.Insumos;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Mantenimiento.PreciosInsumo;

public sealed record CreatePrecioInsumoCommand(Guid InsumoId, PrecioInsumoRequest Datos) : ICommand<PrecioInsumoResponse>;

internal sealed class CreatePrecioInsumoHandler(AppDbContext db, TimeProvider timeProvider) : ICommandHandler<CreatePrecioInsumoCommand, PrecioInsumoResponse>
{
    public async Task<Result<PrecioInsumoResponse>> HandleAsync(CreatePrecioInsumoCommand command, CancellationToken cancellationToken)
    {
        var datos = command.Datos;
        if (!await db.Set<Insumo>().AnyAsync(i => i.Id == command.InsumoId, cancellationToken))
        {
            return InsumosApi.NotFound(command.InsumoId);
        }

        if (await db.Set<InsumoPrecio>().AnyAsync(p => p.InsumoId == command.InsumoId && p.FechaVigencia == datos.FechaVigencia, cancellationToken))
        {
            return PreciosInsumoApi.FechaDuplicada(datos.FechaVigencia);
        }

        var precio = InsumoPrecio.Create(command.InsumoId, datos.ValorUnitario, datos.Moneda, datos.FechaVigencia, datos.Fuente, timeProvider.GetUtcNow());
        db.Add(precio);
        await db.SaveChangesAsync(cancellationToken);
        return precio.AsResponse();
    }
}

internal sealed class CreatePrecioInsumoEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(PreciosInsumoApi.Route, async (Guid insumoId, PrecioInsumoRequest datos, ICommandHandler<CreatePrecioInsumoCommand, PrecioInsumoResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new CreatePrecioInsumoCommand(insumoId, datos), ct))
                    .ToHttp(p => Results.Created($"{PreciosInsumoApi.RouteFor(insumoId)}/{p.Id}", p)))
            .WithName("CreatePrecioInsumo")
            .WithTags(MantenimientoApi.Tag)
            .WithRequestValidation<PrecioInsumoRequest>()
            .Produces<PrecioInsumoResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(Modulos.PreciosInsumo, Accion.Insertar);
}
