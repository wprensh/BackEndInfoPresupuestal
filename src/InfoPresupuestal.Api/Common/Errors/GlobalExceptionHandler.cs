using Microsoft.AspNetCore.Diagnostics;

namespace InfoPresupuestal.Api.Common.Errors;

/// <summary>Captura excepciones no controladas, las registra y responde ProblemDetails sin filtrar detalles internos.</summary>
internal sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        LogUnhandledException(logger, exception, httpContext.TraceIdentifier);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Title = "Server.Error",
                Detail = "Ocurrió un error inesperado.",
            },
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Excepción no controlada en la solicitud {TraceId}")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception, string traceId);
}
