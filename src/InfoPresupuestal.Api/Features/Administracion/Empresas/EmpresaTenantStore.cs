using InfoPresupuestal.Api.Common.MultiTenancy;
using InfoPresupuestal.Api.Common.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace InfoPresupuestal.Api.Features.Administracion.Empresas;

/// <summary>Tenants válidos = empresas activas. Se cachea un minuto para no consultar en cada solicitud.</summary>
internal sealed class EmpresaTenantStore(AppDbContext db, IMemoryCache cache) : ITenantStore
{
    private static readonly TimeSpan Duracion = TimeSpan.FromMinutes(1);

    public async Task<bool> ExistsAsync(string tenantId, CancellationToken cancellationToken) =>
        await cache.GetOrCreateAsync(CacheKey(tenantId), entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Duracion;
            return db.Set<Empresa>().AnyAsync(e => e.TenantId == tenantId && e.Activo, cancellationToken);
        });

    /// <summary>Se llama al cambiar el estado de una empresa para que el cambio aplique de inmediato.</summary>
    public static void Invalidate(IMemoryCache cache, string tenantId) => cache.Remove(CacheKey(tenantId));

    private static string CacheKey(string tenantId) => $"tenant-activo:{tenantId}";
}
