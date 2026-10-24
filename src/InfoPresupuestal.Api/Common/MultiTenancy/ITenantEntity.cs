namespace InfoPresupuestal.Api.Common.MultiTenancy;

/// <summary>
/// Marca una entidad como perteneciente a un tenant. <see cref="Persistence.AppDbContext"/> le aplica
/// el filtro global por tenant y <see cref="TenantSaveChangesInterceptor"/> asigna el valor al insertar.
/// </summary>
public interface ITenantEntity
{
    string TenantId { get; }
}
