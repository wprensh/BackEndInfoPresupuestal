using InfoPresupuestal.Api.Common.MultiTenancy;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Features.Administracion.Roles;
using InfoPresupuestal.Api.Features.Administracion.Usuarios;
using Microsoft.AspNetCore.Identity;

namespace InfoPresupuestal.Api.Features.Administracion.Empresas;

/// <summary>
/// Da de alta una empresa completa en una sola transacción: la empresa, sus roles de sistema
/// (Administrador y Consulta según su tipo, más Proveedor en una constructora) y su primer administrador.
/// </summary>
internal sealed class EmpresaProvisioner(
    AppDbContext db,
    TenantContext tenantContext,
    IPasswordHasher<Usuario> passwordHasher,
    TimeProvider timeProvider)
{
    public async Task<Usuario> ProvisionAsync(
        Empresa empresa,
        string adminNombre,
        string adminEmail,
        string adminPassword,
        CancellationToken cancellationToken,
        bool adminEsSuperAdmin = false)
    {
        var now = timeProvider.GetUtcNow();
        db.Add(empresa);

        // Las filas nuevas toman el tenant de la empresa creada, no el de quien la crea.
        using (tenantContext.Use(empresa.TenantId))
        {
            var administrador = Rol.Create(Rol.Administrador, "Acceso completo a la empresa.", Rol.PermisosCompletos(empresa.Tipo), now, esSistema: true);
            var consulta = Rol.Create(Rol.Consulta, "Solo lectura.", Rol.PermisosSoloLectura(empresa.Tipo), now, esSistema: true);
            db.AddRange(administrador, consulta);
            if (empresa.Tipo == TipoEmpresa.Constructora)
            {
                db.Add(Rol.CrearProveedor(now));
            }

            var admin = Usuario.Create(adminEmail, adminNombre, administrador.Id, now, adminEsSuperAdmin);
            admin.SetPasswordHash(passwordHasher.HashPassword(admin, adminPassword));
            db.Add(admin);

            await db.SaveChangesAsync(cancellationToken);
            return admin;
        }
    }
}
