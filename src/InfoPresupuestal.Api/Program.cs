using FluentValidation;
using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Caching;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Errors;
using InfoPresupuestal.Api.Common.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var assembly = typeof(Program).Assembly;

// Infraestructura transversal (Common/)
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddCaching(builder.Configuration);

// Slices verticales (Features/): se descubren por reflexión
builder.Services.AddHandlers(assembly);
builder.Services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
builder.Services.AddEndpoints(assembly);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.MapEndpoints();

await app.RunAsync();

public partial class Program;
