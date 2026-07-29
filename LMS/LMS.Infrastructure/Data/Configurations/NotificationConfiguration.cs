using LMS.Domain.Entities;
using LMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LMS.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core fluent configuration for <see cref="Notification"/> → <c>notifications</c> table.
/// Two composite indexes support the two primary read patterns:
/// <list type="bullet">
///   <item><c>(user_id, is_read, created_at DESC)</c> — unread count query</item>
///   <item><c>(user_id, created_at DESC)</c> — notification list with pagination</item>
/// </list>
/// </summary>
public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(n => n.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        // Store NotificationType enum as smallint
        builder.Property(n => n.Type)
            .HasColumnName("type")
            .HasConversion<short>()
            .IsRequired();

        builder.Property(n => n.Title)
            .HasColumnName("title")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(n => n.Body)
            .HasColumnName("body")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(n => n.IsRead)
            .HasColumnName("is_read")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(n => n.ResourceType)
            .HasColumnName("resource_type")
            .HasMaxLength(50);

        builder.Property(n => n.ResourceId)
            .HasColumnName("resource_id")
            .HasColumnType("uuid");

        builder.Property(n => n.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        // Composite index: unread count query (user_id, is_read, created_at DESC)
        builder.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt })
            .IsDescending(false, false, true)
            .HasDatabaseName("ix_notifications_user_id_is_read_created_at");

        // Composite index: notification list (user_id, created_at DESC)
        builder.HasIndex(n => new { n.UserId, n.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_notifications_user_id_created_at");

        // FK → users
        builder.HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .HasConstraintName("fk_notifications_users_user_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
