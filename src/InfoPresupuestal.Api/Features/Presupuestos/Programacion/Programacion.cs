using InfoPresupuestal.Api.Common.MultiTenancy;
using InfoPresupuestal.Api.Features.Presupuestos.Obras;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfoPresupuestal.Api.Features.Presupuestos.Programacion;

/// <summary>Tipos de actividad de DHTMLX Gantt.</summary>
public static class TipoTarea
{
    public const string Tarea = "task";
    public const string Capitulo = "project";
    public const string Hito = "milestone";

    public static readonly IReadOnlyList<string> Todos = [Tarea, Capitulo, Hito];
}

/// <summary>Escalas de tiempo con que se abre el diagrama.</summary>
public static class EscalaProgramacion
{
    public const string Dia = "dia";
    public const string Semana = "semana";
    public const string Mes = "mes";

    public static readonly IReadOnlyList<string> Todas = [Dia, Semana, Mes, "trimestre", "anio"];
}

/// <summary>Datos de una actividad tal como llegan del diagrama (ya validados).</summary>
public sealed record TareaDatos(
    Guid Id,
    Guid? ParentId,
    int Orden,
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
    bool Abierta);

/// <summary>Dependencia entre actividades. Tipo: 0 fin-inicio, 1 inicio-inicio, 2 fin-fin, 3 inicio-fin.</summary>
public sealed record VinculoDatos(Guid Id, Guid OrigenId, Guid DestinoId, short Tipo, int Desfase);

/// <summary>Programación de obra (diagrama de Gantt) de un presupuesto: una por presupuesto.</summary>
public sealed class ProgramacionObra : ITenantEntity
{
    private readonly List<ProgramacionTarea> _tareas = [];
    private readonly List<ProgramacionVinculo> _vinculos = [];

    private ProgramacionObra()
    {
        // Requerido por EF Core.
    }

    public Guid PresupuestoId { get; private set; }

    public string TenantId { get; private set; } = string.Empty;

    public DateOnly FechaInicio { get; private set; }

    public string Escala { get; private set; } = EscalaProgramacion.Semana;

    public DateTimeOffset CreadoEn { get; private set; }

    public DateTimeOffset ActualizadoEn { get; private set; }

    public IReadOnlyList<ProgramacionTarea> Tareas => _tareas;

    public IReadOnlyList<ProgramacionVinculo> Vinculos => _vinculos;

    public static ProgramacionObra Create(Guid presupuestoId, DateTimeOffset now) =>
        new() { PresupuestoId = presupuestoId, CreadoEn = now, ActualizadoEn = now };

    /// <summary>
    /// Deja la programación igual a lo recibido: actualiza las actividades y vínculos que ya existen (por id),
    /// agrega los nuevos y quita los que no llegaron. Así los ids del diagrama se conservan entre guardados.
    /// </summary>
    public void Reemplazar(DateOnly fechaInicio, string escala, IReadOnlyList<TareaDatos> tareas, IReadOnlyList<VinculoDatos> vinculos, DateTimeOffset now)
    {
        FechaInicio = fechaInicio;
        Escala = escala;
        ActualizadoEn = now;

        var tareasPorId = tareas.ToDictionary(t => t.Id);
        _tareas.RemoveAll(t => !tareasPorId.ContainsKey(t.Id));
        var existentes = _tareas.ToDictionary(t => t.Id);
        foreach (var datos in tareas)
        {
            if (existentes.TryGetValue(datos.Id, out var tarea))
            {
                tarea.Update(datos);
            }
            else
            {
                _tareas.Add(ProgramacionTarea.Create(PresupuestoId, datos));
            }
        }

        var vinculosPorId = vinculos.ToDictionary(v => v.Id);
        _vinculos.RemoveAll(v => !vinculosPorId.ContainsKey(v.Id));
        var vinculosExistentes = _vinculos.ToDictionary(v => v.Id);
        foreach (var datos in vinculos)
        {
            if (vinculosExistentes.TryGetValue(datos.Id, out var vinculo))
            {
                vinculo.Update(datos);
            }
            else
            {
                _vinculos.Add(ProgramacionVinculo.Create(PresupuestoId, datos));
            }
        }
    }
}

public sealed class ProgramacionTarea : ITenantEntity
{
    private ProgramacionTarea()
    {
        // Requerido por EF Core.
    }

    public Guid Id { get; private set; }

    public string TenantId { get; private set; } = string.Empty;

    public Guid PresupuestoId { get; private set; }

    public Guid? ParentId { get; private set; }

    public int Orden { get; private set; }

    public string Tipo { get; private set; } = TipoTarea.Tarea;

    public string Texto { get; private set; } = string.Empty;

    public string? Referencia { get; private set; }

    public DateOnly FechaInicio { get; private set; }

    /// <summary>Días calendario; 0 en los hitos.</summary>
    public int Duracion { get; private set; }

    /// <summary>Avance como fracción (0.25 = 25 %).</summary>
    public decimal Progreso { get; private set; }

    public string? Responsable { get; private set; }

    public string? Color { get; private set; }

    public string? Notas { get; private set; }

    /// <summary>Valor del capítulo o ítem del presupuesto; pondera el avance.</summary>
    public decimal? Valor { get; private set; }

    public DateOnly? LineaBaseInicio { get; private set; }

    public DateOnly? LineaBaseFin { get; private set; }

    public bool Abierta { get; private set; }

    public DateOnly FechaFin => FechaInicio.AddDays(Duracion);

    public static ProgramacionTarea Create(Guid presupuestoId, TareaDatos datos)
    {
        var tarea = new ProgramacionTarea { Id = datos.Id, PresupuestoId = presupuestoId };
        tarea.Update(datos);
        return tarea;
    }

    public void Update(TareaDatos d)
    {
        ParentId = d.ParentId;
        Orden = d.Orden;
        Tipo = d.Tipo;
        Texto = d.Texto.Trim();
        Referencia = string.IsNullOrWhiteSpace(d.Referencia) ? null : d.Referencia.Trim();
        FechaInicio = d.FechaInicio;
        Duracion = d.Tipo == TipoTarea.Hito ? 0 : d.Duracion;
        Progreso = decimal.Round(Math.Clamp(d.Progreso, 0, 1), 4);
        Responsable = string.IsNullOrWhiteSpace(d.Responsable) ? null : d.Responsable.Trim();
        Color = string.IsNullOrWhiteSpace(d.Color) ? null : d.Color.Trim().ToLowerInvariant();
        Notas = string.IsNullOrWhiteSpace(d.Notas) ? null : d.Notas.Trim();
        Valor = d.Valor is { } v ? decimal.Round(v, 2, MidpointRounding.AwayFromZero) : null;
        LineaBaseInicio = d.LineaBaseInicio;
        LineaBaseFin = d.LineaBaseInicio is null ? null : d.LineaBaseFin;
        Abierta = d.Abierta;
    }
}

public sealed class ProgramacionVinculo : ITenantEntity
{
    private ProgramacionVinculo()
    {
        // Requerido por EF Core.
    }

    public Guid Id { get; private set; }

    public string TenantId { get; private set; } = string.Empty;

    public Guid PresupuestoId { get; private set; }

    public Guid OrigenId { get; private set; }

    public Guid DestinoId { get; private set; }

    public short Tipo { get; private set; }

    /// <summary>Días de espera (positivo) o adelanto (negativo) entre las actividades.</summary>
    public int Desfase { get; private set; }

    public static ProgramacionVinculo Create(Guid presupuestoId, VinculoDatos datos)
    {
        var vinculo = new ProgramacionVinculo { Id = datos.Id, PresupuestoId = presupuestoId };
        vinculo.Update(datos);
        return vinculo;
    }

    public void Update(VinculoDatos d)
    {
        OrigenId = d.OrigenId;
        DestinoId = d.DestinoId;
        Tipo = d.Tipo;
        Desfase = d.Desfase;
    }
}

internal sealed class ProgramacionObraConfiguration : IEntityTypeConfiguration<ProgramacionObra>
{
    public void Configure(EntityTypeBuilder<ProgramacionObra> builder)
    {
        builder.ToTable("programacion", PresupuestosModulo.Schema, t => t.ExcludeFromMigrations());
        builder.HasKey(p => p.PresupuestoId);
        builder.Property(p => p.PresupuestoId).HasColumnName("presupuesto_id").ValueGeneratedNever();
        builder.Property(p => p.FechaInicio).HasColumnName("fecha_inicio");
        builder.Property(p => p.Escala).HasColumnName("escala").HasMaxLength(12);
        builder.Property(p => p.CreadoEn).HasColumnName("creado_en");
        builder.Property(p => p.ActualizadoEn).HasColumnName("actualizado_en");

        builder.HasOne<Presupuesto>().WithOne().HasForeignKey<ProgramacionObra>(p => p.PresupuestoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(p => p.Tareas).WithOne().HasForeignKey(t => t.PresupuestoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(p => p.Vinculos).WithOne().HasForeignKey(v => v.PresupuestoId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Tareas).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(p => p.Vinculos).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ProgramacionTareaConfiguration : IEntityTypeConfiguration<ProgramacionTarea>
{
    public const int TextoMaxLength = 200;

    public void Configure(EntityTypeBuilder<ProgramacionTarea> builder)
    {
        builder.ToTable("programacion_tarea", PresupuestosModulo.Schema, t => t.ExcludeFromMigrations());
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(t => t.PresupuestoId).HasColumnName("presupuesto_id");
        builder.Property(t => t.ParentId).HasColumnName("parent_id");
        builder.Property(t => t.Orden).HasColumnName("orden");
        builder.Property(t => t.Tipo).HasColumnName("tipo").HasMaxLength(10);
        builder.Property(t => t.Texto).HasColumnName("texto").HasMaxLength(TextoMaxLength).IsRequired();
        builder.Property(t => t.Referencia).HasColumnName("referencia").HasMaxLength(20);
        builder.Property(t => t.FechaInicio).HasColumnName("fecha_inicio");
        builder.Property(t => t.Duracion).HasColumnName("duracion");
        builder.Property(t => t.Progreso).HasColumnName("progreso").HasPrecision(5, 4);
        builder.Property(t => t.Responsable).HasColumnName("responsable").HasMaxLength(120);
        builder.Property(t => t.Color).HasColumnName("color").HasMaxLength(7).IsFixedLength();
        builder.Property(t => t.Notas).HasColumnName("notas").HasMaxLength(1000);
        builder.Property(t => t.Valor).HasColumnName("valor").HasPrecision(18, 2);
        builder.Property(t => t.LineaBaseInicio).HasColumnName("linea_base_inicio");
        builder.Property(t => t.LineaBaseFin).HasColumnName("linea_base_fin");
        builder.Property(t => t.Abierta).HasColumnName("abierta");
        builder.Ignore(t => t.FechaFin);
    }
}

internal sealed class ProgramacionVinculoConfiguration : IEntityTypeConfiguration<ProgramacionVinculo>
{
    public void Configure(EntityTypeBuilder<ProgramacionVinculo> builder)
    {
        builder.ToTable("programacion_vinculo", PresupuestosModulo.Schema, t => t.ExcludeFromMigrations());
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(v => v.PresupuestoId).HasColumnName("presupuesto_id");
        builder.Property(v => v.OrigenId).HasColumnName("origen_id");
        builder.Property(v => v.DestinoId).HasColumnName("destino_id");
        builder.Property(v => v.Tipo).HasColumnName("tipo");
        builder.Property(v => v.Desfase).HasColumnName("desfase");

        // Mapear las FK hacia las actividades hace que EF inserte las actividades antes que sus vínculos.
        builder.HasOne<ProgramacionTarea>().WithMany().HasForeignKey(v => v.OrigenId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ProgramacionTarea>().WithMany().HasForeignKey(v => v.DestinoId).OnDelete(DeleteBehavior.Cascade);
    }
}
