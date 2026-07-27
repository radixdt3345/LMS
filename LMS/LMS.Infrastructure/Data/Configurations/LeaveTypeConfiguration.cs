using LMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LMS.Infrastructure.Data.Configurations;

/// <summary>EF configuration for LeaveType stub. No carry-forward column (org policy POL-06).</summary>
public class LeaveTypeConfiguration : IEntityTypeConfiguration<LeaveType>
{
    public void Configure(EntityTypeBuilder<LeaveType> builder)
    {
        builder.ToTable("leave_types");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(l => l.Name).HasColumnName("name").HasMaxLength(256).IsRequired();
        builder.Property(l => l.MaxDays).HasColumnName("max_days");
        builder.Property(l => l.AccrualType).HasColumnName("accrual_type").HasConversion<int>();
        builder.Property(l => l.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(l => l.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();
        builder.HasIndex(l => l.Name).IsUnique().HasDatabaseName("ix_leave_types_name");
    }
}
