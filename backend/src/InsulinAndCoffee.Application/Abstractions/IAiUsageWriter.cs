using InsulinAndCoffee.Domain.Entities;

namespace InsulinAndCoffee.Application.Abstractions;

public interface IAiUsageWriter
{
    Task WriteAsync(AiUsageRecord record);
}

public sealed class AiRequestContext
{
    public Guid? UserId { get; set; }
    public Guid OperationId { get; set; }
    public Guid? ConversationId { get; set; }
    public string? CorrelationId { get; set; }
}
