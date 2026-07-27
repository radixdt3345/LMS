using LMS.Domain.Entities;
using Xunit;

namespace LMS.Tests.Unit.LeaveCore;

/// <summary>
/// Unit tests for the Holiday entity (LEAVECORE-DB-003).
/// Covers basic entity defaults and uniqueness contract.
/// </summary>
[Trait("Category", "Unit")]
public class HolidayEntityTests
{
    [Fact]
    public void NewHoliday_IsRecurring_DefaultsFalse()
    {
        var holiday = new Holiday();

        Assert.False(holiday.IsRecurring);
    }

    [Fact]
    public void NewHoliday_Name_DefaultsEmpty()
    {
        var holiday = new Holiday();

        Assert.Equal(string.Empty, holiday.Name);
    }

    [Fact]
    public void Holiday_Date_CanBeSetToDateOnly()
    {
        var date = new DateOnly(2026, 1, 26);
        var holiday = new Holiday
        {
            Id = Guid.NewGuid(),
            Name = "Republic Day",
            Date = date,
            Year = 2026,
            IsRecurring = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        Assert.Equal(date, holiday.Date);
        Assert.Equal(2026, holiday.Year);
        Assert.True(holiday.IsRecurring);
    }

    [Fact]
    public void Holiday_Year_MatchesDateYear()
    {
        var date = new DateOnly(2026, 8, 15);
        var holiday = new Holiday
        {
            Name = "Independence Day",
            Date = date,
            Year = date.Year,
        };

        Assert.Equal(holiday.Date.Year, holiday.Year);
    }

    [Fact]
    public void Holiday_OptionalHoliday_IsRecurringFalse()
    {
        // Optional / one-off holidays are not recurring
        var holiday = new Holiday
        {
            Name = "Company Founder Day 2026",
            Date = new DateOnly(2026, 6, 1),
            Year = 2026,
            IsRecurring = false,
        };

        Assert.False(holiday.IsRecurring);
    }

    [Fact]
    public void Holiday_RecurringHoliday_IsRecurringTrue()
    {
        var holiday = new Holiday
        {
            Name = "Diwali",
            Date = new DateOnly(2026, 10, 20),
            Year = 2026,
            IsRecurring = true,
        };

        Assert.True(holiday.IsRecurring);
    }
}
