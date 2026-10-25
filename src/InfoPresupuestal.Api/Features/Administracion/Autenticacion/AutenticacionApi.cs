using System.Threading.RateLimiting;
using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Features.Administracion.Roles;
using InfoPresupuestal.Api.Features.Administracion.Usuarios;

namespace InfoPresupuestal.Api.Features.Administracion.Autenticacion;

internal static class AutenticacionApi
{
    public const string Route = "/api/auth";
    public const string Tag = "Autenticación";
    public const string LoginRateLimit = "login";

    /// <summary>El mismo error para empresa, correo o contraseña incorrectos: no revela cuál falló.</summary>
    public static readonly Error CredencialesInvalidas =
        Error.Unauthorized("Auth.InvalidCredentials", "Empresa, correo o contraseña incorrectos.");

    /// <summary>Permisos del usuario: todos si es superadministrador, si no los de su rol.</summary>
    public static IReadOnlyCollection<string> Permisos(Usuario usuario) =>
        usuario.EsSuperAdmin
            ? Modulos.Todos.SelectMany(m => Enum.GetValues<Accion>().Select(a => Modulos.Permiso(m.Codigo, a))).ToArray()
            : usuario.Rol?.PermisosEfectivos().ToArray() ?? [];

    public static string TipoTexto(TipoEmpresa tipo) => tipo == TipoEmpresa.Proveedor ? "PROVEEDOR" : "CONSTRUCTORA";
}

/// <param name="TipoEmpresa">CONSTRUCTORA o PROVEEDOR: la interfaz muestra el menú que corresponde.</param>
public sealed record SesionUsuario(Guid Id, string Email, string Nombre, string Empresa, string RazonSocial, string Rol, bool EsSuperAdmin, string TipoEmpresa);

internal sealed class AutenticacionInstaller : IServiceInstaller
{
    public void Install(IServiceCollection services, IConfiguration configuration) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // 10 intentos por minuto por dirección IP frenan ataques de fuerza bruta.
            options.AddPolicy(AutenticacionApi.LoginRateLimit, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
}
