using InfoPresupuestal.Api.Common.MultiTenancy;
using InfoPresupuestal.Api.Features.Mantenimiento.Insumos;
using InfoPresupuestal.Api.Features.Mantenimiento.UnidadesMedida;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfoPresupuestal.Api.Features.Presupuestos.Apus;

/// <summary>Análisis de precios unitarios: costo directo de una actividad por unidad. Se reutiliza en presupuestos.</summary>
public sealed class Apu : ITenantEntity
{
    public const string Prefijo = "APU";

    private readonly List<ApuDetalle> _detalles = [];

    private Apu()
    {
        // Requerido por EF Core.
    }

    public Guid Id { get; private set; }

    public string TenantId { get; private set; } = string.Empty;

    public string Codigo { get; private set; } = string.Empty;

    public string Descripcion { get; private set; } = string.Empty;

    public short UnidadMedidaId { get; private set; }

    public UnidadMedida? UnidadMedida { get; private set; }

    public DateOnly Fecha { get; private set; }

    public string? Observaciones { get; private set; }

    public bool Activo { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public DateTimeOffset ActualizadoEn { get; private set; }

    public IReadOnlyList<ApuDetalle> Detalles => _detalles;

    public static Apu Create(string codigo, string descripcion, short unidadMedidaId, DateOnly fecha, string? observaciones, IEnumerable<ApuDetalle> detalles, DateTimeOffset now)
    {
        var apu = new Apu { Id = Guid.CreateVersion7(now), Codigo = codigo, Activo = true, CreadoEn = now };
        apu.Update(descripcion, unidadMedidaId, fecha, observaciones, activo: true, detalles, now);
        return apu;
    }

    /// <summary>Reemplaza el encabezado y todas las líneas: el APU se guarda completo, como en el Excel.</summary>
    public void Update(string descripcion, short unidadMedidaId, DateOnly fecha, string? observaciones, bool activo, IEnumerable<ApuDetalle> detalles, DateTimeOffset now)
    {
        Descripcion = Insumo.NormalizeDescripcion(descripcion);
        UnidadMedidaId = unidadMedidaId;
        Fecha = fecha;
        Observaciones = string.IsNullOrWhiteSpace(observaciones) ? null : observaciones.Trim();
        Activo = activo;
        ActualizadoEn = now;
        _detalles.Clear();
        _detalles.AddRange(detalles);
    }
}

/// <summary>Línea de material, equipo o mano de obra. Las columnas que no usa su tipo quedan en su valor neutro.</summary>
public sealed class ApuDetalle : ITenantEntity
{
    private ApuDetalle()
    {
        // Requerido por EF Core.
    }

    public long Id { get; private set; }

    public string TenantId { get; private set; } = string.Empty;

    public Guid ApuId { get; private set; }

    public short TipoInsumoId { get; private set; }

    public short Orden { get; private set; }

    public Guid? InsumoId { get; private set; }

    public string Descripcion { get; private set; } = string.Empty;

    public short? UnidadMedidaId { get; private set; }

    public UnidadMedida? UnidadMedida { get; private set; }

    /// <summary>Material: cantidad. Equipo: incidencia.</summary>
    public decimal Cantidad { get; private set; }

    /// <summary>Material y equipo: precio unitario. Mano de obra: valor del jornal.</summary>
    public decimal PrecioUnitario { get; private set; }

    /// <summary>Fracción 0–1 (0.05 = 5 %).</summary>
    public decimal Desperdicio { get; private set; }

    public decimal FactorPrestacional { get; private set; }

    /// <summary>Unidades de la actividad por día.</summary>
    public decimal Rendimiento { get; private set; }

    public static ApuDetalle Material(short orden, Guid? insumoId, string descripcion, short unidadMedidaId, decimal cantidad, decimal desperdicio, decimal precioUnitario) =>
        new()
        {
            TipoInsumoId = PresupuestosModulo.Material,
            Orden = orden,
            InsumoId = insumoId,
            Descripcion = Insumo.NormalizeDescripcion(descripcion),
            UnidadMedidaId = unidadMedidaId,
            Cantidad = cantidad,
            Desperdicio = desperdicio,
            PrecioUnitario = decimal.Round(precioUnitario, 2),
            FactorPrestacional = 1,
            Rendimiento = 1,
        };

    public static ApuDetalle Equipo(short orden, Guid? insumoId, string descripcion, short unidadMedidaId, decimal incidencia, decimal precioUnitario) =>
        new()
        {
            TipoInsumoId = PresupuestosModulo.Equipo,
            Orden = orden,
            InsumoId = insumoId,
            Descripcion = Insumo.NormalizeDescripcion(descripcion),
            UnidadMedidaId = unidadMedidaId,
            Cantidad = incidencia,
            PrecioUnitario = decimal.Round(precioUnitario, 2),
            FactorPrestacional = 1,
            Rendimiento = 1,
        };

    public static ApuDetalle ManoObra(short orden, Guid? insumoId, string descripcion, decimal jornal, decimal factorPrestacional, decimal rendimiento) =>
        new()
        {
            TipoInsumoId = PresupuestosModulo.ManoObra,
            Orden = orden,
            InsumoId = insumoId,
            Descripcion = Insumo.NormalizeDescripcion(descripcion),
            Cantidad = 1,
            PrecioUnitario = decimal.Round(jornal, 2),
            FactorPrestacional = factorPrestacional,
            Rendimiento = rendimiento,
        };

    /// <summary>Copia sin identidad, para duplicar un APU.</summary>
    public ApuDetalle Copiar() =>
        new()
        {
            TipoInsumoId = TipoInsumoId,
            Orden = Orden,
            InsumoId = InsumoId,
            Descripcion = Descripcion,
            UnidadMedidaId = UnidadMedidaId,
            Cantidad = Cantidad,
            PrecioUnitario = PrecioUnitario,
            Desperdicio = Desperdicio,
            FactorPrestacional = FactorPrestacional,
            Rendimiento = Rendimiento,
        };
}

/// <summary>Fórmulas de la hoja APU's. La base las repite en la columna generada <c>apu_detalle.valor_parcial</c>.</summary>
public static class ApuCalculo
{
    public static decimal ValorParcial(short tipoInsumoId, decimal cantidad, decimal precioUnitario, decimal desperdicio, decimal factorPrestacional, decimal rendimiento) =>
        tipoInsumoId switch
        {
            PresupuestosModulo.Material => cantidad * precioUnitario * (1 + desperdicio),
            PresupuestosModulo.Equipo => cantidad * precioUnitario,
            PresupuestosModulo.ManoObra => rendimiento > 0 ? precioUnitario * factorPrestacional / rendimiento : 0,
            _ => 0,
        };

    public static decimal ValorParcial(ApuDetalle d) =>
        ValorParcial(d.TipoInsumoId, d.Cantidad, d.PrecioUnitario, d.Desperdicio, d.FactorPrestacional, d.Rendimiento);
}

internal sealed class ApuConfiguration : IEntityTypeConfiguration<Apu>
{
    public const int DescripcionMaxLength = 200;
    public const int ObservacionesMaxLength = 500;

    public void Configure(EntityTypeBuilder<Apu> builder)
    {
        builder.ToTable("apu", PresupuestosModulo.Schema, t => t.ExcludeFromMigrations());
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(a => a.Codigo).HasColumnName("codigo").HasMaxLength(20).IsRequired();
        builder.Property(a => a.Descripcion).HasColumnName("descripcion").HasMaxLength(DescripcionMaxLength).IsRequired();
        builder.Property(a => a.UnidadMedidaId).HasColumnName("unidad_medida_id");
        builder.Property(a => a.Fecha).HasColumnName("fecha");
        builder.Property(a => a.Observaciones).HasColumnName("observaciones").HasMaxLength(ObservacionesMaxLength);
        builder.Property(a => a.Activo).HasColumnName("activo");
        builder.Property(a => a.CreadoEn).HasColumnName("creado_en");
        builder.Property(a => a.ActualizadoEn).HasColumnName("actualizado_en");

        builder.HasOne(a => a.UnidadMedida).WithMany().HasForeignKey(a => a.UnidadMedidaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(a => a.Detalles).WithOne().HasForeignKey(d => d.ApuId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(a => a.Detalles).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ApuDetalleConfiguration : IEntityTypeConfiguration<ApuDetalle>
{
    public void Configure(EntityTypeBuilder<ApuDetalle> builder)
    {
        // valor_parcial es una columna generada de la base; la API usa ApuCalculo con las mismas fórmulas.
        builder.ToTable("apu_detalle", PresupuestosModulo.Schema, t => t.ExcludeFromMigrations());
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        builder.Property(d => d.ApuId).HasColumnName("apu_id");
        builder.Property(d => d.TipoInsumoId).HasColumnName("tipo_insumo_id");
        builder.Property(d => d.Orden).HasColumnName("orden");
        builder.Property(d => d.InsumoId).HasColumnName("insumo_id");
        builder.Property(d => d.Descripcion).HasColumnName("descripcion").HasMaxLength(ApuConfiguration.DescripcionMaxLength).IsRequired();
        builder.Property(d => d.UnidadMedidaId).HasColumnName("unidad_medida_id");
        builder.Property(d => d.Cantidad).HasColumnName("cantidad").HasPrecision(18, 6);
        builder.Property(d => d.PrecioUnitario).HasColumnName("precio_unitario").HasPrecision(18, 2);
        builder.Property(d => d.Desperdicio).HasColumnName("desperdicio").HasPrecision(7, 4);
        builder.Property(d => d.FactorPrestacional).HasColumnName("factor_prestacional").HasPrecision(6, 4);
        builder.Property(d => d.Rendimiento).HasColumnName("rendimiento").HasPrecision(18, 6);

        builder.HasOne(d => d.UnidadMedida).WithMany().HasForeignKey(d => d.UnidadMedidaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Insumo>().WithMany().HasForeignKey(d => d.InsumoId).OnDelete(DeleteBehavior.SetNull);

        // Le dice a EF que borre las líneas viejas antes de insertar las nuevas al reemplazar un APU.
        builder.HasIndex(d => new { d.ApuId, d.TipoInsumoId, d.Orden }).IsUnique();
    }
}
