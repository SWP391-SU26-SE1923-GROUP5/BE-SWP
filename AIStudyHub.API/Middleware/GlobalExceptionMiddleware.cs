using System.Net;
using System.Text.Json;
using FluentValidation;
using AIStudyHub.Business.Exceptions;

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
        catch (OtpInvalidException exception)
        {
            await WriteErrorResponseAsync(context, HttpStatusCode.BadRequest, exception.Message);
        }
        catch (OtpExpiredException exception)
        {
            await WriteErrorResponseAsync(context, (HttpStatusCode)410, exception.Message);
        }
        catch (OtpLockedException exception)
        {
            await WriteLockedResponseAsync(context, exception);
        }
        catch (InvalidOperationException exception)
        {
            await WriteErrorResponseAsync(context, HttpStatusCode.Conflict, exception.Message);
        }
        catch (QuotaExceededException exception)
        {
            await WriteQuotaExceededResponseAsync(context, exception);
        }
        catch (FileSizeLimitExceededException exception)
        {
            await WriteFileSizeLimitExceededResponseAsync(context, exception);
        }
        catch (StorageQuotaExceededException exception)
        {
            await WriteStorageQuotaExceededResponseAsync(context, exception);
        }
        catch (ExactGenerationCountException exception)
        {
            await WriteErrorResponseAsync(
                context,
                HttpStatusCode.UnprocessableEntity,
                exception.Message);
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

    private static async Task WriteLockedResponseAsync(HttpContext context, OtpLockedException exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.Locked;

        var payload = new
        {
            statusCode = context.Response.StatusCode,
            message = exception.Message,
            error = "OtpLocked",
            lockoutMinutes = exception.LockoutMinutes
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }

    private static async Task WriteQuotaExceededResponseAsync(HttpContext context, QuotaExceededException exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.Forbidden;

        var payload = new
        {
            statusCode = context.Response.StatusCode,
            message = exception.Message,
            error = "QuotaExceeded",
            currentUsage = exception.CurrentUsage,
            limit = exception.Limit,
            requestedTokens = exception.RequestedTokens
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }

    private static async Task WriteFileSizeLimitExceededResponseAsync(
        HttpContext context,
        FileSizeLimitExceededException exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;

        var payload = new
        {
            statusCode = context.Response.StatusCode,
            message = exception.Message,
            error = "FileSizeLimitExceeded",
            actualBytes = exception.ActualBytes,
            limitBytes = exception.LimitBytes
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }

    private static async Task WriteStorageQuotaExceededResponseAsync(
        HttpContext context,
        StorageQuotaExceededException exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status403Forbidden;

        var payload = new
        {
            statusCode = context.Response.StatusCode,
            message = exception.Message,
            error = "StorageQuotaExceeded",
            currentBytes = exception.CurrentBytes,
            limitBytes = exception.LimitBytes,
            requestedBytes = exception.RequestedBytes
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
