using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Common.Validation;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Mantenimiento.Clasificaciones;

public sealed record CreateClasificacionCommand(CreateClasificacionRequest Datos) : ICommand<ClasificacionResponse>;

internal sealed class CreateClasificacionHandler(AppDbContext db, TimeProvider timeProvider) : ICommandHandler<CreateClasificacionCommand, ClasificacionResponse>
{
    public async Task<Result<ClasificacionResponse>> HandleAsync(CreateClasificacionCommand command, CancellationToken cancellationToken)
    {
        var nombre = Clasificacion.Normalize(command.Datos.Nombre);
        if (await db.Set<Clasificacion>().AnyAsync(c => c.Nombre == nombre, cancellationToken))
        {
            return ClasificacionesApi.NombreDuplicado(nombre);
        }

        var clasificacion = Clasificacion.Create(nombre, timeProvider.GetUtcNow());
        db.Add(clasificacion);
        await db.SaveChangesAsync(cancellationToken);
        return new ClasificacionResponse(clasificacion.Id, clasificacion.Nombre, clasificacion.Activo, 0);
    }
}

internal sealed class CreateClasificacionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(ClasificacionesApi.Route, async (CreateClasificacionRequest datos, ICommandHandler<CreateClasificacionCommand, ClasificacionResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new CreateClasificacionCommand(datos), ct)).ToHttp(c => Results.Created($"{ClasificacionesApi.Route}/{c.Id}", c)))
            .WithName("CreateClasificacion")
            .WithTags(MantenimientoApi.Tag)
            .WithRequestValidation<CreateClasificacionRequest>()
            .Produces<ClasificacionResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(Modulos.Clasificaciones, Accion.Insertar);
}
