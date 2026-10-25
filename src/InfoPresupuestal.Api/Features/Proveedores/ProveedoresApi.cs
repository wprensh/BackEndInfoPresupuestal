using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.MultiTenancy;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Features.Administracion.Empresas;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Proveedores;

/// <summary>
/// Catálogo propio de las empresas proveedoras (esquema <c>presupuesto</c>, script 06): cada proveedor
/// publica sus productos y precios por ciudad, y las constructoras los consultan. Las tablas las crea
/// <c>database/scripts/06_proveedores.sql</c>, por eso EF Core las mapea con <c>ExcludeFromMigrations</c>.
/// </summary>
internal static class ProveedoresApi
{
    public const string Schema = "presupuesto";
    public const string Tag = "Proveedores";

    /// <summary>Catálogo de la empresa proveedora que inició sesión.</summary>
    public const string CatalogoRoute = "/api/proveedor/productos";

    /// <summary>Directorio de proveedores que consultan las constructoras.</summary>
    public const string DirectorioRoute = "/api/proveedores";

    public static readonly Error SoloProveedor =
        Error.Forbidden("Proveedores.SoloProveedor", "Solo una empresa proveedora tiene catálogo propio.");

    /// <summary>Restringe el endpoint a empresas de ese tipo; el tenant ya lo resolvió el grupo de endpoints.</summary>
    public static RouteHandlerBuilder RequireTipoEmpresa(this RouteHandlerBuilder builder, TipoEmpresa tipo) =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            var services = context.HttpContext.RequestServices;
            var tenant = services.GetRequiredService<ITenantContext>();
            var db = services.GetRequiredService<AppDbContext>();
            var actual = await db.Set<Empresa>().Where(e => e.TenantId == tenant.TenantId).Select(e => (TipoEmpresa?)e.Tipo)
                .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

            return actual == tipo
                ? await next(context)
                : (tipo == TipoEmpresa.Proveedor
                    ? SoloProveedor
                    : Error.Forbidden("Proveedores.SoloConstructora", "Esta opción es para empresas constructoras.")).ToProblem();
        });
}
