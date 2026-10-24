using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Common.Validation;
using InfoPresupuestal.Api.Features.Mantenimiento.Insumos;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Mantenimiento.Clasificaciones;

public sealed record UpdateClasificacionCommand(int Id, UpdateClasificacionRequest Datos) : ICommand<ClasificacionResponse>;

internal sealed class UpdateClasificacionHandler(AppDbContext db) : ICommandHandler<UpdateClasificacionCommand, ClasificacionResponse>
{
    public async Task<Result<ClasificacionResponse>> HandleAsync(UpdateClasificacionCommand command, CancellationToken cancellationToken)
    {
        var clasificacion = await db.Set<Clasificacion>().FirstOrDefaultAsync(c => c.Id == command.Id, cancellationToken);
        if (clasificacion is null)
        {
            return ClasificacionesApi.NotFound(command.Id);
        }

        var nombre = Clasificacion.Normalize(command.Datos.Nombre);
        if (await db.Set<Clasificacion>().AnyAsync(c => c.Id != clasificacion.Id && c.Nombre == nombre, cancellationToken))
        {
            return ClasificacionesApi.NombreDuplicado(nombre);
        }

        clasificacion.Update(nombre, command.Datos.Activo);
        await db.SaveChangesAsync(cancellationToken);
        var insumos = await db.Set<Insumo>().CountAsync(i => i.ClasificacionId == clasificacion.Id, cancellationToken);
        return new ClasificacionResponse(clasificacion.Id, clasificacion.Nombre, clasificacion.Activo, insumos);
    }
}

internal sealed class UpdateClasificacionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut($"{ClasificacionesApi.Route}/{{id:int}}", async (int id, UpdateClasificacionRequest datos, ICommandHandler<UpdateClasificacionCommand, ClasificacionResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new UpdateClasificacionCommand(id, datos), ct)).ToOk())
            .WithName("UpdateClasificacion")
            .WithTags(MantenimientoApi.Tag)
            .WithRequestValidation<UpdateClasificacionRequest>()
            .Produces<ClasificacionResponse>()
            .RequirePermission(Modulos.Clasificaciones, Accion.Editar);
}
