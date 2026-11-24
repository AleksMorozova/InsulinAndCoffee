using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace InsulinAndCoffee.Infrastructure;

// EF supplies durations; no additional queries or connections are made. Never log SQL or parameters.
public sealed class AuthenticationDatabaseTimingInterceptor(ILogger<AuthenticationDatabaseTimingInterceptor> logger) : DbCommandInterceptor
{
    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        Record(eventData.Duration, "Reader", false);
        return ValueTask.FromResult(result);
    }

    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Record(eventData.Duration, "NonQuery", false);
        return ValueTask.FromResult(result);
    }

    public override ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default)
    {
        Record(eventData.Duration, "Scalar", false);
        return ValueTask.FromResult(result);
    }

    public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Record(eventData.Duration, "Command", true);
        return Task.CompletedTask;
    }

    private void Record(TimeSpan duration, string operation, bool failed)
    {
        if (Activity.Current?.GetTagItem("iac.auth.performance") is true)
            logger.LogInformation("Authentication database timing Operation={Operation} DurationMs={DurationMs} Failed={Failed} TraceId={TraceId}",
                operation, duration.TotalMilliseconds, failed, Activity.Current.TraceId.ToString());
    }
}

public sealed class AuthenticationConnectionTimingInterceptor(ILogger<AuthenticationConnectionTimingInterceptor> logger) : DbConnectionInterceptor
{
    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Record(eventData.Duration, false);
        return Task.CompletedTask;
    }

    public override Task ConnectionFailedAsync(DbConnection connection, ConnectionErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Record(eventData.Duration, true);
        return Task.CompletedTask;
    }

    private void Record(TimeSpan duration, bool failed)
    {
        if (Activity.Current?.GetTagItem("iac.auth.performance") is true)
            logger.LogInformation("Authentication database connection timing DurationMs={DurationMs} Failed={Failed} TraceId={TraceId}",
                duration.TotalMilliseconds, failed, Activity.Current.TraceId.ToString());
    }
}
