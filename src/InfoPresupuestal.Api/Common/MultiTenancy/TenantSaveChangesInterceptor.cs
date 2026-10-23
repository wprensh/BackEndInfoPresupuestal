using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace InfoPresupuestal.Api.Common.MultiTenancy;

/// <summary>
/// Asigna el tenant actual a las entidades nuevas e impide guardar cambios sobre filas de otro tenant.
/// Los handlers nunca escriben TenantId.
/// </summary>
internal sealed class TenantSaveChangesInterceptor(ITenantContext tenantContext) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ApplyTenant(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyTenant(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyTenant(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<ITenantEntity>())
        {
            var tenantId = entry.Property(e => e.TenantId);

            switch (entry.State)
            {
                case EntityState.Added:
                    tenantId.CurrentValue = tenantContext.TenantId;
                    break;
                case EntityState.Modified or EntityState.Deleted when tenantId.OriginalValue != tenantContext.TenantId:
                    throw new InvalidOperationException("No se pueden modificar datos de otro tenant.");
            }
        }
    }
}
