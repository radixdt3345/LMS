using LMS.Domain.Entities;
using LMS.Domain.Enums;
using LMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LMS.Tests.Unit.LeaveCore;

/// <summary>
/// UT-21: Create leave type with AccrualType=Unlimited (AccrualType=2), MaxDaysPerYear=null
///        → persists successfully. Null max days is valid for Unpaid Leave.
/// Uses EF Core InMemory provider; no PostgreSQL required.
/// Run: dotnet test --filter Category=Unit
/// </summary>
[Trait("Category", "Unit")]
public class LeaveTypeServiceTests
{
    private static LmsDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<LmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new LmsDbContext(options);
    }

    // ── UT-21: Unlimited accrual + null max days is valid ────────────────────

    [Fact]
    public async Task UT21_CreateLeaveType_UnlimitedAccrual_NullMaxDays_Succeeds()
    {
        // Arrange
        await using var db = CreateInMemoryDb();

        var leaveType = new LeaveType
        {
            Id = Guid.NewGuid(),
            Name = "Unpaid Leave",
            AccrualType = AccrualType.Unlimited, // enum value = 2
            MaxDaysPerYear = null,               // null = unlimited — valid for Unpaid Leave (UT-26, IT-25)
            RequiresDocument = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        // Act — persist (simulates Result.Success returned by LeaveTypeService.CreateAsync)
        db.LeaveTypes.Add(leaveType);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        // Assert — stored correctly; null max days accepted without error
        var stored = await db.LeaveTypes.FindAsync(leaveType.Id);
        Assert.NotNull(stored);
        Assert.Equal(AccrualType.Unlimited, stored.AccrualType);
        Assert.Equal(2, (int)stored.AccrualType);
        Assert.Null(stored.MaxDaysPerYear);
        Assert.Equal("Unpaid Leave", stored.Name);
        Assert.True(stored.IsActive);
    }

    // ── Supplementary: Annual accrual with explicit max days ─────────────────

    [Fact]
    public async Task CreateLeaveType_AnnualAccrual_WithMaxDays_Succeeds()
    {
        // Arrange
        await using var db = CreateInMemoryDb();

        var leaveType = new LeaveType
        {
            Id = Guid.NewGuid(),
            Name = "Annual Leave",
            AccrualType = AccrualType.Annual, // enum value = 0
            MaxDaysPerYear = 18,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        // Act
        db.LeaveTypes.Add(leaveType);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        // Assert
        var stored = await db.LeaveTypes.FindAsync(leaveType.Id);
        Assert.NotNull(stored);
        Assert.Equal(AccrualType.Annual, stored.AccrualType);
        Assert.Equal(18, stored.MaxDaysPerYear);
    }

    // ── Supplementary: OneTime accrual (maternity/paternity) ─────────────────

    [Fact]
    public async Task CreateLeaveType_OneTimeAccrual_WithMaxDays_Succeeds()
    {
        // Arrange
        await using var db = CreateInMemoryDb();

        var leaveType = new LeaveType
        {
            Id = Guid.NewGuid(),
            Name = "Maternity Leave",
            AccrualType = AccrualType.OneTime, // enum value = 1
            MaxDaysPerYear = 180,
            RequiresDocument = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        // Act
        db.LeaveTypes.Add(leaveType);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        // Assert
        var stored = await db.LeaveTypes.FindAsync(leaveType.Id);
        Assert.NotNull(stored);
        Assert.Equal(AccrualType.OneTime, stored.AccrualType);
        Assert.Equal(180, stored.MaxDaysPerYear);
        Assert.True(stored.RequiresDocument);
    }
}
