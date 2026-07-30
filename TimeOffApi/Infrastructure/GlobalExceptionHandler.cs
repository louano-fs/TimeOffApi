using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace TimeOffApi.Infrastructure;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, code, message) = exception switch
        {
            AppException app => (app.StatusCode, app.Code, app.Message),
            DbUpdateException => (StatusCodes.Status409Conflict, "DATABASE_CONFLICT",
                "The operation conflicted with another request. Please refresh and try again."),
            _ => (StatusCodes.Status500InternalServerError, "INTERNAL_ERROR",
                "An unexpected error occurred.")
        };

        if (status >= 500)
            logger.LogError(exception, "Unhandled exception for trace {TraceId}", httpContext.TraceIdentifier);
        else
            logger.LogWarning("Request failed with {Code}: {Message}", code, message);

        var response = new
        {
            statusCode = status,
            code,
            message,
            traceId = httpContext.TraceIdentifier,
            detail = environment.IsDevelopment() && status == 500 ? exception.Message : null
        };

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
        return true;
    }
}
