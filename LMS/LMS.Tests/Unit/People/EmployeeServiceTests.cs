using LMS.Application.DTOs.People;
using LMS.Application.Interfaces;
using LMS.Domain.Entities;
using LMS.Domain.Enums;
using LMS.Infrastructure.Data;
using LMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace LMS.Tests.Unit.People;

/// <summary>
/// Unit tests for EmployeeService using EF Core InMemory database.
/// UT-20: GetEmployees returns only active employees, paginated correctly
/// UT-21: GetEmployeeById returns 404 when employee does not exist
/// UT-22: CreateEmployee returns 409 when email already registered
/// UT-23: DeactivateEmployee is idempotent (already-inactive user returns success)
/// </summary>
public class EmployeeServiceTests
{
    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static LmsDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<LmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new LmsDbContext(options);
    }

    private static EmployeeService CreateService(LmsDbContext db)
    {
        var auditMock = new Mock<IAuditService>();
        auditMock
            .Setup(a => a.LogAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new EmployeeService(db, auditMock.Object);
    }

    private static User MakeUser(string email, bool isActive = true) => new()
    {
        Id         = Guid.NewGuid(),
        Email      = email,
        IsActive   = isActive,
        Role       = UserRole.Employee,
        CreatedAt  = DateTime.UtcNow,
        UpdatedAt  = DateTime.UtcNow
    };

    // ------------------------------------------------------------------
    // UT-20
    // ------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UT20_GetEmployees_ReturnsOnlyActiveEmployees_Paginated()
    {
        // Arrange
        using var db = CreateDb();
        db.Users.AddRange(
            MakeUser("alice@test.com", isActive: true),
            MakeUser("bob@test.com",   isActive: true),
            MakeUser("carol@test.com", isActive: false)  // inactive — must not appear
        );
        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Act
        var result = await service.GetEmployeesAsync(page: 1, limit: 10, deptId: null, search: null);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Total);
        Assert.Equal(2, result.Value.Items.Count());
        Assert.All(result.Value.Items, dto => Assert.True(dto.IsActive));
    }

    // ------------------------------------------------------------------
    // UT-21
    // ------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UT21_GetEmployeeById_Returns404_WhenNotFound()
    {
        // Arrange
        using var db = CreateDb();
        var service = CreateService(db);

        // Act
        var result = await service.GetEmployeeByIdAsync(Guid.NewGuid());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.StatusCode);
    }

    // ------------------------------------------------------------------
    // UT-22
    // ------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UT22_CreateEmployee_Returns409_WhenEmailAlreadyExists()
    {
        // Arrange
        using var db = CreateDb();
        db.Users.Add(MakeUser("existing@test.com"));
        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Act — attempt to create a second user with the same email
        var result = await service.CreateEmployeeAsync(
            new CreateEmployeeDto("existing@test.com", null, null, null, null, null));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
    }

    // ------------------------------------------------------------------
    // UT-23
    // ------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UT23_DeactivateEmployee_IsIdempotent_WhenAlreadyInactive()
    {
        // Arrange — user is already inactive
        using var db = CreateDb();
        var user = MakeUser("emp@test.com", isActive: false);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Act — deactivating an already-inactive employee must still succeed
        var result = await service.DeactivateEmployeeAsync(user.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
    }
}
