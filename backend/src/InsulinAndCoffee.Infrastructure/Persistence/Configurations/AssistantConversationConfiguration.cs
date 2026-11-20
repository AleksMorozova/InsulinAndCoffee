using InsulinAndCoffee.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InsulinAndCoffee.Infrastructure.Persistence.Configurations;

public class AssistantConversationConfiguration : IEntityTypeConfiguration<AssistantConversation>
{
    public void Configure(EntityTypeBuilder<AssistantConversation> entity)
    {
        entity.HasOne<User>().WithMany().HasForeignKey(conversation => conversation.UserId);
        entity.HasMany(conversation => conversation.Messages).WithOne().HasForeignKey(message => message.ConversationId);
    }
}

public class AssistantMessageConfiguration : IEntityTypeConfiguration<AssistantMessage>
{
    public void Configure(EntityTypeBuilder<AssistantMessage> entity)
    {
        entity.Property(message => message.Content).HasMaxLength(1000);
        entity.Property(message => message.Role).HasMaxLength(16);
        entity.HasIndex(message => new { message.ConversationId, message.CreatedAt });
    }
}
