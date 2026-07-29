using LMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LMS.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core fluent configuration for <see cref="AuditLog"/> → <c>audit_logs</c> table.
/// <para>
/// The table is APPEND-ONLY. There are deliberately no cascade or set-null rules
/// on <c>actor_id</c> because audit records must survive user account deletion.
/// </para>
/// <para>Indexes support the three primary read patterns:</para>
/// <list type="bullet">
///   <item><c>(entity_type, entity_id)</c> — entity history lookup</item>
///   <item><c>(actor_id)</c> — all actions by a given user</item>
///   <item><c>(created_at DESC)</c> — chronological audit trail</item>
/// </list>
/// </summary>
public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(a => a.Action)
            .HasColumnName("action")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.EntityType)
            .HasColumnName("entity_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.EntityId)
            .HasColumnName("entity_id")
            .IsRequired();

        builder.Property(a => a.ActorId)
            .HasColumnName("actor_id")
            .IsRequired();

        builder.Property(a => a.OldValue)
            .HasColumnName("old_value")
            .HasColumnType("jsonb");

        builder.Property(a => a.NewValue)
            .HasColumnName("new_value")
            .HasColumnType("jsonb");

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        // Composite: entity history lookup
        builder.HasIndex(a => new { a.EntityType, a.EntityId })
            .HasDatabaseName("ix_audit_logs_entity_type_entity_id");

        // All actions by a user
        builder.HasIndex(a => a.ActorId)
            .HasDatabaseName("ix_audit_logs_actor_id");

        // Chronological audit trail (most recent first)
        builder.HasIndex(a => a.CreatedAt)
            .IsDescending(true)
            .HasDatabaseName("ix_audit_logs_created_at");

        // FK → users (RESTRICT: audit logs must survive even if account is locked)
        // NoAction is preferred over Restrict for append-only tables to avoid
        // deferral complexities; the service layer never deletes users with audit rows.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.ActorId)
            .HasConstraintName("fk_audit_logs_users_actor_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
