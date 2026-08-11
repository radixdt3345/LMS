using LMS.Domain.Services;
using Xunit;

namespace LMS.Tests.Unit.LeaveCore;

[Trait("Category", "Unit")]
public class SandwichRuleEngineTests
{
    [Fact]
    public void UT34_WorkingWeekMonToFri_Returns5Days()
    {
        var result = SandwichRuleEngine.ComputeLeaveDays(new DateOnly(2025, 1, 6), new DateOnly(2025, 1, 10), new HashSet<DateOnly>());
        Assert.Equal(5m, result);
    }

    [Fact]
    public void UT35_WeekendSandwichedBetweenWorkDays_CountsWeekendDays()
    {
        var result = SandwichRuleEngine.ComputeLeaveDays(new DateOnly(2025, 1, 6), new DateOnly(2025, 1, 13), new HashSet<DateOnly>());
        Assert.Equal(8m, result);
    }

    [Fact]
    public void UT36_RangeStartsOnSaturday_LeadingWeekendNotCounted()
    {
        var result = SandwichRuleEngine.ComputeLeaveDays(new DateOnly(2025, 1, 11), new DateOnly(2025, 1, 15), new HashSet<DateOnly>());
        Assert.Equal(3m, result);
    }

    [Fact]
    public void UT37_RangeEndsOnSunday_TrailingWeekendNotCounted()
    {
        var result = SandwichRuleEngine.ComputeLeaveDays(new DateOnly(2025, 1, 6), new DateOnly(2025, 1, 12), new HashSet<DateOnly>());
        Assert.Equal(5m, result);
    }

    [Fact]
    public void UT38_SeparateRequests_InterveningDaysNeverCounted()
    {
        var noHolidays = new HashSet<DateOnly>();
        var r1 = SandwichRuleEngine.ComputeLeaveDays(new DateOnly(2025, 1, 10), new DateOnly(2025, 1, 10), noHolidays);
        var r2 = SandwichRuleEngine.ComputeLeaveDays(new DateOnly(2025, 1, 13), new DateOnly(2025, 1, 13), noHolidays);
        Assert.Equal(1m, r1);
        Assert.Equal(1m, r2);
        Assert.Equal(2m, r1 + r2);
    }

    [Fact]
    public void UT39_HolidayInMiddleOfWeek_Sandwiched_IsCounted()
    {
        var result = SandwichRuleEngine.ComputeLeaveDays(new DateOnly(2025, 1, 6), new DateOnly(2025, 1, 10), new HashSet<DateOnly> { new(2025, 1, 8) });
        Assert.Equal(5m, result);
    }

    [Fact]
    public void UT40_HolidayAtStartOfRange_NotSandwiched_NotCounted()
    {
        var result = SandwichRuleEngine.ComputeLeaveDays(new DateOnly(2025, 1, 6), new DateOnly(2025, 1, 10), new HashSet<DateOnly> { new(2025, 1, 6) });
        Assert.Equal(4m, result);
    }

    [Fact]
    public void UT41_HolidayAtEndOfRange_NotSandwiched_NotCounted()
    {
        var result = SandwichRuleEngine.ComputeLeaveDays(new DateOnly(2025, 1, 6), new DateOnly(2025, 1, 10), new HashSet<DateOnly> { new(2025, 1, 10) });
        Assert.Equal(4m, result);
    }

    [Fact]
    public void UT42_EndBeforeStart_ReturnsZero()
    {
        var result = SandwichRuleEngine.ComputeLeaveDays(new DateOnly(2025, 1, 10), new DateOnly(2025, 1, 6), new HashSet<DateOnly>());
        Assert.Equal(0m, result);
    }
}
