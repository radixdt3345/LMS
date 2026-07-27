using LMS.Application.DTOs.Auth;
using LMS.Application.Interfaces;
using LMS.Application.Settings;
using LMS.Domain.Entities;
using LMS.Domain.Enums;
using LMS.Infrastructure.Data;
using LMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using BC = BCrypt.Net.BCrypt;

namespace LMS.Tests.Unit.Auth;

/// <summary>
/// Unit tests for AuthService — local login, lockout, credential validation.
/// Uses EF Core InMemory provider; no PostgreSQL required.
/// </summary>
[Trait("Category", "Unit")]
public class AuthServiceTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static LmsDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<LmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new LmsDbContext(options);
    }

    private static IOptions<JwtSettings> DefaultJwtOptions() =>
        Options.Create(new JwtSettings
        {
            SecretKey = "test-secret-key-min-32-chars-long!!",
            Issuer = "lms-test",
            Audience = "lms-client",
            AccessTokenExpiryMinutes = 15,
            RefreshTokenExpiryDays = 7
        });

    private static User CreateActiveUser(string email, string password) => new()
    {
        Id = Guid.NewGuid(),
        Email = email,
        PasswordHash = BC.HashPassword(password),
        Role = UserRole.Employee,
        IsActive = true,
        FailedLoginCount = 0,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private static AuthService BuildService(LmsDbContext db, ITokenService? tokenSvc = null)
    {
        tokenSvc ??= new Mock<ITokenService>().Object;
        return new AuthService(db, tokenSvc, DefaultJwtOptions());
    }

    // ── UT-1: valid credentials → success + tokens ───────────────────────────

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsSuccess()
    {
        await using var db = CreateInMemoryDb();

        var tokenSvc = new Mock<ITokenService>();
        tokenSvc.Setup(t => t.IssueAccessToken(It.IsAny<User>())).Returns("access.token.jwt");
        tokenSvc.Setup(t => t.IssueRefreshTokenAsync(It.IsAny<Guid>(), default))
                .ReturnsAsync("refresh-raw-token");

        var user = CreateActiveUser("valid@example.com", "Password123!");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var svc = new AuthService(db, tokenSvc.Object, DefaultJwtOptions());
        var result = await svc.LoginAsync(new LoginRequestDto
        {
            Email = "valid@example.com",
            Password = "Password123!"
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("access.token.jwt", result.Value.AccessToken);
        Assert.Equal("refresh-raw-token", result.Value.RefreshToken);
        Assert.Equal(15 * 60, result.Value.ExpiresIn);
    }

    // ── UT-2: wrong password → 401 ───────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_InvalidPassword_Returns401()
    {
        await using var db = CreateInMemoryDb();

        var user = CreateActiveUser("user2@example.com", "CorrectPass!");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var result = await BuildService(db).LoginAsync(new LoginRequestDto
        {
            Email = "user2@example.com",
            Password = "WrongPass"
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(401, result.StatusCode);
    }

    // ── UT-3: unknown email → 401 ─────────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_UnknownEmail_Returns401()
    {
        await using var db = CreateInMemoryDb();

        var result = await BuildService(db).LoginAsync(new LoginRequestDto
        {
            Email = "nobody@example.com",
            Password = "AnyPass"
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(401, result.StatusCode);
    }

    // ── UT-4 (covers FR-7): 5 consecutive failures → lockout applied ──────────

    [Fact]
    public async Task LoginAsync_FiveConsecutiveFailures_SetsLockout()
    {
        await using var db = CreateInMemoryDb();

        var user = CreateActiveUser("lock@example.com", "CorrectPass!");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var svc = BuildService(db);
        for (var i = 0; i < 5; i++)
            await svc.LoginAsync(new LoginRequestDto { Email = "lock@example.com", Password = "Wrong" });

        var updated = await db.Users.FindAsync(user.Id);
        Assert.NotNull(updated);
        Assert.NotNull(updated!.LockoutUntil);
        Assert.True(updated.LockoutUntil > DateTime.UtcNow);
    }

    // ── UT-5: already-locked account → 423 regardless of correct password ─────

    [Fact]
    public async Task LoginAsync_LockedAccount_Returns423()
    {
        await using var db = CreateInMemoryDb();

        var user = CreateActiveUser("locked@example.com", "Pass!");
        user.LockoutUntil = DateTime.UtcNow.AddMinutes(10);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var result = await BuildService(db).LoginAsync(new LoginRequestDto
        {
            Email = "locked@example.com",
            Password = "Pass!" // correct password — still locked
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(423, result.StatusCode);
    }

    // ── UT-6: inactive user → 401 ─────────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_InactiveUser_Returns401()
    {
        await using var db = CreateInMemoryDb();

        var user = CreateActiveUser("inactive@example.com", "Pass!");
        user.IsActive = false;
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var result = await BuildService(db).LoginAsync(new LoginRequestDto
        {
            Email = "inactive@example.com",
            Password = "Pass!"
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(401, result.StatusCode);
    }

    // ── UT-7: successful login resets failed counter ──────────────────────────

    [Fact]
    public async Task LoginAsync_SuccessAfterFailures_ResetsFailedCount()
    {
        await using var db = CreateInMemoryDb();

        var tokenSvc = new Mock<ITokenService>();
        tokenSvc.Setup(t => t.IssueAccessToken(It.IsAny<User>())).Returns("tok");
        tokenSvc.Setup(t => t.IssueRefreshTokenAsync(It.IsAny<Guid>(), default)).ReturnsAsync("ref");

        var user = CreateActiveUser("reset@example.com", "CorrectPass!");
        user.FailedLoginCount = 3;
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var svc = new AuthService(db, tokenSvc.Object, DefaultJwtOptions());
        var result = await svc.LoginAsync(new LoginRequestDto
        {
            Email = "reset@example.com",
            Password = "CorrectPass!"
        });

        Assert.True(result.IsSuccess);
        var updated = await db.Users.FindAsync(user.Id);
        Assert.Equal(0, updated!.FailedLoginCount);
        Assert.Null(updated.LockoutUntil);
    }
}
