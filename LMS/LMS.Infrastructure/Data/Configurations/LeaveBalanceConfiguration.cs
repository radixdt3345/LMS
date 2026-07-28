using LMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LMS.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the leave_balances table.
/// Unique index on (user_id, leave_type_id, year) enforces one record per user/type/year.
/// FK to users CASCADE; FK to leave_types RESTRICT.
/// </summary>
public class LeaveBalanceConfiguration : IEntityTypeConfiguration<LeaveBalance>
{
    public void Configure(EntityTypeBuilder<LeaveBalance> builder)
    {
        builder.ToTable("leave_balances");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(e => e.LeaveTypeId)
            .HasColumnName("leave_type_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(e => e.Year)
            .HasColumnName("year")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(e => e.Balance)
            .HasColumnName("balance")
            .HasColumnType("numeric(5,1)")
            .IsRequired();

        builder.Property(e => e.Used)
            .HasColumnName("used")
            .HasColumnType("numeric(5,1)")
            .IsRequired();

        builder.Property(e => e.Allocated)
            .HasColumnName("allocated")
            .HasColumnType("numeric(5,1)")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        // One record per user, leave type, and year
        builder.HasIndex(e => new { e.UserId, e.LeaveTypeId, e.Year })
            .IsUnique()
            .HasDatabaseName("ix_leave_balances_user_leavetype_year");

        // FK: user_id → users.id CASCADE
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .HasConstraintName("fk_leave_balances_users")
            .OnDelete(DeleteBehavior.Cascade);

        // FK: leave_type_id → leave_types.id RESTRICT
        builder.HasOne(e => e.LeaveType)
            .WithMany()
            .HasForeignKey(e => e.LeaveTypeId)
            .HasConstraintName("fk_leave_balances_leave_types")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
