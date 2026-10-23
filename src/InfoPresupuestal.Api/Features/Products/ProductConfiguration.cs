using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfoPresupuestal.Api.Features.Products;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public const int NameMaxLength = 200;
    public const int DescriptionMaxLength = 1000;

    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(p => p.Name).HasColumnName("name").HasMaxLength(NameMaxLength).IsRequired();
        builder.Property(p => p.Description).HasColumnName("description").HasMaxLength(DescriptionMaxLength);
        builder.Property(p => p.Price).HasColumnName("price").HasPrecision(18, 2);
        builder.Property(p => p.Stock).HasColumnName("stock");
        builder.Property(p => p.CreatedAtUtc).HasColumnName("created_at_utc");

        // El nombre es único dentro de cada tenant, no en toda la base.
        builder.HasIndex(p => new { p.TenantId, p.Name }).IsUnique();
    }
}
