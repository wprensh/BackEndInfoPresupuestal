using FluentValidation;
using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Caching;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Errors;
using InfoPresupuestal.Api.Common.MultiTenancy;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;

var builder = WebApplication.CreateBuilder(args);
var assembly = typeof(Program).Assembly;

// Infraestructura transversal (Common/)
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerOpenApiTransformer>();
    options.AddOperationTransformer<TenantOpenApiTransformer>();
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSecurity(builder.Configuration);
builder.Services.AddMultiTenancy();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddCaching(builder.Configuration);
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()));

// Slices verticales (Features/): se descubren por reflexión
builder.Services.AddServiceInstallers(builder.Configuration, assembly);
builder.Services.AddHandlers(assembly);
builder.Services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
builder.Services.AddDataSeeders(assembly);
builder.Services.AddEndpoints(assembly);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    await app.MigrateAndSeedAsync();
}

app.MapEndpoints();

await app.RunAsync();

public partial class Program;
