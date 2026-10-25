using InfoPresupuestal.Api.Common.MultiTenancy;
using InfoPresupuestal.Api.Features.Mantenimiento.Insumos;
using InfoPresupuestal.Api.Features.Mantenimiento.TiposInsumo;
using InfoPresupuestal.Api.Features.Mantenimiento.UnidadesMedida;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfoPresupuestal.Api.Features.Proveedores.Catalogo;

/// <summary>Producto que vende un proveedor, con su propio código. Sus precios viven en <see cref="ProveedorPrecio"/>.</summary>
public sealed class ProveedorProducto : ITenantEntity
{
    private ProveedorProducto()
    {
        // Requerido por EF Core.
    }

    public Guid Id { get; private set; }

    public string TenantId { get; private set; } = string.Empty;

    /// <summary>Código del proveedor (SKU). Único por proveedor.</summary>
    public string Codigo { get; private set; } = string.Empty;

    public string Descripcion { get; private set; } = string.Empty;

    public short TipoInsumoId { get; private set; }

    public TipoInsumo? TipoInsumo { get; private set; }

    public short UnidadMedidaId { get; private set; }

    public UnidadMedida? UnidadMedida { get; private set; }

    public string? Marca { get; private set; }

    public string? Categoria { get; private set; }

    public bool Activo { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public DateTimeOffset ActualizadoEn { get; private set; }

    public static ProveedorProducto Create(
        string codigo,
        string descripcion,
        short tipoInsumoId,
        short unidadMedidaId,
        string? marca,
        string? categoria,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(now),
            Codigo = NormalizeCodigo(codigo),
            Descripcion = Insumo.NormalizeDescripcion(descripcion),
            TipoInsumoId = tipoInsumoId,
            UnidadMedidaId = unidadMedidaId,
            Marca = Clean(marca),
            Categoria = Clean(categoria),
            Activo = true,
            CreadoEn = now,
            ActualizadoEn = now,
        };

    public void Update(string codigo, string descripcion, short tipoInsumoId, short unidadMedidaId, string? marca, string? categoria, bool activo, DateTimeOffset now)
    {
        Codigo = NormalizeCodigo(codigo);
        Descripcion = Insumo.NormalizeDescripcion(descripcion);
        TipoInsumoId = tipoInsumoId;
        UnidadMedidaId = unidadMedidaId;
        Marca = Clean(marca);
        Categoria = Clean(categoria);
        Activo = activo;
        ActualizadoEn = now;
    }

    public static string NormalizeCodigo(string codigo) => codigo.Trim().ToUpperInvariant();

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : Insumo.NormalizeDescripcion(value);
}

internal sealed class ProveedorProductoConfiguration : IEntityTypeConfiguration<ProveedorProducto>
{
    public const int CodigoMaxLength = 40;
    public const int DescripcionMaxLength = 200;
    public const int TextoCortoMaxLength = 80;

    public void Configure(EntityTypeBuilder<ProveedorProducto> builder)
    {
        builder.ToTable("proveedor_producto", ProveedoresApi.Schema, t => t.ExcludeFromMigrations());
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(p => p.Codigo).HasColumnName("codigo").HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(p => p.Descripcion).HasColumnName("descripcion").HasMaxLength(DescripcionMaxLength).IsRequired();
        builder.Property(p => p.TipoInsumoId).HasColumnName("tipo_insumo_id");
        builder.Property(p => p.UnidadMedidaId).HasColumnName("unidad_medida_id");
        builder.Property(p => p.Marca).HasColumnName("marca").HasMaxLength(TextoCortoMaxLength);
        builder.Property(p => p.Categoria).HasColumnName("categoria").HasMaxLength(TextoCortoMaxLength);
        builder.Property(p => p.Activo).HasColumnName("activo");
        builder.Property(p => p.CreadoEn).HasColumnName("creado_en");
        builder.Property(p => p.ActualizadoEn).HasColumnName("actualizado_en");

        builder.HasOne(p => p.TipoInsumo).WithMany().HasForeignKey(p => p.TipoInsumoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.UnidadMedida).WithMany().HasForeignKey(p => p.UnidadMedidaId).OnDelete(DeleteBehavior.Restrict);
    }
}
