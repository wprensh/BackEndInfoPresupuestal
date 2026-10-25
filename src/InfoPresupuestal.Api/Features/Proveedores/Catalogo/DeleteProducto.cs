using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Proveedores.Catalogo;

/// <summary>Borra el producto y su historial de precios. Para dejar de venderlo sin perder el historial, se desactiva.</summary>
public sealed record DeleteProductoCommand(Guid Id) : ICommand<Done>;

internal sealed class DeleteProductoHandler(AppDbContext db) : ICommandHandler<DeleteProductoCommand, Done>
{
    public async Task<Result<Done>> HandleAsync(DeleteProductoCommand command, CancellationToken cancellationToken)
    {
        var producto = await db.Set<ProveedorProducto>().FirstOrDefaultAsync(p => p.Id == command.Id, cancellationToken);
        if (producto is null)
        {
            return CatalogoApi.NotFound(command.Id);
        }

        db.Remove(producto);
        await db.SaveChangesAsync(cancellationToken);
        return new Done();
    }
}

internal sealed class DeleteProductoEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete($"{ProveedoresApi.CatalogoRoute}/{{id:guid}}", async (Guid id, ICommandHandler<DeleteProductoCommand, Done> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new DeleteProductoCommand(id), ct)).ToNoContent())
            .WithName("DeleteProductoProveedor")
            .WithTags(ProveedoresApi.Tag)
            .Produces(StatusCodes.Status204NoContent)
            .RequireTipoEmpresa(TipoEmpresa.Proveedor)
            .RequirePermission(Modulos.CatalogoProveedor, Accion.Eliminar);
}
