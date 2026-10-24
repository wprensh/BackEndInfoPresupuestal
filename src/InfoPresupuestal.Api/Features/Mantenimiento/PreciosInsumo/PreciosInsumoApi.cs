using FluentValidation;
using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Features.Mantenimiento.Insumos;

namespace InfoPresupuestal.Api.Features.Mantenimiento.PreciosInsumo;

internal static class PreciosInsumoApi
{
    public const string Route = $"{InsumosApi.Route}/{{insumoId:guid}}/precios";

    public static string RouteFor(Guid insumoId) => $"{InsumosApi.Route}/{insumoId}/precios";

    public static Error NotFound(long id) => Error.NotFound("PreciosInsumo.NotFound", $"No existe el precio {id} para el insumo.");

    public static Error FechaDuplicada(DateOnly fecha) =>
        Error.Conflict("PreciosInsumo.FechaDuplicada", $"El insumo ya tiene un precio con vigencia {fecha:yyyy-MM-dd}.");

    public static PrecioInsumoResponse AsResponse(this InsumoPrecio p) => new(p.Id, p.InsumoId, p.ValorUnitario, p.Moneda, p.FechaVigencia, p.Fuente);
}

public sealed record PrecioInsumoRequest(decimal ValorUnitario, DateOnly FechaVigencia, string? Moneda = null, string? Fuente = null);

public sealed record PrecioInsumoResponse(long Id, Guid InsumoId, decimal ValorUnitario, string Moneda, DateOnly FechaVigencia, string? Fuente);

internal sealed class PrecioInsumoRequestValidator : AbstractValidator<PrecioInsumoRequest>
{
    public PrecioInsumoRequestValidator()
    {
        RuleFor(r => r.ValorUnitario).GreaterThan(0).LessThan(10_000_000_000_000_000m).PrecisionScale(18, 2, ignoreTrailingZeros: true);
        RuleFor(r => r.FechaVigencia).NotEmpty();
        RuleFor(r => r.Moneda).Matches("^[A-Za-z]{3}$").When(r => !string.IsNullOrWhiteSpace(r.Moneda)).WithMessage("La moneda es un código ISO de 3 letras (COP).");
        RuleFor(r => r.Fuente).MaximumLength(InsumoPrecioConfiguration.FuenteMaxLength);
    }
}
