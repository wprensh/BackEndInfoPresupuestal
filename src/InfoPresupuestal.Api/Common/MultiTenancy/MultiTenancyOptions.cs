using System.ComponentModel.DataAnnotations;

namespace InfoPresupuestal.Api.Common.MultiTenancy;

public sealed class MultiTenancyOptions
{
    public const string SectionName = "MultiTenancy";

    /// <summary>
    /// Header con el que el superadministrador actúa sobre otro tenant. Para el resto de usuarios
    /// el tenant sale del claim <c>tenant</c> del JWT y este header se ignora.
    /// </summary>
    [Required]
    public string HeaderName { get; set; } = "X-Tenant-Id";
}
