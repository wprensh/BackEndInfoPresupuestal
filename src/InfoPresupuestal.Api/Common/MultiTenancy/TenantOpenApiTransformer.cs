using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

namespace InfoPresupuestal.Api.Common.MultiTenancy;

/// <summary>Documenta el header opcional del superadministrador en los endpoints que dependen de tenant.</summary>
internal sealed class TenantOpenApiTransformer(IOptions<MultiTenancyOptions> options) : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.Description.ActionDescriptor.EndpointMetadata.OfType<RequiresTenantMetadata>().Any())
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = options.Value.HeaderName,
                In = ParameterLocation.Header,
                Required = false,
                Description = "Solo superadministrador: empresa sobre la que actúa. Los demás usuarios usan la empresa de su token.",
                Schema = new OpenApiSchema { Type = "string" },
            });
        }

        return Task.CompletedTask;
    }
}

/// <summary>Metadato que marca un endpoint como dependiente de tenant.</summary>
internal sealed class RequiresTenantMetadata;
