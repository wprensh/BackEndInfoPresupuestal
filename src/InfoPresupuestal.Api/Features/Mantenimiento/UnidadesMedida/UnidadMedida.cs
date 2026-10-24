using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfoPresupuestal.Api.Features.Mantenimiento.UnidadesMedida;

/// <summary>Catálogo global (UN, ML, M2, HR...): lo ven todas las empresas y solo lo cambia el superadministrador.</summary>
public sealed class UnidadMedida
{
    private UnidadMedida()
    {
        // Requerido por EF Core.
    }

    public short Id { get; private set; }

    public string Codigo { get; private set; } = string.Empty;

    public string Nombre { get; private set; } = string.Empty;

    public static UnidadMedida Create(string codigo, string nombre) => new() { Codigo = NormalizeCodigo(codigo), Nombre = nombre.Trim() };

    public void Update(string codigo, string nombre)
    {
        Codigo = NormalizeCodigo(codigo);
        Nombre = nombre.Trim();
    }

    public static string NormalizeCodigo(string codigo) => codigo.Trim().ToUpperInvariant();
}

internal sealed class UnidadMedidaConfiguration : IEntityTypeConfiguration<UnidadMedida>
{
    public const int CodigoMaxLength = 10;
    public const int NombreMaxLength = 50;

    public void Configure(EntityTypeBuilder<UnidadMedida> builder)
    {
        builder.ToTable("unidad_medida", MantenimientoApi.Schema, t => t.ExcludeFromMigrations());
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        builder.Property(u => u.Codigo).HasColumnName("codigo").HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(u => u.Nombre).HasColumnName("nombre").HasMaxLength(NombreMaxLength).IsRequired();
        builder.HasIndex(u => u.Codigo).IsUnique();
    }
}
