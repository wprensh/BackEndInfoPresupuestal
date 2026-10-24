namespace InfoPresupuestal.Api.Common.Abstractions;

public enum ErrorType
{
    Failure,
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,
}

/// <summary>Error de negocio esperado. Las excepciones quedan para fallos inesperados.</summary>
public sealed record Error(string Code, string Description, ErrorType Type = ErrorType.Failure)
{
    public static Error NotFound(string code, string description) => new(code, description, ErrorType.NotFound);

    public static Error Conflict(string code, string description) => new(code, description, ErrorType.Conflict);

    public static Error Validation(string code, string description) => new(code, description, ErrorType.Validation);

    public static Error Unauthorized(string code, string description) => new(code, description, ErrorType.Unauthorized);

    public static Error Forbidden(string code, string description) => new(code, description, ErrorType.Forbidden);
}
