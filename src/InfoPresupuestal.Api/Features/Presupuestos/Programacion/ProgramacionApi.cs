using FluentValidation;
using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Features.Presupuestos.Obras;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Presupuestos.Programacion;

internal static class ProgramacionApi
{
    public const string Tag = "Programación de obra";
    public const string ListRoute = "/api/programaciones";
    public const int MaxTareas = 3000;
    public const int MaxVinculos = 6000;

    public static string Route(string id) => $"{PresupuestosApi.Route}/{id}/programacion";

    public static Error YaExiste(Guid id) =>
        Error.Conflict("Programacion.YaExiste", $"El presupuesto '{id}' ya tiene programación. Envía reemplazar=true para generarla de nuevo.");

    public static async Task<ProgramacionResponse?> LeerAsync(AppDbContext db, Guid presupuestoId, CancellationToken cancellationToken)
    {
        var presupuesto = await db.Set<Presupuesto>().AsNoTracking()
            .Where(p => p.Id == presupuestoId)
            .Select(p => new { p.Id, p.Codigo, p.NombreObra, p.Estado, p.Fecha, p.DuracionDias })
            .FirstOrDefaultAsync(cancellationToken);
        if (presupuesto is null)
        {
            return null;
        }

        var costoDirecto = await db.Set<PresupuestoCapitulo>().Where(c => c.PresupuestoId == presupuestoId)
            .SelectMany(c => c.Items).SumAsync(i => Math.Round(i.Cantidad * i.ValorUnitario, 2), cancellationToken);

        var programacion = await db.Set<ProgramacionObra>().AsNoTracking()
            .Include(p => p.Tareas)
            .Include(p => p.Vinculos)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.PresupuestoId == presupuestoId, cancellationToken);

        return new ProgramacionResponse(
            presupuesto.Id,
            presupuesto.Codigo,
            presupuesto.NombreObra,
            presupuesto.Estado,
            presupuesto.DuracionDias,
            costoDirecto,
            programacion is not null,
            programacion?.FechaInicio ?? presupuesto.Fecha,
            programacion?.Escala ?? EscalaProgramacion.Semana,
            (programacion?.Tareas ?? []).OrderBy(t => t.Orden).Select(ToResponse).ToList(),
            (programacion?.Vinculos ?? []).Select(v => new VinculoResponse(v.Id, v.OrigenId, v.DestinoId, v.Tipo, v.Desfase)).ToList(),
            programacion?.ActualizadoEn);
    }

    private static TareaResponse ToResponse(ProgramacionTarea t) =>
        new(t.Id, t.ParentId, t.Orden, t.Tipo, t.Texto, t.Referencia, t.FechaInicio, t.Duracion, t.FechaFin, t.Progreso,
            t.Responsable, t.Color, t.Notas, t.Valor, t.LineaBaseInicio, t.LineaBaseFin, t.Abierta);

    /// <summary>
    /// Revisa la coherencia del conjunto: ids únicos, padres existentes sin ciclos (un hito no tiene hijos)
    /// y vínculos entre actividades del mismo diagrama, sin repetir ni enlazar una actividad consigo misma.
    /// </summary>
    public static Error? Validar(IReadOnlyList<TareaRequest> tareas, IReadOnlyList<VinculoRequest> vinculos)
    {
        var porId = new Dictionary<Guid, TareaRequest>();
        foreach (var t in tareas)
        {
            if (!porId.TryAdd(t.Id, t))
            {
                return Error.Validation("Programacion.TareaDuplicada", $"La actividad '{t.Id}' está repetida.");
            }
        }

        foreach (var t in tareas)
        {
            if (t.ParentId is not { } parentId)
            {
                continue;
            }

            if (!porId.TryGetValue(parentId, out var padre))
            {
                return Error.Validation("Programacion.PadreInvalido", $"La actividad '{t.Texto}' pertenece a una actividad que no existe.");
            }

            if (padre.Tipo == TipoTarea.Hito)
            {
                return Error.Validation("Programacion.PadreInvalido", $"Un hito no puede contener actividades ('{t.Texto}').");
            }

            // Subir por los padres: si se vuelve a la misma actividad hay un ciclo.
            var actual = padre;
            for (var pasos = 0; actual is not null; pasos++)
            {
                if (actual.Id == t.Id || pasos > tareas.Count)
                {
                    return Error.Validation("Programacion.Ciclo", $"La actividad '{t.Texto}' no puede contenerse a sí misma.");
                }

                actual = actual.ParentId is { } p ? porId.GetValueOrDefault(p) : null;
            }
        }

        var claves = new HashSet<(Guid, Guid)>();
        var ids = new HashSet<Guid>();
        foreach (var v in vinculos)
        {
            if (!ids.Add(v.Id))
            {
                return Error.Validation("Programacion.VinculoDuplicado", $"El vínculo '{v.Id}' está repetido.");
            }

            if (!porId.ContainsKey(v.Origen) || !porId.ContainsKey(v.Destino))
            {
                return Error.Validation("Programacion.VinculoInvalido", "Un vínculo une actividades que no existen.");
            }

            if (v.Origen == v.Destino || !claves.Add((v.Origen, v.Destino)))
            {
                return Error.Validation("Programacion.VinculoInvalido", "Hay vínculos repetidos o de una actividad consigo misma.");
            }
        }

        return null;
    }

    public static TareaDatos ToDatos(TareaRequest t, int orden) =>
        new(t.Id, t.ParentId, orden, t.Tipo, t.Texto, t.Referencia, t.FechaInicio, t.Duracion, t.Progreso,
            t.Responsable, t.Color, t.Notas, t.Valor, t.LineaBaseInicio, t.LineaBaseFin, t.Abierta);
}

public sealed record TareaRequest(
    Guid Id,
    Guid? ParentId,
    string Tipo,
    string Texto,
    string? Referencia,
    DateOnly FechaInicio,
    int Duracion,
    decimal Progreso,
    string? Responsable,
    string? Color,
    string? Notas,
    decimal? Valor,
    DateOnly? LineaBaseInicio,
    DateOnly? LineaBaseFin,
    bool Abierta = true);

/// <param name="Tipo">0 fin-inicio, 1 inicio-inicio, 2 fin-fin, 3 inicio-fin.</param>
/// <param name="Desfase">Días de espera (positivo) o adelanto (negativo).</param>
public sealed record VinculoRequest(Guid Id, Guid Origen, Guid Destino, short Tipo, int Desfase = 0);

/// <summary>Programación completa: el orden de <see cref="Tareas"/> es el orden del diagrama.</summary>
public sealed record ProgramacionRequest(DateOnly FechaInicio, string? Escala, IReadOnlyList<TareaRequest>? Tareas, IReadOnlyList<VinculoRequest>? Vinculos);

/// <summary>Crea la programación desde los capítulos e ítems del presupuesto.</summary>
/// <param name="DuracionDias">Plazo total a repartir; si falta se usa el del presupuesto (o 30 días).</param>
public sealed record GenerarProgramacionRequest(DateOnly? FechaInicio, int? DuracionDias, bool Reemplazar = false);

public sealed record TareaResponse(
    Guid Id,
    Guid? ParentId,
    int Orden,
    string Tipo,
    string Texto,
    string? Referencia,
    DateOnly FechaInicio,
    int Duracion,
    DateOnly FechaFin,
    decimal Progreso,
    string? Responsable,
    string? Color,
    string? Notas,
    decimal? Valor,
    DateOnly? LineaBaseInicio,
    DateOnly? LineaBaseFin,
    bool Abierta);

public sealed record VinculoResponse(Guid Id, Guid Origen, Guid Destino, short Tipo, int Desfase);

public sealed record ProgramacionResponse(
    Guid PresupuestoId,
    string Codigo,
    string NombreObra,
    string Estado,
    int? DuracionPresupuesto,
    decimal CostoDirecto,
    bool Existe,
    DateOnly FechaInicio,
    string Escala,
    IReadOnlyList<TareaResponse> Tareas,
    IReadOnlyList<VinculoResponse> Vinculos,
    DateTimeOffset? ActualizadoEn);

public sealed record ProgramacionResumenResponse(
    Guid PresupuestoId,
    string Codigo,
    string NombreObra,
    string? Cliente,
    string Estado,
    bool Existe,
    DateOnly? FechaInicio,
    DateOnly? FechaFin,
    int Actividades,
    decimal? Avance,
    DateTimeOffset? ActualizadoEn);

internal sealed class ProgramacionRequestValidator : AbstractValidator<ProgramacionRequest>
{
    public ProgramacionRequestValidator()
    {
        RuleFor(r => r.Escala).Must(e => e is null || EscalaProgramacion.Todas.Contains(e))
            .WithMessage($"Escala no válida. Use {string.Join(", ", EscalaProgramacion.Todas)}.");
        RuleFor(r => r.Tareas).Must(t => t is null || t.Count <= ProgramacionApi.MaxTareas).WithMessage($"Máximo {ProgramacionApi.MaxTareas} actividades.");
        RuleFor(r => r.Vinculos).Must(v => v is null || v.Count <= ProgramacionApi.MaxVinculos).WithMessage($"Máximo {ProgramacionApi.MaxVinculos} vínculos.");
        RuleForEach(r => r.Tareas).ChildRules(t =>
        {
            t.RuleFor(x => x.Id).NotEmpty();
            t.RuleFor(x => x.Tipo).Must(x => TipoTarea.Todos.Contains(x)).WithMessage("Tipo de actividad no válido (task, project o milestone).");
            t.RuleFor(x => x.Texto).NotEmpty().MaximumLength(ProgramacionTareaConfiguration.TextoMaxLength);
            t.RuleFor(x => x.Referencia).MaximumLength(20);
            t.RuleFor(x => x.Duracion).InclusiveBetween(0, 3650);
            t.RuleFor(x => x.Progreso).InclusiveBetween(0, 1);
            t.RuleFor(x => x.Responsable).MaximumLength(120);
            t.RuleFor(x => x.Color).Matches("^#[0-9a-fA-F]{6}$").When(x => !string.IsNullOrWhiteSpace(x.Color)).WithMessage("El color debe tener el formato #RRGGBB.");
            t.RuleFor(x => x.Notas).MaximumLength(1000);
            t.RuleFor(x => x.Valor).GreaterThanOrEqualTo(0);
            t.RuleFor(x => x.LineaBaseFin).NotNull().GreaterThanOrEqualTo(x => x.LineaBaseInicio).When(x => x.LineaBaseInicio is not null)
                .WithMessage("La línea base necesita un fin igual o posterior al inicio.");
        });
        RuleForEach(r => r.Vinculos).ChildRules(v =>
        {
            v.RuleFor(x => x.Id).NotEmpty();
            v.RuleFor(x => x.Tipo).InclusiveBetween((short)0, (short)3);
            v.RuleFor(x => x.Desfase).InclusiveBetween(-3650, 3650);
        });
    }
}

internal sealed class GenerarProgramacionRequestValidator : AbstractValidator<GenerarProgramacionRequest>
{
    public GenerarProgramacionRequestValidator() => RuleFor(r => r.DuracionDias).InclusiveBetween(1, 3650);
}
