using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Presupuestos.Apus;

public sealed record DuplicarApuCommand(Guid Id) : ICommand<ApuResponse>;

/// <summary>Crea un APU nuevo a partir de otro, con descripción "(COPIA)", para ajustarlo sin tocar el original.</summary>
internal sealed class DuplicarApuHandler(AppDbContext db, TimeProvider timeProvider) : ICommandHandler<DuplicarApuCommand, ApuResponse>
{
    public async Task<Result<ApuResponse>> HandleAsync(DuplicarApuCommand command, CancellationToken cancellationToken)
    {
        var origen = await db.Set<Apu>().AsNoTracking().Include(a => a.Detalles).FirstOrDefaultAsync(a => a.Id == command.Id, cancellationToken);
        if (origen is null)
        {
            return ApusApi.NotFound(command.Id);
        }

        var descripcion = await DescripcionLibreAsync(origen, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var copia = Apu.Create(
            await ApusApi.SiguienteCodigoAsync(db, cancellationToken), descripcion, origen.UnidadMedidaId, DateOnly.FromDateTime(now.UtcDateTime),
            origen.Observaciones, origen.Detalles.Select(d => d.Copiar()), now);
        db.Add(copia);
        await db.SaveChangesAsync(cancellationToken);

        return (await ApusApi.LeerAsync(db, copia.Id, cancellationToken))!;
    }

    private async Task<string> DescripcionLibreAsync(Apu origen, CancellationToken cancellationToken)
    {
        for (var n = 1; ; n++)
        {
            var sufijo = n == 1 ? " (COPIA)" : $" (COPIA {n})";
            var maximo = ApuConfiguration.DescripcionMaxLength - sufijo.Length;
            var descripcion = (origen.Descripcion.Length > maximo ? origen.Descripcion[..maximo].TrimEnd() : origen.Descripcion) + sufijo;
            if (!await ApusApi.ExisteDuplicadoAsync(db, null, descripcion, origen.UnidadMedidaId, cancellationToken))
            {
                return descripcion;
            }
        }
    }
}

internal sealed class DuplicarApuEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost($"{ApusApi.Route}/{{id:guid}}/duplicar", async (Guid id, ICommandHandler<DuplicarApuCommand, ApuResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new DuplicarApuCommand(id), ct)).ToHttp(a => Results.Created($"{ApusApi.Route}/{a.Id}", a)))
            .WithName("DuplicarApu")
            .WithTags(PresupuestosModulo.Tag)
            .Produces<ApuResponse>(StatusCodes.Status201Created)
            .RequirePermission(Modulos.Apu, Accion.Insertar);
}
