using InfoPresupuestal.Api.Common.MultiTenancy;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Features.Administracion.Empresas;
using InfoPresupuestal.Api.Features.Administracion.Usuarios;
using Microsoft.AspNetCore.Identity;

namespace InfoPresupuestal.Api.Tests.Features.Administracion;

internal static class AdminTestData
{
    public const string Password = "Clave1234";

    public static readonly PasswordHasher<Usuario> Hasher = new();

    /// <summary>Da de alta empresas como lo hace la API: empresa + roles del sistema + administrador.</summary>
    public static async Task ProvisionAsync(string databaseName, params (string TenantId, string AdminEmail)[] empresas)
    {
        var tenant = new TenantContext();
        await using var db = TestDb.Create(tenant, databaseName);
        var provisioner = new EmpresaProvisioner(db, tenant, Hasher, TimeProvider.System);

        foreach (var (tenantId, adminEmail) in empresas)
        {
            var empresa = Empresa.Create(tenantId, $"Empresa {tenantId}", null, null, null, TimeProvider.System.GetUtcNow());
            await provisioner.ProvisionAsync(empresa, "Administrador", adminEmail, Password, CancellationToken.None);
        }
    }

    public static AppDbContext SinTenant(string databaseName, out TenantContext tenant)
    {
        tenant = new TenantContext();
        return TestDb.Create(tenant, databaseName);
    }
}
