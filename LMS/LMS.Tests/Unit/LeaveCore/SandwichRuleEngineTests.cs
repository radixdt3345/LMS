using LMS.Domain.Services;
using Xunit;

namespace LMS.Tests.Unit.LeaveCore;

/// <summary>
/// Unit tests for <see cref="SandwichRuleEngine"/>.
/// Covers UT-34 through UT-42.
/// Run: dotnet test --filter Category=Unit
///
/// Sandwich rule summary:
/// - Working days (Mon–Fri, not a holiday) are always counted.
/// - Non-working days (weekend/holiday) are counted ONLY when they lie strictly
///   between the first and last working days of the same request range.
/// - Days outside the request range are NEVER counted (UT-38 — single-request scope).
/// </summary>
[Trait("Category", "Unit")]
public class SandwichRuleEngineTests
{
    // ── UT-34: Standard Mon–Fri working week → 5 days ────────────────────────

    [Fact]
    public void UT34_WorkingWeekMonToFri_Returns5Days()
    {
        var start    = new DateOnly(2025, 1, 6);  // Monday
        var end      = new DateOnly(2025, 1, 10); // Friday
        var holidays = new HashSet<DateOnly>();

        var result = SandwichRuleEngine.ComputeLeaveDays(start, end, holidays);

        Assert.Equal(5m, result);
    }

    // ── UT-35: Weekend sandwiched between Mon and Mon → 8 days ───────────────

    [Fact]
    public void UT35_WeekendSandwichedBetweenWorkDays_CountsWeekendDays()
    {
        // Mon 6 Jan → Mon 13 Jan: Mon,Tue,Wed,Thu,Fri,Sat,Sun,Mon
        // Sat+Sun lie between Fri(firstWork candidate? no — Mon6 is firstWork) and Mon13(lastWork)
        // firstWork=Mon6, lastWork=Mon13; Sat11 and Sun12 are strictly between → sandwiched.
        var start    = new DateOnly(2025, 1, 6);  // Monday
        var end      = new DateOnly(2025, 1, 13); // Monday
        var holidays = new HashSet<DateOnly>();

        var result = SandwichRuleEngine.ComputeLeaveDays(start, end, holidays);

        Assert.Equal(8m, result); // 6 working + Sat + Sun sandwiched
    }

    // ── UT-36: Range starts on Saturday — leading non-working days NOT counted ─

    [Fact]
    public void UT36_RangeStartsOnSaturday_LeadingWeekendNotCounted()
    {
        // Sat 11 Jan → Wed 15 Jan
        // firstWork=Mon13; Sat11 and Sun12 are NOT after firstWork → not sandwiched.
        var start    = new DateOnly(2025, 1, 11); // Saturday
        var end      = new DateOnly(2025, 1, 15); // Wednesday
        var holidays = new HashSet<DateOnly>();

        var result = SandwichRuleEngine.ComputeLeaveDays(start, end, holidays);

        Assert.Equal(3m, result); // Mon13, Tue14, Wed15 only
    }

    // ── UT-37: Range ends on Sunday — trailing non-working days NOT counted ───

    [Fact]
    public void UT37_RangeEndsOnSunday_TrailingWeekendNotCounted()
    {
        // Mon 6 Jan → Sun 12 Jan
        // lastWork=Fri10; Sat11 and Sun12 are NOT before lastWork → not sandwiched.
        var start    = new DateOnly(2025, 1, 6);  // Monday
        var end      = new DateOnly(2025, 1, 12); // Sunday
        var holidays = new HashSet<DateOnly>();

        var result = SandwichRuleEngine.ComputeLeaveDays(start, end, holidays);

        Assert.Equal(5m, result); // Mon–Fri only; Sat+Sun not sandwiched
    }

    // ── UT-38: Two separate requests — between-request days NEVER counted ─────
    //
    // CRITICAL: the sandwich rule is single-request-scope only.
    // Non-working days between two separate leave requests must never be counted
    // in either request's computation.

    [Fact]
    public void UT38_SeparateRequests_InterveningDaysNeverCounted()
    {
        var noHolidays = new HashSet<DateOnly>();

        // Request 1: Fri 10 Jan only → 1 day
        var r1 = SandwichRuleEngine.ComputeLeaveDays(
            new DateOnly(2025, 1, 10),
            new DateOnly(2025, 1, 10),
            noHolidays);

        // Request 2: Mon 13 Jan only → 1 day
        // The Sat 11 + Sun 12 between these requests must NOT be counted in either call.
        var r2 = SandwichRuleEngine.ComputeLeaveDays(
            new DateOnly(2025, 1, 13),
            new DateOnly(2025, 1, 13),
            noHolidays);

        Assert.Equal(1m, r1);                  // just Friday
        Assert.Equal(1m, r2);                  // just Monday
        Assert.Equal(2m, r1 + r2);             // total = 2, NOT 4
    }

    // ── UT-39: Holiday in the middle of a working week — sandwiched → counted ─

    [Fact]
    public void UT39_HolidayInMiddleOfWeek_Sandwiched_IsCounted()
    {
        // Mon 6 → Fri 10; Wed 8 is a registered holiday.
        // firstWork=Mon6, lastWork=Fri10; Wed8(holiday) is strictly between → sandwiched.
        var start    = new DateOnly(2025, 1, 6);
        var end      = new DateOnly(2025, 1, 10);
        var holidays = new HashSet<DateOnly> { new(2025, 1, 8) }; // Wednesday

        var result = SandwichRuleEngine.ComputeLeaveDays(start, end, holidays);

        Assert.Equal(5m, result); // Mon, Tue, [Wed holiday sandwiched], Thu, Fri
    }

    // ── UT-40: Holiday at the start of the range → NOT sandwiched → NOT counted

    [Fact]
    public void UT40_HolidayAtStartOfRange_NotSandwiched_NotCounted()
    {
        // Mon 6 is a holiday; range Mon 6 → Fri 10.
        // firstWork=Tue7 (Mon6 is holiday); Mon6 < firstWork → NOT sandwiched.
        var start    = new DateOnly(2025, 1, 6);
        var end      = new DateOnly(2025, 1, 10);
        var holidays = new HashSet<DateOnly> { new(2025, 1, 6) }; // Monday is holiday

        var result = SandwichRuleEngine.ComputeLeaveDays(start, end, holidays);

        Assert.Equal(4m, result); // Tue, Wed, Thu, Fri — Monday holiday excluded
    }

    // ── UT-41: Holiday at the end of the range → NOT sandwiched → NOT counted ─

    [Fact]
    public void UT41_HolidayAtEndOfRange_NotSandwiched_NotCounted()
    {
        // Fri 10 is a holiday; range Mon 6 → Fri 10.
        // lastWork=Thu9 (Fri10 is holiday); Fri10 > lastWork → NOT sandwiched.
        var start    = new DateOnly(2025, 1, 6);
        var end      = new DateOnly(2025, 1, 10);
        var holidays = new HashSet<DateOnly> { new(2025, 1, 10) }; // Friday is holiday

        var result = SandwichRuleEngine.ComputeLeaveDays(start, end, holidays);

        Assert.Equal(4m, result); // Mon, Tue, Wed, Thu — Friday holiday excluded
    }

    // ── UT-42: End before start → returns 0 ──────────────────────────────────

    [Fact]
    public void UT42_EndBeforeStart_ReturnsZero()
    {
        var start    = new DateOnly(2025, 1, 10);
        var end      = new DateOnly(2025, 1, 6); // before start
        var holidays = new HashSet<DateOnly>();

        var result = SandwichRuleEngine.ComputeLeaveDays(start, end, holidays);

        Assert.Equal(0m, result);
    }
}
