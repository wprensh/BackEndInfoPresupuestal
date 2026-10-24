namespace InfoPresupuestal.Api.Features.Administracion.Empresas;

/// <summary>
/// Empresa cliente de la plataforma. Cada empresa es un tenant: su <see cref="TenantId"/> es el valor
/// que llevan todas sus filas en la columna tenant_id. Por eso esta entidad no es <c>ITenantEntity</c>.
/// </summary>
public sealed class Empresa
{
    /// <summary>Tenant reservado de la plataforma, donde vive el superadministrador.</summary>
    public const string TenantPlataforma = "sistema";

    private Empresa()
    {
        // Requerido por EF Core.
    }

    public Guid Id { get; private set; }

    public string TenantId { get; private set; } = string.Empty;

    public string RazonSocial { get; private set; } = string.Empty;

    public string? Nit { get; private set; }

    public string? Email { get; private set; }

    public string? Telefono { get; private set; }

    public bool Activo { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public bool EsPlataforma => TenantId == TenantPlataforma;

    public static Empresa Create(string tenantId, string razonSocial, string? nit, string? email, string? telefono, DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(now),
            TenantId = tenantId.Trim().ToLowerInvariant(),
            RazonSocial = razonSocial.Trim(),
            Nit = Clean(nit),
            Email = Clean(email)?.ToLowerInvariant(),
            Telefono = Clean(telefono),
            Activo = true,
            CreadoEn = now,
        };

    public void Update(string razonSocial, string? nit, string? email, string? telefono, bool activo)
    {
        RazonSocial = razonSocial.Trim();
        Nit = Clean(nit);
        Email = Clean(email)?.ToLowerInvariant();
        Telefono = Clean(telefono);
        Activo = activo || EsPlataforma; // la plataforma nunca se desactiva
    }

    public void Desactivar() => Activo = EsPlataforma;

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
