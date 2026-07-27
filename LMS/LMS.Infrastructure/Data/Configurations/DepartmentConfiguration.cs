using LMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LMS.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the Department entity — snake_case columns, UUID PK, timestamptz.
/// </summary>
public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("departments");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(d => d.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(d => d.Description)
            .HasColumnName("description");

        builder.Property(d => d.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(d => d.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(d => d.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        // Unique name constraint (enforced at DB level)
        builder.HasIndex(d => d.Name)
            .IsUnique()
            .HasDatabaseName("ix_departments_name");

        // Index on is_active for soft-delete filter queries
        builder.HasIndex(d => d.IsActive)
            .HasDatabaseName("ix_departments_is_active");
    }
}
