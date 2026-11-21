using InsulinAndCoffee.Application.Services;
using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace InsulinAndCoffee.Api.ExceptionHandling;

public class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problemDetails = exception switch
        {
            DeliveryMealConflictException ex => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Saved order changed",
                Detail = ex.Message
            },
            CarbVerificationConflictException ex => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Carb snapshot changed",
                Detail = ex.Message
            },
            AssistantConflictException ex => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Assistant request conflict",
                Detail = ex.Message
            },
            MealAssistantUnavailableException ex => new ProblemDetails
            {
                Status = ex.Code == "assistant_timeout" ? StatusCodes.Status504GatewayTimeout : StatusCodes.Status503ServiceUnavailable,
                Title = "Meal Assistant unavailable",
                Detail = ex.Message
            },

            AppValidationException ex => new ValidationProblemDetails(ex.Errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
                Detail = ex.Message
            },

            CarbCalculationException ex => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
                Detail = ex.Message
            },

            ValidationException ex => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
                Detail = ex.Message
            },

            NotFoundException ex => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Resource not found",
                Detail = ex.Message
            },

            UnauthorizedAccessException => new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Access denied",
                Detail = "You do not have permission to perform this operation."
            },

            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred"
            }
        };

        if (problemDetails.Status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception while processing {Method} {Path}",
                httpContext.Request.Method,
                httpContext.Request.Path);
        }

        if (exception is MealAssistantUnavailableException assistantError)
            problemDetails.Extensions["code"] = assistantError.Code;
        if (exception is CarbVerificationConflictException) problemDetails.Extensions["code"] = "carb_snapshot_changed";
        if (exception is DeliveryMealConflictException) problemDetails.Extensions["code"] = "saved_order_changed";
        if (exception is AssistantConflictException conflict)
        {
            problemDetails.Extensions["code"] = conflict.Code;
            if (conflict.CurrentDraftVersion is long version) problemDetails.Extensions["currentDraftVersion"] = version;
        }
        problemDetails.Instance = httpContext.Request.Path;
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;

        var written = await problemDetailsService.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = problemDetails,
                Exception = exception
            });

        if (!written)
        {
            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        }

        return true;
    }
}
