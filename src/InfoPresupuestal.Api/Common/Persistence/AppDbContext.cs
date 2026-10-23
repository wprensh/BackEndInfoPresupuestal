using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Common.Persistence;

/// <summary>
/// Único DbContext de la API. No declara DbSet por feature: cada slice usa <c>Set&lt;T&gt;()</c>
/// y cada feature aporta su <see cref="IEntityTypeConfiguration{TEntity}"/>, así este archivo
/// no cambia cuando se agrega una feature.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
