using LMS.Domain.Entities;
using LMS.Domain.Enums;
using LMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LMS.Tests.Unit.LeaveCore;

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

    [Fact]
    public async Task UT21_CreateLeaveType_UnlimitedAccrual_NullMaxDays_Succeeds()
    {
        await using var db = CreateInMemoryDb();

        var leaveType = new LeaveType
        {
            Id = Guid.NewGuid(),
            Name = "Unpaid Leave",
            AccrualType = AccrualType.Unlimited,
            MaxDaysPerYear = null,
            RequiresDocument = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        db.LeaveTypes.Add(leaveType);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var stored = await db.LeaveTypes.FindAsync(leaveType.Id);
        Assert.NotNull(stored);
        Assert.Equal(AccrualType.Unlimited, stored.AccrualType);
        Assert.Equal(2, (int)stored.AccrualType);
        Assert.Null(stored.MaxDaysPerYear);
        Assert.Equal("Unpaid Leave", stored.Name);
        Assert.True(stored.IsActive);
    }

    [Fact]
    public async Task CreateLeaveType_AnnualAccrual_WithMaxDays_Succeeds()
    {
        await using var db = CreateInMemoryDb();

        var leaveType = new LeaveType { Id = Guid.NewGuid(), Name = "Annual Leave", AccrualType = AccrualType.Annual, MaxDaysPerYear = 18, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

        db.LeaveTypes.Add(leaveType);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var stored = await db.LeaveTypes.FindAsync(leaveType.Id);
        Assert.NotNull(stored);
        Assert.Equal(AccrualType.Annual, stored.AccrualType);
        Assert.Equal(18, stored.MaxDaysPerYear);
    }

    [Fact]
    public async Task CreateLeaveType_OneTimeAccrual_WithMaxDays_Succeeds()
    {
        await using var db = CreateInMemoryDb();

        var leaveType = new LeaveType { Id = Guid.NewGuid(), Name = "Maternity Leave", AccrualType = AccrualType.OneTime, MaxDaysPerYear = 180, RequiresDocument = true, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

        db.LeaveTypes.Add(leaveType);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var stored = await db.LeaveTypes.FindAsync(leaveType.Id);
        Assert.NotNull(stored);
        Assert.Equal(AccrualType.OneTime, stored.AccrualType);
        Assert.Equal(180, stored.MaxDaysPerYear);
        Assert.True(stored.RequiresDocument);
    }
}
