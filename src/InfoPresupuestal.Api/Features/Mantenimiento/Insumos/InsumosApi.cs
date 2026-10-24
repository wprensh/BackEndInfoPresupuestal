using System.Linq.Expressions;
using FluentValidation;
using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Features.Mantenimiento.Clasificaciones;
using InfoPresupuestal.Api.Features.Mantenimiento.UnidadesMedida;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Mantenimiento.Insumos;

internal static class InsumosApi
{
    public const string Route = $"{MantenimientoApi.Route}/insumos";

    public static Error NotFound(Guid id) => Error.NotFound("Insumos.NotFound", $"No existe el insumo '{id}'.");

    public static Error Duplicado() =>
        Error.Conflict("Insumos.Duplicado", "Ya existe un insumo del mismo tipo con esa descripción y unidad.");

    /// <summary>Revisa que la unidad exista y que la clasificación sea de la empresa actual (filtro por tenant).</summary>
    public static async Task<Error?> ValidarCatalogosAsync(AppDbContext db, short unidadMedidaId, int? clasificacionId, CancellationToken cancellationToken)
    {
        if (!await db.Set<UnidadMedida>().AnyAsync(u => u.Id == unidadMedidaId, cancellationToken))
        {
            return Error.Validation("Insumos.UnidadInvalida", $"No existe la unidad de medida {unidadMedidaId}.");
        }

        if (clasificacionId is { } id && !await db.Set<Clasificacion>().AnyAsync(c => c.Id == id, cancellationToken))
        {
            return Error.Validation("Insumos.ClasificacionInvalida", $"No existe la clasificación {id} en la empresa.");
        }

        return null;
    }

    /// <summary>Misma regla que el índice único de la base: tipo + descripción sin distinguir mayúsculas + unidad.</summary>
    public static Task<bool> ExisteDuplicadoAsync(AppDbContext db, Guid? excluirId, short tipoInsumoId, string descripcion, short unidadMedidaId, CancellationToken cancellationToken) =>
        db.Set<Insumo>().AnyAsync(
            i => i.Id != excluirId
                && i.TipoInsumoId == tipoInsumoId
                && i.UnidadMedidaId == unidadMedidaId
                && EF.Functions.ILike(i.Descripcion, LikePattern.Exact(descripcion)),
            cancellationToken);
}

public sealed record PrecioVigente(decimal ValorUnitario, string Moneda, DateOnly FechaVigencia);

public sealed record InsumoResponse(
    Guid Id,
    string Codigo,
    short TipoInsumoId,
    string Tipo,
    int? ClasificacionId,
    string? Clasificacion,
    short UnidadMedidaId,
    string Unidad,
    string Descripcion,
    bool Activo,
    PrecioVigente? Precio,
    DateTimeOffset ActualizadoEn);

internal static class InsumoMapping
{
    /// <summary>Proyección traducible a SQL, con el precio más reciente (como la vista <c>v_insumo_vigente</c>).</summary>
    public static Expression<Func<Insumo, InsumoResponse>> ToResponse(AppDbContext db) => i =>
        new InsumoResponse(
            i.Id,
            i.Codigo,
            i.TipoInsumoId,
            i.TipoInsumo!.Nombre,
            i.ClasificacionId,
            i.Clasificacion != null ? i.Clasificacion.Nombre : null,
            i.UnidadMedidaId,
            i.UnidadMedida!.Codigo,
            i.Descripcion,
            i.Activo,
            db.Set<InsumoPrecio>()
                .Where(p => p.InsumoId == i.Id)
                .OrderByDescending(p => p.FechaVigencia)
                .Select(p => new PrecioVigente(p.ValorUnitario, p.Moneda, p.FechaVigencia))
                .FirstOrDefault(),
            i.ActualizadoEn);
}

public sealed record CreateInsumoRequest(short TipoInsumoId, int? ClasificacionId, short UnidadMedidaId, string Descripcion);

public sealed record UpdateInsumoRequest(int? ClasificacionId, short UnidadMedidaId, string Descripcion, bool Activo);

internal sealed class CreateInsumoRequestValidator : AbstractValidator<CreateInsumoRequest>
{
    public CreateInsumoRequestValidator()
    {
        RuleFor(r => r.TipoInsumoId).GreaterThan((short)0);
        RuleFor(r => r.UnidadMedidaId).GreaterThan((short)0);
        RuleFor(r => r.Descripcion).NotEmpty().MaximumLength(InsumoConfiguration.DescripcionMaxLength);
    }
}

internal sealed class UpdateInsumoRequestValidator : AbstractValidator<UpdateInsumoRequest>
{
    public UpdateInsumoRequestValidator()
    {
        RuleFor(r => r.UnidadMedidaId).GreaterThan((short)0);
        RuleFor(r => r.Descripcion).NotEmpty().MaximumLength(InsumoConfiguration.DescripcionMaxLength);
    }
}
