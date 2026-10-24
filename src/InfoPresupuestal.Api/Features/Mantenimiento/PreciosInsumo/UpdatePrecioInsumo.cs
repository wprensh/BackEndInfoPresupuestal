using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Common.Validation;
using InfoPresupuestal.Api.Features.Mantenimiento.Insumos;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Mantenimiento.PreciosInsumo;

/// <summary>Corrige un precio mal digitado. Un cambio de precio real se registra como un precio nuevo.</summary>
public sealed record UpdatePrecioInsumoCommand(Guid InsumoId, long Id, PrecioInsumoRequest Datos) : ICommand<PrecioInsumoResponse>;

internal sealed class UpdatePrecioInsumoHandler(AppDbContext db) : ICommandHandler<UpdatePrecioInsumoCommand, PrecioInsumoResponse>
{
    public async Task<Result<PrecioInsumoResponse>> HandleAsync(UpdatePrecioInsumoCommand command, CancellationToken cancellationToken)
    {
        var datos = command.Datos;
        var precio = await db.Set<InsumoPrecio>().FirstOrDefaultAsync(p => p.Id == command.Id && p.InsumoId == command.InsumoId, cancellationToken);
        if (precio is null)
        {
            return PreciosInsumoApi.NotFound(command.Id);
        }

        if (await db.Set<InsumoPrecio>().AnyAsync(p => p.Id != precio.Id && p.InsumoId == precio.InsumoId && p.FechaVigencia == datos.FechaVigencia, cancellationToken))
        {
            return PreciosInsumoApi.FechaDuplicada(datos.FechaVigencia);
        }

        precio.Update(datos.ValorUnitario, datos.Moneda, datos.FechaVigencia, datos.Fuente);
        await db.SaveChangesAsync(cancellationToken);
        return precio.AsResponse();
    }
}

internal sealed class UpdatePrecioInsumoEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut($"{PreciosInsumoApi.Route}/{{id:long}}", async (Guid insumoId, long id, PrecioInsumoRequest datos, ICommandHandler<UpdatePrecioInsumoCommand, PrecioInsumoResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new UpdatePrecioInsumoCommand(insumoId, id, datos), ct)).ToOk())
            .WithName("UpdatePrecioInsumo")
            .WithTags(MantenimientoApi.Tag)
            .WithRequestValidation<PrecioInsumoRequest>()
            .Produces<PrecioInsumoResponse>()
            .RequirePermission(Modulos.PreciosInsumo, Accion.Editar);
}
