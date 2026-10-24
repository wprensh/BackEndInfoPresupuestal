using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfoPresupuestal.Api.Features.Administracion.Empresas;

internal sealed class EmpresaConfiguration : IEntityTypeConfiguration<Empresa>
{
    public const int RazonSocialMaxLength = 200;

    public void Configure(EntityTypeBuilder<Empresa> builder)
    {
        builder.ToTable("empresa", AdministracionApi.Schema);

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
        builder.Property(e => e.RazonSocial).HasColumnName("razon_social").HasMaxLength(RazonSocialMaxLength).IsRequired();
        builder.Property(e => e.Nit).HasColumnName("nit").HasMaxLength(20);
        builder.Property(e => e.Email).HasColumnName("email").HasMaxLength(200);
        builder.Property(e => e.Telefono).HasColumnName("telefono").HasMaxLength(30);
        builder.Property(e => e.Activo).HasColumnName("activo");
        builder.Property(e => e.CreadoEn).HasColumnName("creado_en");
        builder.Ignore(e => e.EsPlataforma);

        builder.HasIndex(e => e.TenantId).IsUnique();
    }
}
