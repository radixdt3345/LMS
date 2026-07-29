using LMS.Domain.Entities;
using LMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LMS.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for <see cref="ApprovalStep"/> (LEAVECORE-DB-004).
/// Table: approval_steps — snake_case columns, UUID PK, CASCADE DELETE from leave_requests.
/// </summary>
public class ApprovalStepConfiguration : IEntityTypeConfiguration<ApprovalStep>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ApprovalStep> builder)
    {
        builder.ToTable("approval_steps");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(s => s.LeaveRequestId)
            .HasColumnName("leave_request_id")
            .IsRequired();

        builder.Property(s => s.StepNumber)
            .HasColumnName("step_number")
            .HasColumnType("smallint")
            .IsRequired();

        builder.Property(s => s.ApproverId)
            .HasColumnName("approver_id")
            .IsRequired();

        builder.Property(s => s.Status)
            .HasColumnName("status")
            .HasConversion<short>()
            .HasDefaultValue(ApprovalStepStatus.Pending)
            .IsRequired();

        builder.Property(s => s.ActedAt)
            .HasColumnName("acted_at")
            .HasColumnType("timestamptz");

        builder.Property(s => s.Comment)
            .HasColumnName("comment")
            .HasColumnType("text");

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        // FK: leave_request_id → leave_requests.id (CASCADE DELETE configured on LeaveRequestConfiguration)
        // Relationship is owned by LeaveRequestConfiguration; only the approver FK is declared here.

        // FK: approver_id → users.id
        builder.HasOne(s => s.Approver)
            .WithMany()
            .HasForeignKey(s => s.ApproverId)
            .HasConstraintName("fk_approval_steps_users_approver_id")
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(s => s.LeaveRequestId)
            .HasDatabaseName("ix_approval_steps_leave_request_id");

        builder.HasIndex(s => new { s.ApproverId, s.Status })
            .HasDatabaseName("ix_approval_steps_approver_id_status");
    }
}
