using LMS.Application.DTOs.People;
using LMS.Domain.Entities;
using LMS.Domain.Enums;
using LMS.Infrastructure.Data;
using LMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace LMS.Tests.Unit.People;

/// <summary>
/// UT-14: CreateDepartmentAsync — 409 when department name already exists.
/// UT-15: DeleteDepartmentAsync — 409 when department has active employees.
/// </summary>
[Trait("Category", "Unit")]
public class DepartmentServiceTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static LmsDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<LmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new LmsDbContext(options);
    }

    private static IMemoryCache CreateCache() =>
        new MemoryCache(new MemoryCacheOptions());

    private static DepartmentService BuildService(LmsDbContext db, IMemoryCache? cache = null) =>
        new(db, cache ?? CreateCache());

    private static async Task<Department> SeedDepartmentAsync(
        LmsDbContext db, string name, bool isActive = true)
    {
        var now = DateTime.UtcNow;
        var dept = new Department
        {
            Id = Guid.NewGuid(),
            Name = name,
            IsActive = isActive,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Departments.Add(dept);
        await db.SaveChangesAsync();
        return dept;
    }

    // ── UT-14: Create — 409 if name already exists ────────────────────────

    [Fact]
    public async Task CreateDepartmentAsync_DuplicateName_Returns409()
    {
        await using var db = CreateInMemoryDb();
        await SeedDepartmentAsync(db, "Engineering");

        var svc = BuildService(db);
        var result = await svc.CreateDepartmentAsync(
            new CreateDepartmentRequest("Engineering", null));

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Contains("already exists", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateDepartmentAsync_UniqueName_ReturnsCreatedDepartment()
    {
        await using var db = CreateInMemoryDb();

        var svc = BuildService(db);
        var result = await svc.CreateDepartmentAsync(
            new CreateDepartmentRequest("HR", "Human Resources"));

        Assert.True(result.IsSuccess);
        Assert.Equal("HR", result.Value!.Name);
        Assert.Equal("Human Resources", result.Value.Description);
        Assert.True(result.Value.IsActive);
        Assert.Equal(1, await db.Departments.CountAsync());
    }

    [Fact]
    public async Task CreateDepartmentAsync_InactiveDuplicateName_Succeeds()
    {
        // An inactive department with the same name should not block creation
        await using var db = CreateInMemoryDb();
        await SeedDepartmentAsync(db, "Finance", isActive: false);

        var svc = BuildService(db);
        var result = await svc.CreateDepartmentAsync(
            new CreateDepartmentRequest("Finance", null));

        Assert.True(result.IsSuccess);
    }

    // ── UT-15: Delete — 409 if department has active employees ────────────

    [Fact]
    public async Task DeleteDepartmentAsync_HasActiveEmployees_Returns409()
    {
        await using var db = CreateInMemoryDb();
        var dept = await SeedDepartmentAsync(db, "Operations");

        var now = DateTime.UtcNow;
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "emp@example.com",
            Role = UserRole.Employee,
            DepartmentId = dept.Id,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();

        var svc = BuildService(db);
        var result = await svc.DeleteDepartmentAsync(dept.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Contains("active employees", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteDepartmentAsync_InactiveEmployeesOnly_SoftDeletes()
    {
        // Inactive employees do not block soft-delete
        await using var db = CreateInMemoryDb();
        var dept = await SeedDepartmentAsync(db, "Legacy");

        var now = DateTime.UtcNow;
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "former@example.com",
            Role = UserRole.Employee,
            DepartmentId = dept.Id,
            IsActive = false, // inactive user
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();

        var svc = BuildService(db);
        var result = await svc.DeleteDepartmentAsync(dept.Id);

        Assert.True(result.IsSuccess);
        var updated = await db.Departments.FindAsync(dept.Id);
        Assert.False(updated!.IsActive);
    }

    [Fact]
    public async Task DeleteDepartmentAsync_NoEmployees_SoftDeletes()
    {
        await using var db = CreateInMemoryDb();
        var dept = await SeedDepartmentAsync(db, "Empty Dept");

        var svc = BuildService(db);
        var result = await svc.DeleteDepartmentAsync(dept.Id);

        Assert.True(result.IsSuccess);
        var updated = await db.Departments.FindAsync(dept.Id);
        Assert.False(updated!.IsActive);
    }

    [Fact]
    public async Task DeleteDepartmentAsync_NotFound_Returns404()
    {
        await using var db = CreateInMemoryDb();
        var svc = BuildService(db);
        var result = await svc.DeleteDepartmentAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task DeleteDepartmentAsync_AlreadyInactive_IsIdempotent()
    {
        await using var db = CreateInMemoryDb();
        var dept = await SeedDepartmentAsync(db, "Archived", isActive: false);

        var svc = BuildService(db);
        var result = await svc.DeleteDepartmentAsync(dept.Id);

        Assert.True(result.IsSuccess);
    }

    // ── Cache: mutation invalidates active list ───────────────────────────

    [Fact]
    public async Task GetDepartmentsAsync_AfterCreate_ReturnsUpdatedList()
    {
        await using var db = CreateInMemoryDb();
        var cache = CreateCache();
        var svc = BuildService(db, cache);

        // Warm the cache with an empty result
        var initial = await svc.GetDepartmentsAsync();
        Assert.True(initial.IsSuccess);
        Assert.Empty(initial.Value!);

        // Create busts cache, next GET re-fetches from DB
        await svc.CreateDepartmentAsync(new CreateDepartmentRequest("Marketing", null));

        var after = await svc.GetDepartmentsAsync();
        Assert.True(after.IsSuccess);
        Assert.Single(after.Value!);
    }

    [Fact]
    public async Task UpdateDepartmentAsync_DuplicateName_Returns409()
    {
        await using var db = CreateInMemoryDb();
        await SeedDepartmentAsync(db, "Sales");
        var target = await SeedDepartmentAsync(db, "Marketing");

        var svc = BuildService(db);
        var result = await svc.UpdateDepartmentAsync(
            target.Id, new UpdateDepartmentRequest("Sales", null, null));

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
    }
}
