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

namespace LMS.Tests.Integration;

/// <summary>
/// Integration tests for account lockout and unlock flows.
/// Uses a real PostgreSQL database — set TEST_DB_CONNECTION or rely on the default.
///
/// IT-7: 5 consecutive wrong passwords lock the account;
///        the 6th attempt returns HTTP 423 and DB confirms lockout_until is set.
/// IT-8: A locked account is unlocked by AccountService;
///        subsequent login with correct credentials returns success + JWT.
/// </summary>
[Trait("Category", "Integration")]
public class AccountIntegrationTests : IAsyncLifetime
{
    private DbContextOptions<LmsDbContext> _options = null!;
    private LmsDbContext _context = null!;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("TEST_DB_CONNECTION")
            ?? "Host=localhost;Database=lms_test;Username=postgres;Password=postgres";

        _options = new DbContextOptionsBuilder<LmsDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        _context = new LmsDbContext(_options);
        await _context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private LmsDbContext CreateContext() => new LmsDbContext(_options);

    private static IOptions<JwtSettings> DefaultJwtOptions() =>
        Options.Create(new JwtSettings
        {
            SecretKey = "test-secret-key-min-32-chars-long!!",
            Issuer = "lms-test",
            Audience = "lms-client",
            AccessTokenExpiryMinutes = 15,
            RefreshTokenExpiryDays = 7,
        });

    private static AuthService BuildAuthService(LmsDbContext db)
    {
        var tokenSvc = new Mock<ITokenService>();
        tokenSvc
            .Setup(t => t.IssueAccessToken(It.IsAny<User>()))
            .Returns("access.token.jwt");
        tokenSvc
            .Setup(t => t.IssueRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("refresh.token");

        var msalMock = new Mock<IMsalAuthProvider>();

        return new AuthService(db, tokenSvc.Object, DefaultJwtOptions(), msalMock.Object);
    }

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

    // ── IT-7 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// IT-7: Five consecutive wrong passwords trigger lockout on the 5th attempt.
    /// The subsequent (6th) attempt returns 423 via the lockout-check path,
    /// and the DB confirms lockout_until is set to a future UTC timestamp.
    /// </summary>
    [Fact]
    public async Task IT7_FiveWrongPasswords_SixthAttemptReturns423_AndDbShowsLockoutUntilSet()
    {
        // Arrange — seed a fresh active user
        var email = $"it7-{Guid.NewGuid():N}@example.com";
        var user = CreateActiveUser(email, "CorrectPass123!");

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var svc = BuildAuthService(_context);

        // Act — 5 consecutive wrong-password attempts
        // Attempt 1-4 → 401; attempt 5 → FailedLoginCount reaches MaxFailedAttempts (5),
        // LockoutUntil is stamped, returns 423.
        for (var i = 0; i < 5; i++)
        {
            await svc.LoginAsync(new LoginRequestDto
            {
                Email = email,
                Password = "WrongPassword!",
            });
        }

        // Act — 6th attempt (lockout already triggered; lockout-check fires first)
        var result = await svc.LoginAsync(new LoginRequestDto
        {
            Email = email,
            Password = "WrongPassword!",
        });

        // Assert — 6th attempt blocked with 423
        Assert.False(result.IsSuccess);
        Assert.Equal(423, result.StatusCode);

        // Assert — DB confirms lockout_until is set and in the future
        _context.ChangeTracker.Clear();
        var dbUser = await _context.Users.FindAsync(user.Id);
        Assert.NotNull(dbUser);
        Assert.NotNull(dbUser!.LockoutUntil);
        Assert.True(
            dbUser.LockoutUntil > DateTime.UtcNow,
            $"lockout_until ({dbUser.LockoutUntil:O}) must be a future UTC timestamp");
    }

    // ── IT-8 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// IT-8: HRAdmin calls AccountService.UnlockAccountAsync on a locked user.
    /// After unlock, login with correct credentials returns HTTP 200 + JWT.
    /// </summary>
    [Fact]
    public async Task IT8_HrAdminUnlocksAccount_ThenCorrectLoginSucceeds()
    {
        // Arrange — seed a user that is already locked
        var email = $"it8-{Guid.NewGuid():N}@example.com";
        var password = "ValidPass123!";
        var user = CreateActiveUser(email, password);
        user.FailedLoginCount = 5;
        user.LockoutUntil = DateTime.UtcNow.AddMinutes(30); // locked for 30 min

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var authSvc = BuildAuthService(_context);
        var accountSvc = new AccountService(_context);

        // Act 1 — confirm account is currently locked
        var blockedResult = await authSvc.LoginAsync(new LoginRequestDto
        {
            Email = email,
            Password = password, // correct password — still blocked by lockout
        });
        Assert.Equal(423, blockedResult.StatusCode);

        // Act 2 — HRAdmin unlocks the account (POST /api/v1/auth/accounts/{id}/unlock)
        var unlockResult = await accountSvc.UnlockAccountAsync(user.Id);
        Assert.True(unlockResult.IsSuccess, "UnlockAccountAsync should succeed");

        // Assert — DB confirms lockout cleared
        _context.ChangeTracker.Clear();
        var dbUser = await _context.Users.FindAsync(user.Id);
        Assert.NotNull(dbUser);
        Assert.Null(dbUser!.LockoutUntil);
        Assert.Equal(0, dbUser.FailedLoginCount);

        // Act 3 — login with correct password now succeeds (POST /api/v1/auth/login)
        var loginResult = await authSvc.LoginAsync(new LoginRequestDto
        {
            Email = email,
            Password = password,
        });

        Assert.True(loginResult.IsSuccess, "Login after unlock must succeed");
        Assert.NotNull(loginResult.Value);
        Assert.NotEmpty(loginResult.Value!.AccessToken);
        Assert.NotEmpty(loginResult.Value.RefreshToken);
    }
}
