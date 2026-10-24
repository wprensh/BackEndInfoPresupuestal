using InfoPresupuestal.Api.Common.MultiTenancy;
using InfoPresupuestal.Api.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Tests;

internal static class TestDb
{
    public const string DefaultTenant = "demo";

    /// <summary>Crea un contexto para <paramref name="tenantId"/>. Con el mismo <paramref name="databaseName"/> varios contextos comparten datos.</summary>
    public static AppDbContext Create(string tenantId = DefaultTenant, string? databaseName = null)
    {
        var tenant = new TenantContext();
        tenant.SetTenant(tenantId);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .AddInterceptors(new TenantSaveChangesInterceptor(tenant))
            .Options;

        return new AppDbContext(options, tenant);
    }
}
