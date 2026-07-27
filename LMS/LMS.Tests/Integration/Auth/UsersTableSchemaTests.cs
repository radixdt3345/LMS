using LMS.Domain.Entities;
using LMS.Domain.Enums;
using LMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LMS.Tests.Integration.Auth;

/// <summary>
/// IT-1: Verifies users and refresh_tokens table schema — columns, constraints, indexes, cascade delete.
/// Requires a running PostgreSQL instance. Set TEST_DB_CONNECTION env var or use default.
/// Run: dotnet test --filter Category=Integration
/// </summary>
[Trait("Category", "Integration")]
public class UsersTableSchemaTests : IAsyncLifetime
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

    [Fact]
    public async Task UsersTable_CanInsertAndRetrieve_User()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"it1-basic-{Guid.NewGuid()}@example.com",
            Role = UserRole.Employee,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var retrieved = await _context.Users.FindAsync(user.Id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(user.Email, retrieved.Email);
        Assert.Equal(UserRole.Employee, retrieved.Role);
        Assert.True(retrieved.IsActive);
    }

    [Fact]
    public async Task UsersTable_NullableColumns_StoreCorrectly()
    {
        // password_hash, azure_ad_oid, department_id, lockout_until are all nullable
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"it1-nullable-{Guid.NewGuid()}@example.com",
            PasswordHash = null,
            AzureAdOid = null,
            DepartmentId = null,
            LockoutUntil = null,
            Role = UserRole.HRAdmin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var retrieved = await _context.Users.FindAsync(user.Id);
        Assert.NotNull(retrieved);
        Assert.Null(retrieved.PasswordHash);
        Assert.Null(retrieved.AzureAdOid);
        Assert.Null(retrieved.DepartmentId);
        Assert.Null(retrieved.LockoutUntil);
    }

    [Fact]
    public async Task RefreshTokens_CascadeDeleteWithUser()
    {
        // Arrange — create user + linked refresh token
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"it1-cascade-{Guid.NewGuid()}@example.com",
            Role = UserRole.Employee,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = $"hash-{Guid.NewGuid()}",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        _context.RefreshTokens.Add(token);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act — delete the user
        var toDelete = await _context.Users.FindAsync(user.Id);
        _context.Users.Remove(toDelete!);
        await _context.SaveChangesAsync();

        // Assert — token is gone (cascade)
        var orphanToken = await _context.RefreshTokens.FindAsync(token.Id);
        Assert.Null(orphanToken);
    }

    [Fact]
    public async Task Users_EmailUniqueConstraint_IsEnforced()
    {
        var email = $"it1-unique-{Guid.NewGuid()}@example.com";

        var user1 = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Role = UserRole.Employee,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var user2 = new User
        {
            Id = Guid.NewGuid(),
            Email = email, // duplicate
            Role = UserRole.Employee,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user1);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        _context.Users.Add(user2);
        await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());
    }

    [Fact]
    public async Task Users_AzureAdOidUniqueConstraint_IsEnforced()
    {
        var oid = $"oid-{Guid.NewGuid()}";

        var user1 = new User
        {
            Id = Guid.NewGuid(),
            Email = $"it1-oid1-{Guid.NewGuid()}@example.com",
            AzureAdOid = oid,
            Role = UserRole.Employee,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var user2 = new User
        {
            Id = Guid.NewGuid(),
            Email = $"it1-oid2-{Guid.NewGuid()}@example.com",
            AzureAdOid = oid, // duplicate OID
            Role = UserRole.Employee,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user1);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        _context.Users.Add(user2);
        await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());
    }

    [Fact]
    public async Task Users_AllRoles_CanBeStored()
    {
        foreach (var role in Enum.GetValues<UserRole>())
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = $"it1-role-{role}-{Guid.NewGuid()}@example.com",
                Role = role,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Users.Add(user);
        }
        await _context.SaveChangesAsync();
        // If we get here all 4 role values stored without error
        Assert.True(true);
    }
}
