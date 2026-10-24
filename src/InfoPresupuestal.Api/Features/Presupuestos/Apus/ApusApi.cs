using FluentValidation;
using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Features.Mantenimiento.Insumos;
using InfoPresupuestal.Api.Features.Mantenimiento.UnidadesMedida;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Presupuestos.Apus;

internal static class ApusApi
{
    public const string Route = "/api/apus";
    public const int MaxLineasPorSeccion = 200;

    public static Error NotFound(Guid id) => Error.NotFound("Apus.NotFound", $"No existe el APU '{id}'.");

    public static Error Duplicado() => Error.Conflict("Apus.Duplicado", "Ya existe un APU con esa descripción y unidad.");

    /// <summary>Misma regla que el índice único de la base: descripción sin distinguir mayúsculas + unidad.</summary>
    public static Task<bool> ExisteDuplicadoAsync(AppDbContext db, Guid? excluirId, string descripcion, short unidadMedidaId, CancellationToken cancellationToken) =>
        db.Set<Apu>().AnyAsync(
            a => a.Id != excluirId && a.UnidadMedidaId == unidadMedidaId && EF.Functions.ILike(a.Descripcion, LikePattern.Exact(descripcion)),
            cancellationToken);

    /// <summary>Siguiente APU-NNNN de la empresa.</summary>
    public static async Task<string> SiguienteCodigoAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var prefijo = $"{Apu.Prefijo}-";
        var ultimo = await db.Set<Apu>()
            .Where(a => a.Codigo.StartsWith(prefijo))
            .OrderByDescending(a => a.Codigo.Length).ThenByDescending(a => a.Codigo)
            .Select(a => a.Codigo)
            .FirstOrDefaultAsync(cancellationToken);
        return PresupuestosModulo.SiguienteCodigo(Apu.Prefijo, ultimo);
    }

    /// <summary>
    /// Valida las referencias del APU contra los catálogos (unidades globales e insumos de la empresa) y arma
    /// las líneas con su orden dentro de cada sección.
    /// </summary>
    public static async Task<Result<List<ApuDetalle>>> ConstruirDetallesAsync(AppDbContext db, ApuRequest datos, CancellationToken cancellationToken)
    {
        var materiales = datos.Materiales ?? [];
        var equipo = datos.Equipo ?? [];
        var manoObra = datos.ManoObra ?? [];

        var unidades = materiales.Select(m => m.UnidadMedidaId).Concat(equipo.Select(e => e.UnidadMedidaId)).Append(datos.UnidadMedidaId).Distinct().ToList();
        var unidadesExistentes = await db.Set<UnidadMedida>().CountAsync(u => unidades.Contains(u.Id), cancellationToken);
        if (unidadesExistentes != unidades.Count)
        {
            return Error.Validation("Apus.UnidadInvalida", "Alguna unidad de medida no existe.");
        }

        var insumos = materiales.Select(m => m.InsumoId).Concat(equipo.Select(e => e.InsumoId)).Concat(manoObra.Select(m => m.InsumoId))
            .OfType<Guid>().Distinct().ToList();
        if (insumos.Count > 0 && await db.Set<Insumo>().CountAsync(i => insumos.Contains(i.Id), cancellationToken) != insumos.Count)
        {
            return Error.Validation("Apus.InsumoInvalido", "Algún insumo no existe en la empresa.");
        }

        var detalles = new List<ApuDetalle>(materiales.Count + equipo.Count + manoObra.Count);
        detalles.AddRange(materiales.Select((m, i) => ApuDetalle.Material((short)(i + 1), m.InsumoId, m.Descripcion, m.UnidadMedidaId, m.Cantidad, m.Desperdicio, m.PrecioUnitario)));
        detalles.AddRange(equipo.Select((e, i) => ApuDetalle.Equipo((short)(i + 1), e.InsumoId, e.Descripcion, e.UnidadMedidaId, e.Incidencia, e.PrecioUnitario)));
        detalles.AddRange(manoObra.Select((m, i) => ApuDetalle.ManoObra((short)(i + 1), m.InsumoId, m.Descripcion, m.Jornal, m.FactorPrestacional, m.Rendimiento)));
        return detalles;
    }

    /// <summary>Costos de varios APU en una consulta agrupada por APU y sección.</summary>
    public static async Task<Dictionary<Guid, ApuCostos>> CostosAsync(AppDbContext db, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var filas = await db.Set<ApuDetalle>()
            .Where(d => ids.Contains(d.ApuId))
            .GroupBy(d => new { d.ApuId, d.TipoInsumoId })
            .Select(g => new
            {
                g.Key.ApuId,
                g.Key.TipoInsumoId,
                Valor = g.Sum(d => d.TipoInsumoId == PresupuestosModulo.ManoObra
                    ? d.PrecioUnitario * d.FactorPrestacional / d.Rendimiento
                    : d.Cantidad * d.PrecioUnitario * (1 + d.Desperdicio)),
            })
            .ToListAsync(cancellationToken);

        return filas.GroupBy(f => f.ApuId).ToDictionary(
            g => g.Key,
            g => ApuCostos.Desde(
                g.Where(f => f.TipoInsumoId == PresupuestosModulo.Material).Sum(f => f.Valor),
                g.Where(f => f.TipoInsumoId == PresupuestosModulo.Equipo).Sum(f => f.Valor),
                g.Where(f => f.TipoInsumoId == PresupuestosModulo.ManoObra).Sum(f => f.Valor)));
    }

    /// <summary>Respuesta completa de un APU, leída de nuevo para devolver lo que quedó guardado.</summary>
    public static async Task<ApuResponse?> LeerAsync(AppDbContext db, Guid id, CancellationToken cancellationToken)
    {
        var apu = await db.Set<Apu>().AsNoTracking()
            .Include(a => a.UnidadMedida)
            .Include(a => a.Detalles).ThenInclude(d => d.UnidadMedida)
            .AsSplitQuery()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (apu is null)
        {
            return null;
        }

        var usos = await db.Set<Obras.PresupuestoItem>().CountAsync(i => i.ApuId == id, cancellationToken);
        return ApuMapping.ToResponse(apu, usos);
    }
}

public sealed record ApuMaterialRequest(Guid? InsumoId, string Descripcion, short UnidadMedidaId, decimal Cantidad, decimal Desperdicio, decimal PrecioUnitario);

public sealed record ApuEquipoRequest(Guid? InsumoId, string Descripcion, short UnidadMedidaId, decimal Incidencia, decimal PrecioUnitario);

public sealed record ApuManoObraRequest(Guid? InsumoId, string Descripcion, decimal Jornal, decimal FactorPrestacional, decimal Rendimiento);

/// <summary>APU completo. <c>Desperdicio</c> es una fracción (0.05 = 5 %) y <c>FactorPrestacional</c> un multiplicador (1.7).</summary>
public sealed record ApuRequest(
    string Descripcion,
    short UnidadMedidaId,
    DateOnly? Fecha,
    string? Observaciones,
    bool? Activo,
    IReadOnlyList<ApuMaterialRequest>? Materiales,
    IReadOnlyList<ApuEquipoRequest>? Equipo,
    IReadOnlyList<ApuManoObraRequest>? ManoObra);

/// <summary>Costo directo por sección, redondeado a centavos como lo muestra el Excel.</summary>
public sealed record ApuCostos(decimal Materiales, decimal Equipo, decimal ManoObra, decimal CostoDirecto)
{
    public static readonly ApuCostos Cero = new(0, 0, 0, 0);

    public static ApuCostos Desde(decimal materiales, decimal equipo, decimal manoObra) =>
        new(Redondear(materiales), Redondear(equipo), Redondear(manoObra), Redondear(materiales + equipo + manoObra));

    private static decimal Redondear(decimal valor) => decimal.Round(valor, 2, MidpointRounding.AwayFromZero);
}

public sealed record ApuLineaResponse(
    long Id,
    short Orden,
    Guid? InsumoId,
    string Descripcion,
    short? UnidadMedidaId,
    string? Unidad,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal Desperdicio,
    decimal FactorPrestacional,
    decimal Rendimiento,
    decimal ValorParcial);

public sealed record ApuResponse(
    Guid Id,
    string Codigo,
    string Descripcion,
    short UnidadMedidaId,
    string Unidad,
    DateOnly Fecha,
    string? Observaciones,
    bool Activo,
    ApuCostos Costos,
    IReadOnlyList<ApuLineaResponse> Materiales,
    IReadOnlyList<ApuLineaResponse> Equipo,
    IReadOnlyList<ApuLineaResponse> ManoObra,
    int UsosEnPresupuestos,
    DateTimeOffset ActualizadoEn);

public sealed record ApuResumenResponse(
    Guid Id,
    string Codigo,
    string Descripcion,
    short UnidadMedidaId,
    string Unidad,
    DateOnly Fecha,
    bool Activo,
    ApuCostos Costos,
    DateTimeOffset ActualizadoEn);

internal static class ApuMapping
{
    public static ApuResponse ToResponse(Apu apu, int usos)
    {
        List<ApuLineaResponse> Seccion(short tipo) =>
            apu.Detalles.Where(d => d.TipoInsumoId == tipo).OrderBy(d => d.Orden)
                .Select(d => new ApuLineaResponse(
                    d.Id, d.Orden, d.InsumoId, d.Descripcion, d.UnidadMedidaId, d.UnidadMedida?.Codigo,
                    d.Cantidad, d.PrecioUnitario, d.Desperdicio, d.FactorPrestacional, d.Rendimiento,
                    decimal.Round(ApuCalculo.ValorParcial(d), 2, MidpointRounding.AwayFromZero)))
                .ToList();

        var materiales = Seccion(PresupuestosModulo.Material);
        var equipo = Seccion(PresupuestosModulo.Equipo);
        var manoObra = Seccion(PresupuestosModulo.ManoObra);
        decimal Total(short tipo) => apu.Detalles.Where(d => d.TipoInsumoId == tipo).Sum(ApuCalculo.ValorParcial);

        return new ApuResponse(
            apu.Id, apu.Codigo, apu.Descripcion, apu.UnidadMedidaId, apu.UnidadMedida?.Codigo ?? string.Empty, apu.Fecha,
            apu.Observaciones, apu.Activo,
            ApuCostos.Desde(Total(PresupuestosModulo.Material), Total(PresupuestosModulo.Equipo), Total(PresupuestosModulo.ManoObra)),
            materiales, equipo, manoObra, usos, apu.ActualizadoEn);
    }
}

internal sealed class ApuRequestValidator : AbstractValidator<ApuRequest>
{
    public ApuRequestValidator()
    {
        RuleFor(r => r.Descripcion).NotEmpty().MaximumLength(ApuConfiguration.DescripcionMaxLength);
        RuleFor(r => r.UnidadMedidaId).GreaterThan((short)0);
        RuleFor(r => r.Observaciones).MaximumLength(ApuConfiguration.ObservacionesMaxLength);
        RuleFor(r => r.Materiales).Must(l => l is null || l.Count <= ApusApi.MaxLineasPorSeccion).WithMessage($"Máximo {ApusApi.MaxLineasPorSeccion} materiales.");
        RuleFor(r => r.Equipo).Must(l => l is null || l.Count <= ApusApi.MaxLineasPorSeccion).WithMessage($"Máximo {ApusApi.MaxLineasPorSeccion} equipos.");
        RuleFor(r => r.ManoObra).Must(l => l is null || l.Count <= ApusApi.MaxLineasPorSeccion).WithMessage($"Máximo {ApusApi.MaxLineasPorSeccion} líneas de mano de obra.");
        RuleFor(r => r)
            .Must(r => (r.Materiales?.Count ?? 0) + (r.Equipo?.Count ?? 0) + (r.ManoObra?.Count ?? 0) > 0)
            .WithName("Lineas")
            .WithMessage("El APU necesita al menos un material, equipo o mano de obra.");

        RuleForEach(r => r.Materiales).ChildRules(m =>
        {
            m.RuleFor(x => x.Descripcion).NotEmpty().MaximumLength(ApuConfiguration.DescripcionMaxLength);
            m.RuleFor(x => x.UnidadMedidaId).GreaterThan((short)0);
            m.RuleFor(x => x.Cantidad).GreaterThan(0);
            m.RuleFor(x => x.Desperdicio).InclusiveBetween(0, 1);
            m.RuleFor(x => x.PrecioUnitario).GreaterThanOrEqualTo(0);
        });
        RuleForEach(r => r.Equipo).ChildRules(e =>
        {
            e.RuleFor(x => x.Descripcion).NotEmpty().MaximumLength(ApuConfiguration.DescripcionMaxLength);
            e.RuleFor(x => x.UnidadMedidaId).GreaterThan((short)0);
            e.RuleFor(x => x.Incidencia).GreaterThan(0);
            e.RuleFor(x => x.PrecioUnitario).GreaterThanOrEqualTo(0);
        });
        RuleForEach(r => r.ManoObra).ChildRules(m =>
        {
            m.RuleFor(x => x.Descripcion).NotEmpty().MaximumLength(ApuConfiguration.DescripcionMaxLength);
            m.RuleFor(x => x.Jornal).GreaterThanOrEqualTo(0);
            m.RuleFor(x => x.FactorPrestacional).InclusiveBetween(1, 3);
            m.RuleFor(x => x.Rendimiento).GreaterThan(0);
        });
    }
}
