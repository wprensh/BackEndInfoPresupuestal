using System.ComponentModel.DataAnnotations;

namespace InfoPresupuestal.Api.Common.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = "infopresupuestal";

    [Required]
    public string Audience { get; set; } = "infopresupuestal";

    /// <summary>Clave HMAC de al menos 32 caracteres. Fuera de Development va por variable de entorno (Jwt__SigningKey).</summary>
    [Required]
    [MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    [Range(5, 1440)]
    public int ExpirationMinutes { get; set; } = 60;
}
