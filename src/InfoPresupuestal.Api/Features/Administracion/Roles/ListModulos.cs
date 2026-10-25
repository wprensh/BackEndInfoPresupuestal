using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.MultiTenancy;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Features.Administracion.Empresas;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Administracion.Roles;

/// <summary>Una columna de la matriz: qué acciones admite el módulo en un rol de empresa.</summary>
public sealed record ModuloResponse(string Codigo, string Nombre, string Grupo, string Alcance, IReadOnlyList<string> AccionesAsignables);

/// <summary>Catálogo de módulos para armar la matriz de permisos: solo los que aplican al tipo de la empresa actual.</summary>
internal sealed class ListModulosEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet($"{AdministracionApi.Route}/modulos", async (AppDbContext db, ITenantContext tenant, CancellationToken ct) =>
            {
                var tipo = await db.Set<Empresa>().Where(e => e.TenantId == tenant.TenantId).Select(e => e.Tipo).FirstOrDefaultAsync(ct);
                return Modulos.ParaTipo(tipo).Select(m => new ModuloResponse(
                    m.Codigo,
                    m.Nombre,
                    m.Grupo,
                    m.Alcance.ToString(),
                    Enum.GetValues<Accion>().Where(m.PermiteEnRol).Select(a => a.ToString().ToLowerInvariant()).ToList()));
            })
            .WithName("ListModulos")
            .WithTags(AdministracionApi.Tag)
            .RequirePermission(Modulos.Roles, Accion.Ver);
}
