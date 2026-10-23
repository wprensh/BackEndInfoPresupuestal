using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

namespace InfoPresupuestal.Api.Common.MultiTenancy;

/// <summary>Documenta el header del tenant en los endpoints que lo exigen.</summary>
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
                Required = true,
                Description = "Id del tenant.",
                Schema = new OpenApiSchema { Type = "string" },
            });
        }

        return Task.CompletedTask;
    }
}

/// <summary>Metadato que marca un endpoint como dependiente de tenant.</summary>
internal sealed class RequiresTenantMetadata;
