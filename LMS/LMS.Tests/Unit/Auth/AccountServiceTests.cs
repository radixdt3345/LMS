using LMS.Domain.Entities;
using LMS.Domain.Enums;
using LMS.Infrastructure.Data;
using LMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LMS.Tests.Unit.Auth;

[Trait("Category", "Unit")]
public class AccountServiceTests
{
    private static LmsDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<LmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new LmsDbContext(options);
    }

    private static AccountService BuildService(LmsDbContext db) => new(db);

    private static User MakeLockedUser(string email) => new()
    {
        Id = Guid.NewGuid(),
        Email = email,
        Role = UserRole.Employee,
        IsActive = true,
        FailedLoginCount = 5,
        LockoutUntil = DateTime.UtcNow.AddMinutes(25),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    [Fact]
    public async Task UnlockAccountAsync_LockedUser_ClearsLockoutFields()
    {
        await using var db = CreateInMemoryDb();

        var lockedUser = MakeLockedUser("locked@example.com");
        db.Users.Add(lockedUser);
        await db.SaveChangesAsync();

        var svc = BuildService(db);
        var result = await svc.UnlockAccountAsync(lockedUser.Id);

        Assert.True(result.IsSuccess);

        var updated = await db.Users.FindAsync(lockedUser.Id);
        Assert.NotNull(updated);
        Assert.Equal(0, updated!.FailedLoginCount);
        Assert.Null(updated.LockoutUntil);
    }

    [Fact]
    public async Task UnlockAccountAsync_UnknownUser_Returns404()
    {
        await using var db = CreateInMemoryDb();

        var svc = BuildService(db);
        var result = await svc.UnlockAccountAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task GetAccountsAsync_ReturnsCorrectPage()
    {
        await using var db = CreateInMemoryDb();

        for (var i = 1; i <= 5; i++)
        {
            db.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Email = $"user{i:D2}@example.com",
                Role = UserRole.Employee,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        }
        await db.SaveChangesAsync();

        var svc = BuildService(db);

        var result = await svc.GetAccountsAsync(page: 1, limit: 2);

        Assert.True(result.IsSuccess);
        var paged = result.Value!;
        Assert.Equal(5, paged.Total);
        Assert.Equal(1, paged.Page);
        Assert.Equal(2, paged.Limit);
        Assert.Equal(2, paged.Items.Count());
    }

    [Fact]
    public async Task GetAccountsAsync_LockedUser_HasIsLockedTrue()
    {
        await using var db = CreateInMemoryDb();

        var lockedUser = MakeLockedUser("locked-listed@example.com");
        db.Users.Add(lockedUser);
        await db.SaveChangesAsync();

        var svc = BuildService(db);
        var result = await svc.GetAccountsAsync(page: 1, limit: 20);

        Assert.True(result.IsSuccess);
        var account = result.Value!.Items.Single();
        Assert.True(account.IsLocked);
        Assert.Equal(5, account.FailedLoginCount);
    }
}
