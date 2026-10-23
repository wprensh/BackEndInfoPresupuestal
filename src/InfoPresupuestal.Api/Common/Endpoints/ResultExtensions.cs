using InfoPresupuestal.Api.Common.Abstractions;

namespace InfoPresupuestal.Api.Common.Endpoints;

public static class ResultExtensions
{
    /// <summary>Convierte un <see cref="Error"/> en una respuesta ProblemDetails (RFC 9457).</summary>
    public static IResult ToProblem(this Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };

        return Results.Problem(
            statusCode: statusCode,
            title: error.Code,
            detail: error.Description);
    }
}
