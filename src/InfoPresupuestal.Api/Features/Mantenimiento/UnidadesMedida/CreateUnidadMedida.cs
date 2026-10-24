using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Common.Validation;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Mantenimiento.UnidadesMedida;

public sealed record CreateUnidadMedidaCommand(UnidadMedidaRequest Datos) : ICommand<UnidadMedidaResponse>;

internal sealed class CreateUnidadMedidaHandler(AppDbContext db) : ICommandHandler<CreateUnidadMedidaCommand, UnidadMedidaResponse>
{
    public async Task<Result<UnidadMedidaResponse>> HandleAsync(CreateUnidadMedidaCommand command, CancellationToken cancellationToken)
    {
        var codigo = UnidadMedida.NormalizeCodigo(command.Datos.Codigo);
        if (await db.Set<UnidadMedida>().AnyAsync(u => u.Codigo == codigo, cancellationToken))
        {
            return UnidadesMedidaApi.CodigoDuplicado(codigo);
        }

        var unidad = UnidadMedida.Create(codigo, command.Datos.Nombre);
        db.Add(unidad);
        await db.SaveChangesAsync(cancellationToken);
        return unidad.AsResponse();
    }
}

/// <summary>Catálogo global: solo el superadministrador puede insertar (los roles de empresa solo tienen Ver).</summary>
internal sealed class CreateUnidadMedidaEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(UnidadesMedidaApi.Route, async (UnidadMedidaRequest datos, ICommandHandler<CreateUnidadMedidaCommand, UnidadMedidaResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new CreateUnidadMedidaCommand(datos), ct)).ToHttp(u => Results.Created($"{UnidadesMedidaApi.Route}/{u.Id}", u)))
            .WithName("CreateUnidadMedida")
            .WithTags(MantenimientoApi.Tag)
            .WithRequestValidation<UnidadMedidaRequest>()
            .Produces<UnidadMedidaResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(Modulos.UnidadesMedida, Accion.Insertar);
}
