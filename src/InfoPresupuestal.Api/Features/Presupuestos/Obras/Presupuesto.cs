using InfoPresupuestal.Api.Common.MultiTenancy;
using InfoPresupuestal.Api.Features.Mantenimiento.Insumos;
using InfoPresupuestal.Api.Features.Mantenimiento.UnidadesMedida;
using InfoPresupuestal.Api.Features.Presupuestos.Apus;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfoPresupuestal.Api.Features.Presupuestos.Obras;

public static class EstadoPresupuesto
{
    public const string Borrador = "BORRADOR";
    public const string Aprobado = "APROBADO";
    public const string Anulado = "ANULADO";

    public static readonly IReadOnlyList<string> Todos = [Borrador, Aprobado, Anulado];
}

/// <summary>Porcentajes del presupuesto como fracción (0.09 = 9 %).</summary>
public sealed record PorcentajesAiu(decimal Administracion, decimal Imprevistos, decimal Utilidad, decimal IvaUtilidad)
{
    /// <summary>Valores de la plantilla FORMATOS del Excel, con el IVA vigente en Colombia (19 %).</summary>
    public static readonly PorcentajesAiu PorDefecto = new(0.09m, 0.03m, 0.08m, 0.19m);
}

/// <summary>Datos del encabezado que se pueden editar.</summary>
public sealed record EncabezadoPresupuesto(
    string NombreObra,
    string? Cliente,
    string? Ubicacion,
    DateOnly Fecha,
    string? PropuestaNumero,
    PorcentajesAiu Aiu,
    int? DuracionDias,
    decimal? AnticipoPct,
    string? Observaciones);

/// <summary>Presupuesto de obra (hoja PRESUPUESTO): capítulos con ítems y costos indirectos AIU.</summary>
public sealed class Presupuesto : ITenantEntity
{
    public const string Prefijo = "PRE";
    public const string MonedaPorDefecto = "COP";

    private readonly List<PresupuestoCapitulo> _capitulos = [];

    private Presupuesto()
    {
        // Requerido por EF Core.
    }

    public Guid Id { get; private set; }

    public string TenantId { get; private set; } = string.Empty;

    public string Codigo { get; private set; } = string.Empty;

    public string NombreObra { get; private set; } = string.Empty;

    public string? Cliente { get; private set; }

    public string? Ubicacion { get; private set; }

    public DateOnly Fecha { get; private set; }

    public string? PropuestaNumero { get; private set; }

    public string Estado { get; private set; } = EstadoPresupuesto.Borrador;

    public string Moneda { get; private set; } = MonedaPorDefecto;

    public decimal AdministracionPct { get; private set; }

    public decimal ImprevistosPct { get; private set; }

    public decimal UtilidadPct { get; private set; }

    public decimal IvaUtilidadPct { get; private set; }

    public int? DuracionDias { get; private set; }

    public decimal? AnticipoPct { get; private set; }

    public string? Observaciones { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public DateTimeOffset ActualizadoEn { get; private set; }

    public IReadOnlyList<PresupuestoCapitulo> Capitulos => _capitulos;

    public bool EsEditable => Estado == EstadoPresupuesto.Borrador;

    public PorcentajesAiu Aiu => new(AdministracionPct, ImprevistosPct, UtilidadPct, IvaUtilidadPct);

    public static Presupuesto Create(string codigo, EncabezadoPresupuesto encabezado, IEnumerable<PresupuestoCapitulo> capitulos, DateTimeOffset now)
    {
        var presupuesto = new Presupuesto { Id = Guid.CreateVersion7(now), Codigo = codigo, CreadoEn = now };
        presupuesto.Update(encabezado, capitulos, now);
        return presupuesto;
    }

    /// <summary>Reemplaza encabezado, capítulos e ítems. La numeración sale del orden recibido.</summary>
    public void Update(EncabezadoPresupuesto e, IEnumerable<PresupuestoCapitulo> capitulos, DateTimeOffset now)
    {
        NombreObra = Insumo.NormalizeDescripcion(e.NombreObra);
        Cliente = Limpiar(e.Cliente);
        Ubicacion = Limpiar(e.Ubicacion);
        Fecha = e.Fecha;
        PropuestaNumero = Limpiar(e.PropuestaNumero);
        AdministracionPct = e.Aiu.Administracion;
        ImprevistosPct = e.Aiu.Imprevistos;
        UtilidadPct = e.Aiu.Utilidad;
        IvaUtilidadPct = e.Aiu.IvaUtilidad;
        DuracionDias = e.DuracionDias;
        AnticipoPct = e.AnticipoPct;
        Observaciones = Limpiar(e.Observaciones);
        ActualizadoEn = now;
        _capitulos.Clear();
        _capitulos.AddRange(capitulos);
    }

    public void CambiarEstado(string estado, DateTimeOffset now)
    {
        Estado = estado;
        ActualizadoEn = now;
    }

    public void Tocar(DateTimeOffset now) => ActualizadoEn = now;

    private static string? Limpiar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

public sealed class PresupuestoCapitulo : ITenantEntity
{
    private readonly List<PresupuestoItem> _items = [];

    private PresupuestoCapitulo()
    {
        // Requerido por EF Core.
    }

    public long Id { get; private set; }

    public string TenantId { get; private set; } = string.Empty;

    public Guid PresupuestoId { get; private set; }

    public short Numero { get; private set; }

    public string Nombre { get; private set; } = string.Empty;

    public IReadOnlyList<PresupuestoItem> Items => _items;

    public static PresupuestoCapitulo Create(short numero, string nombre, IEnumerable<PresupuestoItem> items)
    {
        var capitulo = new PresupuestoCapitulo { Numero = numero, Nombre = Insumo.NormalizeDescripcion(nombre) };
        capitulo._items.AddRange(items);
        return capitulo;
    }
}

/// <summary>Ítem del presupuesto. Su consecutivo visible es <c>capítulo.número</c> (1.1, 1.2...).</summary>
public sealed class PresupuestoItem : ITenantEntity
{
    private PresupuestoItem()
    {
        // Requerido por EF Core.
    }

    public long Id { get; private set; }

    public string TenantId { get; private set; } = string.Empty;

    public long CapituloId { get; private set; }

    public short Numero { get; private set; }

    public Guid? ApuId { get; private set; }

    public Apu? Apu { get; private set; }

    public string Descripcion { get; private set; } = string.Empty;

    public short UnidadMedidaId { get; private set; }

    public UnidadMedida? UnidadMedida { get; private set; }

    public decimal Cantidad { get; private set; }

    /// <summary>Costo directo del APU copiado al agregar el ítem (o valor manual si no viene de un APU).</summary>
    public decimal ValorUnitario { get; private set; }

    public decimal ValorTotal => PresupuestoCalculo.ValorItem(Cantidad, ValorUnitario);

    public static PresupuestoItem Create(short numero, Guid? apuId, string descripcion, short unidadMedidaId, decimal cantidad, decimal valorUnitario) =>
        new()
        {
            Numero = numero,
            ApuId = apuId,
            Descripcion = Insumo.NormalizeDescripcion(descripcion),
            UnidadMedidaId = unidadMedidaId,
            Cantidad = decimal.Round(cantidad, 4),
            ValorUnitario = decimal.Round(valorUnitario, 2, MidpointRounding.AwayFromZero),
        };

    public void ActualizarValorUnitario(decimal valorUnitario) => ValorUnitario = decimal.Round(valorUnitario, 2, MidpointRounding.AwayFromZero);

    public PresupuestoItem Copiar() => Create(Numero, ApuId, Descripcion, UnidadMedidaId, Cantidad, ValorUnitario);
}

/// <summary>Totales del presupuesto con las fórmulas de la hoja PRESUPUESTO.</summary>
public sealed record PresupuestoTotales(
    decimal CostoDirecto,
    decimal Administracion,
    decimal Imprevistos,
    decimal Utilidad,
    decimal TotalAiu,
    decimal IvaUtilidad,
    decimal CostosIndirectos,
    decimal ValorTotal,
    decimal? Anticipo);

public static class PresupuestoCalculo
{
    /// <summary>La base calcula lo mismo en la columna generada <c>presupuesto_item.valor_total</c>.</summary>
    public static decimal ValorItem(decimal cantidad, decimal valorUnitario) => decimal.Round(cantidad * valorUnitario, 2, MidpointRounding.AwayFromZero);

    /// <summary>AIU sobre el costo directo, IVA sobre la utilidad y total truncado a pesos, como el Excel.</summary>
    public static PresupuestoTotales Totales(decimal costoDirecto, PorcentajesAiu aiu, decimal? anticipoPct)
    {
        static decimal R(decimal v) => decimal.Round(v, 2, MidpointRounding.AwayFromZero);
        var administracion = R(costoDirecto * aiu.Administracion);
        var imprevistos = R(costoDirecto * aiu.Imprevistos);
        var utilidad = R(costoDirecto * aiu.Utilidad);
        var iva = R(utilidad * aiu.IvaUtilidad);
        var indirectos = administracion + imprevistos + utilidad + iva;
        var total = decimal.Truncate(costoDirecto + indirectos);
        return new PresupuestoTotales(
            costoDirecto, administracion, imprevistos, utilidad, administracion + imprevistos + utilidad, iva, indirectos, total,
            anticipoPct is { } pct ? R(total * pct) : null);
    }
}

internal sealed class PresupuestoConfiguration : IEntityTypeConfiguration<Presupuesto>
{
    public const int TextoMaxLength = 200;
    public const int ObservacionesMaxLength = 1000;

    public void Configure(EntityTypeBuilder<Presupuesto> builder)
    {
        builder.ToTable("presupuesto", PresupuestosModulo.Schema, t => t.ExcludeFromMigrations());
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(p => p.Codigo).HasColumnName("codigo").HasMaxLength(20).IsRequired();
        builder.Property(p => p.NombreObra).HasColumnName("nombre_obra").HasMaxLength(TextoMaxLength).IsRequired();
        builder.Property(p => p.Cliente).HasColumnName("cliente").HasMaxLength(TextoMaxLength);
        builder.Property(p => p.Ubicacion).HasColumnName("ubicacion").HasMaxLength(TextoMaxLength);
        builder.Property(p => p.Fecha).HasColumnName("fecha");
        builder.Property(p => p.PropuestaNumero).HasColumnName("propuesta_numero").HasMaxLength(30);
        builder.Property(p => p.Estado).HasColumnName("estado").HasMaxLength(12).IsRequired();
        builder.Property(p => p.Moneda).HasColumnName("moneda").HasMaxLength(3).IsFixedLength();
        builder.Property(p => p.AdministracionPct).HasColumnName("administracion_pct").HasPrecision(6, 4);
        builder.Property(p => p.ImprevistosPct).HasColumnName("imprevistos_pct").HasPrecision(6, 4);
        builder.Property(p => p.UtilidadPct).HasColumnName("utilidad_pct").HasPrecision(6, 4);
        builder.Property(p => p.IvaUtilidadPct).HasColumnName("iva_utilidad_pct").HasPrecision(6, 4);
        builder.Property(p => p.DuracionDias).HasColumnName("duracion_dias");
        builder.Property(p => p.AnticipoPct).HasColumnName("anticipo_pct").HasPrecision(6, 4);
        builder.Property(p => p.Observaciones).HasColumnName("observaciones").HasMaxLength(ObservacionesMaxLength);
        builder.Property(p => p.CreadoEn).HasColumnName("creado_en");
        builder.Property(p => p.ActualizadoEn).HasColumnName("actualizado_en");
        builder.Ignore(p => p.Aiu);
        builder.Ignore(p => p.EsEditable);

        builder.HasMany(p => p.Capitulos).WithOne().HasForeignKey(c => c.PresupuestoId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Capitulos).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class PresupuestoCapituloConfiguration : IEntityTypeConfiguration<PresupuestoCapitulo>
{
    public void Configure(EntityTypeBuilder<PresupuestoCapitulo> builder)
    {
        builder.ToTable("presupuesto_capitulo", PresupuestosModulo.Schema, t => t.ExcludeFromMigrations());
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        builder.Property(c => c.PresupuestoId).HasColumnName("presupuesto_id");
        builder.Property(c => c.Numero).HasColumnName("numero");
        builder.Property(c => c.Nombre).HasColumnName("nombre").HasMaxLength(PresupuestoConfiguration.TextoMaxLength).IsRequired();

        builder.HasMany(c => c.Items).WithOne().HasForeignKey(i => i.CapituloId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(c => new { c.PresupuestoId, c.Numero }).IsUnique();
    }
}

internal sealed class PresupuestoItemConfiguration : IEntityTypeConfiguration<PresupuestoItem>
{
    public void Configure(EntityTypeBuilder<PresupuestoItem> builder)
    {
        // valor_total es una columna generada de la base; la API usa PresupuestoCalculo con la misma fórmula.
        builder.ToTable("presupuesto_item", PresupuestosModulo.Schema, t => t.ExcludeFromMigrations());
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        builder.Property(i => i.CapituloId).HasColumnName("capitulo_id");
        builder.Property(i => i.Numero).HasColumnName("numero");
        builder.Property(i => i.ApuId).HasColumnName("apu_id");
        builder.Property(i => i.Descripcion).HasColumnName("descripcion").HasMaxLength(PresupuestoConfiguration.TextoMaxLength).IsRequired();
        builder.Property(i => i.UnidadMedidaId).HasColumnName("unidad_medida_id");
        builder.Property(i => i.Cantidad).HasColumnName("cantidad").HasPrecision(18, 4);
        builder.Property(i => i.ValorUnitario).HasColumnName("valor_unitario").HasPrecision(18, 2);
        builder.Ignore(i => i.ValorTotal);

        builder.HasOne(i => i.Apu).WithMany().HasForeignKey(i => i.ApuId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.UnidadMedida).WithMany().HasForeignKey(i => i.UnidadMedidaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(i => new { i.CapituloId, i.Numero }).IsUnique();
    }
}
