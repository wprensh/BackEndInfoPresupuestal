using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Features.Mantenimiento.Clasificaciones;
using InfoPresupuestal.Api.Features.Mantenimiento.Insumos;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Tests.Features.Mantenimiento;

public sealed class MantenimientoTests
{
    private readonly string _database = Guid.NewGuid().ToString();

    [Theory]
    [InlineData("MAT", null, "MAT-0001")]
    [InlineData("MAT", "MAT-0041", "MAT-0042")]
    [InlineData("MO", "MO-9999", "MO-10000")]
    public void Next_insumo_code_follows_the_last_one(string prefijo, string? ultimo, string esperado) =>
        Assert.Equal(esperado, Insumo.SiguienteCodigo(prefijo, ultimo));

    [Fact]
    public void Descriptions_and_names_are_normalized_like_the_excel_load()
    {
        Assert.Equal("TUBO PVC 4\"", Insumo.NormalizeDescripcion("  TUBO   PVC 4\" "));
        Assert.Equal("HIERROS Y ACEROS", Clasificacion.Normalize(" hierros  y aceros"));
    }

    [Fact]
    public async Task Classification_names_are_unique_per_company_only()
    {
        await using (var acme = TestDb.Create("acme", _database))
        {
            Assert.True((await new CreateClasificacionHandler(acme, TimeProvider.System)
                .HandleAsync(new CreateClasificacionCommand(new CreateClasificacionRequest("Eléctricos")), CancellationToken.None)).IsSuccess);
        }

        await using var demo = TestDb.Create("demo", _database);
        var handler = new CreateClasificacionHandler(demo, TimeProvider.System);

        Assert.True((await handler.HandleAsync(new CreateClasificacionCommand(new CreateClasificacionRequest("eléctricos")), CancellationToken.None)).IsSuccess);
        var duplicada = await handler.HandleAsync(new CreateClasificacionCommand(new CreateClasificacionRequest("ELÉCTRICOS ")), CancellationToken.None);
        Assert.False(duplicada.IsSuccess);
        Assert.Equal(ErrorType.Conflict, duplicada.Error.Type);
        Assert.Single(await demo.Set<Clasificacion>().ToListAsync());
    }

    [Fact]
    public async Task A_classification_in_use_cannot_be_deleted()
    {
        await using var db = TestDb.Create("demo", _database);
        var clasificacion = Clasificacion.Create("HIERROS", TimeProvider.System.GetUtcNow());
        db.Add(clasificacion);
        await db.SaveChangesAsync();
        db.Add(Insumo.Create("MAT-0001", 1, clasificacion.Id, 1, "VARILLA 1/2", TimeProvider.System.GetUtcNow()));
        await db.SaveChangesAsync();

        var result = await new DeleteClasificacionHandler(db).HandleAsync(new DeleteClasificacionCommand(clasificacion.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Clasificaciones.EnUso", result.Error.Code);
    }
}
