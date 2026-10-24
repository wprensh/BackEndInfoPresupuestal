using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfoPresupuestal.Api.Features.Administracion.Roles;

internal sealed class RolConfiguration : IEntityTypeConfiguration<Rol>
{
    public const int NombreMaxLength = 60;
    public const int DescripcionMaxLength = 300;

    public void Configure(EntityTypeBuilder<Rol> builder)
    {
        builder.ToTable("rol", AdministracionApi.Schema);

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(r => r.Nombre).HasColumnName("nombre").HasMaxLength(NombreMaxLength).IsRequired();
        builder.Property(r => r.Descripcion).HasColumnName("descripcion").HasMaxLength(DescripcionMaxLength);
        builder.Property(r => r.EsSistema).HasColumnName("es_sistema");
        builder.Property(r => r.CreadoEn).HasColumnName("creado_en");
        builder.HasIndex(r => new { r.TenantId, r.Nombre }).IsUnique();

        builder.OwnsMany(r => r.Permisos, permiso =>
        {
            permiso.ToTable("rol_permiso", AdministracionApi.Schema);
            permiso.WithOwner().HasForeignKey("RolId");
            permiso.Property<Guid>("RolId").HasColumnName("rol_id");
            permiso.HasKey(p => p.Id);
            permiso.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
            permiso.Property(p => p.Modulo).HasColumnName("modulo").HasMaxLength(50).IsRequired();
            permiso.Property(p => p.Ver).HasColumnName("ver");
            permiso.Property(p => p.Insertar).HasColumnName("insertar");
            permiso.Property(p => p.Editar).HasColumnName("editar");
            permiso.Property(p => p.Eliminar).HasColumnName("eliminar");
            permiso.HasIndex("RolId", nameof(RolPermiso.Modulo)).IsUnique();
        });
        builder.Navigation(r => r.Permisos).HasField("_permisos").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
