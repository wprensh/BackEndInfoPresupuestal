using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Common.Validation;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Proveedores.Catalogo;

/// <summary>Agrega un precio al historial: un cambio de precio es una fila nueva con su fecha de vigencia.</summary>
public sealed record CreatePrecioCommand(Guid ProductoId, PrecioProveedorRequest Datos) : ICommand<PrecioProveedorResponse>;

internal sealed class CreatePrecioHandler(AppDbContext db, TimeProvider timeProvider) : ICommandHandler<CreatePrecioCommand, PrecioProveedorResponse>
{
    public async Task<Result<PrecioProveedorResponse>> HandleAsync(CreatePrecioCommand command, CancellationToken cancellationToken)
    {
        var d = command.Datos;
        if (!await db.Set<ProveedorProducto>().AnyAsync(p => p.Id == command.ProductoId, cancellationToken))
        {
            return CatalogoApi.NotFound(command.ProductoId);
        }

        var ciudad = ProveedorPrecio.NormalizeCiudad(d.Ciudad);
        if (await PreciosApi.ExisteAsync(db, command.ProductoId, null, ciudad, d.FechaVigencia, cancellationToken))
        {
            return CatalogoApi.PrecioDuplicado(ciudad, d.FechaVigencia);
        }

        var precio = ProveedorPrecio.Create(command.ProductoId, ciudad, d.Valor, d.IncluyeIva, d.IvaPct, d.Moneda, d.FechaVigencia, timeProvider.GetUtcNow());
        db.Add(precio);
        await db.SaveChangesAsync(cancellationToken);
        return precio.AsResponse();
    }
}

internal static class PreciosApi
{
    /// <summary>Misma regla que el índice único de la base: un precio por producto, ciudad y fecha.</summary>
    public static Task<bool> ExisteAsync(AppDbContext db, Guid productoId, long? excluirId, string? ciudad, DateOnly fecha, CancellationToken cancellationToken) =>
        db.Set<ProveedorPrecio>().AnyAsync(
            p => p.ProductoId == productoId && p.Id != excluirId && p.Ciudad == ciudad && p.FechaVigencia == fecha,
            cancellationToken);
}

internal sealed class CreatePrecioEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(CatalogoApi.PreciosRoute, async (Guid productoId, PrecioProveedorRequest datos, ICommandHandler<CreatePrecioCommand, PrecioProveedorResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new CreatePrecioCommand(productoId, datos), ct))
                    .ToHttp(p => Results.Created($"{CatalogoApi.PreciosRouteFor(productoId)}/{p.Id}", p)))
            .WithName("CreatePrecioProveedor")
            .WithTags(ProveedoresApi.Tag)
            .WithRequestValidation<PrecioProveedorRequest>()
            .Produces<PrecioProveedorResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireTipoEmpresa(TipoEmpresa.Proveedor)
            .RequirePermission(Modulos.CatalogoProveedor, Accion.Insertar);
}
