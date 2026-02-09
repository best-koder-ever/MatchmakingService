namespace MatchmakingService.Common;

/// <summary>
/// Standard API response envelope.
/// Provides consistent JSON shape for all endpoints:
/// { success: bool, data: T?, message: string?, errorCode: string?, errors: string[]? }
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public string? ErrorCode { get; set; }
    public List<string>? Errors { get; set; }

    public static ApiResponse<T> SuccessResult(T data, string? message = null) => new()
    {
        Success = true,
        Data = data,
        Message = message
    };

    public static ApiResponse<T> FailureResult(string message, string? errorCode = null) => new()
    {
        Success = false,
        Message = message,
        ErrorCode = errorCode
    };

    public static ApiResponse<T> FailureResult(string message, List<string> errors, string? errorCode = null) => new()
    {
        Success = false,
        Message = message,
        Errors = errors,
        ErrorCode = errorCode
    };
}
