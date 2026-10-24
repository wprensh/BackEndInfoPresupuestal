using InfoPresupuestal.Api.Common.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfoPresupuestal.Api.Features.Mantenimiento.Clasificaciones;

/// <summary>Agrupación de insumos de una empresa (ELECTRICOS, HIERROS...).</summary>
public sealed class Clasificacion : ITenantEntity
{
    private Clasificacion()
    {
        // Requerido por EF Core.
    }

    public int Id { get; private set; }

    public string TenantId { get; private set; } = string.Empty;

    public string Nombre { get; private set; } = string.Empty;

    public bool Activo { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public static Clasificacion Create(string nombre, DateTimeOffset now) => new() { Nombre = Normalize(nombre), Activo = true, CreadoEn = now };

    public void Update(string nombre, bool activo)
    {
        Nombre = Normalize(nombre);
        Activo = activo;
    }

    /// <summary>Igual que la carga del Excel: mayúsculas y espacios simples.</summary>
    public static string Normalize(string nombre) => string.Join(' ', nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
}

internal sealed class ClasificacionConfiguration : IEntityTypeConfiguration<Clasificacion>
{
    public const int NombreMaxLength = 60;

    public void Configure(EntityTypeBuilder<Clasificacion> builder)
    {
        builder.ToTable("clasificacion", MantenimientoApi.Schema, t => t.ExcludeFromMigrations());
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        builder.Property(c => c.Nombre).HasColumnName("nombre").HasMaxLength(NombreMaxLength).IsRequired();
        builder.Property(c => c.Activo).HasColumnName("activo");
        builder.Property(c => c.CreadoEn).HasColumnName("creado_en");
    }
}
