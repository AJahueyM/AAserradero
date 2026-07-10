using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using AntiguoAserradero.Domain.Errors;
using FluentValidation;

namespace AntiguoAserradero.Api.Middleware;

public sealed class ErrorHandlingMiddleware
{
    private static readonly Action<ILogger, string, PathString, Exception?> LogUnhandledException =
        LoggerMessage.Define<string, PathString>(
            LogLevel.Error,
            new EventId(1, nameof(LogUnhandledException)),
            "Unhandled exception while processing {Method} {Path}");

    private static readonly Action<ILogger, string, Exception?> LogTypedError =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(2, nameof(LogTypedError)),
            "Request failed with typed error {ErrorCode}");

    private readonly RequestDelegate _next;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, IHostEnvironment environment, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _environment = environment;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception) when (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, code, message, details) = MapException(exception);

        if (statusCode >= (int)HttpStatusCode.InternalServerError)
        {
            LogUnhandledException(_logger, context.Request.Method, context.Request.Path, exception);
        }
        else
        {
            LogTypedError(_logger, code, exception);
        }

        if (context.Response.HasStarted)
        {
            throw exception;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var body = new ErrorEnvelope(new ErrorBody(code, message, details));
        await JsonSerializer.SerializeAsync(context.Response.Body, body, ErrorJsonContext.Default.ErrorEnvelope, context.RequestAborted);
    }

    private (int StatusCode, string Code, string Message, object? Details) MapException(Exception exception)
    {
        return exception switch
        {
            AntiguoAserradero.Domain.Errors.ValidationException domainException => (StatusCodes.Status400BadRequest, domainException.Code, domainException.Message, domainException.Details),
            NotFoundException domainException => (StatusCodes.Status404NotFound, domainException.Code, domainException.Message, domainException.Details),
            ConflictException domainException => (StatusCodes.Status409Conflict, domainException.Code, domainException.Message, domainException.Details),
            ForbiddenException domainException => (StatusCodes.Status403Forbidden, domainException.Code, domainException.Message, domainException.Details),
            UnauthorizedException domainException => (StatusCodes.Status401Unauthorized, domainException.Code, domainException.Message, domainException.Details),
            FluentValidation.ValidationException validationException => (StatusCodes.Status400BadRequest, "Validation.Failed", "One or more validation errors occurred.", validationException.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray())),
            _ => (StatusCodes.Status500InternalServerError, "Server.Unexpected", _environment.IsProduction() ? "An unexpected error occurred." : exception.Message, _environment.IsProduction() ? null : new { exception.GetType().Name }),
        };
    }
}

public sealed record ErrorEnvelope(ErrorBody Error);

public sealed record ErrorBody(string Code, string Message, object? Details = null);

[JsonSerializable(typeof(ErrorEnvelope))]
internal sealed partial class ErrorJsonContext : JsonSerializerContext;
