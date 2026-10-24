using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Security;

namespace InfoPresupuestal.Api.Features.Administracion.Roles;

/// <summary>Una columna de la matriz: qué acciones admite el módulo en un rol de empresa.</summary>
public sealed record ModuloResponse(string Codigo, string Nombre, string Grupo, string Alcance, IReadOnlyList<string> AccionesAsignables);

/// <summary>Catálogo de módulos para armar la matriz de permisos en la interfaz.</summary>
internal sealed class ListModulosEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet($"{AdministracionApi.Route}/modulos", () =>
                Modulos.Todos.Where(m => m.Alcance != AlcanceModulo.Plataforma).Select(m => new ModuloResponse(
                    m.Codigo,
                    m.Nombre,
                    m.Grupo,
                    m.Alcance.ToString(),
                    Enum.GetValues<Accion>().Where(m.PermiteEnRol).Select(a => a.ToString().ToLowerInvariant()).ToList())))
            .WithName("ListModulos")
            .WithTags(AdministracionApi.Tag)
            .RequirePermission(Modulos.Roles, Accion.Ver);
}
