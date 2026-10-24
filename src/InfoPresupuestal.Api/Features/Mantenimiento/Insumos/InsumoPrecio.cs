using InfoPresupuestal.Api.Common.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfoPresupuestal.Api.Features.Mantenimiento.Insumos;

/// <summary>Precio de un insumo desde una fecha de vigencia. Un precio nuevo agrega una fila; no sobrescribe.</summary>
public sealed class InsumoPrecio : ITenantEntity
{
    public const string MonedaPorDefecto = "COP";

    private InsumoPrecio()
    {
        // Requerido por EF Core.
    }

    public long Id { get; private set; }

    public string TenantId { get; private set; } = string.Empty;

    public Guid InsumoId { get; private set; }

    public decimal ValorUnitario { get; private set; }

    public string Moneda { get; private set; } = MonedaPorDefecto;

    public DateOnly FechaVigencia { get; private set; }

    public string? Fuente { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public static InsumoPrecio Create(Guid insumoId, decimal valorUnitario, string? moneda, DateOnly fechaVigencia, string? fuente, DateTimeOffset now) =>
        new()
        {
            InsumoId = insumoId,
            ValorUnitario = decimal.Round(valorUnitario, 2),
            Moneda = NormalizeMoneda(moneda),
            FechaVigencia = fechaVigencia,
            Fuente = string.IsNullOrWhiteSpace(fuente) ? null : fuente.Trim(),
            CreadoEn = now,
        };

    public void Update(decimal valorUnitario, string? moneda, DateOnly fechaVigencia, string? fuente)
    {
        ValorUnitario = decimal.Round(valorUnitario, 2);
        Moneda = NormalizeMoneda(moneda);
        FechaVigencia = fechaVigencia;
        Fuente = string.IsNullOrWhiteSpace(fuente) ? null : fuente.Trim();
    }

    private static string NormalizeMoneda(string? moneda) => string.IsNullOrWhiteSpace(moneda) ? MonedaPorDefecto : moneda.Trim().ToUpperInvariant();
}

internal sealed class InsumoPrecioConfiguration : IEntityTypeConfiguration<InsumoPrecio>
{
    public const int FuenteMaxLength = 120;

    public void Configure(EntityTypeBuilder<InsumoPrecio> builder)
    {
        builder.ToTable("insumo_precio", MantenimientoApi.Schema, t => t.ExcludeFromMigrations());
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        builder.Property(p => p.InsumoId).HasColumnName("insumo_id");
        builder.Property(p => p.ValorUnitario).HasColumnName("valor_unitario").HasPrecision(18, 2);
        builder.Property(p => p.Moneda).HasColumnName("moneda").HasMaxLength(3).IsFixedLength();
        builder.Property(p => p.FechaVigencia).HasColumnName("fecha_vigencia");
        builder.Property(p => p.Fuente).HasColumnName("fuente").HasMaxLength(FuenteMaxLength);
        builder.Property(p => p.CreadoEn).HasColumnName("creado_en");

        // La base borra el historial junto con el insumo (ON DELETE CASCADE).
        builder.HasOne<Insumo>().WithMany().HasForeignKey(p => p.InsumoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(p => new { p.InsumoId, p.FechaVigencia }).IsUnique();
    }
}
