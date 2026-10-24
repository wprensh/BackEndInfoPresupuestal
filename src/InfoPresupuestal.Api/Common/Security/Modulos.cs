namespace InfoPresupuestal.Api.Common.Security;

public enum Accion
{
    Ver,
    Insertar,
    Editar,
    Eliminar,
}

/// <summary>Alcance de un módulo: qué pueden recibir los roles de un tenant.</summary>
public enum AlcanceModulo
{
    /// <summary>Datos del tenant: los roles del tenant pueden recibir las cuatro acciones.</summary>
    Tenant,

    /// <summary>Datos compartidos por todos los tenants: los roles solo reciben Ver; escribir es del superadministrador.</summary>
    Global,

    /// <summary>Administración de la plataforma: solo el superadministrador.</summary>
    Plataforma,
}

public sealed record ModuloDefinicion(string Codigo, string Nombre, string Grupo, AlcanceModulo Alcance)
{
    public bool PermiteEnRol(Accion accion) => Alcance switch
    {
        AlcanceModulo.Tenant => true,
        AlcanceModulo.Global => accion == Accion.Ver,
        _ => false,
    };
}

/// <summary>
/// Catálogo de módulos sobre los que se otorgan permisos. Un permiso se escribe
/// <c>modulo:accion</c> (por ejemplo <c>insumos:editar</c>) y viaja como claim en el JWT.
/// </summary>
public static class Modulos
{
    public const string Empresas = "empresas";
    public const string Usuarios = "usuarios";
    public const string Roles = "roles";
    public const string Clasificaciones = "clasificaciones";
    public const string UnidadesMedida = "unidades-medida";
    public const string TiposInsumo = "tipos-insumo";
    public const string Insumos = "insumos";
    public const string PreciosInsumo = "precios-insumo";
    public const string Productos = "productos";

    public static readonly IReadOnlyList<ModuloDefinicion> Todos =
    [
        new(Empresas, "Empresas", "Administración", AlcanceModulo.Plataforma),
        new(Usuarios, "Usuarios", "Administración", AlcanceModulo.Tenant),
        new(Roles, "Roles y permisos", "Administración", AlcanceModulo.Tenant),
        new(Clasificaciones, "Clasificaciones", "Mantenimiento", AlcanceModulo.Tenant),
        new(UnidadesMedida, "Unidades de medida", "Mantenimiento", AlcanceModulo.Global),
        new(TiposInsumo, "Tipos de insumo", "Mantenimiento", AlcanceModulo.Global),
        new(Insumos, "Insumos", "Mantenimiento", AlcanceModulo.Tenant),
        new(PreciosInsumo, "Precios de insumos", "Mantenimiento", AlcanceModulo.Tenant),
        new(Productos, "Productos", "Operación", AlcanceModulo.Tenant),
    ];

    public static ModuloDefinicion? Buscar(string codigo) =>
        Todos.FirstOrDefault(m => string.Equals(m.Codigo, codigo, StringComparison.Ordinal));

    public static string Permiso(string modulo, Accion accion) => $"{modulo}:{accion.ToString().ToLowerInvariant()}";
}
