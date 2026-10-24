using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Common.Validation;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Mantenimiento.UnidadesMedida;

public sealed record UpdateUnidadMedidaCommand(short Id, UnidadMedidaRequest Datos) : ICommand<UnidadMedidaResponse>;

internal sealed class UpdateUnidadMedidaHandler(AppDbContext db) : ICommandHandler<UpdateUnidadMedidaCommand, UnidadMedidaResponse>
{
    public async Task<Result<UnidadMedidaResponse>> HandleAsync(UpdateUnidadMedidaCommand command, CancellationToken cancellationToken)
    {
        var unidad = await db.Set<UnidadMedida>().FirstOrDefaultAsync(u => u.Id == command.Id, cancellationToken);
        if (unidad is null)
        {
            return UnidadesMedidaApi.NotFound(command.Id);
        }

        var codigo = UnidadMedida.NormalizeCodigo(command.Datos.Codigo);
        if (await db.Set<UnidadMedida>().AnyAsync(u => u.Id != unidad.Id && u.Codigo == codigo, cancellationToken))
        {
            return UnidadesMedidaApi.CodigoDuplicado(codigo);
        }

        unidad.Update(codigo, command.Datos.Nombre);
        await db.SaveChangesAsync(cancellationToken);
        return unidad.AsResponse();
    }
}

internal sealed class UpdateUnidadMedidaEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut($"{UnidadesMedidaApi.Route}/{{id:int}}", async (short id, UnidadMedidaRequest datos, ICommandHandler<UpdateUnidadMedidaCommand, UnidadMedidaResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new UpdateUnidadMedidaCommand(id, datos), ct)).ToOk())
            .WithName("UpdateUnidadMedida")
            .WithTags(MantenimientoApi.Tag)
            .WithRequestValidation<UnidadMedidaRequest>()
            .Produces<UnidadMedidaResponse>()
            .RequirePermission(Modulos.UnidadesMedida, Accion.Editar);
}
