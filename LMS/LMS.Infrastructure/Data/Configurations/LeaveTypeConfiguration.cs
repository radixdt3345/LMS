using LMS.Domain.Entities;
using LMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LMS.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for LeaveType — full schema (LEAVECORE-DB-001).
/// No carry_forward column — org policy POL-06/FR-30 absolutely forbids it.
/// </summary>
public class LeaveTypeConfiguration : IEntityTypeConfiguration<LeaveType>
{
    public void Configure(EntityTypeBuilder<LeaveType> builder)
    {
        builder.ToTable("leave_types");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(l => l.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(l => l.MaxDaysPerYear)
            .HasColumnName("max_days_per_year");

        builder.Property(l => l.AccrualType)
            .HasColumnName("accrual_type")
            .HasDefaultValue(AccrualType.Annual)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(l => l.RequiresDocument)
            .HasColumnName("requires_document")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(l => l.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(l => l.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(l => l.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(l => l.Name)
            .IsUnique()
            .HasDatabaseName("ix_leave_types_name");
    }
}
