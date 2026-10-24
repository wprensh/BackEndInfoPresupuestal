using InfoPresupuestal.Api.Features.Administracion.Roles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfoPresupuestal.Api.Features.Administracion.Usuarios;

internal sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public const int EmailMaxLength = 200;
    public const int NombreMaxLength = 150;

    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuario", AdministracionApi.Schema);

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(u => u.Email).HasColumnName("email").HasMaxLength(EmailMaxLength).IsRequired();
        builder.Property(u => u.Nombre).HasColumnName("nombre").HasMaxLength(NombreMaxLength).IsRequired();
        builder.Property(u => u.PasswordHash).HasColumnName("password_hash").HasMaxLength(500).IsRequired();
        builder.Property(u => u.RolId).HasColumnName("rol_id");
        builder.Property(u => u.EsSuperAdmin).HasColumnName("es_super_admin");
        builder.Property(u => u.Activo).HasColumnName("activo");
        builder.Property(u => u.UltimoAcceso).HasColumnName("ultimo_acceso");
        builder.Property(u => u.CreadoEn).HasColumnName("creado_en");

        builder.HasIndex(u => new { u.TenantId, u.Email }).IsUnique();
        builder.HasOne(u => u.Rol).WithMany().HasForeignKey(u => u.RolId).OnDelete(DeleteBehavior.Restrict);
    }
}
