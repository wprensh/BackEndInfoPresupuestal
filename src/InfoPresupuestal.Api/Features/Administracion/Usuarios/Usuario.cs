using InfoPresupuestal.Api.Common.MultiTenancy;
using InfoPresupuestal.Api.Features.Administracion.Roles;

namespace InfoPresupuestal.Api.Features.Administracion.Usuarios;

/// <summary>Usuario de una empresa. Inicia sesión con empresa + correo + contraseña.</summary>
public sealed class Usuario : ITenantEntity
{
    private Usuario()
    {
        // Requerido por EF Core.
    }

    public Guid Id { get; private set; }

    public string TenantId { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string Nombre { get; private set; } = string.Empty;

    /// <summary>Hash PBKDF2 de ASP.NET Core Identity; la contraseña nunca se guarda.</summary>
    public string PasswordHash { get; private set; } = string.Empty;

    public Guid RolId { get; private set; }

    public Rol? Rol { get; private set; }

    /// <summary>Administra la plataforma completa. Solo se crea por siembra, nunca desde la API.</summary>
    public bool EsSuperAdmin { get; private set; }

    public bool Activo { get; private set; }

    public DateTimeOffset? UltimoAcceso { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public static Usuario Create(string email, string nombre, Guid rolId, DateTimeOffset now, bool esSuperAdmin = false) =>
        new()
        {
            Id = Guid.CreateVersion7(now),
            Email = NormalizeEmail(email),
            Nombre = nombre.Trim(),
            RolId = rolId,
            EsSuperAdmin = esSuperAdmin,
            Activo = true,
            CreadoEn = now,
        };

    public void Update(string nombre, Guid rolId, bool activo)
    {
        Nombre = nombre.Trim();
        RolId = rolId;
        Activo = activo;
    }

    public void SetPasswordHash(string hash) => PasswordHash = hash;

    public void RegistrarAcceso(DateTimeOffset now) => UltimoAcceso = now;

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
