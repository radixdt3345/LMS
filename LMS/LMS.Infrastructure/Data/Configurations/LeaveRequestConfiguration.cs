using LMS.Domain.Entities;
using LMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LMS.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for <see cref="LeaveRequest"/> (LEAVECORE-DB-004).
/// Table: leave_requests — snake_case columns, UUID PK, FK to users and leave_types.
/// </summary>
public class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<LeaveRequest> builder)
    {
        builder.ToTable("leave_requests");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(r => r.EmployeeId)
            .HasColumnName("employee_id")
            .IsRequired();

        builder.Property(r => r.LeaveTypeId)
            .HasColumnName("leave_type_id")
            .IsRequired();

        builder.Property(r => r.StartDate)
            .HasColumnName("start_date")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(r => r.EndDate)
            .HasColumnName("end_date")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(r => r.ComputedDays)
            .HasColumnName("computed_days")
            .HasColumnType("numeric(5,1)")
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(r => r.Status)
            .HasColumnName("status")
            .HasConversion<short>()
            .HasDefaultValue(LeaveRequestStatus.Draft)
            .IsRequired();

        builder.Property(r => r.IsRetroactive)
            .HasColumnName("is_retroactive")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(r => r.Reason)
            .HasColumnName("reason")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(r => r.DocumentUrl)
            .HasColumnName("document_url")
            .HasColumnType("text");

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(r => r.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        // FK: employee_id → users.id
        builder.HasOne(r => r.Employee)
            .WithMany()
            .HasForeignKey(r => r.EmployeeId)
            .HasConstraintName("fk_leave_requests_users_employee_id")
            .OnDelete(DeleteBehavior.Restrict);

        // FK: leave_type_id → leave_types.id
        builder.HasOne(r => r.LeaveType)
            .WithMany()
            .HasForeignKey(r => r.LeaveTypeId)
            .HasConstraintName("fk_leave_requests_leave_types_leave_type_id")
            .OnDelete(DeleteBehavior.Restrict);

        // HasMany side for ApprovalSteps is configured in ApprovalStepConfiguration
        builder.HasMany(r => r.ApprovalSteps)
            .WithOne(s => s.LeaveRequest)
            .HasForeignKey(s => s.LeaveRequestId)
            .HasConstraintName("fk_approval_steps_leave_requests_leave_request_id")
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(r => r.EmployeeId)
            .HasDatabaseName("ix_leave_requests_employee_id");

        builder.HasIndex(r => r.Status)
            .HasDatabaseName("ix_leave_requests_status");

        builder.HasIndex(r => r.StartDate)
            .HasDatabaseName("ix_leave_requests_start_date");

        builder.HasIndex(r => new { r.EmployeeId, r.Status })
            .HasDatabaseName("ix_leave_requests_employee_id_status");
    }
}
