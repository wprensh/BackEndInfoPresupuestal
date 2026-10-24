using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Common.Validation;
using InfoPresupuestal.Api.Features.Mantenimiento.TiposInsumo;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Mantenimiento.Insumos;

public sealed record CreateInsumoCommand(CreateInsumoRequest Datos) : ICommand<InsumoResponse>;

internal sealed class CreateInsumoHandler(AppDbContext db, TimeProvider timeProvider) : ICommandHandler<CreateInsumoCommand, InsumoResponse>
{
    public async Task<Result<InsumoResponse>> HandleAsync(CreateInsumoCommand command, CancellationToken cancellationToken)
    {
        var datos = command.Datos;
        var tipo = await db.Set<TipoInsumo>().AsNoTracking().FirstOrDefaultAsync(t => t.Id == datos.TipoInsumoId, cancellationToken);
        if (tipo is null)
        {
            return Error.Validation("Insumos.TipoInvalido", $"No existe el tipo de insumo {datos.TipoInsumoId}.");
        }

        if (await InsumosApi.ValidarCatalogosAsync(db, datos.UnidadMedidaId, datos.ClasificacionId, cancellationToken) is { } error)
        {
            return error;
        }

        var descripcion = Insumo.NormalizeDescripcion(datos.Descripcion);
        if (await InsumosApi.ExisteDuplicadoAsync(db, null, tipo.Id, descripcion, datos.UnidadMedidaId, cancellationToken))
        {
            return InsumosApi.Duplicado();
        }

        // El mayor código del tipo en la empresa; ordenar por largo primero evita que MAT-999 quede después de MAT-1000.
        var prefijo = $"{tipo.Prefijo}-";
        var ultimo = await db.Set<Insumo>()
            .Where(i => i.Codigo.StartsWith(prefijo))
            .OrderByDescending(i => i.Codigo.Length).ThenByDescending(i => i.Codigo)
            .Select(i => i.Codigo)
            .FirstOrDefaultAsync(cancellationToken);

        var insumo = Insumo.Create(Insumo.SiguienteCodigo(tipo.Prefijo, ultimo), tipo.Id, datos.ClasificacionId, datos.UnidadMedidaId, descripcion, timeProvider.GetUtcNow());
        db.Add(insumo);
        await db.SaveChangesAsync(cancellationToken);

        return await db.Set<Insumo>().AsNoTracking().Where(i => i.Id == insumo.Id).Select(InsumoMapping.ToResponse(db)).FirstAsync(cancellationToken);
    }
}

internal sealed class CreateInsumoEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(InsumosApi.Route, async (CreateInsumoRequest datos, ICommandHandler<CreateInsumoCommand, InsumoResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new CreateInsumoCommand(datos), ct)).ToHttp(i => Results.Created($"{InsumosApi.Route}/{i.Id}", i)))
            .WithName("CreateInsumo")
            .WithTags(MantenimientoApi.Tag)
            .WithRequestValidation<CreateInsumoRequest>()
            .Produces<InsumoResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(Modulos.Insumos, Accion.Insertar);
}
