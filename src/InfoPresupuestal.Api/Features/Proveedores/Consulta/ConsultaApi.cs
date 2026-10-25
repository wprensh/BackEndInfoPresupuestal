using FluentValidation;
using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Features.Administracion.Empresas;
using InfoPresupuestal.Api.Features.Mantenimiento.Clasificaciones;
using InfoPresupuestal.Api.Features.Mantenimiento.Insumos;
using InfoPresupuestal.Api.Features.Proveedores.Catalogo;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Proveedores.Consulta;

/// <summary>
/// La constructora consulta los catálogos de los proveedores y trae precios a sus propios insumos.
/// Importar es en dos pasos: <c>revision</c> propone qué pasaría con cada precio y la importación lo
/// aplica volviendo a calcular la propuesta en el servidor, así lo que se acepta es lo que se revisó.
/// </summary>
internal static class ConsultaApi
{
    public const string ProductosRoute = $"{ProveedoresApi.DirectorioRoute}/productos";
    public const string ImportacionRoute = $"{ProveedoresApi.DirectorioRoute}/importacion";
    public const int MaxItems = 200;

    public const string Crear = "CREAR";
    public const string Actualizar = "ACTUALIZAR";
    public const string SinCambio = "SIN_CAMBIO";
    public const string Repetido = "REPETIDO";

    /// <summary>
    /// Qué pasaría con cada precio: crear un insumo nuevo, actualizar el precio de uno que ya existe
    /// (mismo tipo, unidad y descripción) o nada si el precio es igual. Un producto repetido en la misma
    /// importación solo cuenta la primera vez. Se importa el valor sin IVA.
    /// </summary>
    public static async Task<Result<List<PropuestaImportacion>>> ProponerAsync(AppDbContext db, IReadOnlyList<long> precioIds, CancellationToken cancellationToken)
    {
        var proveedores = db.Set<Empresa>().AsNoTracking().Where(e => e.Tipo == TipoEmpresa.Proveedor && e.Activo);
        var filas = await db.Set<ProveedorPrecio>().IgnoreQueryFilters().AsNoTracking()
            .Where(x => precioIds.Contains(x.Id))
            .Join(db.Set<ProveedorProducto>().IgnoreQueryFilters().Where(p => p.Activo), x => x.ProductoId, p => p.Id, (x, p) => new { Precio = x, Producto = p })
            .Join(proveedores, r => r.Producto.TenantId, e => e.TenantId, (r, e) => new
            {
                r.Precio, r.Producto, e.RazonSocial, Tipo = r.Producto.TipoInsumo!.Nombre, Unidad = r.Producto.UnidadMedida!.Codigo,
            })
            .ToListAsync(cancellationToken);

        var porId = filas.ToDictionary(f => f.Precio.Id);
        var faltantes = precioIds.Where(id => !porId.ContainsKey(id)).ToList();
        if (faltantes.Count > 0)
        {
            return Error.NotFound("Importacion.PrecioNoEncontrado", $"No existen o ya no están publicados los precios {string.Join(", ", faltantes)}.");
        }

        // Insumos propios (el filtro global deja solo los de la empresa) con la misma descripción.
        // ToUpper() se traduce a upper() de PostgreSQL, igual que el índice único de insumo.
#pragma warning disable CA1304, CA1311
        var descripciones = filas.Select(f => f.Producto.Descripcion.ToUpperInvariant()).Distinct().ToList();
        var insumos = await db.Set<Insumo>().AsNoTracking()
            .Where(i => descripciones.Contains(i.Descripcion.ToUpper()))
            .Select(i => new
            {
                i.Id, i.Codigo, i.TipoInsumoId, i.UnidadMedidaId, Descripcion = i.Descripcion.ToUpper(), i.ClasificacionId,
                Clasificacion = i.Clasificacion != null ? i.Clasificacion.Nombre : null,
                Precio = db.Set<InsumoPrecio>().Where(p => p.InsumoId == i.Id).OrderByDescending(p => p.FechaVigencia).Select(p => (decimal?)p.ValorUnitario).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);
#pragma warning restore CA1304, CA1311

        var clasificaciones = await db.Set<Clasificacion>().AsNoTracking().Where(c => c.Activo).Select(c => new { c.Id, c.Nombre }).ToListAsync(cancellationToken);

        var vistos = new HashSet<(short, short, string)>();
        var propuestas = new List<PropuestaImportacion>(precioIds.Count);
        foreach (var id in precioIds)
        {
            var f = porId[id];
            var clave = (f.Producto.TipoInsumoId, f.Producto.UnidadMedidaId, f.Producto.Descripcion.ToUpperInvariant());
            var insumo = insumos.FirstOrDefault(i => (i.TipoInsumoId, i.UnidadMedidaId, i.Descripcion.ToUpperInvariant()) == clave);
            var valor = f.Precio.ValorSinIva;
            var accion = !vistos.Add(clave) ? Repetido
                : insumo is null ? Crear
                : insumo.Precio == valor ? SinCambio
                : Actualizar;
            var sugerida = insumo is null && f.Producto.Categoria is { } categoria
                ? clasificaciones.FirstOrDefault(c => string.Equals(c.Nombre, categoria, StringComparison.OrdinalIgnoreCase))
                : null;

            propuestas.Add(new PropuestaImportacion(
                f.Precio.Id, f.Producto.Id, f.Producto.TenantId, f.RazonSocial, f.Producto.Codigo, f.Producto.Descripcion,
                f.Producto.TipoInsumoId, f.Tipo, f.Producto.UnidadMedidaId, f.Unidad, f.Precio.Ciudad, valor, f.Precio.FechaVigencia, accion,
                insumo?.Id, insumo?.Codigo, insumo?.Precio, insumo?.ClasificacionId ?? sugerida?.Id, insumo?.Clasificacion ?? sugerida?.Nombre));
        }

        return propuestas;
    }

    /// <summary>Texto de la fuente del precio importado: de qué proveedor y producto viene.</summary>
    public static string Fuente(PropuestaImportacion p)
    {
        var texto = $"{p.RazonSocial} {p.CodigoProveedor}{(p.Ciudad is null ? "" : $" {p.Ciudad}")} (sin IVA, lista del {p.FechaProveedor:yyyy-MM-dd})";
        return texto.Length <= InsumoPrecioConfiguration.FuenteMaxLength ? texto : texto[..InsumoPrecioConfiguration.FuenteMaxLength];
    }
}

/// <param name="Accion">CREAR, ACTUALIZAR, SIN_CAMBIO o REPETIDO.</param>
/// <param name="ValorSinIva">Lo que se guardaría como precio del insumo.</param>
/// <param name="PrecioActual">Precio vigente del insumo de la empresa, si ya existe.</param>
public sealed record PropuestaImportacion(
    long PrecioId,
    Guid ProductoId,
    string Proveedor,
    string RazonSocial,
    string CodigoProveedor,
    string Descripcion,
    short TipoInsumoId,
    string Tipo,
    short UnidadMedidaId,
    string Unidad,
    string? Ciudad,
    decimal ValorSinIva,
    DateOnly FechaProveedor,
    string Accion,
    Guid? InsumoId,
    string? InsumoCodigo,
    decimal? PrecioActual,
    int? ClasificacionId,
    string? Clasificacion);

public sealed record RevisionImportacionRequest(IReadOnlyList<long> PrecioIds);

/// <param name="ClasificacionId">Solo para insumos nuevos; vacío = la sugerida por la revisión.</param>
public sealed record ImportarItem(long PrecioId, int? ClasificacionId = null);

public sealed record ImportarRequest(IReadOnlyList<ImportarItem> Items);

public sealed record ResultadoImportacion(int Creados, int Actualizados, int SinCambio, IReadOnlyList<PropuestaImportacion> Items);

internal sealed class RevisionImportacionRequestValidator : AbstractValidator<RevisionImportacionRequest>
{
    public RevisionImportacionRequestValidator()
    {
        RuleFor(r => r.PrecioIds).NotEmpty().Must(ids => ids.Count <= ConsultaApi.MaxItems)
            .WithMessage($"Se importan máximo {ConsultaApi.MaxItems} precios a la vez.");
    }
}

internal sealed class ImportarRequestValidator : AbstractValidator<ImportarRequest>
{
    public ImportarRequestValidator()
    {
        RuleFor(r => r.Items).NotEmpty().Must(items => items.Count <= ConsultaApi.MaxItems)
            .WithMessage($"Se importan máximo {ConsultaApi.MaxItems} precios a la vez.");
    }
}
