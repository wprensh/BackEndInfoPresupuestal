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
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError,
        };

        return Results.Problem(
            statusCode: statusCode,
            title: error.Code,
            detail: error.Description);
    }

    public static IResult ToOk<T>(this Result<T> result) =>
        result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();

    public static IResult ToNoContent<T>(this Result<T> result) =>
        result.IsSuccess ? Results.NoContent() : result.Error.ToProblem();

    public static IResult ToHttp<T>(this Result<T> result, Func<T, IResult> onSuccess) =>
        result.IsSuccess ? onSuccess(result.Value) : result.Error.ToProblem();
}
