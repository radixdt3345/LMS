using LMS.Domain.Entities;
using LMS.Domain.Enums;
using Xunit;

namespace LMS.Tests.Unit.LeaveCore;

/// <summary>
/// Unit tests for the LeaveType entity.
/// UT-26: Unpaid Leave MaxDaysPerYear is null (unlimited).
/// UT-27: AccrualType defaults to Annual.
/// UT-28: RequiresDocument defaults to false.
/// UT-29: IsActive defaults to true.
/// UT-30: AccrualType enum values match DB contract (Annual=0, OneTime=1, Unlimited=2).
/// </summary>
[Trait("Category", "Unit")]
public class LeaveTypeEntityTests
{
    // ── UT-26: Unpaid Leave — MaxDaysPerYear must be null (unlimited) ──────

    [Fact]
    public void UT26_UnpaidLeave_MaxDaysPerYear_IsNull()
    {
        var unpaid = new LeaveType
        {
            Id = Guid.NewGuid(),
            Name = "Unpaid Leave",
            MaxDaysPerYear = null,
            AccrualType = AccrualType.Unlimited,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        Assert.Null(unpaid.MaxDaysPerYear);
        Assert.Equal(AccrualType.Unlimited, unpaid.AccrualType);
    }

    // ── UT-27: AccrualType defaults to Annual ───────────────────────

    [Fact]
    public void UT27_NewLeaveType_AccrualType_DefaultsToAnnual()
    {
        var lt = new LeaveType();

        Assert.Equal(AccrualType.Annual, lt.AccrualType);
    }

    // ── UT-28: RequiresDocument defaults to false ────────────────────

    [Fact]
    public void UT28_NewLeaveType_RequiresDocument_DefaultsFalse()
    {
        var lt = new LeaveType();

        Assert.False(lt.RequiresDocument);
    }

    // ── UT-29: IsActive defaults to true ─────────────────────────

    [Fact]
    public void UT29_NewLeaveType_IsActive_DefaultsTrue()
    {
        var lt = new LeaveType();

        Assert.True(lt.IsActive);
    }

    // ── UT-30: AccrualType enum integer values match DB contract ──────────

    [Theory]
    [InlineData(AccrualType.Annual, 0)]
    [InlineData(AccrualType.OneTime, 1)]
    [InlineData(AccrualType.Unlimited, 2)]
    public void UT30_AccrualType_EnumValues_MatchDbContract(AccrualType accrualType, int expectedValue)
    {
        Assert.Equal(expectedValue, (int)accrualType);
    }

    // ── No carry-forward variant exists in AccrualType (POL-06) ─────────

    [Fact]
    public void AccrualType_HasNoCarryForwardVariant()
    {
        var values = Enum.GetValues<AccrualType>();

        // Only Annual, OneTime, Unlimited must exist — POL-06 forbids carry-forward
        Assert.Equal(3, values.Length);
        Assert.Contains(AccrualType.Annual, values);
        Assert.Contains(AccrualType.OneTime, values);
        Assert.Contains(AccrualType.Unlimited, values);
    }

    // ── Annual leave: MaxDaysPerYear is a positive integer ─────────────

    [Fact]
    public void AnnualLeave_MaxDaysPerYear_IsPositive()
    {
        var annual = new LeaveType
        {
            Name = "Annual Leave",
            MaxDaysPerYear = 18,
            AccrualType = AccrualType.Annual,
        };

        Assert.NotNull(annual.MaxDaysPerYear);
        Assert.True(annual.MaxDaysPerYear > 0);
    }
}
