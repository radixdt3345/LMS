using LMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LMS.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for LeaveBalance (LEAVECORE-DB-002 / LEAVECORE-API-002).
/// Maps to the <c>leave_balances</c> table using snake_case column names.
/// UNIQUE constraint on (user_id, leave_type_id, year) — one record per
/// employee per leave type per calendar year.
/// No carry-forward accumulation — POL-06.
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
            .HasColumnType("smallint")
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

        // UNIQUE: one balance record per employee per leave type per year
        builder.HasIndex(b => new { b.UserId, b.LeaveTypeId, b.Year })
            .IsUnique()
            .HasDatabaseName("ix_leave_balances_user_leavetype_year");

        // Fast lookup by user + year
        builder.HasIndex(b => new { b.UserId, b.Year })
            .HasDatabaseName("ix_leave_balances_user_year");

        // FK: user_id → users (Restrict so rows are not silently deleted)
        builder.HasOne(b => b.User)
            .WithMany()
            .HasForeignKey(b => b.UserId)
            .HasConstraintName("fk_leave_balances_users")
            .OnDelete(DeleteBehavior.Restrict);

        // FK: leave_type_id → leave_types
        builder.HasOne(b => b.LeaveType)
            .WithMany()
            .HasForeignKey(b => b.LeaveTypeId)
            .HasConstraintName("fk_leave_balances_leave_types")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
