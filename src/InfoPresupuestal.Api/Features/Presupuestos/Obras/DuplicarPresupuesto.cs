using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Presupuestos.Obras;

public sealed record DuplicarPresupuestoCommand(Guid Id) : ICommand<PresupuestoResponse>;

/// <summary>Copia un presupuesto (de cualquier estado) como borrador nuevo, con los mismos valores unitarios.</summary>
internal sealed class DuplicarPresupuestoHandler(AppDbContext db, TimeProvider timeProvider) : ICommandHandler<DuplicarPresupuestoCommand, PresupuestoResponse>
{
    public async Task<Result<PresupuestoResponse>> HandleAsync(DuplicarPresupuestoCommand command, CancellationToken cancellationToken)
    {
        var origen = await db.Set<Presupuesto>().AsNoTracking()
            .Include(p => p.Capitulos).ThenInclude(c => c.Items)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == command.Id, cancellationToken);
        if (origen is null)
        {
            return PresupuestosApi.NotFound(command.Id);
        }

        var now = timeProvider.GetUtcNow();
        const string sufijo = " (COPIA)";
        var maximo = PresupuestoConfiguration.TextoMaxLength - sufijo.Length;
        var nombre = (origen.NombreObra.Length > maximo ? origen.NombreObra[..maximo].TrimEnd() : origen.NombreObra) + sufijo;
        var encabezado = new EncabezadoPresupuesto(
            nombre, origen.Cliente, origen.Ubicacion, DateOnly.FromDateTime(now.UtcDateTime), null, origen.Aiu,
            origen.DuracionDias, origen.AnticipoPct, origen.Observaciones);
        var capitulos = origen.Capitulos.OrderBy(c => c.Numero)
            .Select(c => PresupuestoCapitulo.Create(c.Numero, c.Nombre, c.Items.OrderBy(i => i.Numero).Select(i => i.Copiar())));

        var copia = Presupuesto.Create(await PresupuestosApi.SiguienteCodigoAsync(db, cancellationToken), encabezado, capitulos, now);
        db.Add(copia);
        await db.SaveChangesAsync(cancellationToken);
        return (await PresupuestosApi.LeerAsync(db, copia.Id, cancellationToken))!;
    }
}

internal sealed class DuplicarPresupuestoEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost($"{PresupuestosApi.Route}/{{id:guid}}/duplicar", async (Guid id, ICommandHandler<DuplicarPresupuestoCommand, PresupuestoResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new DuplicarPresupuestoCommand(id), ct)).ToHttp(p => Results.Created($"{PresupuestosApi.Route}/{p.Id}", p)))
            .WithName("DuplicarPresupuesto")
            .WithTags(PresupuestosModulo.Tag)
            .Produces<PresupuestoResponse>(StatusCodes.Status201Created)
            .RequirePermission(Modulos.Presupuestos, Accion.Insertar);
}
