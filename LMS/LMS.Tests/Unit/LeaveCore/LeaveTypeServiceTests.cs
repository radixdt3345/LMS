using LMS.Application.DTOs.LeaveCore;
using LMS.Domain.Entities;
using LMS.Domain.Enums;
using LMS.Infrastructure.Data;
using LMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LMS.Tests.Unit.LeaveCore;

/// <summary>
/// Unit tests for LeaveTypeService (service-layer logic).
/// UT-27: GetLeaveTypes excludes inactive leave types by default.
/// UT-28: CreateLeaveType with null MaxDaysPerYear creates an unlimited type.
/// UT-29: DeactivateLeaveType sets IsActive=false (soft delete).
/// UT-30: UpdateLeaveType returns 404 for an unknown id.
/// POL-06/FR-30: CreateLeaveTypeDto has no carry_forward field.
/// </summary>
[Trait("Category", "Unit")]
public class LeaveTypeServiceTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static LmsDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<LmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new LmsDbContext(options);
    }

    private static LeaveTypeService BuildService(LmsDbContext db) => new(db);

    private static LeaveType MakeLeaveType(string name, bool isActive = true,
        int? maxDays = 10) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        MaxDaysPerYear = maxDays,
        AccrualType = AccrualType.Annual,
        RequiresDocument = false,
        IsActive = isActive,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    // ── UT-27: GetLeaveTypes excludes inactive by default ─────────────────

    [Fact]
    public async Task UT27_GetLeaveTypes_ExcludesInactiveByDefault()
    {
        await using var db = CreateInMemoryDb();
        db.LeaveTypes.Add(MakeLeaveType("Annual Leave", isActive: true));
        db.LeaveTypes.Add(MakeLeaveType("Deprecated Leave", isActive: false));
        await db.SaveChangesAsync();

        var svc = BuildService(db);
        var result = await svc.GetLeaveTypesAsync(includeInactive: false);

        Assert.True(result.IsSuccess);
        var items = result.Value!.ToList();
        Assert.Single(items);
        Assert.Equal("Annual Leave", items[0].Name);
        Assert.True(items[0].IsActive);
    }

    [Fact]
    public async Task UT27b_GetLeaveTypes_IncludeInactiveTrue_ReturnsAll()
    {
        await using var db = CreateInMemoryDb();
        db.LeaveTypes.Add(MakeLeaveType("Annual Leave", isActive: true));
        db.LeaveTypes.Add(MakeLeaveType("Deprecated Leave", isActive: false));
        await db.SaveChangesAsync();

        var svc = BuildService(db);
        var result = await svc.GetLeaveTypesAsync(includeInactive: true);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count());
    }

    // ── UT-28: CreateLeaveType null MaxDaysPerYear → unlimited ────────────

    [Fact]
    public async Task UT28_CreateLeaveType_NullMaxDays_CreatesUnlimitedType()
    {
        await using var db = CreateInMemoryDb();
        var svc = BuildService(db);

        var dto = new CreateLeaveTypeDto
        {
            Name = "Unpaid Leave",
            MaxDaysPerYear = null,
            AccrualType = AccrualType.Unlimited,
            RequiresDocument = false,
        };

        var result = await svc.CreateLeaveTypeAsync(dto);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.MaxDaysPerYear);
        Assert.Equal(AccrualType.Unlimited, result.Value.AccrualType);

        // Verify persisted to DB
        var persisted = await db.LeaveTypes.FirstAsync(lt => lt.Name == "Unpaid Leave");
        Assert.Null(persisted.MaxDaysPerYear);
        Assert.True(persisted.IsActive);
    }

    // ── UT-29: DeactivateLeaveType sets IsActive=false ────────────────────

    [Fact]
    public async Task UT29_DeactivateLeaveType_SetsIsActiveFalse()
    {
        await using var db = CreateInMemoryDb();
        var lt = MakeLeaveType("Sick Leave");
        db.LeaveTypes.Add(lt);
        await db.SaveChangesAsync();

        var svc = BuildService(db);
        var result = await svc.DeactivateLeaveTypeAsync(lt.Id);

        Assert.True(result.IsSuccess);

        var updated = await db.LeaveTypes.FindAsync(lt.Id);
        Assert.NotNull(updated);
        Assert.False(updated!.IsActive);
    }

    [Fact]
    public async Task UT29b_DeactivateLeaveType_AlreadyInactive_IsIdempotent()
    {
        await using var db = CreateInMemoryDb();
        var lt = MakeLeaveType("Old Leave", isActive: false);
        db.LeaveTypes.Add(lt);
        await db.SaveChangesAsync();

        var svc = BuildService(db);
        var result = await svc.DeactivateLeaveTypeAsync(lt.Id);

        Assert.True(result.IsSuccess); // idempotent — no error
    }

    // ── UT-30: UpdateLeaveType returns 404 for unknown id ─────────────────

    [Fact]
    public async Task UT30_UpdateLeaveType_UnknownId_Returns404()
    {
        await using var db = CreateInMemoryDb();
        var svc = BuildService(db);

        var result = await svc.UpdateLeaveTypeAsync(Guid.NewGuid(), new UpdateLeaveTypeDto
        {
            Name = "Does Not Exist",
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.StatusCode);
    }

    // ── POL-06/FR-30: CreateLeaveTypeDto has no carry_forward field ───────

    [Fact]
    public void POL06_CreateLeaveTypeDto_HasNoCarryForwardField()
    {
        var propertyNames = typeof(CreateLeaveTypeDto)
            .GetProperties()
            .Select(p => p.Name.Replace("_", string.Empty).ToLowerInvariant())
            .ToList();

        Assert.DoesNotContain("carryforward", propertyNames);
        Assert.DoesNotContain("carryfwd", propertyNames);
    }
}
