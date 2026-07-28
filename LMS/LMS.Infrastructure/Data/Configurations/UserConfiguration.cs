using LMS.Domain.Entities;
using LMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LMS.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the User entity — snake_case columns, UUID PK, timestamptz.
/// PEOPLE-DB-002 adds: first_name, last_name, phone, join_date, manager_id, employee_code
/// plus a self-referencing FK for manager hierarchy.
/// </summary>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(u => u.Email)
            .HasColumnName("email")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(u => u.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(512);

        builder.Property(u => u.AzureAdOid)
            .HasColumnName("azure_ad_oid")
            .HasMaxLength(128);

        builder.Property(u => u.Role)
            .HasColumnName("role")
            .HasConversion<short>()
            .HasDefaultValue(UserRole.Employee);

        builder.Property(u => u.DepartmentId)
            .HasColumnName("department_id");

        builder.Property(u => u.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(u => u.FailedLoginCount)
            .HasColumnName("failed_login_count")
            .HasDefaultValue((short)0);

        builder.Property(u => u.LockoutUntil)
            .HasColumnName("lockout_until")
            .HasColumnType("timestamptz");

        // Profile columns (PEOPLE-DB-002, FR-12 to FR-20)
        builder.Property(u => u.FirstName)
            .HasColumnName("first_name")
            .HasMaxLength(100);

        builder.Property(u => u.LastName)
            .HasColumnName("last_name")
            .HasMaxLength(100);

        builder.Property(u => u.Phone)
            .HasColumnName("phone")
            .HasMaxLength(20);

        builder.Property(u => u.JoinDate)
            .HasColumnName("join_date")
            .HasColumnType("date");

        builder.Property(u => u.ManagerId)
            .HasColumnName("manager_id");

        builder.Property(u => u.EmployeeCode)
            .HasColumnName("employee_code")
            .HasMaxLength(20);

        builder.Property(u => u.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(u => u.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        // Self-referencing FK: manager_id → users.id (nullable, SetNull on delete)
        // No-manager rule: manager_id IS NULL means HR Admin handles L1+L2 (CLAUDE.md)
        builder.HasOne(u => u.Manager)
            .WithMany(u => u.DirectReports)
            .HasForeignKey(u => u.ManagerId)
            .HasConstraintName("fk_users_manager")
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("ix_users_email");

        builder.HasIndex(u => u.AzureAdOid)
            .IsUnique()
            .HasDatabaseName("ix_users_azure_ad_oid");

        builder.HasIndex(u => u.DepartmentId)
            .HasDatabaseName("ix_users_department_id");

        builder.HasIndex(u => u.IsActive)
            .HasDatabaseName("ix_users_is_active");

        builder.HasIndex(u => u.ManagerId)
            .HasDatabaseName("ix_users_manager_id");

        // Unique on employee_code — PostgreSQL allows multiple NULLs in a unique index
        builder.HasIndex(u => u.EmployeeCode)
            .IsUnique()
            .HasDatabaseName("ix_users_employee_code");
    }
}
