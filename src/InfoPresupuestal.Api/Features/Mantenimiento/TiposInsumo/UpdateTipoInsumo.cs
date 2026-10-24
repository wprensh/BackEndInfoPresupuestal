using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Common.Validation;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Mantenimiento.TiposInsumo;

public sealed record UpdateTipoInsumoCommand(short Id, UpdateTipoInsumoRequest Datos) : ICommand<TipoInsumoResponse>;

internal sealed class UpdateTipoInsumoHandler(AppDbContext db) : ICommandHandler<UpdateTipoInsumoCommand, TipoInsumoResponse>
{
    public async Task<Result<TipoInsumoResponse>> HandleAsync(UpdateTipoInsumoCommand command, CancellationToken cancellationToken)
    {
        var tipo = await db.Set<TipoInsumo>().FirstOrDefaultAsync(t => t.Id == command.Id, cancellationToken);
        if (tipo is null)
        {
            return TiposInsumoApi.NotFound(command.Id);
        }

        var codigo = TipoInsumo.Normalize(command.Datos.Codigo);
        var nombre = command.Datos.Nombre.Trim();
        if (await db.Set<TipoInsumo>().AnyAsync(t => t.Id != tipo.Id && (t.Codigo == codigo || t.Nombre == nombre), cancellationToken))
        {
            return TiposInsumoApi.Duplicado();
        }

        tipo.Update(codigo, nombre);
        await db.SaveChangesAsync(cancellationToken);
        return tipo.AsResponse();
    }
}

internal sealed class UpdateTipoInsumoEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut($"{TiposInsumoApi.Route}/{{id:int}}", async (short id, UpdateTipoInsumoRequest datos, ICommandHandler<UpdateTipoInsumoCommand, TipoInsumoResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new UpdateTipoInsumoCommand(id, datos), ct)).ToOk())
            .WithName("UpdateTipoInsumo")
            .WithTags(MantenimientoApi.Tag)
            .WithRequestValidation<UpdateTipoInsumoRequest>()
            .Produces<TipoInsumoResponse>()
            .RequirePermission(Modulos.TiposInsumo, Accion.Editar);
}
