using InfoPresupuestal.Api.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Administracion.Roles;

/// <summary>
/// Cuando la aplicación agrega módulos (por ejemplo APU y presupuestos), los roles de sistema de las
/// empresas ya creadas los reciben: Administrador con todas las acciones y Consulta solo con Ver.
/// Los roles personalizados no se tocan; su administrador decide.
/// </summary>
internal sealed partial class PermisosSistemaSeeder(AppDbContext db, ILogger<PermisosSistemaSeeder> logger) : IDataSeeder
{
    public int Order => 20;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var roles = await db.Set<Rol>().IgnoreQueryFilters()
            .Where(r => r.EsSistema && (r.Nombre == Rol.Administrador || r.Nombre == Rol.Consulta))
            .ToListAsync(cancellationToken);

        var agregados = roles.Sum(r => r.CompletarPermisos(r.Nombre == Rol.Administrador ? Rol.PermisosCompletos() : Rol.PermisosSoloLectura()));
        if (agregados > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            LogAgregados(logger, agregados);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Roles de sistema: {Count} permisos de módulos nuevos agregados")]
    private static partial void LogAgregados(ILogger logger, int count);
}
