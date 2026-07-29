using LMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LMS.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core fluent configuration for <see cref="CompOffCredit"/> → <c>comp_off_credits</c> table.
/// Credits cascade-delete when their originating <see cref="CompOffRequest"/> is deleted.
/// </summary>
public class CompOffCreditConfiguration : IEntityTypeConfiguration<CompOffCredit>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CompOffCredit> builder)
    {
        builder.ToTable("comp_off_credits");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(c => c.EmployeeId)
            .HasColumnName("employee_id")
            .IsRequired();

        builder.Property(c => c.CompOffRequestId)
            .HasColumnName("comp_off_request_id")
            .IsRequired();

        builder.Property(c => c.CreditDays)
            .HasColumnName("credit_days")
            .HasColumnType("numeric(3,1)")
            .IsRequired();

        builder.Property(c => c.ExpiresAt)
            .HasColumnName("expires_at")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(c => c.UsedDays)
            .HasColumnName("used_days")
            .HasColumnType("numeric(3,1)")
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        // Explicit indexes for FK columns
        builder.HasIndex(c => c.EmployeeId)
            .HasDatabaseName("ix_comp_off_credits_employee_id");

        builder.HasIndex(c => c.CompOffRequestId)
            .HasDatabaseName("ix_comp_off_credits_comp_off_request_id");

        // FK → users (restrict)
        builder.HasOne(c => c.Employee)
            .WithMany()
            .HasForeignKey(c => c.EmployeeId)
            .HasConstraintName("fk_comp_off_credits_users_employee_id")
            .OnDelete(DeleteBehavior.Restrict);

        // FK → comp_off_requests (cascade: credits are owned by the request)
        builder.HasOne(c => c.CompOffRequest)
            .WithMany(r => r.Credits)
            .HasForeignKey(c => c.CompOffRequestId)
            .HasConstraintName("fk_comp_off_credits_comp_off_requests")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
