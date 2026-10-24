using System.Globalization;
using FluentValidation;
using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Features.Mantenimiento.UnidadesMedida;
using InfoPresupuestal.Api.Features.Presupuestos.Apus;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Presupuestos.Obras;

internal static class PresupuestosApi
{
    public const string Route = "/api/presupuestos";
    public const int MaxCapitulos = 100;
    public const int MaxItemsPorCapitulo = 500;

    public static Error NotFound(Guid id) => Error.NotFound("Presupuestos.NotFound", $"No existe el presupuesto '{id}'.");

    public static Error NoEditable(string estado) =>
        Error.Conflict("Presupuestos.NoEditable", $"El presupuesto está {estado.ToLowerInvariant()}. Vuelve a borrador para modificarlo.");

    public static async Task<string> SiguienteCodigoAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var prefijo = $"{Presupuesto.Prefijo}-";
        var ultimo = await db.Set<Presupuesto>()
            .Where(p => p.Codigo.StartsWith(prefijo))
            .OrderByDescending(p => p.Codigo.Length).ThenByDescending(p => p.Codigo)
            .Select(p => p.Codigo)
            .FirstOrDefaultAsync(cancellationToken);
        return PresupuestosModulo.SiguienteCodigo(Presupuesto.Prefijo, ultimo);
    }

    public static EncabezadoPresupuesto Encabezado(PresupuestoRequest r, DateOnly hoy) =>
        new(
            r.NombreObra,
            r.Cliente,
            r.Ubicacion,
            r.Fecha ?? hoy,
            r.PropuestaNumero,
            new PorcentajesAiu(
                r.AdministracionPct ?? PorcentajesAiu.PorDefecto.Administracion,
                r.ImprevistosPct ?? PorcentajesAiu.PorDefecto.Imprevistos,
                r.UtilidadPct ?? PorcentajesAiu.PorDefecto.Utilidad,
                r.IvaUtilidadPct ?? PorcentajesAiu.PorDefecto.IvaUtilidad),
            r.DuracionDias,
            r.AnticipoPct,
            r.Observaciones);

    /// <summary>
    /// Arma capítulos e ítems numerados por posición. Un ítem con APU toma de él la descripción, la unidad y el
    /// costo directo cuando la solicitud no los trae; así se reutilizan los APU existentes.
    /// </summary>
    public static async Task<Result<List<PresupuestoCapitulo>>> ConstruirCapitulosAsync(AppDbContext db, IReadOnlyList<CapituloRequest>? capitulos, CancellationToken cancellationToken)
    {
        capitulos ??= [];
        var items = capitulos.SelectMany(c => c.Items ?? []).ToList();

        var apuIds = items.Select(i => i.ApuId).OfType<Guid>().Distinct().ToList();
        var apus = await db.Set<Apu>().AsNoTracking()
            .Where(a => apuIds.Contains(a.Id))
            .Select(a => new { a.Id, a.Descripcion, a.UnidadMedidaId })
            .ToDictionaryAsync(a => a.Id, cancellationToken);
        if (apus.Count != apuIds.Count)
        {
            return Error.Validation("Presupuestos.ApuInvalido", "Algún APU no existe en la empresa.");
        }

        var costos = await ApusApi.CostosAsync(db, apuIds, cancellationToken);

        var unidades = items.Select(i => i.UnidadMedidaId).OfType<short>().Distinct().ToList();
        if (unidades.Count > 0 && await db.Set<UnidadMedida>().CountAsync(u => unidades.Contains(u.Id), cancellationToken) != unidades.Count)
        {
            return Error.Validation("Presupuestos.UnidadInvalida", "Alguna unidad de medida no existe.");
        }

        var resultado = new List<PresupuestoCapitulo>(capitulos.Count);
        for (var c = 0; c < capitulos.Count; c++)
        {
            var lista = new List<PresupuestoItem>();
            var itemsCapitulo = capitulos[c].Items ?? [];
            for (var i = 0; i < itemsCapitulo.Count; i++)
            {
                var item = itemsCapitulo[i];
                var apu = item.ApuId is { } apuId ? apus[apuId] : null;
                var descripcion = string.IsNullOrWhiteSpace(item.Descripcion) ? apu?.Descripcion : item.Descripcion;
                var unidad = item.UnidadMedidaId ?? apu?.UnidadMedidaId;
                var valor = item.ValorUnitario ?? (apu is null ? null : costos.GetValueOrDefault(apu.Id, ApuCostos.Cero).CostoDirecto);
                if (descripcion is null || unidad is null || valor is null)
                {
                    return Error.Validation(
                        "Presupuestos.ItemIncompleto",
                        $"El ítem {c + 1}.{i + 1} necesita un APU o descripción, unidad y valor unitario.");
                }

                lista.Add(PresupuestoItem.Create((short)(i + 1), apu?.Id, descripcion, unidad.Value, item.Cantidad, valor.Value));
            }

            resultado.Add(PresupuestoCapitulo.Create((short)(c + 1), capitulos[c].Nombre, lista));
        }

        return resultado;
    }

    /// <summary>Presupuesto completo con subtotales, AIU y el costo actual de cada APU usado.</summary>
    public static async Task<PresupuestoResponse?> LeerAsync(AppDbContext db, Guid id, CancellationToken cancellationToken)
    {
        var p = await db.Set<Presupuesto>().AsNoTracking()
            .Include(x => x.Capitulos).ThenInclude(c => c.Items).ThenInclude(i => i.UnidadMedida)
            .Include(x => x.Capitulos).ThenInclude(c => c.Items).ThenInclude(i => i.Apu)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (p is null)
        {
            return null;
        }

        var apuIds = p.Capitulos.SelectMany(c => c.Items).Select(i => i.ApuId).OfType<Guid>().Distinct().ToList();
        var costos = await ApusApi.CostosAsync(db, apuIds, cancellationToken);
        return PresupuestoMapping.ToResponse(p, costos);
    }
}

public sealed record ItemRequest(Guid? ApuId, string? Descripcion, short? UnidadMedidaId, decimal Cantidad, decimal? ValorUnitario);

public sealed record CapituloRequest(string Nombre, IReadOnlyList<ItemRequest>? Items);

/// <summary>Presupuesto completo. Los porcentajes son fracciones (0.09 = 9 %); si faltan se usan los de la plantilla.</summary>
public sealed record PresupuestoRequest(
    string NombreObra,
    string? Cliente,
    string? Ubicacion,
    DateOnly? Fecha,
    string? PropuestaNumero,
    decimal? AdministracionPct,
    decimal? ImprevistosPct,
    decimal? UtilidadPct,
    decimal? IvaUtilidadPct,
    int? DuracionDias,
    decimal? AnticipoPct,
    string? Observaciones,
    IReadOnlyList<CapituloRequest>? Capitulos);

public sealed record CambiarEstadoRequest(string Estado);

public sealed record ItemResponse(
    long Id,
    string Codigo,
    short Numero,
    Guid? ApuId,
    string? ApuCodigo,
    string Descripcion,
    short UnidadMedidaId,
    string Unidad,
    decimal Cantidad,
    decimal ValorUnitario,
    decimal ValorTotal,
    decimal? CostoApuActual);

public sealed record CapituloResponse(long Id, short Numero, string Nombre, decimal Subtotal, decimal Participacion, IReadOnlyList<ItemResponse> Items);

public sealed record PresupuestoResponse(
    Guid Id,
    string Codigo,
    string NombreObra,
    string? Cliente,
    string? Ubicacion,
    DateOnly Fecha,
    string? PropuestaNumero,
    string Estado,
    string Moneda,
    decimal AdministracionPct,
    decimal ImprevistosPct,
    decimal UtilidadPct,
    decimal IvaUtilidadPct,
    int? DuracionDias,
    decimal? AnticipoPct,
    string? Observaciones,
    IReadOnlyList<CapituloResponse> Capitulos,
    PresupuestoTotales Totales,
    int ItemsDesactualizados,
    DateTimeOffset ActualizadoEn);

public sealed record PresupuestoResumenResponse(
    Guid Id,
    string Codigo,
    string NombreObra,
    string? Cliente,
    DateOnly Fecha,
    string? PropuestaNumero,
    string Estado,
    int Capitulos,
    int Items,
    decimal CostoDirecto,
    decimal ValorTotal,
    DateTimeOffset ActualizadoEn);

internal static class PresupuestoMapping
{
    public static PresupuestoResponse ToResponse(Presupuesto p, IReadOnlyDictionary<Guid, ApuCostos> costos)
    {
        var costoDirecto = p.Capitulos.SelectMany(c => c.Items).Sum(i => i.ValorTotal);
        var desactualizados = 0;
        var capitulos = p.Capitulos.OrderBy(c => c.Numero).Select(c =>
        {
            var subtotal = c.Items.Sum(i => i.ValorTotal);
            var items = c.Items.OrderBy(i => i.Numero).Select(i =>
            {
                decimal? actual = i.ApuId is { } apuId ? costos.GetValueOrDefault(apuId, ApuCostos.Cero).CostoDirecto : null;
                if (actual is { } a && a != i.ValorUnitario)
                {
                    desactualizados++;
                }

                return new ItemResponse(
                    i.Id, Codigo(c.Numero, i.Numero), i.Numero, i.ApuId, i.Apu?.Codigo, i.Descripcion, i.UnidadMedidaId,
                    i.UnidadMedida?.Codigo ?? string.Empty, i.Cantidad, i.ValorUnitario, i.ValorTotal, actual);
            }).ToList();
            var participacion = costoDirecto == 0 ? 0 : decimal.Round(subtotal / costoDirecto, 4);
            return new CapituloResponse(c.Id, c.Numero, c.Nombre, subtotal, participacion, items);
        }).ToList();

        return new PresupuestoResponse(
            p.Id, p.Codigo, p.NombreObra, p.Cliente, p.Ubicacion, p.Fecha, p.PropuestaNumero, p.Estado, p.Moneda,
            p.AdministracionPct, p.ImprevistosPct, p.UtilidadPct, p.IvaUtilidadPct, p.DuracionDias, p.AnticipoPct, p.Observaciones,
            capitulos, PresupuestoCalculo.Totales(costoDirecto, p.Aiu, p.AnticipoPct), desactualizados, p.ActualizadoEn);
    }

    /// <summary>Consecutivo visible del ítem: capítulo.número (1.1, 1.2, 2.1).</summary>
    public static string Codigo(short capitulo, short item) =>
        string.Create(CultureInfo.InvariantCulture, $"{capitulo}.{item}");
}

internal sealed class PresupuestoRequestValidator : AbstractValidator<PresupuestoRequest>
{
    public PresupuestoRequestValidator()
    {
        RuleFor(r => r.NombreObra).NotEmpty().MaximumLength(PresupuestoConfiguration.TextoMaxLength);
        RuleFor(r => r.Cliente).MaximumLength(PresupuestoConfiguration.TextoMaxLength);
        RuleFor(r => r.Ubicacion).MaximumLength(PresupuestoConfiguration.TextoMaxLength);
        RuleFor(r => r.PropuestaNumero).MaximumLength(30);
        RuleFor(r => r.Observaciones).MaximumLength(PresupuestoConfiguration.ObservacionesMaxLength);
        RuleFor(r => r.AdministracionPct).InclusiveBetween(0, 1);
        RuleFor(r => r.ImprevistosPct).InclusiveBetween(0, 1);
        RuleFor(r => r.UtilidadPct).InclusiveBetween(0, 1);
        RuleFor(r => r.IvaUtilidadPct).InclusiveBetween(0, 1);
        RuleFor(r => r.AnticipoPct).InclusiveBetween(0, 1);
        RuleFor(r => r.DuracionDias).GreaterThan(0);
        RuleFor(r => r.Capitulos).Must(c => c is null || c.Count <= PresupuestosApi.MaxCapitulos).WithMessage($"Máximo {PresupuestosApi.MaxCapitulos} capítulos.");
        RuleForEach(r => r.Capitulos).ChildRules(c =>
        {
            c.RuleFor(x => x.Nombre).NotEmpty().MaximumLength(PresupuestoConfiguration.TextoMaxLength);
            c.RuleFor(x => x.Items).Must(i => i is null || i.Count <= PresupuestosApi.MaxItemsPorCapitulo)
                .WithMessage($"Máximo {PresupuestosApi.MaxItemsPorCapitulo} ítems por capítulo.");
            c.RuleForEach(x => x.Items).ChildRules(i =>
            {
                i.RuleFor(x => x.Descripcion).MaximumLength(PresupuestoConfiguration.TextoMaxLength);
                i.RuleFor(x => x.Cantidad).GreaterThan(0);
                i.RuleFor(x => x.ValorUnitario).GreaterThanOrEqualTo(0);
            });
        });
    }
}

internal sealed class CambiarEstadoRequestValidator : AbstractValidator<CambiarEstadoRequest>
{
    public CambiarEstadoRequestValidator() =>
        RuleFor(r => r.Estado).Must(e => EstadoPresupuesto.Todos.Contains(e))
            .WithMessage($"Estado no válido. Use {string.Join(", ", EstadoPresupuesto.Todos)}.");
}
