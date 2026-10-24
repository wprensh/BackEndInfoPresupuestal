using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Features.Mantenimiento.Insumos;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Mantenimiento.TiposInsumo;

public sealed record DeleteTipoInsumoCommand(short Id) : ICommand<Done>;

internal sealed class DeleteTipoInsumoHandler(AppDbContext db) : ICommandHandler<DeleteTipoInsumoCommand, Done>
{
    public async Task<Result<Done>> HandleAsync(DeleteTipoInsumoCommand command, CancellationToken cancellationToken)
    {
        var tipo = await db.Set<TipoInsumo>().FirstOrDefaultAsync(t => t.Id == command.Id, cancellationToken);
        if (tipo is null)
        {
            return TiposInsumoApi.NotFound(command.Id);
        }

        // Catálogo global: se revisa el uso en todas las empresas, no solo en la actual.
        if (await db.Set<Insumo>().IgnoreQueryFilters().AnyAsync(i => i.TipoInsumoId == tipo.Id, cancellationToken))
        {
            return Error.Conflict("TiposInsumo.EnUso", $"El tipo '{tipo.Codigo}' lo usan insumos y no se puede eliminar.");
        }

        db.Remove(tipo);
        await db.SaveChangesAsync(cancellationToken);
        return new Done();
    }
}

internal sealed class DeleteTipoInsumoEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete($"{TiposInsumoApi.Route}/{{id:int}}", async (short id, ICommandHandler<DeleteTipoInsumoCommand, Done> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new DeleteTipoInsumoCommand(id), ct)).ToNoContent())
            .WithName("DeleteTipoInsumo")
            .WithTags(MantenimientoApi.Tag)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(Modulos.TiposInsumo, Accion.Eliminar);
}
