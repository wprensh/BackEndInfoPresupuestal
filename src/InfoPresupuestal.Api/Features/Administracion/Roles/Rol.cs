using InfoPresupuestal.Api.Common.MultiTenancy;
using InfoPresupuestal.Api.Common.Security;

namespace InfoPresupuestal.Api.Features.Administracion.Roles;

/// <summary>Tipo de usuario dentro de una empresa, con sus permisos por módulo.</summary>
public sealed class Rol : ITenantEntity
{
    public const string Administrador = "Administrador";
    public const string Consulta = "Consulta";
    public const string Proveedor = "Proveedor";

    private readonly List<RolPermiso> _permisos = [];

    private Rol()
    {
        // Requerido por EF Core.
    }

    public Guid Id { get; private set; }

    public string TenantId { get; private set; } = string.Empty;

    public string Nombre { get; private set; } = string.Empty;

    public string? Descripcion { get; private set; }

    /// <summary>Roles creados con la empresa: no se editan ni se eliminan.</summary>
    public bool EsSistema { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public IReadOnlyList<RolPermiso> Permisos => _permisos;

    public static Rol Create(string nombre, string? descripcion, IEnumerable<RolPermiso> permisos, DateTimeOffset now, bool esSistema = false)
    {
        var rol = new Rol
        {
            Id = Guid.CreateVersion7(now),
            Nombre = nombre.Trim(),
            Descripcion = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim(),
            EsSistema = esSistema,
            CreadoEn = now,
        };
        rol._permisos.AddRange(permisos);
        return rol;
    }

    public void Update(string nombre, string? descripcion, IEnumerable<RolPermiso> permisos)
    {
        Nombre = nombre.Trim();
        Descripcion = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim();
        _permisos.Clear();
        _permisos.AddRange(permisos);
    }

    /// <summary>
    /// Agrega los módulos que el rol aún no tiene (módulos nuevos de la aplicación). No toca los existentes.
    /// Devuelve cuántos se agregaron.
    /// </summary>
    public int CompletarPermisos(IEnumerable<RolPermiso> requeridos)
    {
        var faltantes = requeridos.Where(r => _permisos.All(p => p.Modulo != r.Modulo)).ToList();
        _permisos.AddRange(faltantes);
        return faltantes.Count;
    }

    /// <summary>Permisos en formato <c>modulo:accion</c>, tal como viajan en el JWT.</summary>
    public IEnumerable<string> PermisosEfectivos() => _permisos.SelectMany(p => p.Acciones().Select(a => Modulos.Permiso(p.Modulo, a)));

    /// <summary>Todos los permisos que un rol de una empresa de ese tipo puede tener (rol Administrador).</summary>
    public static IEnumerable<RolPermiso> PermisosCompletos(TipoEmpresa tipo = TipoEmpresa.Constructora) =>
        Modulos.ParaTipo(tipo)
            .Select(m => RolPermiso.Create(
                m.Codigo,
                ver: true,
                insertar: m.PermiteEnRol(Accion.Insertar),
                editar: m.PermiteEnRol(Accion.Editar),
                eliminar: m.PermiteEnRol(Accion.Eliminar)));

    /// <summary>Solo lectura de todos los módulos del tipo de empresa (rol Consulta).</summary>
    public static IEnumerable<RolPermiso> PermisosSoloLectura(TipoEmpresa tipo = TipoEmpresa.Constructora) =>
        Modulos.ParaTipo(tipo)
            .Select(m => RolPermiso.Create(m.Codigo, ver: true, insertar: false, editar: false, eliminar: false));

    /// <summary>
    /// Rol Proveedor: mantiene el catálogo de la empresa (crea y edita insumos y sus precios, agrega
    /// clasificaciones) sin eliminar nada ni ver APU, presupuestos o administración.
    /// </summary>
    public static IEnumerable<RolPermiso> PermisosProveedor() =>
    [
        RolPermiso.Create(Modulos.Insumos, ver: true, insertar: true, editar: true, eliminar: false),
        RolPermiso.Create(Modulos.PreciosInsumo, ver: true, insertar: true, editar: true, eliminar: false),
        RolPermiso.Create(Modulos.Clasificaciones, ver: true, insertar: true, editar: false, eliminar: false),
        RolPermiso.Create(Modulos.UnidadesMedida, ver: true, insertar: false, editar: false, eliminar: false),
        RolPermiso.Create(Modulos.TiposInsumo, ver: true, insertar: false, editar: false, eliminar: false),
    ];

    /// <summary>Crea el rol de sistema Proveedor (el tenant lo asigna el contexto activo).</summary>
    public static Rol CrearProveedor(DateTimeOffset now) =>
        Create(Proveedor, "Mantiene insumos y precios del catálogo.", PermisosProveedor(), now, esSistema: true);
}

/// <summary>Qué puede hacer un rol en un módulo.</summary>
public sealed class RolPermiso
{
    private RolPermiso()
    {
        // Requerido por EF Core.
    }

    public Guid Id { get; private set; }

    public string Modulo { get; private set; } = string.Empty;

    public bool Ver { get; private set; }

    public bool Insertar { get; private set; }

    public bool Editar { get; private set; }

    public bool Eliminar { get; private set; }

    public static RolPermiso Create(string modulo, bool ver, bool insertar, bool editar, bool eliminar) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            Modulo = modulo,
            // Insertar, editar o eliminar implican poder ver.
            Ver = ver || insertar || editar || eliminar,
            Insertar = insertar,
            Editar = editar,
            Eliminar = eliminar,
        };

    public IEnumerable<Accion> Acciones()
    {
        if (Ver) yield return Accion.Ver;
        if (Insertar) yield return Accion.Insertar;
        if (Editar) yield return Accion.Editar;
        if (Eliminar) yield return Accion.Eliminar;
    }
}
