namespace LMS.Domain.Common;

/// <summary>
/// Represents the outcome of an operation that can succeed or fail without throwing.
/// Services return Result<T> instead of throwing for expected failures.
/// </summary>
public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }
    public int StatusCode { get; }

    private Result(bool isSuccess, T? value, string? error, int statusCode)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
        StatusCode = statusCode;
    }

    public static Result<T> Success(T value) => new(true, value, null, 200);
    public static Result<T> Failure(string error, int statusCode = 400) => new(false, default, error, statusCode);
}
