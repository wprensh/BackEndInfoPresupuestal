using FluentValidation;
using InfoPresupuestal.Api.Common.Abstractions;

namespace InfoPresupuestal.Api.Features.Mantenimiento.UnidadesMedida;

internal static class UnidadesMedidaApi
{
    public const string Route = $"{MantenimientoApi.Route}/unidades-medida";

    public static Error NotFound(short id) => Error.NotFound("UnidadesMedida.NotFound", $"No existe la unidad de medida {id}.");

    public static Error CodigoDuplicado(string codigo) => Error.Conflict("UnidadesMedida.CodigoDuplicado", $"Ya existe la unidad '{codigo}'.");

    public static UnidadMedidaResponse AsResponse(this UnidadMedida u) => new(u.Id, u.Codigo, u.Nombre);
}

public sealed record UnidadMedidaRequest(string Codigo, string Nombre);

public sealed record UnidadMedidaResponse(short Id, string Codigo, string Nombre);

internal sealed class UnidadMedidaRequestValidator : AbstractValidator<UnidadMedidaRequest>
{
    public UnidadMedidaRequestValidator()
    {
        RuleFor(r => r.Codigo).NotEmpty().MaximumLength(UnidadMedidaConfiguration.CodigoMaxLength);
        RuleFor(r => r.Nombre).NotEmpty().MaximumLength(UnidadMedidaConfiguration.NombreMaxLength);
    }
}
