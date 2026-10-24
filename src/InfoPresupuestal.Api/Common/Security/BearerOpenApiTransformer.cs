using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

namespace InfoPresupuestal.Api.Common.Security;

/// <summary>Documenta en OpenAPI que la API se usa con <c>Authorization: Bearer &lt;token&gt;</c>.</summary>
internal sealed class BearerOpenApiTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var scheme = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Token de POST /api/auth/login.",
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes["Bearer"] = scheme;
        document.SecurityRequirements.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = [],
        });

        return Task.CompletedTask;
    }
}
