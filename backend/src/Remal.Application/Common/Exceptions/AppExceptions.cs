namespace Remal.Application.Common.Exceptions;

public class AppException : Exception
{
    public int StatusCode { get; }
    public string? ErrorCode { get; }
    public IDictionary<string, string[]>? Errors { get; }

    public AppException(string message, int statusCode = 400, string? errorCode = null, IDictionary<string, string[]>? errors = null)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        Errors = errors;
    }
}

public class NotFoundException : AppException
{
    public NotFoundException(string entity, object key)
        : base($"{entity} '{key}' غير موجود", 404, "NOT_FOUND") { }

    public NotFoundException(string message)
        : base(message, 404, "NOT_FOUND") { }
}

public class ValidationException : AppException
{
    public ValidationException(IDictionary<string, string[]> errors)
        : base("فشل التحقق من البيانات", 422, "VALIDATION_FAILED", errors) { }
}

public class BadRequestException : AppException
{
    public BadRequestException(string message, string? errorCode = null)
        : base(message, 400, errorCode ?? "BAD_REQUEST") { }
}

public class UnauthorizedException : AppException
{
    public UnauthorizedException(string message = "غير مصرح بالوصول")
        : base(message, 401, "UNAUTHORIZED") { }
}

public class ForbiddenException : AppException
{
    public ForbiddenException(string message = "ممنوع")
        : base(message, 403, "FORBIDDEN") { }
}

public class ConflictException : AppException
{
    public ConflictException(string message)
        : base(message, 409, "CONFLICT") { }
}
