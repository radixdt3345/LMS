using LMS.Domain.Entities;
using LMS.Domain.Enums;
using LMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LMS.Tests.Integration.People;

/// <summary>
/// IT-9:  Department CRUD — create, read, update, soft-delete via EF Core + PostgreSQL.
/// IT-10a: Duplicate department name → DbUpdateException (unique index enforced at DB level).
/// IT-10b: Soft-delete a department that has assigned employees — employees retain DepartmentId.
/// IT-10c: Authorization — Employee role cannot manage departments (role-level guard).
/// Requires a running PostgreSQL instance. Set TEST_DB_CONNECTION env var or use default.
/// Run: dotnet test --filter Category=Integration
/// </summary>
[Trait("Category", "Integration")]
public class DepartmentIntegrationTests : IAsyncLifetime
{
    private LmsDbContext _context = null!;

    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION")
            ?? "Host=localhost;Database=lms_test;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<LmsDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        _context = new LmsDbContext(options);
        await _context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    // ── IT-9: Department CRUD ────────────────────────────────────────────────

    [Fact]
    public async Task IT9_Department_CreateReadUpdateSoftDelete_WorksCorrectly()
    {
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];

        // Create
        var dept = new Department
        {
            Id = Guid.NewGuid(),
            Name = $"Engineering-{uniqueSuffix}",
            Description = "Software engineering team",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _context.Departments.Add(dept);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Read by id
        var retrieved = await _context.Departments.FindAsync(dept.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(dept.Name, retrieved.Name);
        Assert.True(retrieved.IsActive);

        // Update name
        retrieved.Name = $"Engineering-Updated-{uniqueSuffix}";
        retrieved.UpdatedAt = DateTime.UtcNow;
        _context.Departments.Update(retrieved);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var updated = await _context.Departments.FindAsync(dept.Id);
        Assert.NotNull(updated);
        Assert.Contains("Updated", updated.Name);

        // Soft-delete
        updated.IsActive = false;
        updated.UpdatedAt = DateTime.UtcNow;
        _context.Departments.Update(updated);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // GET list does not include soft-deleted department
        var activeDepts = await _context.Departments
            .Where(d => d.IsActive)
            .ToListAsync();
        Assert.DoesNotContain(activeDepts, d => d.Id == dept.Id);

        // Record still exists in DB (soft delete, not hard delete)
        var softDeleted = await _context.Departments.FindAsync(dept.Id);
        Assert.NotNull(softDeleted);
        Assert.False(softDeleted.IsActive);
    }

    // ── IT-10a: Duplicate name → DB unique constraint ──────────────────────

    [Fact]
    public async Task IT10a_Department_DuplicateName_ThrowsDbUpdateException()
    {
        var sharedName = $"Finance-{Guid.NewGuid():N}";

        var dept1 = new Department
        {
            Id = Guid.NewGuid(),
            Name = sharedName,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _context.Departments.Add(dept1);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var dept2 = new Department
        {
            Id = Guid.NewGuid(),
            Name = sharedName, // duplicate — violates ix_departments_name
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _context.Departments.Add(dept2);

        await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());
    }

    // ── IT-10b: Department with employees — soft-delete preserves employee DepartmentId ──

    [Fact]
    public async Task IT10b_Department_WithEmployee_SoftDeletePreservesEmployeeReference()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];

        // Create department
        var dept = new Department
        {
            Id = Guid.NewGuid(),
            Name = $"Marketing-{suffix}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _context.Departments.Add(dept);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Assign an employee to the department
        var employee = new User
        {
            Id = Guid.NewGuid(),
            Email = $"emp-it10b-{suffix}@example.com",
            Role = UserRole.Employee,
            DepartmentId = dept.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _context.Users.Add(employee);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Verify the department has active employees — the service layer returns 409 here
        var hasActiveEmployees = await _context.Users
            .AnyAsync(u => u.DepartmentId == dept.Id && u.IsActive);
        Assert.True(hasActiveEmployees,
            "Department has active employees — DepartmentService must return 409 Conflict on DELETE");

        // DB-layer soft-delete does NOT cascade-null the employee's DepartmentId
        var deptToDelete = await _context.Departments.FindAsync(dept.Id);
        Assert.NotNull(deptToDelete);
        deptToDelete.IsActive = false;
        deptToDelete.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Employee still references the (now inactive) department
        var emp = await _context.Users.FindAsync(employee.Id);
        Assert.NotNull(emp);
        Assert.Equal(dept.Id, emp.DepartmentId);
    }

    // ── IT-10c: Employee role cannot create departments (HTTP authorization guard) ──

    [Fact(Skip = "Requires DepartmentsController (PEOPLE-API-001) — HTTP-level 403 test deferred until controller is implemented")]
    public Task IT10c_EmployeeJwt_PostDepartments_Returns403()
    {
        // When DepartmentsController is implemented:
        // 1. Obtain JWT for a seeded Employee-role user via POST /api/v1/auth/login
        // 2. POST /api/v1/departments with Authorization: Bearer <employee-jwt>
        // 3. Assert HTTP 403 Forbidden
        // 4. Assert response body: { "success": false, "error": { "code": "FORBIDDEN" } }
        return Task.CompletedTask;
    }
}
