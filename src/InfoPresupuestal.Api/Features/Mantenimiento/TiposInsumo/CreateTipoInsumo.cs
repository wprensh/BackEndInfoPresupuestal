using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Common.Validation;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Mantenimiento.TiposInsumo;

public sealed record CreateTipoInsumoCommand(CreateTipoInsumoRequest Datos) : ICommand<TipoInsumoResponse>;

internal sealed class CreateTipoInsumoHandler(AppDbContext db) : ICommandHandler<CreateTipoInsumoCommand, TipoInsumoResponse>
{
    public async Task<Result<TipoInsumoResponse>> HandleAsync(CreateTipoInsumoCommand command, CancellationToken cancellationToken)
    {
        var codigo = TipoInsumo.Normalize(command.Datos.Codigo);
        var nombre = command.Datos.Nombre.Trim();
        var prefijo = TipoInsumo.Normalize(command.Datos.Prefijo);
        if (await db.Set<TipoInsumo>().AnyAsync(t => t.Codigo == codigo || t.Nombre == nombre || t.Prefijo == prefijo, cancellationToken))
        {
            return TiposInsumoApi.Duplicado();
        }

        var maxId = await db.Set<TipoInsumo>().MaxAsync(t => (short?)t.Id, cancellationToken) ?? 0;
        var tipo = TipoInsumo.Create((short)(maxId + 1), codigo, nombre, prefijo);
        db.Add(tipo);
        await db.SaveChangesAsync(cancellationToken);
        return tipo.AsResponse();
    }
}

internal sealed class CreateTipoInsumoEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(TiposInsumoApi.Route, async (CreateTipoInsumoRequest datos, ICommandHandler<CreateTipoInsumoCommand, TipoInsumoResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new CreateTipoInsumoCommand(datos), ct)).ToHttp(t => Results.Created($"{TiposInsumoApi.Route}/{t.Id}", t)))
            .WithName("CreateTipoInsumo")
            .WithTags(MantenimientoApi.Tag)
            .WithRequestValidation<CreateTipoInsumoRequest>()
            .Produces<TipoInsumoResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(Modulos.TiposInsumo, Accion.Insertar);
}
