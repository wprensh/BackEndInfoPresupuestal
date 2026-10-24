using FluentValidation;
using InfoPresupuestal.Api.Common.Abstractions;

namespace InfoPresupuestal.Api.Features.Mantenimiento.Clasificaciones;

internal static class ClasificacionesApi
{
    public const string Route = $"{MantenimientoApi.Route}/clasificaciones";

    public static Error NotFound(int id) => Error.NotFound("Clasificaciones.NotFound", $"No existe la clasificación {id}.");

    public static Error NombreDuplicado(string nombre) => Error.Conflict("Clasificaciones.NombreDuplicado", $"Ya existe la clasificación '{nombre}'.");
}

public sealed record CreateClasificacionRequest(string Nombre);

public sealed record UpdateClasificacionRequest(string Nombre, bool Activo);

public sealed record ClasificacionResponse(int Id, string Nombre, bool Activo, int Insumos);

internal sealed class CreateClasificacionRequestValidator : AbstractValidator<CreateClasificacionRequest>
{
    public CreateClasificacionRequestValidator() =>
        RuleFor(r => r.Nombre).NotEmpty().MaximumLength(ClasificacionConfiguration.NombreMaxLength);
}

internal sealed class UpdateClasificacionRequestValidator : AbstractValidator<UpdateClasificacionRequest>
{
    public UpdateClasificacionRequestValidator() =>
        RuleFor(r => r.Nombre).NotEmpty().MaximumLength(ClasificacionConfiguration.NombreMaxLength);
}
