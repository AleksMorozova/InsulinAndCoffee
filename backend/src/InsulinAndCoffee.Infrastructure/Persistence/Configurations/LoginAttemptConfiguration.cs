using InsulinAndCoffee.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InsulinAndCoffee.Infrastructure.Persistence.Configurations;

public sealed class LoginAttemptConfiguration : IEntityTypeConfiguration<LoginAttempt>
{
    public void Configure(EntityTypeBuilder<LoginAttempt> entity)
    {
        entity.HasKey(attempt => attempt.Id);
        entity.Property(attempt => attempt.Username).HasMaxLength(50).IsRequired();
        entity.Property(attempt => attempt.FailureReason).HasMaxLength(64);
        entity.Property(attempt => attempt.IpAddress).HasMaxLength(64);
        entity.Property(attempt => attempt.UserAgent).HasMaxLength(512);
        entity.HasIndex(attempt => attempt.AttemptedAtUtc);
        entity.HasIndex(attempt => attempt.UserId);
        entity.HasIndex(attempt => attempt.IsSuccessful);
        entity.HasOne(attempt => attempt.User)
            .WithMany(user => user.LoginAttempts)
            .HasForeignKey(attempt => attempt.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
