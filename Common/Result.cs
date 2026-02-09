namespace MatchmakingService.Common;

/// <summary>
/// Generic result type for service operations.
/// Encapsulates success/failure with typed data and error messages.
/// </summary>
public class Result<T>
{
    public bool IsSuccess { get; set; }
    public T? Data { get; set; }
    public string? Error { get; set; }
    public string? ErrorCode { get; set; }

    public static Result<T> Success(T data) => new()
    {
        IsSuccess = true,
        Data = data
    };

    public static Result<T> Failure(string error, string? errorCode = null) => new()
    {
        IsSuccess = false,
        Error = error,
        ErrorCode = errorCode
    };
}

/// <summary>
/// Non-generic result for operations that don't return data.
/// </summary>
public class Result
{
    public bool IsSuccess { get; set; }
    public string? Error { get; set; }
    public string? ErrorCode { get; set; }

    public static Result Success() => new() { IsSuccess = true };

    public static Result Failure(string error, string? errorCode = null) => new()
    {
        IsSuccess = false,
        Error = error,
        ErrorCode = errorCode
    };
}
