using System.Diagnostics.CodeAnalysis;

namespace InfoPresupuestal.Api.Common.Abstractions;

/// <summary>Resultado de un caso de uso: un valor o un <see cref="Error"/>, nunca ambos.</summary>
public sealed class Result<TValue>
{
    private readonly TValue? _value;

    private Result(TValue value)
    {
        _value = value;
        IsSuccess = true;
    }

    private Result(Error error)
    {
        Error = error;
        IsSuccess = false;
    }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess { get; }

    public Error? Error { get; }

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("No se puede leer el valor de un resultado fallido.");

    public static Result<TValue> Success(TValue value) => new(value);

    public static Result<TValue> Failure(Error error) => new(error);

    public static implicit operator Result<TValue>(TValue value) => Success(value);

    public static implicit operator Result<TValue>(Error error) => Failure(error);
}
