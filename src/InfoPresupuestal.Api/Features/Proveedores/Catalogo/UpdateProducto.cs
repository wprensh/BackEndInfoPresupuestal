using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Common.Validation;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Proveedores.Catalogo;

public sealed record UpdateProductoCommand(Guid Id, ProductoProveedorRequest Datos) : ICommand<ProductoProveedorResponse>;

internal sealed class UpdateProductoHandler(AppDbContext db, TimeProvider timeProvider) : ICommandHandler<UpdateProductoCommand, ProductoProveedorResponse>
{
    public async Task<Result<ProductoProveedorResponse>> HandleAsync(UpdateProductoCommand command, CancellationToken cancellationToken)
    {
        var d = command.Datos;
        var producto = await db.Set<ProveedorProducto>().FirstOrDefaultAsync(p => p.Id == command.Id, cancellationToken);
        if (producto is null)
        {
            return CatalogoApi.NotFound(command.Id);
        }

        if (await CatalogoApi.ValidarCatalogosAsync(db, d.TipoInsumoId, d.UnidadMedidaId, cancellationToken) is { } error)
        {
            return error;
        }

        if (await CatalogoApi.ExisteCodigoAsync(db, producto.Id, d.Codigo, cancellationToken))
        {
            return CatalogoApi.CodigoDuplicado(ProveedorProducto.NormalizeCodigo(d.Codigo));
        }

        producto.Update(d.Codigo, d.Descripcion, d.TipoInsumoId, d.UnidadMedidaId, d.Marca, d.Categoria, d.Activo, timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return (await GetProductoHandler.CargarAsync(db, producto.Id, cancellationToken))!;
    }
}

internal sealed class UpdateProductoEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut($"{ProveedoresApi.CatalogoRoute}/{{id:guid}}", async (Guid id, ProductoProveedorRequest datos, ICommandHandler<UpdateProductoCommand, ProductoProveedorResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new UpdateProductoCommand(id, datos), ct)).ToOk())
            .WithName("UpdateProductoProveedor")
            .WithTags(ProveedoresApi.Tag)
            .WithRequestValidation<ProductoProveedorRequest>()
            .Produces<ProductoProveedorResponse>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireTipoEmpresa(TipoEmpresa.Proveedor)
            .RequirePermission(Modulos.CatalogoProveedor, Accion.Editar);
}
