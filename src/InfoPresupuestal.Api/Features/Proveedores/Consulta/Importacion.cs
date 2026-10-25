using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Common.Validation;
using InfoPresupuestal.Api.Features.Administracion.Empresas;
using InfoPresupuestal.Api.Features.Mantenimiento.Clasificaciones;
using InfoPresupuestal.Api.Features.Mantenimiento.Insumos;
using InfoPresupuestal.Api.Features.Mantenimiento.TiposInsumo;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Proveedores.Consulta;

public sealed record RevisarImportacionQuery(RevisionImportacionRequest Datos) : IQuery<IReadOnlyList<PropuestaImportacion>>;

/// <summary>Paso 1: muestra qué haría la importación sin cambiar nada.</summary>
internal sealed class RevisarImportacionHandler(AppDbContext db) : IQueryHandler<RevisarImportacionQuery, IReadOnlyList<PropuestaImportacion>>
{
    public async Task<Result<IReadOnlyList<PropuestaImportacion>>> HandleAsync(RevisarImportacionQuery query, CancellationToken cancellationToken)
    {
        var propuestas = await ConsultaApi.ProponerAsync(db, query.Datos.PrecioIds.Distinct().ToList(), cancellationToken);
        return propuestas.IsSuccess ? propuestas.Value : propuestas.Error;
    }
}

public sealed record ImportarPreciosCommand(ImportarRequest Datos) : ICommand<ResultadoImportacion>;

/// <summary>
/// Paso 2: aplica lo que el usuario aceptó. Crea los insumos nuevos con su código consecutivo y agrega el
/// precio con vigencia de hoy, para que pase a ser el vigente; si ya hay un precio de hoy, lo corrige.
/// </summary>
internal sealed class ImportarPreciosHandler(AppDbContext db, TimeProvider timeProvider) : ICommandHandler<ImportarPreciosCommand, ResultadoImportacion>
{
    public async Task<Result<ResultadoImportacion>> HandleAsync(ImportarPreciosCommand command, CancellationToken cancellationToken)
    {
        var items = command.Datos.Items.DistinctBy(i => i.PrecioId).ToList();
        var propuestas = await ConsultaApi.ProponerAsync(db, items.Select(i => i.PrecioId).ToList(), cancellationToken);
        if (!propuestas.IsSuccess)
        {
            return propuestas.Error;
        }

        var clasificacionesValidas = await db.Set<Clasificacion>().Select(c => c.Id).ToListAsync(cancellationToken);
        var elegidas = items.ToDictionary(i => i.PrecioId, i => i.ClasificacionId);
        foreach (var id in elegidas.Values.OfType<int>().Where(id => !clasificacionesValidas.Contains(id)))
        {
            return Error.Validation("Importacion.ClasificacionInvalida", $"No existe la clasificación {id} en la empresa.");
        }

        var now = timeProvider.GetUtcNow();
        var hoy = DateOnly.FromDateTime(now.UtcDateTime);
        var tipos = await db.Set<TipoInsumo>().AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Prefijo, cancellationToken);
        var ultimos = new Dictionary<short, string?>();
        var aplicadas = new List<PropuestaImportacion>();
        int creados = 0, actualizados = 0, sinCambio = 0;

        foreach (var p in propuestas.Value)
        {
            switch (p.Accion)
            {
                case ConsultaApi.Crear:
                {
                    var prefijo = tipos[p.TipoInsumoId];
                    if (!ultimos.TryGetValue(p.TipoInsumoId, out var ultimo))
                    {
                        ultimo = await db.Set<Insumo>()
                            .Where(i => i.Codigo.StartsWith($"{prefijo}-"))
                            .OrderByDescending(i => i.Codigo.Length).ThenByDescending(i => i.Codigo)
                            .Select(i => i.Codigo)
                            .FirstOrDefaultAsync(cancellationToken);
                    }

                    var codigo = Insumo.SiguienteCodigo(prefijo, ultimo);
                    ultimos[p.TipoInsumoId] = codigo;
                    var clasificacionId = elegidas[p.PrecioId] ?? p.ClasificacionId;
                    var insumo = Insumo.Create(codigo, p.TipoInsumoId, clasificacionId, p.UnidadMedidaId, p.Descripcion, now);
                    db.Add(insumo);
                    db.Add(InsumoPrecio.Create(insumo.Id, p.ValorSinIva, null, hoy, ConsultaApi.Fuente(p), now));
                    aplicadas.Add(p with { InsumoId = insumo.Id, InsumoCodigo = codigo, ClasificacionId = clasificacionId });
                    creados++;
                    break;
                }

                case ConsultaApi.Actualizar:
                {
                    var deHoy = await db.Set<InsumoPrecio>().FirstOrDefaultAsync(x => x.InsumoId == p.InsumoId && x.FechaVigencia == hoy, cancellationToken);
                    if (deHoy is null)
                    {
                        db.Add(InsumoPrecio.Create(p.InsumoId!.Value, p.ValorSinIva, null, hoy, ConsultaApi.Fuente(p), now));
                    }
                    else
                    {
                        deHoy.Update(p.ValorSinIva, null, hoy, ConsultaApi.Fuente(p));
                    }

                    aplicadas.Add(p);
                    actualizados++;
                    break;
                }

                case ConsultaApi.SinCambio:
                    aplicadas.Add(p);
                    sinCambio++;
                    break;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return new ResultadoImportacion(creados, actualizados, sinCambio, aplicadas);
    }
}

internal sealed class RevisarImportacionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost($"{ConsultaApi.ImportacionRoute}/revision", async (RevisionImportacionRequest datos, IQueryHandler<RevisarImportacionQuery, IReadOnlyList<PropuestaImportacion>> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new RevisarImportacionQuery(datos), ct)).ToOk())
            .WithName("RevisarImportacionProveedores")
            .WithTags(ProveedoresApi.Tag)
            .WithRequestValidation<RevisionImportacionRequest>()
            .RequireTipoEmpresa(TipoEmpresa.Constructora)
            .RequirePermission(Modulos.Proveedores, Accion.Ver)
            .RequirePermission(Modulos.Insumos, Accion.Ver);
}

internal sealed class ImportarPreciosEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(ConsultaApi.ImportacionRoute, async (ImportarRequest datos, ICommandHandler<ImportarPreciosCommand, ResultadoImportacion> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new ImportarPreciosCommand(datos), ct)).ToOk())
            .WithName("ImportarPreciosProveedores")
            .WithTags(ProveedoresApi.Tag)
            .WithRequestValidation<ImportarRequest>()
            .RequireTipoEmpresa(TipoEmpresa.Constructora)
            .RequirePermission(Modulos.Proveedores, Accion.Ver)
            .RequirePermission(Modulos.Insumos, Accion.Insertar)
            .RequirePermission(Modulos.PreciosInsumo, Accion.Insertar);
}
