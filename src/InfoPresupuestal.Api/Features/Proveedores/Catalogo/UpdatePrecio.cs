using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Common.Validation;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Proveedores.Catalogo;

/// <summary>Corrige un precio mal digitado. Un cambio de precio real se registra como un precio nuevo.</summary>
public sealed record UpdatePrecioCommand(Guid ProductoId, long Id, PrecioProveedorRequest Datos) : ICommand<PrecioProveedorResponse>;

internal sealed class UpdatePrecioHandler(AppDbContext db) : ICommandHandler<UpdatePrecioCommand, PrecioProveedorResponse>
{
    public async Task<Result<PrecioProveedorResponse>> HandleAsync(UpdatePrecioCommand command, CancellationToken cancellationToken)
    {
        var d = command.Datos;
        var precio = await db.Set<ProveedorPrecio>().FirstOrDefaultAsync(p => p.Id == command.Id && p.ProductoId == command.ProductoId, cancellationToken);
        if (precio is null)
        {
            return CatalogoApi.PrecioNotFound(command.Id);
        }

        var ciudad = ProveedorPrecio.NormalizeCiudad(d.Ciudad);
        if (await PreciosApi.ExisteAsync(db, precio.ProductoId, precio.Id, ciudad, d.FechaVigencia, cancellationToken))
        {
            return CatalogoApi.PrecioDuplicado(ciudad, d.FechaVigencia);
        }

        precio.Update(ciudad, d.Valor, d.IncluyeIva, d.IvaPct, d.Moneda, d.FechaVigencia);
        await db.SaveChangesAsync(cancellationToken);
        return precio.AsResponse();
    }
}

internal sealed class UpdatePrecioEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut($"{CatalogoApi.PreciosRoute}/{{id:long}}", async (Guid productoId, long id, PrecioProveedorRequest datos, ICommandHandler<UpdatePrecioCommand, PrecioProveedorResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new UpdatePrecioCommand(productoId, id, datos), ct)).ToOk())
            .WithName("UpdatePrecioProveedor")
            .WithTags(ProveedoresApi.Tag)
            .WithRequestValidation<PrecioProveedorRequest>()
            .Produces<PrecioProveedorResponse>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireTipoEmpresa(TipoEmpresa.Proveedor)
            .RequirePermission(Modulos.CatalogoProveedor, Accion.Editar);
}
