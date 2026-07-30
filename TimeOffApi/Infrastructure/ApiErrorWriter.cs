namespace TimeOffApi.Infrastructure;

public static class ApiErrorWriter
{
    public static Task WriteAsync(
        HttpResponse response,
        int statusCode,
        string code,
        string message,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        response.StatusCode = statusCode;
        return response.WriteAsJsonAsync(new
        {
            statusCode,
            code,
            message,
            traceId
        }, cancellationToken);
    }
}
