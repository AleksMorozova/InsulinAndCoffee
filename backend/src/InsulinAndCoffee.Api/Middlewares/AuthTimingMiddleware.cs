using System.Diagnostics;

namespace InsulinAndCoffee.Api.Middlewares;

public sealed class AuthTimingMiddleware(RequestDelegate next, ILogger<AuthTimingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        // Fixed route labels only: never include query strings, identities or bodies.
        var path = context.Request.Path.Value?.ToLowerInvariant();
        if (path is not ("/api/auth/login" or "/api/auth/google" or "/api/auth/me" or "/api/settings"))
        {
            await next(context);
            return;
        }

        using var activity = new Activity("AuthenticationPipeline").Start();
        activity.SetTag("iac.auth.performance", true);
        var timer = Stopwatch.StartNew();
        try { await next(context); }
        finally
        {
            logger.LogInformation("Authentication HTTP timing Endpoint={Endpoint} Method={Method} StatusCode={StatusCode} DurationMs={DurationMs} Cancelled={Cancelled} TraceId={TraceId} CorrelationId={CorrelationId}",
                path, context.Request.Method, context.Response.StatusCode, timer.Elapsed.TotalMilliseconds,
                context.RequestAborted.IsCancellationRequested, activity.TraceId.ToString(), context.TraceIdentifier);
        }
    }
}
