using InfoPresupuestal.Api.Common.Abstractions;

namespace InfoPresupuestal.Api.Features.Products;

internal static class ProductErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("Products.NotFound", $"No existe un producto con id '{id}'.");

    public static Error NameAlreadyExists(string name) =>
        Error.Conflict("Products.NameAlreadyExists", $"Ya existe un producto llamado '{name}'.");
}
