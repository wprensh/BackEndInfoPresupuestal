using FluentValidation;
using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Features.Mantenimiento.TiposInsumo;
using InfoPresupuestal.Api.Features.Mantenimiento.UnidadesMedida;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Proveedores.Catalogo;

internal static class CatalogoApi
{
    public const string PreciosRoute = $"{ProveedoresApi.CatalogoRoute}/{{productoId:guid}}/precios";

    public static string PreciosRouteFor(Guid productoId) => $"{ProveedoresApi.CatalogoRoute}/{productoId}/precios";

    public static Error NotFound(Guid id) => Error.NotFound("Catalogo.NotFound", $"No existe el producto '{id}'.");

    public static Error PrecioNotFound(long id) => Error.NotFound("Catalogo.PrecioNotFound", $"No existe el precio {id} del producto.");

    public static Error CodigoDuplicado(string codigo) => Error.Conflict("Catalogo.CodigoDuplicado", $"Ya tienes un producto con el código '{codigo}'.");

    public static Error PrecioDuplicado(string? ciudad, DateOnly fecha) =>
        Error.Conflict("Catalogo.PrecioDuplicado", $"El producto ya tiene un precio para {ciudad ?? "todo el país"} con vigencia {fecha:yyyy-MM-dd}.");

    public static async Task<Error?> ValidarCatalogosAsync(AppDbContext db, short tipoInsumoId, short unidadMedidaId, CancellationToken cancellationToken)
    {
        if (!await db.Set<TipoInsumo>().AnyAsync(t => t.Id == tipoInsumoId, cancellationToken))
        {
            return Error.Validation("Catalogo.TipoInvalido", $"No existe el tipo de insumo {tipoInsumoId}.");
        }

        return await db.Set<UnidadMedida>().AnyAsync(u => u.Id == unidadMedidaId, cancellationToken)
            ? null
            : Error.Validation("Catalogo.UnidadInvalida", $"No existe la unidad de medida {unidadMedidaId}.");
    }

    public static Task<bool> ExisteCodigoAsync(AppDbContext db, Guid? excluirId, string codigo, CancellationToken cancellationToken)
    {
        var normalizado = ProveedorProducto.NormalizeCodigo(codigo);
        return db.Set<ProveedorProducto>().AnyAsync(p => p.Id != excluirId && p.Codigo == normalizado, cancellationToken);
    }

    /// <summary>
    /// Último precio de cada producto en cada ciudad. <paramref name="precios"/> ya viene filtrado por tenant;
    /// la agrupación se hace en memoria porque son pocos productos por página.
    /// </summary>
    public static async Task<Dictionary<Guid, List<PrecioProveedorResponse>>> PreciosVigentesAsync(
        IQueryable<ProveedorPrecio> precios, IReadOnlyCollection<Guid> productoIds, CancellationToken cancellationToken)
    {
        var filas = await precios.AsNoTracking().Where(p => productoIds.Contains(p.ProductoId)).ToListAsync(cancellationToken);
        return filas
            .GroupBy(p => p.ProductoId)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(p => p.Ciudad)
                    .Select(c => c.MaxBy(p => p.FechaVigencia)!)
                    .OrderBy(p => p.Ciudad is not null).ThenBy(p => p.Ciudad, StringComparer.Ordinal)
                    .Select(p => p.AsResponse())
                    .ToList());
    }

    public static PrecioProveedorResponse AsResponse(this ProveedorPrecio p) =>
        new(p.Id, p.ProductoId, p.Ciudad, p.Valor, p.IncluyeIva, p.IvaPct, p.Moneda, p.FechaVigencia, p.ValorSinIva, p.ValorConIva);
}

public sealed record PrecioProveedorResponse(
    long Id,
    Guid ProductoId,
    string? Ciudad,
    decimal Valor,
    bool IncluyeIva,
    decimal IvaPct,
    string Moneda,
    DateOnly FechaVigencia,
    decimal ValorSinIva,
    decimal ValorConIva);

public sealed record ProductoProveedorResponse(
    Guid Id,
    string Codigo,
    string Descripcion,
    short TipoInsumoId,
    string Tipo,
    short UnidadMedidaId,
    string Unidad,
    string? Marca,
    string? Categoria,
    bool Activo,
    DateTimeOffset ActualizadoEn,
    IReadOnlyList<PrecioProveedorResponse> Precios);

/// <summary>Campos de un producto, iguales para crear y editar (en editar también <c>Activo</c>).</summary>
public sealed record ProductoProveedorRequest(
    string Codigo,
    string Descripcion,
    short TipoInsumoId,
    short UnidadMedidaId,
    string? Marca = null,
    string? Categoria = null,
    bool Activo = true);

/// <param name="Ciudad">Vacía = el precio aplica a todo el país.</param>
/// <param name="IvaPct">Fracción: 0.19 = 19 %.</param>
public sealed record PrecioProveedorRequest(
    decimal Valor,
    DateOnly FechaVigencia,
    string? Ciudad = null,
    bool IncluyeIva = false,
    decimal IvaPct = ProveedorPrecio.IvaGeneral,
    string? Moneda = null);

internal sealed class ProductoProveedorRequestValidator : AbstractValidator<ProductoProveedorRequest>
{
    public ProductoProveedorRequestValidator()
    {
        RuleFor(r => r.Codigo).NotEmpty().MaximumLength(ProveedorProductoConfiguration.CodigoMaxLength);
        RuleFor(r => r.Descripcion).NotEmpty().MaximumLength(ProveedorProductoConfiguration.DescripcionMaxLength);
        RuleFor(r => r.TipoInsumoId).GreaterThan((short)0);
        RuleFor(r => r.UnidadMedidaId).GreaterThan((short)0);
        RuleFor(r => r.Marca).MaximumLength(ProveedorProductoConfiguration.TextoCortoMaxLength);
        RuleFor(r => r.Categoria).MaximumLength(ProveedorProductoConfiguration.TextoCortoMaxLength);
    }
}

internal sealed class PrecioProveedorRequestValidator : AbstractValidator<PrecioProveedorRequest>
{
    public PrecioProveedorRequestValidator()
    {
        RuleFor(r => r.Valor).GreaterThan(0).LessThan(10_000_000_000_000_000m).PrecisionScale(18, 2, ignoreTrailingZeros: true);
        RuleFor(r => r.FechaVigencia).NotEmpty();
        RuleFor(r => r.Ciudad).MaximumLength(ProveedorPrecioConfiguration.CiudadMaxLength);
        RuleFor(r => r.IvaPct).InclusiveBetween(0m, 1m).WithMessage("El IVA es una fracción entre 0 y 1 (0.19 = 19 %).");
        RuleFor(r => r.Moneda).Matches("^[A-Za-z]{3}$").When(r => !string.IsNullOrWhiteSpace(r.Moneda)).WithMessage("La moneda es un código ISO de 3 letras (COP).");
    }
}
