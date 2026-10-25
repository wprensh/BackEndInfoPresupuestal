using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Proveedores.Catalogo;

public sealed record DeletePrecioCommand(Guid ProductoId, long Id) : ICommand<Done>;

internal sealed class DeletePrecioHandler(AppDbContext db) : ICommandHandler<DeletePrecioCommand, Done>
{
    public async Task<Result<Done>> HandleAsync(DeletePrecioCommand command, CancellationToken cancellationToken)
    {
        var precio = await db.Set<ProveedorPrecio>().FirstOrDefaultAsync(p => p.Id == command.Id && p.ProductoId == command.ProductoId, cancellationToken);
        if (precio is null)
        {
            return CatalogoApi.PrecioNotFound(command.Id);
        }

        db.Remove(precio);
        await db.SaveChangesAsync(cancellationToken);
        return new Done();
    }
}

internal sealed class DeletePrecioEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete($"{CatalogoApi.PreciosRoute}/{{id:long}}", async (Guid productoId, long id, ICommandHandler<DeletePrecioCommand, Done> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new DeletePrecioCommand(productoId, id), ct)).ToNoContent())
            .WithName("DeletePrecioProveedor")
            .WithTags(ProveedoresApi.Tag)
            .Produces(StatusCodes.Status204NoContent)
            .RequireTipoEmpresa(TipoEmpresa.Proveedor)
            .RequirePermission(Modulos.CatalogoProveedor, Accion.Eliminar);
}
