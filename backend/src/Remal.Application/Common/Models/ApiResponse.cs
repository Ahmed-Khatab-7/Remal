namespace Remal.Application.Common.Models;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
    public string? ErrorCode { get; set; }
    public IDictionary<string, string[]>? Errors { get; set; }

    public static ApiResponse<T> Ok(T data, string? message = null) => new() { Success = true, Data = data, Message = message };
    public static ApiResponse<T> Fail(string message, string? errorCode = null, IDictionary<string, string[]>? errors = null)
        => new() { Success = false, Message = message, ErrorCode = errorCode, Errors = errors };
}

public class ApiResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? ErrorCode { get; set; }
    public IDictionary<string, string[]>? Errors { get; set; }

    public static ApiResponse Ok(string? message = null) => new() { Success = true, Message = message };
    public static ApiResponse Fail(string message, string? errorCode = null) => new() { Success = false, Message = message, ErrorCode = errorCode };
}
