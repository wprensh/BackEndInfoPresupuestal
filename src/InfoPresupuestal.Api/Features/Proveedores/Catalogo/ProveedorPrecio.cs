using InfoPresupuestal.Api.Common.MultiTenancy;
using InfoPresupuestal.Api.Features.Mantenimiento.Insumos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfoPresupuestal.Api.Features.Proveedores.Catalogo;

/// <summary>
/// Precio de un producto del proveedor en una ciudad (null = todo el país) desde una fecha de vigencia.
/// El valor puede venir con o sin IVA; <see cref="ValorSinIva"/> y <see cref="ValorConIva"/> dan los dos.
/// </summary>
public sealed class ProveedorPrecio : ITenantEntity
{
    public const decimal IvaGeneral = 0.19m;

    private ProveedorPrecio()
    {
        // Requerido por EF Core.
    }

    public long Id { get; private set; }

    public string TenantId { get; private set; } = string.Empty;

    public Guid ProductoId { get; private set; }

    public string? Ciudad { get; private set; }

    public decimal Valor { get; private set; }

    public bool IncluyeIva { get; private set; }

    /// <summary>Tarifa de IVA como fracción (0.19 = 19 %).</summary>
    public decimal IvaPct { get; private set; }

    public string Moneda { get; private set; } = InsumoPrecio.MonedaPorDefecto;

    public DateOnly FechaVigencia { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public decimal ValorSinIva => IncluyeIva ? decimal.Round(Valor / (1 + IvaPct), 2) : Valor;

    public decimal ValorConIva => IncluyeIva ? Valor : decimal.Round(Valor * (1 + IvaPct), 2);

    public static ProveedorPrecio Create(Guid productoId, string? ciudad, decimal valor, bool incluyeIva, decimal ivaPct, string? moneda, DateOnly fechaVigencia, DateTimeOffset now)
    {
        var precio = new ProveedorPrecio { ProductoId = productoId, CreadoEn = now };
        precio.Update(ciudad, valor, incluyeIva, ivaPct, moneda, fechaVigencia);
        return precio;
    }

    public void Update(string? ciudad, decimal valor, bool incluyeIva, decimal ivaPct, string? moneda, DateOnly fechaVigencia)
    {
        Ciudad = NormalizeCiudad(ciudad);
        Valor = decimal.Round(valor, 2);
        IncluyeIva = incluyeIva;
        IvaPct = decimal.Round(ivaPct, 4);
        Moneda = string.IsNullOrWhiteSpace(moneda) ? InsumoPrecio.MonedaPorDefecto : moneda.Trim().ToUpperInvariant();
        FechaVigencia = fechaVigencia;
    }

    /// <summary>Igual que el CHECK de la base: mayúsculas sin espacios sobrantes; vacío = nacional (null).</summary>
    public static string? NormalizeCiudad(string? ciudad) =>
        string.IsNullOrWhiteSpace(ciudad) ? null : Insumo.NormalizeDescripcion(ciudad).ToUpperInvariant();
}

internal sealed class ProveedorPrecioConfiguration : IEntityTypeConfiguration<ProveedorPrecio>
{
    public const int CiudadMaxLength = 80;

    public void Configure(EntityTypeBuilder<ProveedorPrecio> builder)
    {
        builder.ToTable("proveedor_precio", ProveedoresApi.Schema, t => t.ExcludeFromMigrations());
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        builder.Property(p => p.ProductoId).HasColumnName("producto_id");
        builder.Property(p => p.Ciudad).HasColumnName("ciudad").HasMaxLength(CiudadMaxLength);
        builder.Property(p => p.Valor).HasColumnName("valor").HasPrecision(18, 2);
        builder.Property(p => p.IncluyeIva).HasColumnName("incluye_iva");
        builder.Property(p => p.IvaPct).HasColumnName("iva_pct").HasPrecision(5, 4);
        builder.Property(p => p.Moneda).HasColumnName("moneda").HasMaxLength(3).IsFixedLength();
        builder.Property(p => p.FechaVigencia).HasColumnName("fecha_vigencia");
        builder.Property(p => p.CreadoEn).HasColumnName("creado_en");
        builder.Ignore(p => p.ValorSinIva);
        builder.Ignore(p => p.ValorConIva);

        // La base borra los precios junto con el producto (ON DELETE CASCADE).
        builder.HasOne<ProveedorProducto>().WithMany().HasForeignKey(p => p.ProductoId).OnDelete(DeleteBehavior.Cascade);
    }
}
