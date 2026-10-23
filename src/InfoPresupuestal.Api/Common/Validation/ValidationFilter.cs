using FluentValidation;

namespace InfoPresupuestal.Api.Common.Validation;

/// <summary>
/// Filtro de endpoint que valida el cuerpo con el <see cref="IValidator{T}"/> del slice
/// antes de llamar al handler. Los handlers reciben siempre datos válidos.
/// </summary>
public sealed class ValidationFilter<TRequest>(IValidator<TRequest> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();
        if (request is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Validation.EmptyBody", detail: "El cuerpo de la solicitud es obligatorio.");
        }

        var validation = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
        return validation.IsValid
            ? await next(context)
            : Results.ValidationProblem(validation.ToDictionary());
    }
}

public static class ValidationFilterExtensions
{
    public static RouteHandlerBuilder WithRequestValidation<TRequest>(this RouteHandlerBuilder builder) =>
        builder
            .AddEndpointFilter<ValidationFilter<TRequest>>()
            .ProducesValidationProblem();
}
