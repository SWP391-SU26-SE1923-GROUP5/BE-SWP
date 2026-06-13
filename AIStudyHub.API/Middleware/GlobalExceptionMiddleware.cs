using System.Net;
using System.Text.Json;
using FluentValidation;

namespace AIStudyHub.API.Middleware;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
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
        catch (ValidationException exception)
        {
            await WriteErrorResponseAsync(context, HttpStatusCode.BadRequest, exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            await WriteErrorResponseAsync(context, HttpStatusCode.Unauthorized, exception.Message);
        }
        catch (KeyNotFoundException exception)
        {
            await WriteErrorResponseAsync(context, HttpStatusCode.NotFound, exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            await WriteErrorResponseAsync(context, HttpStatusCode.Conflict, exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unhandled exception: {Message}\nStackTrace: {StackTrace}", exception.Message, exception.StackTrace);

            var isDevelopment = context.RequestServices.GetService<IHostEnvironment>()?.IsDevelopment() ?? false;
            var message = isDevelopment
                ? $"{exception.GetType().Name}: {exception.Message}"
                : "An unexpected error occurred.";

            await WriteErrorResponseAsync(context, HttpStatusCode.InternalServerError, message);
        }
    }

    private static async Task WriteErrorResponseAsync(HttpContext context, HttpStatusCode statusCode, string message)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var payload = new
        {
            statusCode = context.Response.StatusCode,
            message
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
