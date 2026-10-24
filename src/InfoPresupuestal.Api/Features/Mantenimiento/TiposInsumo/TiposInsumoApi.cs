using FluentValidation;
using InfoPresupuestal.Api.Common.Abstractions;

namespace InfoPresupuestal.Api.Features.Mantenimiento.TiposInsumo;

internal static class TiposInsumoApi
{
    public const string Route = $"{MantenimientoApi.Route}/tipos-insumo";

    public static Error NotFound(short id) => Error.NotFound("TiposInsumo.NotFound", $"No existe el tipo de insumo {id}.");

    public static Error Duplicado() => Error.Conflict("TiposInsumo.Duplicado", "Ya existe un tipo de insumo con ese código, nombre o prefijo.");

    public static TipoInsumoResponse AsResponse(this TipoInsumo t) => new(t.Id, t.Codigo, t.Nombre, t.Prefijo);
}

public sealed record CreateTipoInsumoRequest(string Codigo, string Nombre, string Prefijo);

public sealed record UpdateTipoInsumoRequest(string Codigo, string Nombre);

public sealed record TipoInsumoResponse(short Id, string Codigo, string Nombre, string Prefijo);

internal sealed class CreateTipoInsumoRequestValidator : AbstractValidator<CreateTipoInsumoRequest>
{
    public CreateTipoInsumoRequestValidator()
    {
        RuleFor(r => r.Codigo).NotEmpty().MaximumLength(TipoInsumoConfiguration.CodigoMaxLength);
        RuleFor(r => r.Nombre).NotEmpty().MaximumLength(TipoInsumoConfiguration.NombreMaxLength);
        RuleFor(r => r.Prefijo).NotEmpty().MaximumLength(TipoInsumoConfiguration.PrefijoMaxLength)
            .Matches("^[A-Za-z]+$").WithMessage("El prefijo solo lleva letras.");
    }
}

internal sealed class UpdateTipoInsumoRequestValidator : AbstractValidator<UpdateTipoInsumoRequest>
{
    public UpdateTipoInsumoRequestValidator()
    {
        RuleFor(r => r.Codigo).NotEmpty().MaximumLength(TipoInsumoConfiguration.CodigoMaxLength);
        RuleFor(r => r.Nombre).NotEmpty().MaximumLength(TipoInsumoConfiguration.NombreMaxLength);
    }
}
