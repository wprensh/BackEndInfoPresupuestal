using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfoPresupuestal.Api.Features.Mantenimiento.TiposInsumo;

/// <summary>Material, equipo o mano de obra. Catálogo global; el prefijo arma el código del insumo (MAT-0001).</summary>
public sealed class TipoInsumo
{
    private TipoInsumo()
    {
        // Requerido por EF Core.
    }

    /// <summary>La tabla no tiene identidad: el id lo asigna la aplicación (máximo + 1).</summary>
    public short Id { get; private set; }

    public string Codigo { get; private set; } = string.Empty;

    public string Nombre { get; private set; } = string.Empty;

    public string Prefijo { get; private set; } = string.Empty;

    public static TipoInsumo Create(short id, string codigo, string nombre, string prefijo) =>
        new() { Id = id, Codigo = Normalize(codigo), Nombre = nombre.Trim(), Prefijo = Normalize(prefijo) };

    /// <summary>El prefijo no cambia: los códigos de los insumos ya creados dependen de él.</summary>
    public void Update(string codigo, string nombre)
    {
        Codigo = Normalize(codigo);
        Nombre = nombre.Trim();
    }

    public static string Normalize(string value) => value.Trim().ToUpperInvariant();
}

internal sealed class TipoInsumoConfiguration : IEntityTypeConfiguration<TipoInsumo>
{
    public const int CodigoMaxLength = 10;
    public const int NombreMaxLength = 50;
    public const int PrefijoMaxLength = 5;

    public void Configure(EntityTypeBuilder<TipoInsumo> builder)
    {
        builder.ToTable("tipo_insumo", MantenimientoApi.Schema, t => t.ExcludeFromMigrations());
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(t => t.Codigo).HasColumnName("codigo").HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(t => t.Nombre).HasColumnName("nombre").HasMaxLength(NombreMaxLength).IsRequired();
        builder.Property(t => t.Prefijo).HasColumnName("prefijo").HasMaxLength(PrefijoMaxLength).IsRequired();
        builder.HasIndex(t => t.Codigo).IsUnique();
        builder.HasIndex(t => t.Nombre).IsUnique();
        builder.HasIndex(t => t.Prefijo).IsUnique();
    }
}
