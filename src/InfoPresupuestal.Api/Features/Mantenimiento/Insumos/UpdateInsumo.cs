using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Common.Validation;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Mantenimiento.Insumos;

public sealed record UpdateInsumoCommand(Guid Id, UpdateInsumoRequest Datos) : ICommand<InsumoResponse>;

/// <summary>El tipo no se cambia: el código del insumo depende de él.</summary>
internal sealed class UpdateInsumoHandler(AppDbContext db, TimeProvider timeProvider) : ICommandHandler<UpdateInsumoCommand, InsumoResponse>
{
    public async Task<Result<InsumoResponse>> HandleAsync(UpdateInsumoCommand command, CancellationToken cancellationToken)
    {
        var datos = command.Datos;
        var insumo = await db.Set<Insumo>().FirstOrDefaultAsync(i => i.Id == command.Id, cancellationToken);
        if (insumo is null)
        {
            return InsumosApi.NotFound(command.Id);
        }

        if (await InsumosApi.ValidarCatalogosAsync(db, datos.UnidadMedidaId, datos.ClasificacionId, cancellationToken) is { } error)
        {
            return error;
        }

        var descripcion = Insumo.NormalizeDescripcion(datos.Descripcion);
        if (await InsumosApi.ExisteDuplicadoAsync(db, insumo.Id, insumo.TipoInsumoId, descripcion, datos.UnidadMedidaId, cancellationToken))
        {
            return InsumosApi.Duplicado();
        }

        insumo.Update(datos.ClasificacionId, datos.UnidadMedidaId, descripcion, datos.Activo, timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        return await db.Set<Insumo>().AsNoTracking().Where(i => i.Id == insumo.Id).Select(InsumoMapping.ToResponse(db)).FirstAsync(cancellationToken);
    }
}

internal sealed class UpdateInsumoEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut($"{InsumosApi.Route}/{{id:guid}}", async (Guid id, UpdateInsumoRequest datos, ICommandHandler<UpdateInsumoCommand, InsumoResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new UpdateInsumoCommand(id, datos), ct)).ToOk())
            .WithName("UpdateInsumo")
            .WithTags(MantenimientoApi.Tag)
            .WithRequestValidation<UpdateInsumoRequest>()
            .Produces<InsumoResponse>()
            .RequirePermission(Modulos.Insumos, Accion.Editar);
}
