using LMS.Domain.Entities;
using System.Reflection;
using Xunit;

namespace LMS.Tests.Unit.People;

/// <summary>
/// Unit tests for the LeaveBalance entity.
/// UT-18: entity fields and defaults are correct
/// UT-19: no carry_forward field exists (POL-06 / FR-30 compliance — reflection check)
/// </summary>
public class LeaveBalanceEntityTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void UT18_LeaveBalance_FieldsAndDefaults_AreCorrect()
    {
        // Arrange & Act
        var now = DateTime.UtcNow;
        var balance = new LeaveBalance
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            LeaveTypeId = Guid.NewGuid(),
            Year = 2026,
            Balance = 12.0m,
            Used = 0m,
            Allocated = 12.0m,
            CreatedAt = now,
            UpdatedAt = now
        };

        // Assert — all fields round-trip correctly
        Assert.Equal(2026, balance.Year);
        Assert.Equal(12.0m, balance.Balance);
        Assert.Equal(0m, balance.Used);
        Assert.Equal(12.0m, balance.Allocated);
        Assert.NotEqual(Guid.Empty, balance.Id);
        Assert.NotEqual(Guid.Empty, balance.UserId);
        Assert.NotEqual(Guid.Empty, balance.LeaveTypeId);
        Assert.Equal(now, balance.CreatedAt);
        Assert.Equal(now, balance.UpdatedAt);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void UT19_LeaveBalance_HasNoCarryForwardField()
    {
        // Arrange
        var type = typeof(LeaveBalance);

        // Act — look for any property whose name contains "carry" (case-insensitive)
        // POL-06 / FR-30: carry-forward is absolutely forbidden on this entity.
        var carryForwardProp = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(p => p.Name.Contains("Carry", StringComparison.OrdinalIgnoreCase));

        // Assert
        Assert.Null(carryForwardProp);
    }
}
