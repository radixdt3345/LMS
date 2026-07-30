namespace LMS.Domain.Services;

/// <summary>
/// Pure domain service — no DB or infrastructure dependency.
/// Computes the number of leave days consumed by a single request,
/// applying the organisation's sandwich rule:
/// a non-working day (weekend or registered holiday) within [start, end] is counted
/// only when it lies strictly between the first and last working days of that same range.
///
/// CRITICAL (UT-38): this engine operates solely within [start..end].
/// Non-working days that fall between two SEPARATE leave requests are never counted
/// because they are outside each request's own [start..end] range.
/// The sandwich rule is single-request-scope only — never cross-request.
/// </summary>
public static class SandwichRuleEngine
{
    /// <summary>
    /// Computes the leave days consumed by a single request spanning [start, end] inclusive.
    /// </summary>
    /// <param name="start">First day of the leave period.</param>
    /// <param name="end">Last day of the leave period (inclusive).</param>
    /// <param name="holidays">
    /// Set of company holiday dates for the period. A day is a working day when it is
    /// Monday–Friday AND NOT present in this set.
    /// </param>
    /// <returns>Number of leave days as decimal; returns 0 when end &lt; start.</returns>
    public static decimal ComputeLeaveDays(
        DateOnly start, DateOnly end, HashSet<DateOnly> holidays)
    {
        if (end < start)
            return 0m;

        // First pass: find the first and last working days in [start, end].
        // A non-working day is sandwiched only when it lies strictly between these two anchors.
        DateOnly? firstWork = null;
        DateOnly? lastWork  = null;

        for (var d = start; d <= end; d = d.AddDays(1))
        {
            if (IsWorkingDay(d, holidays))
            {
                firstWork ??= d;
                lastWork   = d;
            }
        }

        // Second pass: count leave days.
        decimal count = 0m;
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            if (IsWorkingDay(d, holidays))
            {
                // Working days are always counted.
                count++;
            }
            else if (firstWork.HasValue && lastWork.HasValue
                     && d > firstWork.Value && d < lastWork.Value)
            {
                // Non-working day sandwiched strictly between the range's first and last
                // working day — counts as a leave day per the sandwich rule.
                count++;
            }
            // else: non-working day with no working day before or after it within the
            // request range — not sandwiched, not counted.
        }

        return count;
    }

    /// <summary>
    /// Returns true when <paramref name="date"/> is a Monday–Friday weekday
    /// AND is not present in the <paramref name="holidays"/> set.
    /// </summary>
    private static bool IsWorkingDay(DateOnly date, HashSet<DateOnly> holidays) =>
        date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)
        && !holidays.Contains(date);
}
