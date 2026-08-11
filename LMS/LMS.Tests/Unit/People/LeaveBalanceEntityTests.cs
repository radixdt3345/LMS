using LMS.Domain.Entities;
using System.Reflection;
using Xunit;

namespace LMS.Tests.Unit.People;

public class LeaveBalanceEntityTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void UT18_LeaveBalance_FieldsAndDefaults_AreCorrect()
    {
        var now = DateTime.UtcNow;
        var balance = new LeaveBalance
        {
            Id = Guid.NewGuid(), UserId = Guid.NewGuid(), LeaveTypeId = Guid.NewGuid(),
            Year = 2026, AllocatedDays = 12.0m, UsedDays = 0m, CreatedAt = now, UpdatedAt = now
        };

        Assert.Equal(2026, balance.Year);
        Assert.Equal(12.0m, balance.AllocatedDays);
        Assert.Equal(0m, balance.UsedDays);
        Assert.Equal(12.0m, balance.AllocatedDays - balance.UsedDays);
        Assert.NotEqual(Guid.Empty, balance.Id);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void UT19_LeaveBalance_HasNoCarryForwardField()
    {
        var type = typeof(LeaveBalance);
        var carryForwardProp = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(p => p.Name.Contains("Carry", StringComparison.OrdinalIgnoreCase));
        Assert.Null(carryForwardProp);
    }
}
