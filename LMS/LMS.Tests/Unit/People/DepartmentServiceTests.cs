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

[Trait("Category", "Unit")]
public class DepartmentServiceTests
{
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
        var dept = new Department { Id = Guid.NewGuid(), Name = name, IsActive = isActive, CreatedAt = now, UpdatedAt = now };
        db.Departments.Add(dept);
        await db.SaveChangesAsync();
        return dept;
    }

    [Fact]
    public async Task CreateDepartmentAsync_DuplicateName_Returns409()
    {
        await using var db = CreateInMemoryDb();
        await SeedDepartmentAsync(db, "Engineering");
        var svc = BuildService(db);
        var result = await svc.CreateAsync(new CreateDepartmentDto("Engineering", null));
        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Contains("already exists", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateDepartmentAsync_UniqueName_ReturnsCreatedDepartment()
    {
        await using var db = CreateInMemoryDb();
        var svc = BuildService(db);
        var result = await svc.CreateAsync(new CreateDepartmentDto("HR", "Human Resources"));
        Assert.True(result.IsSuccess);
        Assert.Equal("HR", result.Value!.Name);
        Assert.Equal("Human Resources", result.Value.Description);
        Assert.True(result.Value.IsActive);
        Assert.Equal(1, await db.Departments.CountAsync());
    }

    [Fact]
    public async Task CreateDepartmentAsync_InactiveDuplicateName_Succeeds()
    {
        await using var db = CreateInMemoryDb();
        await SeedDepartmentAsync(db, "Finance", isActive: false);
        var svc = BuildService(db);
        var result = await svc.CreateAsync(new CreateDepartmentDto("Finance", null));
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task DeleteDepartmentAsync_HasActiveEmployees_Returns409()
    {
        await using var db = CreateInMemoryDb();
        var dept = await SeedDepartmentAsync(db, "Operations");
        var now = DateTime.UtcNow;
        db.Users.Add(new User { Id = Guid.NewGuid(), Email = "emp@example.com", Role = UserRole.Employee, DepartmentId = dept.Id, IsActive = true, CreatedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync();
        var svc = BuildService(db);
        var result = await svc.DeleteAsync(dept.Id);
        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Contains("active employees", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteDepartmentAsync_InactiveEmployeesOnly_SoftDeletes()
    {
        await using var db = CreateInMemoryDb();
        var dept = await SeedDepartmentAsync(db, "Legacy");
        var now = DateTime.UtcNow;
        db.Users.Add(new User { Id = Guid.NewGuid(), Email = "former@example.com", Role = UserRole.Employee, DepartmentId = dept.Id, IsActive = false, CreatedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync();
        var svc = BuildService(db);
        var result = await svc.DeleteAsync(dept.Id);
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
        var result = await svc.DeleteAsync(dept.Id);
        Assert.True(result.IsSuccess);
        var updated = await db.Departments.FindAsync(dept.Id);
        Assert.False(updated!.IsActive);
    }

    [Fact]
    public async Task DeleteDepartmentAsync_NotFound_Returns404()
    {
        await using var db = CreateInMemoryDb();
        var svc = BuildService(db);
        var result = await svc.DeleteAsync(Guid.NewGuid());
        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task DeleteDepartmentAsync_AlreadyInactive_IsIdempotent()
    {
        await using var db = CreateInMemoryDb();
        var dept = await SeedDepartmentAsync(db, "Archived", isActive: false);
        var svc = BuildService(db);
        var result = await svc.DeleteAsync(dept.Id);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task GetDepartmentsAsync_AfterCreate_ReturnsUpdatedList()
    {
        await using var db = CreateInMemoryDb();
        var cache = CreateCache();
        var svc = BuildService(db, cache);
        var initial = await svc.GetAllAsync(page: 1, limit: 20);
        Assert.True(initial.IsSuccess);
        Assert.Empty(initial.Value!.Items);
        await svc.CreateAsync(new CreateDepartmentDto("Marketing", null));
        var after = await svc.GetAllAsync(page: 1, limit: 20);
        Assert.True(after.IsSuccess);
        Assert.Single(after.Value!.Items);
    }

    [Fact]
    public async Task UpdateDepartmentAsync_DuplicateName_Returns409()
    {
        await using var db = CreateInMemoryDb();
        await SeedDepartmentAsync(db, "Sales");
        var target = await SeedDepartmentAsync(db, "Marketing");
        var svc = BuildService(db);
        var result = await svc.UpdateAsync(target.Id, new UpdateDepartmentDto("Sales", null));
        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
    }
}
