using System.Linq.Expressions;
using InfoPresupuestal.Api.Common.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Common.Persistence;

/// <summary>
/// Único DbContext de la API. No declara DbSet por feature: cada slice usa <c>Set&lt;T&gt;()</c>
/// y cada feature aporta su <see cref="IEntityTypeConfiguration{TEntity}"/>, así este archivo
/// no cambia cuando se agrega una feature.
/// Toda entidad <see cref="ITenantEntity"/> recibe la columna tenant_id y un filtro global por tenant.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext) : DbContext(options)
{
    public const int TenantIdMaxLength = 64;

    // EF Core lee esta propiedad en cada consulta, así el filtro usa el tenant de la solicitud actual.
    private string CurrentTenantId => tenantContext.TenantId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(t => t.ClrType.IsAssignableTo(typeof(ITenantEntity))))
        {
            var entity = modelBuilder.Entity(entityType.ClrType);
            entity.Property(nameof(ITenantEntity.TenantId)).HasColumnName("tenant_id").HasMaxLength(TenantIdMaxLength).IsRequired();
            entity.HasIndex(nameof(ITenantEntity.TenantId));
            entity.HasQueryFilter(BuildTenantFilter(entityType.ClrType));
        }
    }

    private LambdaExpression BuildTenantFilter(Type entityType)
    {
        // e => e.TenantId == this.CurrentTenantId
        var parameter = Expression.Parameter(entityType, "e");
        var entityTenant = Expression.Property(parameter, nameof(ITenantEntity.TenantId));
        var currentTenant = Expression.Property(Expression.Constant(this), nameof(CurrentTenantId));
        return Expression.Lambda(Expression.Equal(entityTenant, currentTenant), parameter);
    }
}
