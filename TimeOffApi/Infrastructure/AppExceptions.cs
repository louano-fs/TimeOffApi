namespace TimeOffApi.Infrastructure;

public abstract class AppException(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}

public sealed class ValidationException(string code, string message)
    : AppException(StatusCodes.Status400BadRequest, code, message);

public sealed class UnauthorizedException(string code, string message)
    : AppException(StatusCodes.Status401Unauthorized, code, message);

public sealed class ForbiddenException(string code, string message)
    : AppException(StatusCodes.Status403Forbidden, code, message);

public sealed class NotFoundException(string code, string message)
    : AppException(StatusCodes.Status404NotFound, code, message);

public sealed class ConflictException(string code, string message)
    : AppException(StatusCodes.Status409Conflict, code, message);

public sealed class TooManyRequestsException(string code, string message)
    : AppException(StatusCodes.Status429TooManyRequests, code, message);

public sealed class ServiceUnavailableException(string code, string message)
    : AppException(StatusCodes.Status503ServiceUnavailable, code, message);
