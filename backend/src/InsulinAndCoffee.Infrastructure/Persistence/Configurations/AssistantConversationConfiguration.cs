using InsulinAndCoffee.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InsulinAndCoffee.Infrastructure.Persistence.Configurations;

public class AssistantConversationConfiguration : IEntityTypeConfiguration<AssistantConversation>
{
    public void Configure(EntityTypeBuilder<AssistantConversation> entity)
    {
        entity.HasOne<User>().WithMany().HasForeignKey(conversation => conversation.UserId);
        entity.Property(conversation => conversation.Title).HasMaxLength(100);
        entity.Property(conversation => conversation.DraftVersion).IsConcurrencyToken().HasDefaultValue(0L);
        entity.Property(conversation => conversation.Revision).IsConcurrencyToken().HasDefaultValue(0L);
        entity.HasMany(conversation => conversation.Messages).WithOne().HasForeignKey(message => message.ConversationId);
    }
}

public class AssistantRequestReceiptConfiguration : IEntityTypeConfiguration<AssistantRequestReceipt>
{
    public void Configure(EntityTypeBuilder<AssistantRequestReceipt> entity)
    {
        entity.HasKey(receipt => new { receipt.UserId, receipt.RequestId });
        entity.HasOne<User>().WithMany().HasForeignKey(receipt => receipt.UserId);
        entity.Property(receipt => receipt.PayloadHash).HasMaxLength(64).IsRequired();
        entity.Property(receipt => receipt.ResponseJson).IsRequired();
        entity.HasIndex(receipt => receipt.ConversationId);
        // Keep receipts after explicit conversation deletion; replay then returns 404, never recreates it.
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
