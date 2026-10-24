namespace InfoPresupuestal.Api.Features.Presupuestos;

/// <summary>
/// Análisis de precios unitarios y presupuestos de obra (esquema <c>presupuesto</c>). Las tablas las crea
/// <c>database/scripts/05_apu_presupuesto.sql</c>, por eso EF Core las mapea con <c>ExcludeFromMigrations</c>.
/// </summary>
internal static class PresupuestosModulo
{
    public const string Schema = "presupuesto";
    public const string Tag = "Presupuestos";

    /// <summary>Tipos de insumo del catálogo global (<c>presupuesto.tipo_insumo</c>).</summary>
    public const short Material = 1;
    public const short Equipo = 2;
    public const short ManoObra = 3;

    /// <summary>Siguiente consecutivo con prefijo: <c>APU-0008</c> después de <c>APU-0007</c>.</summary>
    public static string SiguienteCodigo(string prefijo, string? ultimoCodigo) =>
        Mantenimiento.Insumos.Insumo.SiguienteCodigo(prefijo, ultimoCodigo);
}
