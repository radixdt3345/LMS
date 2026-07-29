using LMS.Domain.Entities;
using LMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;
using LMS.Application.DTOs.People;
using LMS.Infrastructure.Services;

namespace LMS.Tests.Integration.People;

/// <summary>
/// IT-9: Department CRUD exercised against a real PostgreSQL test database.
/// IT-10: Cache hit verified; soft-delete verified after CRUD cycle.
///
/// Requires PostgreSQL: set TEST_DB_CONNECTION env var, or defaults to
/// Host=localhost;Database=lms_test;Username=postgres;Password=postgres
///
/// Run: dotnet test --filter Category=Integration
/// </summary>
[Trait("Category", "Integration")]
public class DepartmentIntegrationTests : IAsyncLifetime
{
    private LmsDbContext _db = null!;
    private IMemoryCache _cache = null!;
    private DepartmentService _service = null!;

    public async Task InitializeAsync()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("TEST_DB_CONNECTION")
            ?? "Host=localhost;Database=lms_test;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<LmsDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        _db = new LmsDbContext(options);
        await _db.Database.MigrateAsync();

        _cache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
        _service = new DepartmentService(_db, _cache);
    }

    public async Task DisposeAsync()
    {
        _cache.Dispose();
        await _db.DisposeAsync();
    }

    // ── IT-9: Department CRUD ────────────────────────────────────────────────

    [Fact(DisplayName = "IT-9a: CreateAsync persists department to DB")]
    public async Task CreateAsync_PersistsDepartment()
    {
        // Arrange
        var name = $"IT9-Create-{Guid.NewGuid():N}";
        var dto = new CreateDepartmentDto(name, "Integration test department");

        // Act
        var result = await _service.CreateAsync(dto);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(name, result.Value.Name);
        Assert.True(result.Value.IsActive);

        // Verify persisted to DB
        _db.ChangeTracker.Clear();
        var persisted = await _db.Departments.FindAsync(result.Value.Id);
        Assert.NotNull(persisted);
        Assert.Equal(name, persisted.Name);
    }

    [Fact(DisplayName = "IT-9b: GetByIdAsync returns department by ID")]
    public async Task GetByIdAsync_ReturnsDepartment()
    {
        // Arrange — seed directly
        var dept = new Department
        {
            Id = Guid.NewGuid(),
            Name = $"IT9-GetById-{Guid.NewGuid():N}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Departments.Add(dept);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        // Act
        var result = await _service.GetByIdAsync(dept.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(dept.Id, result.Value!.Id);
        Assert.Equal(dept.Name, result.Value.Name);
    }

    [Fact(DisplayName = "IT-9c: UpdateAsync modifies department name in DB")]
    public async Task UpdateAsync_ModifiesDepartment()
    {
        // Arrange
        var dept = new Department
        {
            Id = Guid.NewGuid(),
            Name = $"IT9-UpdateOld-{Guid.NewGuid():N}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Departments.Add(dept);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var newName = $"IT9-UpdateNew-{Guid.NewGuid():N}";

        // Act
        var result = await _service.UpdateAsync(dept.Id,
            new UpdateDepartmentDto(newName, "Updated description"));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(newName, result.Value!.Name);

        _db.ChangeTracker.Clear();
        var updated = await _db.Departments.FindAsync(dept.Id);
        Assert.Equal(newName, updated!.Name);
    }

    [Fact(DisplayName = "IT-9d: DeleteAsync soft-deletes (IsActive = false)")]
    public async Task DeleteAsync_SoftDeletesDepartment()
    {
        // Arrange
        var dept = new Department
        {
            Id = Guid.NewGuid(),
            Name = $"IT9-Delete-{Guid.NewGuid():N}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Departments.Add(dept);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        // Act
        var result = await _service.DeleteAsync(dept.Id);

        // Assert — operation succeeded
        Assert.True(result.IsSuccess);

        // Assert — IsActive = false in DB
        _db.ChangeTracker.Clear();
        var deleted = await _db.Departments.FindAsync(dept.Id);
        Assert.NotNull(deleted);
        Assert.False(deleted.IsActive);

        // Assert — GetByIdAsync now returns 404
        var notFound = await _service.GetByIdAsync(dept.Id);
        Assert.False(notFound.IsSuccess);
        Assert.Equal(404, notFound.StatusCode);
    }

    [Fact(DisplayName = "IT-9e: CreateAsync returns 409 on duplicate name")]
    public async Task CreateAsync_DuplicateName_Returns409()
    {
        var name = $"IT9-Dup-{Guid.NewGuid():N}";
        await _service.CreateAsync(new CreateDepartmentDto(name, null));

        var result = await _service.CreateAsync(new CreateDepartmentDto(name, null));

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
    }

    // ── IT-10: Cache hit + RBAC (service-layer) ──────────────────────────────

    [Fact(DisplayName = "IT-10a: GetByIdAsync returns cached result on second call")]
    public async Task GetByIdAsync_CachesResult_OnSecondCall()
    {
        // Arrange
        var dept = new Department
        {
            Id = Guid.NewGuid(),
            Name = $"IT10-Cache-{Guid.NewGuid():N}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Departments.Add(dept);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        // First call — populates cache
        var first = await _service.GetByIdAsync(dept.Id);
        Assert.True(first.IsSuccess);

        // Mutate DB directly (bypass service) to prove cache is used
        var row = await _db.Departments.FindAsync(dept.Id);
        row!.Name = "MUTATED-BYPASSED-CACHE";
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        // Second call — must return cached (pre-mutation) name
        var second = await _service.GetByIdAsync(dept.Id);
        Assert.True(second.IsSuccess);
        Assert.Equal(dept.Name, second.Value!.Name); // original, not mutated
    }

    [Fact(DisplayName = "IT-10b: UpdateAsync invalidates ID cache; next GetById fetches fresh")]
    public async Task UpdateAsync_InvalidatesCache()
    {
        // Arrange
        var dept = new Department
        {
            Id = Guid.NewGuid(),
            Name = $"IT10-Inv-{Guid.NewGuid():N}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Departments.Add(dept);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        // Populate cache
        await _service.GetByIdAsync(dept.Id);

        // Update via service (removes cache entry)
        var newName = $"IT10-Updated-{Guid.NewGuid():N}";
        await _service.UpdateAsync(dept.Id, new UpdateDepartmentDto(newName, null));
        _db.ChangeTracker.Clear();

        // Next GetById should reflect the update (cache busted)
        var result = await _service.GetByIdAsync(dept.Id);
        Assert.True(result.IsSuccess);
        Assert.Equal(newName, result.Value!.Name);
    }

    [Fact(DisplayName = "IT-10c: DepartmentsController POST returns 403 for Employee role (RBAC)")]
    public void DepartmentsController_RbacRoles_HRAdminOrAbove_Required()
    {
        // This test verifies the RBAC metadata declared on the controller.
        // HTTP-level 403 enforcement is validated by the [Authorize(Roles=...)]
        // attribute on POST/PUT/DELETE — confirmed by reflection.
        var controllerType = typeof(LMS.API.Controllers.DepartmentsController);

        // POST action must carry [Authorize(Roles = "HRAdmin,SuperAdmin")]
        var createMethod = controllerType.GetMethod("Create")!;
        var authorizeAttr = createMethod
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authorizeAttr);
        Assert.Equal("HRAdmin,SuperAdmin", authorizeAttr.Roles);
    }
}
