using InfoPresupuestal.Api.Common.MultiTenancy;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Features.Administracion.Empresas;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Administracion.Roles;

/// <summary>
/// Cuando la aplicación agrega módulos (por ejemplo APU y presupuestos), los roles de sistema de las
/// empresas ya creadas los reciben según el tipo de empresa: Administrador con todas las acciones y Consulta solo con Ver.
/// También crea el rol Proveedor en las constructoras que aún no lo tienen.
/// Los roles personalizados no se tocan; su administrador decide.
/// </summary>
internal sealed partial class PermisosSistemaSeeder(
    AppDbContext db,
    TenantContext tenantContext,
    TimeProvider timeProvider,
    ILogger<PermisosSistemaSeeder> logger) : IDataSeeder
{
    public int Order => 20;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var tipos = await db.Set<Empresa>().ToDictionaryAsync(e => e.TenantId, e => e.Tipo, cancellationToken);
        var roles = await db.Set<Rol>().IgnoreQueryFilters()
            .Where(r => r.EsSistema && (r.Nombre == Rol.Administrador || r.Nombre == Rol.Consulta))
            .ToListAsync(cancellationToken);

        var agregados = roles.Sum(r =>
        {
            var tipo = tipos.GetValueOrDefault(r.TenantId, TipoEmpresa.Constructora);
            return r.CompletarPermisos(r.Nombre == Rol.Administrador ? Rol.PermisosCompletos(tipo) : Rol.PermisosSoloLectura(tipo));
        });
        if (agregados > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            LogAgregados(logger, agregados);
        }

        await CrearRolProveedorAsync(cancellationToken);
    }

    private async Task CrearRolProveedorAsync(CancellationToken cancellationToken)
    {
        var tenantsConProveedor = db.Set<Rol>().IgnoreQueryFilters()
            .Where(r => r.EsSistema && r.Nombre == Rol.Proveedor)
            .Select(r => r.TenantId);
        var tenants = await db.Set<Empresa>().IgnoreQueryFilters()
            .Where(e => e.TenantId != Empresa.TenantPlataforma && e.Tipo == TipoEmpresa.Constructora && !tenantsConProveedor.Contains(e.TenantId))
            .Select(e => e.TenantId)
            .ToListAsync(cancellationToken);

        var now = timeProvider.GetUtcNow();
        foreach (var tenant in tenants)
        {
            using (tenantContext.Use(tenant))
            {
                db.Add(Rol.CrearProveedor(now));
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        if (tenants.Count > 0)
        {
            LogProveedor(logger, tenants.Count);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Roles de sistema: {Count} permisos de módulos nuevos agregados")]
    private static partial void LogAgregados(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Rol Proveedor creado en {Count} empresas")]
    private static partial void LogProveedor(ILogger logger, int count);
}
