using System.Text.RegularExpressions;
using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.MultiTenancy;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Features.Administracion.Empresas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfoPresupuestal.Api.Features.Presupuestos.Impresion;

/// <summary>Datos editables del membrete. El logo llega ya decodificado (ver <see cref="LogoImpresion"/>).</summary>
public sealed record DatosImpresion(
    string? RazonSocial,
    string? Nit,
    string? Direccion,
    string? Ciudad,
    string? Telefono,
    string? Email,
    string? SitioWeb,
    string ColorPrimario,
    string? Encabezado,
    string? PiePagina,
    string? FirmaNombre,
    string? FirmaCargo,
    string? FirmaMatricula,
    bool MostrarApu,
    bool MostrarParticipacion,
    bool MostrarValorLetras);

/// <summary>
/// Membrete con que una empresa imprime sus presupuestos (una fila por tenant). Los campos vacíos
/// toman el valor de <see cref="Empresa"/> al imprimir, así la impresión funciona sin configurar nada.
/// </summary>
public sealed class ConfiguracionImpresion : ITenantEntity
{
    public const string ColorPorDefecto = "#006a6a";

    private ConfiguracionImpresion()
    {
        // Requerido por EF Core.
    }

    public string TenantId { get; private set; } = string.Empty;

    public string? RazonSocial { get; private set; }

    public string? Nit { get; private set; }

    public string? Direccion { get; private set; }

    public string? Ciudad { get; private set; }

    public string? Telefono { get; private set; }

    public string? Email { get; private set; }

    public string? SitioWeb { get; private set; }

    public byte[]? Logo { get; private set; }

    public string? LogoTipo { get; private set; }

    public string ColorPrimario { get; private set; } = ColorPorDefecto;

    public string? Encabezado { get; private set; }

    public string? PiePagina { get; private set; }

    public string? FirmaNombre { get; private set; }

    public string? FirmaCargo { get; private set; }

    public string? FirmaMatricula { get; private set; }

    public bool MostrarApu { get; private set; } = true;

    public bool MostrarParticipacion { get; private set; } = true;

    public bool MostrarValorLetras { get; private set; } = true;

    public DateTimeOffset ActualizadoEn { get; private set; }

    public static ConfiguracionImpresion Create(DatosImpresion datos, LogoImpresion? logo, DateTimeOffset now)
    {
        var config = new ConfiguracionImpresion();
        config.Update(datos, logo, now);
        return config;
    }

    public void Update(DatosImpresion d, LogoImpresion? logo, DateTimeOffset now)
    {
        RazonSocial = Limpiar(d.RazonSocial);
        Nit = Limpiar(d.Nit);
        Direccion = Limpiar(d.Direccion);
        Ciudad = Limpiar(d.Ciudad);
        Telefono = Limpiar(d.Telefono);
        Email = Limpiar(d.Email)?.ToLowerInvariant();
        SitioWeb = Limpiar(d.SitioWeb);
        ColorPrimario = d.ColorPrimario.Trim().ToLowerInvariant();
        Encabezado = Limpiar(d.Encabezado);
        PiePagina = Limpiar(d.PiePagina);
        FirmaNombre = Limpiar(d.FirmaNombre);
        FirmaCargo = Limpiar(d.FirmaCargo);
        FirmaMatricula = Limpiar(d.FirmaMatricula);
        MostrarApu = d.MostrarApu;
        MostrarParticipacion = d.MostrarParticipacion;
        MostrarValorLetras = d.MostrarValorLetras;
        Logo = logo?.Contenido;
        LogoTipo = logo?.Tipo;
        ActualizadoEn = now;
    }

    private static string? Limpiar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

/// <summary>Logo decodificado de un data URL <c>data:image/png;base64,...</c>.</summary>
public sealed partial record LogoImpresion(string Tipo, byte[] Contenido)
{
    public const int MaxBytes = 512 * 1024;

    public static readonly IReadOnlyList<string> TiposPermitidos = ["image/png", "image/jpeg", "image/webp"];

    public string DataUrl => $"data:{Tipo};base64,{Convert.ToBase64String(Contenido)}";

    /// <summary>Acepta PNG, JPEG o WebP hasta 512 KB y revisa la firma del archivo, no solo el tipo declarado.</summary>
    public static Result<LogoImpresion?> Parse(string? dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl))
        {
            return (LogoImpresion?)null;
        }

        var match = DataUrlRegex().Match(dataUrl.Trim());
        if (!match.Success || !TiposPermitidos.Contains(match.Groups["tipo"].Value))
        {
            return Error.Validation("Impresion.LogoFormato", "El logo debe ser una imagen PNG, JPG o WebP.");
        }

        var contenido = new byte[match.Groups["datos"].Length];
        if (!Convert.TryFromBase64String(match.Groups["datos"].Value, contenido, out var leidos))
        {
            return Error.Validation("Impresion.LogoFormato", "El logo no es una imagen válida.");
        }

        if (leidos > MaxBytes)
        {
            return Error.Validation("Impresion.LogoTamano", "El logo no puede pasar de 512 KB.");
        }

        var bytes = contenido.AsSpan(0, leidos);
        var tipo = match.Groups["tipo"].Value;
        var firmaValida = tipo switch
        {
            "image/png" => bytes.StartsWith((byte[])[0x89, 0x50, 0x4E, 0x47]),
            "image/jpeg" => bytes.StartsWith((byte[])[0xFF, 0xD8, 0xFF]),
            _ => bytes.Length > 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8),
        };
        return firmaValida
            ? new LogoImpresion(tipo, bytes.ToArray())
            : Error.Validation("Impresion.LogoFormato", "El contenido del logo no corresponde a una imagen PNG, JPG o WebP.");
    }

    [GeneratedRegex(@"^data:(?<tipo>image/[a-z]+);base64,(?<datos>[A-Za-z0-9+/=\s]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex DataUrlRegex();
}

/// <summary>Membrete listo para imprimir: valores propios o, si faltan, los de la empresa.</summary>
public sealed record ConfiguracionImpresionResponse(
    string RazonSocial,
    string? Nit,
    string? Direccion,
    string? Ciudad,
    string? Telefono,
    string? Email,
    string? SitioWeb,
    string? Logo,
    string ColorPrimario,
    string? Encabezado,
    string? PiePagina,
    string? FirmaNombre,
    string? FirmaCargo,
    string? FirmaMatricula,
    bool MostrarApu,
    bool MostrarParticipacion,
    bool MostrarValorLetras,
    bool Personalizada,
    DateTimeOffset? ActualizadoEn);

internal static class ImpresionApi
{
    public const string Route = "/api/impresion/configuracion";
    public const string Tag = "Impresión";

    public static async Task<ConfiguracionImpresionResponse> LeerAsync(AppDbContext db, ITenantContext tenant, CancellationToken cancellationToken)
    {
        var config = await db.Set<ConfiguracionImpresion>().AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var empresa = await db.Set<Empresa>().AsNoTracking()
            .Where(e => e.TenantId == tenant.TenantId)
            .Select(e => new { e.RazonSocial, e.Nit, e.Email, e.Telefono })
            .FirstOrDefaultAsync(cancellationToken);

        if (config is null)
        {
            return new ConfiguracionImpresionResponse(
                empresa?.RazonSocial ?? tenant.TenantId, empresa?.Nit, null, null, empresa?.Telefono, empresa?.Email, null, null,
                ConfiguracionImpresion.ColorPorDefecto, null, null, null, null, null, true, true, true, false, null);
        }

        return new ConfiguracionImpresionResponse(
            config.RazonSocial ?? empresa?.RazonSocial ?? tenant.TenantId,
            config.Nit ?? empresa?.Nit,
            config.Direccion,
            config.Ciudad,
            config.Telefono ?? empresa?.Telefono,
            config.Email ?? empresa?.Email,
            config.SitioWeb,
            config.Logo is { } logo && config.LogoTipo is { } tipo ? new LogoImpresion(tipo, logo).DataUrl : null,
            config.ColorPrimario,
            config.Encabezado,
            config.PiePagina,
            config.FirmaNombre,
            config.FirmaCargo,
            config.FirmaMatricula,
            config.MostrarApu,
            config.MostrarParticipacion,
            config.MostrarValorLetras,
            true,
            config.ActualizadoEn);
    }
}

internal sealed class ConfiguracionImpresionConfiguration : IEntityTypeConfiguration<ConfiguracionImpresion>
{
    public const int TextoMaxLength = 200;
    public const int NotaMaxLength = 500;

    public void Configure(EntityTypeBuilder<ConfiguracionImpresion> builder)
    {
        builder.ToTable("configuracion_impresion", PresupuestosModulo.Schema, t => t.ExcludeFromMigrations());
        builder.HasKey(c => c.TenantId);
        builder.Property(c => c.RazonSocial).HasColumnName("razon_social").HasMaxLength(TextoMaxLength);
        builder.Property(c => c.Nit).HasColumnName("nit").HasMaxLength(20);
        builder.Property(c => c.Direccion).HasColumnName("direccion").HasMaxLength(TextoMaxLength);
        builder.Property(c => c.Ciudad).HasColumnName("ciudad").HasMaxLength(80);
        builder.Property(c => c.Telefono).HasColumnName("telefono").HasMaxLength(30);
        builder.Property(c => c.Email).HasColumnName("email").HasMaxLength(TextoMaxLength);
        builder.Property(c => c.SitioWeb).HasColumnName("sitio_web").HasMaxLength(TextoMaxLength);
        builder.Property(c => c.Logo).HasColumnName("logo");
        builder.Property(c => c.LogoTipo).HasColumnName("logo_tipo").HasMaxLength(40);
        builder.Property(c => c.ColorPrimario).HasColumnName("color_primario").HasMaxLength(7).IsFixedLength();
        builder.Property(c => c.Encabezado).HasColumnName("encabezado").HasMaxLength(NotaMaxLength);
        builder.Property(c => c.PiePagina).HasColumnName("pie_pagina").HasMaxLength(NotaMaxLength);
        builder.Property(c => c.FirmaNombre).HasColumnName("firma_nombre").HasMaxLength(120);
        builder.Property(c => c.FirmaCargo).HasColumnName("firma_cargo").HasMaxLength(120);
        builder.Property(c => c.FirmaMatricula).HasColumnName("firma_matricula").HasMaxLength(60);
        builder.Property(c => c.MostrarApu).HasColumnName("mostrar_apu");
        builder.Property(c => c.MostrarParticipacion).HasColumnName("mostrar_participacion");
        builder.Property(c => c.MostrarValorLetras).HasColumnName("mostrar_valor_letras");
        builder.Property(c => c.ActualizadoEn).HasColumnName("actualizado_en");
    }
}
