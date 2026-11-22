using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Domain;
using InsulinAndCoffee.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InsulinAndCoffee.Infrastructure;

public sealed class AiUsageWriter(IServiceScopeFactory scopes, ILogger<AiUsageWriter> logger) : IAiUsageWriter
{
    public async Task WriteAsync(AiUsageRecord record)
    {
        try
        {
            // Independent context and bounded lifetime: request cancellation or a tracking failure
            // must not poison the business transaction or lose an already completed provider call.
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var price = await db.AiModelPrices.AsNoTracking().Where(price => price.Provider == record.Provider && price.Model == record.Model
                && price.EffectiveFromUtc <= record.StartedAtUtc && (price.EffectiveToUtc == null || price.EffectiveToUtc > record.StartedAtUtc))
                .OrderByDescending(price => price.EffectiveFromUtc).FirstOrDefaultAsync(timeout.Token);
            AiCostCalculation.Apply(record, price);
            db.AiUsageRecords.Add(record);
            await db.SaveChangesAsync(timeout.Token);
        }
        catch (Exception error)
        {
            // Deliberately exclude exception messages and provider payloads from operational logs.
            logger.LogError("AI usage persistence failed: {FailureType}, usage {UsageId}, operation {OperationId}",
                error.GetType().Name, record.Id, record.OperationId);
        }
    }
}
