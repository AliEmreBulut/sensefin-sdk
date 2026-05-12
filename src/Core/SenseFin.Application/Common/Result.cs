namespace SenseFin.Application.Common;

/// <summary>
/// Generic Result pattern for returning success/failure from application layer operations.
/// Eliminates exceptions for control flow and provides explicit error handling.
/// </summary>
public sealed record Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? ErrorMessage { get; }

    private Result(bool isSuccess, T? value, string? errorMessage)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Creates a successful result with a value.
    /// </summary>
    public static Result<T> Success(T value) => new(true, value, null);

    /// <summary>
    /// Creates a failure result with an error message.
    /// </summary>
    public static Result<T> Failure(string errorMessage) => new(false, default, errorMessage);
}
