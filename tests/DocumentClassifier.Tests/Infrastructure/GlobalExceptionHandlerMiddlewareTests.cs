using DocumentClassifier.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DocumentClassifier.Tests.Infrastructure;

public class GlobalExceptionHandlerMiddlewareTests
{
    private static ILogger<GlobalExceptionHandlerMiddleware> CreateMockLogger()
    {
        return new Mock<ILogger<GlobalExceptionHandlerMiddleware>>().Object;
    }

    [Fact]
    public async Task InvokeAsync_WithArgumentNullException_Returns400BadRequest()
    {
        // Arrange
        var middleware = new GlobalExceptionHandlerMiddleware(
            next: async (context) => throw new ArgumentNullException("testParam"),
            logger: CreateMockLogger()
        );

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);

        // Verify error response
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();

        Assert.Contains("INVALID_REQUEST", body);
        Assert.Contains("parameter is missing", body);
    }

    [Fact]
    public async Task InvokeAsync_WithFileNotFoundException_Returns404NotFound()
    {
        // Arrange
        var middleware = new GlobalExceptionHandlerMiddleware(
            next: async (context) => throw new FileNotFoundException(),
            logger: CreateMockLogger()
        );

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();

        Assert.Contains("NOT_FOUND", body);
    }

    [Fact]
    public async Task InvokeAsync_WithTimeoutException_Returns408RequestTimeout()
    {
        // Arrange
        var middleware = new GlobalExceptionHandlerMiddleware(
            next: async (context) => throw new TimeoutException(),
            logger: CreateMockLogger()
        );

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status408RequestTimeout, context.Response.StatusCode);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();

        Assert.Contains("TIMEOUT", body);
    }

    [Fact]
    public async Task InvokeAsync_WithUnknownException_Returns500InternalError()
    {
        // Arrange
        var middleware = new GlobalExceptionHandlerMiddleware(
            next: async (context) => throw new Exception("Unknown error"),
            logger: CreateMockLogger()
        );

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();

        // Verify no stack trace is exposed
        Assert.DoesNotContain("Exception", body);
        Assert.DoesNotContain("Unknown error", body);
        Assert.Contains("INTERNAL_ERROR", body);
    }

    [Fact]
    public async Task InvokeAsync_WithException_ResponseIsJson()
    {
        // Arrange
        var middleware = new GlobalExceptionHandlerMiddleware(
            next: async (context) => throw new Exception("Test"),
            logger: CreateMockLogger()
        );

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.NotNull(context.Response.ContentType);
        Assert.StartsWith("application/json", context.Response.ContentType);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();

        // Verify it's valid JSON with error fields
        Assert.Contains("\"message\"", body);
        Assert.Contains("\"errorCode\"", body);
        Assert.Contains("\"timestamp\"", body);
    }

    [Fact]
    public async Task InvokeAsync_WithSuccessfulRequest_DoesNotThrow()
    {
        // Arrange
        var middleware = new GlobalExceptionHandlerMiddleware(
            next: async (context) => { /* No exception */ },
            logger: CreateMockLogger()
        );

        var context = new DefaultHttpContext();

        // Act & Assert (should not throw)
        await middleware.InvokeAsync(context);
    }
}
