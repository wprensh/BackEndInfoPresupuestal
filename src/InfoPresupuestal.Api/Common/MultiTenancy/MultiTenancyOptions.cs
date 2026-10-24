using System.ComponentModel.DataAnnotations;

namespace InfoPresupuestal.Api.Common.MultiTenancy;

public sealed class MultiTenancyOptions
{
    public const string SectionName = "MultiTenancy";

    /// <summary>Header HTTP con el id del tenant. Cuando haya autenticación, se reemplaza por un claim del JWT.</summary>
    [Required]
    public string HeaderName { get; set; } = "X-Tenant-Id";

    public IList<TenantDefinition> Tenants { get; init; } = [];
}

public sealed class TenantDefinition
{
    [Required]
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
