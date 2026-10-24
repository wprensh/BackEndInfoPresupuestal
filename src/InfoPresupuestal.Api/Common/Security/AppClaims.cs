namespace InfoPresupuestal.Api.Common.Security;

/// <summary>Claims propios del JWT de InfoPresupuestal.</summary>
public static class AppClaims
{
    public const string UserId = "sub";
    public const string Email = "email";
    public const string Name = "name";
    public const string Tenant = "tenant";
    public const string Role = "rol";
    public const string Permission = "perm";
    public const string SuperAdmin = "sa";
}
