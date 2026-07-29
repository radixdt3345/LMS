using LMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LMS.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for LeaveBalance (LEAVECORE-DB-002 / LEAVECORE-API-002).
/// Maps to the <c>leave_balances</c> table using snake_case column names.
/// Unique composite index on (user_id, leave_type_id, year) enforces
/// one balance row per employee per leave type per calendar year.
/// No carry_forward accumulation — POL-06.
/// </summary>
public class LeaveBalanceConfiguration : IEntityTypeConfiguration<LeaveBalance>
{
    public void Configure(EntityTypeBuilder<LeaveBalance> builder)
    {
        builder.ToTable("leave_balances");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(b => b.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(b => b.LeaveTypeId)
            .HasColumnName("leave_type_id")
            .IsRequired();

        builder.Property(b => b.Year)
            .HasColumnName("year")
            .IsRequired();

        builder.Property(b => b.AllocatedDays)
            .HasColumnName("allocated_days")
            .HasColumnType("numeric(5,1)")
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(b => b.UsedDays)
            .HasColumnName("used_days")
            .HasColumnType("numeric(5,1)")
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(b => b.CarriedForwardDays)
            .HasColumnName("carried_forward_days")
            .HasColumnType("numeric(5,1)")
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(b => b.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(b => b.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        // One balance row per (employee, leave type, year).
        builder.HasIndex(b => new { b.UserId, b.LeaveTypeId, b.Year })
            .IsUnique()
            .HasDatabaseName("ix_leave_balances_user_type_year");

        // Fast lookup by user + year.
        builder.HasIndex(b => new { b.UserId, b.Year })
            .HasDatabaseName("ix_leave_balances_user_year");

        builder.HasOne(b => b.User)
            .WithMany()
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.LeaveType)
            .WithMany()
            .HasForeignKey(b => b.LeaveTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
