namespace InsulinAndCoffee.Domain.Entities;

public class AssistantConversation
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long DraftVersion { get; set; }
    public long Revision { get; set; }
    public string? DraftJson { get; set; }
    public List<AssistantMessage> Messages { get; set; } = [];
}

public class AssistantRequestReceipt
{
    public Guid UserId { get; set; }
    public Guid RequestId { get; set; }
    public Guid ConversationId { get; set; }
    public string PayloadHash { get; set; } = "";
    public string ResponseJson { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

public class AssistantMessage
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
    public string? ResponseJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
