namespace InfoPresupuestal.Api.Features.Mantenimiento;

/// <summary>
/// Mantenimiento de las tablas del catálogo de insumos (esquema <c>presupuesto</c>). Las tablas las crean
/// los scripts de <c>database/</c>, por eso EF Core las mapea con <c>ExcludeFromMigrations</c>.
/// </summary>
internal static class MantenimientoApi
{
    public const string Schema = "presupuesto";
    public const string Route = "/api/mantenimiento";
    public const string Tag = "Mantenimiento";
}
