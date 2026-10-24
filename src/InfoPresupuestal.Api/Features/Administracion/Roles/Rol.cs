using InfoPresupuestal.Api.Common.MultiTenancy;
using InfoPresupuestal.Api.Common.Security;

namespace InfoPresupuestal.Api.Features.Administracion.Roles;

/// <summary>Tipo de usuario dentro de una empresa, con sus permisos por módulo.</summary>
public sealed class Rol : ITenantEntity
{
    public const string Administrador = "Administrador";
    public const string Consulta = "Consulta";

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

    /// <summary>Permisos en formato <c>modulo:accion</c>, tal como viajan en el JWT.</summary>
    public IEnumerable<string> PermisosEfectivos() => _permisos.SelectMany(p => p.Acciones().Select(a => Modulos.Permiso(p.Modulo, a)));

    /// <summary>Todos los permisos que un rol de tenant puede tener (rol Administrador).</summary>
    public static IEnumerable<RolPermiso> PermisosCompletos() =>
        Modulos.Todos
            .Where(m => m.Alcance != AlcanceModulo.Plataforma)
            .Select(m => RolPermiso.Create(
                m.Codigo,
                ver: true,
                insertar: m.PermiteEnRol(Accion.Insertar),
                editar: m.PermiteEnRol(Accion.Editar),
                eliminar: m.PermiteEnRol(Accion.Eliminar)));

    /// <summary>Solo lectura de todos los módulos (rol Consulta).</summary>
    public static IEnumerable<RolPermiso> PermisosSoloLectura() =>
        Modulos.Todos
            .Where(m => m.Alcance != AlcanceModulo.Plataforma)
            .Select(m => RolPermiso.Create(m.Codigo, ver: true, insertar: false, editar: false, eliminar: false));
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
