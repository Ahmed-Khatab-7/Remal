using Microsoft.AspNetCore.Mvc;
using FV = FluentValidation;
using Remal.Application.Common.Exceptions;
using Remal.Application.Common.Models;

namespace Remal.Api.Middleware;

/// <summary>
/// RFC 7807 ProblemDetails + ApiResponse envelope. Logs once per exception, never leaks stack traces in production.
/// </summary>
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger, IHostEnvironment env)
    {
        _next = next; _logger = logger; _env = env;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try { await _next(ctx); }
        catch (Exception ex) { await HandleAsync(ctx, ex); }
    }

    private async Task HandleAsync(HttpContext ctx, Exception ex)
    {
        ApiResponse response;
        int status;

        switch (ex)
        {
            case ValidationException vex:
                status = vex.StatusCode;
                response = new ApiResponse { Success = false, Message = vex.Message, ErrorCode = vex.ErrorCode, Errors = vex.Errors };
                break;

            case AppException ax:
                status = ax.StatusCode;
                response = new ApiResponse { Success = false, Message = ax.Message, ErrorCode = ax.ErrorCode, Errors = ax.Errors };
                _logger.LogWarning(ex, "App exception: {Message}", ex.Message);
                break;

            case FV.ValidationException fvex:
                status = 422;
                var errors = fvex.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
                response = new ApiResponse { Success = false, Message = "فشل التحقق من البيانات", ErrorCode = "VALIDATION_FAILED", Errors = errors };
                break;

            default:
                status = 500;
                response = new ApiResponse
                {
                    Success = false,
                    Message = _env.IsDevelopment() ? ex.Message : "حدث خطأ غير متوقع — جرب تاني",
                    ErrorCode = "INTERNAL_ERROR",
                };
                _logger.LogError(ex, "Unhandled exception");
                break;
        }

        ctx.Response.ContentType = "application/json";
        ctx.Response.StatusCode = status;

        // RFC 7807 ProblemDetails as the primary error shape; ApiResponse fields included for backward compat.
        var problem = new ProblemDetails
        {
            Type = $"https://remal.eg/errors/{response.ErrorCode ?? "internal"}",
            Title = response.Message,
            Status = status,
            Instance = ctx.Request.Path,
            Extensions =
            {
                ["success"] = false,
                ["errorCode"] = response.ErrorCode,
                ["errors"] = response.Errors,
                ["traceId"] = ctx.TraceIdentifier,
            },
        };

        await ctx.Response.WriteAsJsonAsync(problem);
    }
}