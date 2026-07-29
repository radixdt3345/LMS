using LMS.Domain.Entities;
using LMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LMS.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core fluent configuration for <see cref="CompOffRequest"/> → <c>comp_off_requests</c> table.
/// Key constraint: UNIQUE(employee_id, worked_date) — one request per employee per worked day.
/// </summary>
public class CompOffRequestConfiguration : IEntityTypeConfiguration<CompOffRequest>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CompOffRequest> builder)
    {
        builder.ToTable("comp_off_requests");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(c => c.EmployeeId)
            .HasColumnName("employee_id")
            .IsRequired();

        builder.Property(c => c.WorkedDate)
            .HasColumnName("worked_date")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(c => c.WorkedHours)
            .HasColumnName("worked_hours")
            .HasColumnType("numeric(4,1)")
            .IsRequired();

        // Store CompOffStatus enum as smallint
        builder.Property(c => c.Status)
            .HasColumnName("status")
            .HasConversion<short>()
            .HasDefaultValue(CompOffStatus.Pending)
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        // UNIQUE: one request per employee per worked day
        builder.HasIndex(c => new { c.EmployeeId, c.WorkedDate })
            .IsUnique()
            .HasDatabaseName("ix_comp_off_requests_employee_id_worked_date");

        // FK → users (restrict: cannot delete a user with open comp-off requests)
        builder.HasOne(c => c.Employee)
            .WithMany()
            .HasForeignKey(c => c.EmployeeId)
            .HasConstraintName("fk_comp_off_requests_users_employee_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
