using System.Globalization;
using InfoPresupuestal.Api.Common.MultiTenancy;
using InfoPresupuestal.Api.Features.Mantenimiento.Clasificaciones;
using InfoPresupuestal.Api.Features.Mantenimiento.TiposInsumo;
using InfoPresupuestal.Api.Features.Mantenimiento.UnidadesMedida;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfoPresupuestal.Api.Features.Mantenimiento.Insumos;

/// <summary>Material, equipo o mano de obra de una empresa. El precio vive en su historial (<see cref="InsumoPrecio"/>).</summary>
public sealed class Insumo : ITenantEntity
{
    private Insumo()
    {
        // Requerido por EF Core.
    }

    public Guid Id { get; private set; }

    public string TenantId { get; private set; } = string.Empty;

    /// <summary>Código legible por empresa (MAT-0001). Se asigna al crear y no cambia.</summary>
    public string Codigo { get; private set; } = string.Empty;

    public short TipoInsumoId { get; private set; }

    public TipoInsumo? TipoInsumo { get; private set; }

    public int? ClasificacionId { get; private set; }

    public Clasificacion? Clasificacion { get; private set; }

    public short UnidadMedidaId { get; private set; }

    public UnidadMedida? UnidadMedida { get; private set; }

    public string Descripcion { get; private set; } = string.Empty;

    public bool Activo { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public DateTimeOffset ActualizadoEn { get; private set; }

    public static Insumo Create(string codigo, short tipoInsumoId, int? clasificacionId, short unidadMedidaId, string descripcion, DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(now),
            Codigo = codigo,
            TipoInsumoId = tipoInsumoId,
            ClasificacionId = clasificacionId,
            UnidadMedidaId = unidadMedidaId,
            Descripcion = NormalizeDescripcion(descripcion),
            Activo = true,
            CreadoEn = now,
            ActualizadoEn = now,
        };

    public void Update(int? clasificacionId, short unidadMedidaId, string descripcion, bool activo, DateTimeOffset now)
    {
        ClasificacionId = clasificacionId;
        UnidadMedidaId = unidadMedidaId;
        Descripcion = NormalizeDescripcion(descripcion);
        Activo = activo;
        ActualizadoEn = now;
    }

    /// <summary>Igual que la carga del Excel: sin espacios sobrantes (la base lo exige con un CHECK).</summary>
    public static string NormalizeDescripcion(string descripcion) => string.Join(' ', descripcion.Split(' ', StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Siguiente código de un tipo: <c>MAT-0041</c> después de <c>MAT-0040</c>.</summary>
    public static string SiguienteCodigo(string prefijo, string? ultimoCodigo)
    {
        var siguiente = 1;
        if (ultimoCodigo is not null && int.TryParse(ultimoCodigo.AsSpan(prefijo.Length + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var ultimo))
        {
            siguiente = ultimo + 1;
        }

        return $"{prefijo}-{siguiente.ToString("0000", CultureInfo.InvariantCulture)}";
    }
}

internal sealed class InsumoConfiguration : IEntityTypeConfiguration<Insumo>
{
    public const int DescripcionMaxLength = 200;

    public void Configure(EntityTypeBuilder<Insumo> builder)
    {
        builder.ToTable("insumo", MantenimientoApi.Schema, t => t.ExcludeFromMigrations());
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(i => i.Codigo).HasColumnName("codigo").HasMaxLength(20).IsRequired();
        builder.Property(i => i.TipoInsumoId).HasColumnName("tipo_insumo_id");
        builder.Property(i => i.ClasificacionId).HasColumnName("clasificacion_id");
        builder.Property(i => i.UnidadMedidaId).HasColumnName("unidad_medida_id");
        builder.Property(i => i.Descripcion).HasColumnName("descripcion").HasMaxLength(DescripcionMaxLength).IsRequired();
        builder.Property(i => i.Activo).HasColumnName("activo");
        builder.Property(i => i.CreadoEn).HasColumnName("creado_en");
        builder.Property(i => i.ActualizadoEn).HasColumnName("actualizado_en");

        builder.HasOne(i => i.TipoInsumo).WithMany().HasForeignKey(i => i.TipoInsumoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.UnidadMedida).WithMany().HasForeignKey(i => i.UnidadMedidaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.Clasificacion).WithMany().HasForeignKey(i => i.ClasificacionId).OnDelete(DeleteBehavior.Restrict);
    }
}
