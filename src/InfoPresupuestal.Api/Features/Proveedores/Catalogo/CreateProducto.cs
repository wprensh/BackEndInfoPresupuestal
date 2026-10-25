using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Common.Validation;

namespace InfoPresupuestal.Api.Features.Proveedores.Catalogo;

public sealed record CreateProductoCommand(ProductoProveedorRequest Datos) : ICommand<ProductoProveedorResponse>;

internal sealed class CreateProductoHandler(AppDbContext db, TimeProvider timeProvider) : ICommandHandler<CreateProductoCommand, ProductoProveedorResponse>
{
    public async Task<Result<ProductoProveedorResponse>> HandleAsync(CreateProductoCommand command, CancellationToken cancellationToken)
    {
        var d = command.Datos;
        if (await CatalogoApi.ValidarCatalogosAsync(db, d.TipoInsumoId, d.UnidadMedidaId, cancellationToken) is { } error)
        {
            return error;
        }

        if (await CatalogoApi.ExisteCodigoAsync(db, null, d.Codigo, cancellationToken))
        {
            return CatalogoApi.CodigoDuplicado(ProveedorProducto.NormalizeCodigo(d.Codigo));
        }

        var producto = ProveedorProducto.Create(d.Codigo, d.Descripcion, d.TipoInsumoId, d.UnidadMedidaId, d.Marca, d.Categoria, timeProvider.GetUtcNow());
        db.Add(producto);
        await db.SaveChangesAsync(cancellationToken);
        return (await GetProductoHandler.CargarAsync(db, producto.Id, cancellationToken))!;
    }
}

internal sealed class CreateProductoEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(ProveedoresApi.CatalogoRoute, async (ProductoProveedorRequest datos, ICommandHandler<CreateProductoCommand, ProductoProveedorResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new CreateProductoCommand(datos), ct)).ToHttp(p => Results.Created($"{ProveedoresApi.CatalogoRoute}/{p.Id}", p)))
            .WithName("CreateProductoProveedor")
            .WithTags(ProveedoresApi.Tag)
            .WithRequestValidation<ProductoProveedorRequest>()
            .Produces<ProductoProveedorResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireTipoEmpresa(TipoEmpresa.Proveedor)
            .RequirePermission(Modulos.CatalogoProveedor, Accion.Insertar);
}
