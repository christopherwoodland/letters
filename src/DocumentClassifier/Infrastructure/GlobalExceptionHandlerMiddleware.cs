using System.Net;
using System.Text.Json;

namespace DocumentClassifier.Infrastructure;

/// <summary>
/// Global exception handler middleware.
/// Catches all unhandled exceptions and returns standardized error responses without exposing internal details.
/// </summary>
public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in request pipeline");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var response = new ErrorResponse();

        switch (exception)
        {
            case ArgumentNullException argNull:
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                response.Message = "Required parameter is missing.";
                response.ErrorCode = "INVALID_REQUEST";
                break;

            case ArgumentException arg:
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                response.Message = "Invalid request parameters.";
                response.ErrorCode = "INVALID_REQUEST";
                break;

            case InvalidOperationException invalid:
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                response.Message = "The operation cannot be performed in the current state.";
                response.ErrorCode = "INVALID_OPERATION";
                break;

            case FileNotFoundException:
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                response.Message = "The requested resource was not found.";
                response.ErrorCode = "NOT_FOUND";
                break;

            case TimeoutException:
                context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
                response.Message = "The request took too long to process.";
                response.ErrorCode = "TIMEOUT";
                break;

            default:
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                response.Message = "An unexpected error occurred. Please try again later.";
                response.ErrorCode = "INTERNAL_ERROR";
                break;
        }

        // Include correlation ID for tracing
        if (context.Items.TryGetValue("CorrelationId", out var correlationId) && correlationId != null)
        {
            response.TraceId = correlationId.ToString();
        }

        return context.Response.WriteAsJsonAsync(response);
    }
}

/// <summary>
/// Standard error response format for API errors.
/// </summary>
public class ErrorResponse
{
    public string Message { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public string? TraceId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
