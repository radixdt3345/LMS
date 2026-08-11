using LMS.Domain.Entities;
using LMS.Domain.Enums;
using Xunit;

namespace LMS.Tests.Unit.LeaveCore;

[Trait("Category", "Unit")]
public class LeaveTypeEntityTests
{
    [Fact]
    public void UT26_UnpaidLeave_MaxDaysPerYear_IsNull()
    {
        var unpaid = new LeaveType { Id = Guid.NewGuid(), Name = "Unpaid Leave", MaxDaysPerYear = null, AccrualType = AccrualType.Unlimited, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        Assert.Null(unpaid.MaxDaysPerYear);
        Assert.Equal(AccrualType.Unlimited, unpaid.AccrualType);
    }

    [Fact]
    public void UT27_NewLeaveType_AccrualType_DefaultsToAnnual()
    {
        var lt = new LeaveType();
        Assert.Equal(AccrualType.Annual, lt.AccrualType);
    }

    [Fact]
    public void UT28_NewLeaveType_RequiresDocument_DefaultsFalse()
    {
        var lt = new LeaveType();
        Assert.False(lt.RequiresDocument);
    }

    [Fact]
    public void UT29_NewLeaveType_IsActive_DefaultsTrue()
    {
        var lt = new LeaveType();
        Assert.True(lt.IsActive);
    }

    [Theory]
    [InlineData(AccrualType.Annual, 0)]
    [InlineData(AccrualType.OneTime, 1)]
    [InlineData(AccrualType.Unlimited, 2)]
    public void UT30_AccrualType_EnumValues_MatchDbContract(AccrualType accrualType, int expectedValue)
    {
        Assert.Equal(expectedValue, (int)accrualType);
    }

    [Fact]
    public void AccrualType_HasNoCarryForwardVariant()
    {
        var values = Enum.GetValues<AccrualType>();
        Assert.Equal(3, values.Length);
        Assert.Contains(AccrualType.Annual, values);
        Assert.Contains(AccrualType.OneTime, values);
        Assert.Contains(AccrualType.Unlimited, values);
    }

    [Fact]
    public void AnnualLeave_MaxDaysPerYear_IsPositive()
    {
        var annual = new LeaveType { Name = "Annual Leave", MaxDaysPerYear = 18, AccrualType = AccrualType.Annual };
        Assert.NotNull(annual.MaxDaysPerYear);
        Assert.True(annual.MaxDaysPerYear > 0);
    }
}
