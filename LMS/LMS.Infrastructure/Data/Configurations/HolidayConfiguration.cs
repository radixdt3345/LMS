using LMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LMS.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for Holiday (LEAVECORE-DB-003).
/// Unique constraint on (date, year) — no duplicate holidays on the same calendar day.
/// </summary>
public class HolidayConfiguration : IEntityTypeConfiguration<Holiday>
{
    public void Configure(EntityTypeBuilder<Holiday> builder)
    {
        builder.ToTable("holidays");

        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(h => h.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(h => h.Date)
            .HasColumnName("date")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(h => h.Year)
            .HasColumnName("year")
            .IsRequired();

        builder.Property(h => h.IsRecurring)
            .HasColumnName("is_recurring")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(h => h.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(h => h.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        // Unique constraint: no two holidays on the same date+year
        builder.HasIndex(h => new { h.Date, h.Year })
            .IsUnique()
            .HasDatabaseName("ix_holidays_date_year");

        // Fast IsHoliday lookup by date
        builder.HasIndex(h => h.Date)
            .HasDatabaseName("ix_holidays_date");
    }
}
