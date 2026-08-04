using System.Security.Cryptography;
using System.Text;
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

namespace LMS.Tests.Integration.Auth;

/// <summary>
/// Integration tests IT-1 through IT-6 for the AUTH domain.
/// Covers DB schema accessibility, local login happy path, SSO upsert idempotency,
/// token refresh rotation, logout revocation, and account lockout enforcement.
///
/// Each test gets a uniquely-named EF Core InMemory database — no shared mutable
/// state between tests. AuthService and TokenService share the same LmsDbContext
/// instance so token writes made by TokenService are visible to AuthService queries.
///
/// IMsalAuthProvider is mocked via Moq; no real Azure AD network calls are made.
/// </summary>
[Trait("Category", "Integration")]
public class AuthIntegrationTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Returns DbContextOptions backed by a uniquely-named InMemory store.</summary>
    private static DbContextOptions<LmsDbContext> InMemoryOptions() =>
        new DbContextOptionsBuilder<LmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private static IOptions<JwtSettings> DefaultJwtOptions() =>
        Options.Create(new JwtSettings
        {
            SecretKey                = "test-secret-key-min-32-chars-long!!",
            Issuer                   = "lms-test",
            Audience                 = "lms-client",
            AccessTokenExpiryMinutes = 15,
            RefreshTokenExpiryDays   = 7,
        });

    /// <summary>
    /// Builds a wired-up AuthService + real TokenService sharing one InMemory LmsDbContext.
    /// Returns the shared db context and MSAL mock so tests can seed data and configure SSO.
    /// </summary>
    private static (AuthService auth, LmsDbContext db, Mock<IMsalAuthProvider> msalMock)
        BuildServices(DbContextOptions<LmsDbContext> options)
    {
        var db       = new LmsDbContext(options);
        var jwtOpts  = DefaultJwtOptions();

        // TokenService shares the exact same db instance as AuthService, so
        // refresh-token rows written by TokenService are immediately queryable
        // by AuthService within the same InMemory store.
        var tokenSvc = new TokenService(jwtOpts, db);
        var msalMock = new Mock<IMsalAuthProvider>();

        var auth = new AuthService(db, tokenSvc, jwtOpts, msalMock.Object);
        return (auth, db, msalMock);
    }

    private static User MakeActiveUser(string email, string password) => new()
    {
        Id               = Guid.NewGuid(),
        Email            = email,
        PasswordHash     = BC.HashPassword(password),
        Role             = UserRole.Employee,
        IsActive         = true,
        FailedLoginCount = 0,
        CreatedAt        = DateTime.UtcNow,
        UpdatedAt        = DateTime.UtcNow,
    };

    /// <summary>
    /// SHA-256 hash that mirrors AuthService.HashToken — used to look up token
    /// rows by raw token value in DB assertions.
    /// </summary>
    private static string HashToken(string raw) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    // ── IT-1 — DB schema: auth tables exist ─────────────────────────────────

    /// <summary>
    /// IT-1: EF Core InMemory DbContext exposes all three auth-related DbSets.
    /// Verifies that Users, RefreshTokens, and AuditLogs are registered in
    /// LmsDbContext so the auth domain can read and write its tables.
    /// </summary>
    [Fact]
    public async Task IT1_AuthTables_ExistInDbContext()
    {
        // Arrange
        var options = InMemoryOptions();
        await using var db = new LmsDbContext(options);

        // Act — EnsureCreated materialises the InMemory schema
        await db.Database.EnsureCreatedAsync();

        // Assert — all three auth-related DbSets are accessible
        Assert.NotNull(db.Users);
        Assert.NotNull(db.RefreshTokens);
        Assert.NotNull(db.AuditLogs);
    }

    // ── IT-2 — Login happy path ──────────────────────────────────────────────

    /// <summary>
    /// IT-2: LoginAsync with valid credentials returns a non-empty AccessToken and
    /// RefreshToken, and persists a RefreshToken row for the user in the database.
    /// </summary>
    [Fact]
    public async Task IT2_LoginAsync_ValidCredentials_ReturnsTokens_AndPersistsRefreshTokenRow()
    {
        // Arrange
        var options = InMemoryOptions();
        var (auth, db, _) = BuildServices(options);

        const string password = "ValidPass1!";
        var user = MakeActiveUser($"it2-{Guid.NewGuid():N}@example.com", password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        // Act
        var result = await auth.LoginAsync(new LoginRequestDto
        {
            Email    = user.Email,
            Password = password,
        });

        // Assert — service result is success with non-empty tokens
        Assert.True(result.IsSuccess, $"Expected success but got: {result.Error}");
        Assert.NotNull(result.Value);
        Assert.NotEmpty(result.Value!.AccessToken);
        Assert.NotEmpty(result.Value.RefreshToken);

        // Assert — a RefreshToken row was persisted for this user
        db.ChangeTracker.Clear();
        var tokenRow = await db.RefreshTokens
            .FirstOrDefaultAsync(t => t.UserId == user.Id);
        Assert.NotNull(tokenRow);
        Assert.Null(tokenRow!.RevokedAt);                      // not revoked
        Assert.True(tokenRow.ExpiresAt > DateTime.UtcNow);    // expires in the future
    }

    // ── IT-3 — SSO upsert idempotency ────────────────────────────────────────

    /// <summary>
    /// IT-3: SsoCallbackAsync with a new Azure AD OID auto-provisions an Employee;
    /// calling again with the same OID returns success without creating a duplicate
    /// — the user count stays at exactly 1.
    /// </summary>
    [Fact]
    public async Task IT3_SsoCallbackAsync_NewOid_CreatesUser_SecondCallDoesNotDuplicate()
    {
        // Arrange
        var options = InMemoryOptions();
        var (auth, db, msalMock) = BuildServices(options);

        const string oid   = "azure-oid-abc123";
        const string email = "sso-user@example.com";
        const string code  = "auth-code-xyz";

        // Mock MSAL: exchange code → (OID, email) — no real Azure AD call
        msalMock
            .Setup(m => m.ExchangeCodeAsync(code, It.IsAny<CancellationToken>()))
            .ReturnsAsync((oid, email));

        // Act 1 — first SSO callback: user does not exist → auto-provisioned
        var firstResult = await auth.SsoCallbackAsync(code);

        // Assert 1 — success; one user with correct OID and role
        Assert.True(firstResult.IsSuccess, $"First SSO call failed: {firstResult.Error}");
        db.ChangeTracker.Clear();

        var countAfterFirst = await db.Users.CountAsync(u => u.Email == email);
        Assert.Equal(1, countAfterFirst);

        var createdUser = await db.Users.FirstAsync(u => u.Email == email);
        Assert.Equal(oid,              createdUser.AzureAdOid);
        Assert.Equal(UserRole.Employee, createdUser.Role);
        Assert.True(createdUser.IsActive);

        // Act 2 — second SSO callback with same OID: should update, not insert
        var secondResult = await auth.SsoCallbackAsync(code);

        // Assert 2 — still exactly one user row, no duplicate
        Assert.True(secondResult.IsSuccess, $"Second SSO call failed: {secondResult.Error}");
        db.ChangeTracker.Clear();

        var countAfterSecond = await db.Users.CountAsync(u => u.Email == email);
        Assert.Equal(1, countAfterSecond);
    }

    // ── IT-4 — Token refresh rotation ────────────────────────────────────────

    /// <summary>
    /// IT-4: RefreshTokenAsync with a valid token issues a new access + refresh token pair.
    /// The old refresh token row is marked revoked (RevokedAt set); exactly one active
    /// (non-revoked) RefreshToken row remains for the user.
    /// </summary>
    [Fact]
    public async Task IT4_RefreshTokenAsync_ValidToken_RotatesToken_OldRevokedNewActive()
    {
        // Arrange — seed user and login to obtain a real raw refresh token
        var options = InMemoryOptions();
        var (auth, db, _) = BuildServices(options);

        const string password = "ValidPass1!";
        var user = MakeActiveUser($"it4-{Guid.NewGuid():N}@example.com", password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var loginResult = await auth.LoginAsync(new LoginRequestDto
        {
            Email    = user.Email,
            Password = password,
        });
        Assert.True(loginResult.IsSuccess, "Login must succeed before testing refresh");
        var rawRefreshToken = loginResult.Value!.RefreshToken;

        // Act — exchange the old refresh token for a new pair
        var refreshResult = await auth.RefreshTokenAsync(rawRefreshToken);

        // Assert — service returns a new token pair different from the original
        Assert.True(refreshResult.IsSuccess, $"RefreshTokenAsync failed: {refreshResult.Error}");
        Assert.NotNull(refreshResult.Value);
        Assert.NotEmpty(refreshResult.Value!.AccessToken);
        Assert.NotEmpty(refreshResult.Value.RefreshToken);
        Assert.NotEqual(rawRefreshToken, refreshResult.Value.RefreshToken);

        // Assert — old refresh token row is now revoked
        db.ChangeTracker.Clear();
        var oldHash  = HashToken(rawRefreshToken);
        var oldToken = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == oldHash);
        Assert.NotNull(oldToken);
        Assert.NotNull(oldToken!.RevokedAt);

        // Assert — exactly one active (non-revoked) RefreshToken row remains for this user
        var activeTokens = await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .ToListAsync();
        Assert.Single(activeTokens);
    }

    // ── IT-5 — Logout revokes refresh token ──────────────────────────────────

    /// <summary>
    /// IT-5: LogoutAsync sets RevokedAt on the refresh token row.
    /// A subsequent call to RefreshTokenAsync with the same (now-revoked) token
    /// returns a failure result with HTTP 401.
    /// </summary>
    [Fact]
    public async Task IT5_LogoutAsync_ValidToken_RevokesRow_SubsequentRefreshReturns401()
    {
        // Arrange — seed user and login
        var options = InMemoryOptions();
        var (auth, db, _) = BuildServices(options);

        const string password = "ValidPass1!";
        var user = MakeActiveUser($"it5-{Guid.NewGuid():N}@example.com", password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var loginResult = await auth.LoginAsync(new LoginRequestDto
        {
            Email    = user.Email,
            Password = password,
        });
        Assert.True(loginResult.IsSuccess, "Login must succeed before testing logout");
        var rawRefreshToken = loginResult.Value!.RefreshToken;

        // Act — logout (revoke token)
        var logoutResult = await auth.LogoutAsync(rawRefreshToken);

        // Assert — logout succeeded
        Assert.True(logoutResult.IsSuccess, $"LogoutAsync failed: {logoutResult.Error}");

        // Assert — token row now has RevokedAt set in the database
        db.ChangeTracker.Clear();
        var hash  = HashToken(rawRefreshToken);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);
        Assert.NotNull(token);
        Assert.NotNull(token!.RevokedAt);

        // Assert — using the revoked token in RefreshTokenAsync returns 401
        var refreshAttempt = await auth.RefreshTokenAsync(rawRefreshToken);
        Assert.False(refreshAttempt.IsSuccess);
        Assert.Equal(401, refreshAttempt.StatusCode);
    }

    // ── IT-6 — Rate limiting / lockout ───────────────────────────────────────

    /// <summary>
    /// IT-6: Five consecutive wrong-password attempts trigger account lockout.
    /// On the 5th failure FailedLoginCount reaches MaxFailedAttempts (5) and
    /// LockoutUntil is stamped to a future UTC timestamp (returns 423).
    /// A 6th attempt hits the lockout-check path at the top of LoginAsync and
    /// also returns 423 — even with a correct password.
    /// </summary>
    [Fact]
    public async Task IT6_LoginAsync_FiveWrongPasswords_LocksAccount_SixthAttemptReturns423()
    {
        // Arrange
        var options = InMemoryOptions();
        var (auth, db, _) = BuildServices(options);

        const string correctPassword = "CorrectPass1!";
        var user = MakeActiveUser($"it6-{Guid.NewGuid():N}@example.com", correctPassword);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        // Act — 5 consecutive wrong-password attempts
        //   Attempts 1–4 → 401 (FailedLoginCount < 5)
        //   Attempt 5   → 423 (FailedLoginCount reaches 5, LockoutUntil stamped)
        for (var i = 0; i < 5; i++)
        {
            await auth.LoginAsync(new LoginRequestDto
            {
                Email    = user.Email,
                Password = "WrongPassword!",
            });
        }

        // Assert — DB confirms lockout was applied after 5 failures
        db.ChangeTracker.Clear();
        var dbUser = await db.Users.FindAsync(user.Id);
        Assert.NotNull(dbUser);
        Assert.NotNull(dbUser!.LockoutUntil);
        Assert.True(
            dbUser.LockoutUntil > DateTime.UtcNow,
            $"LockoutUntil ({dbUser.LockoutUntil:O}) must be a future UTC timestamp");
        Assert.True(
            dbUser.FailedLoginCount >= 5,
            $"FailedLoginCount should be >= 5, was {dbUser.FailedLoginCount}");

        // Act — 6th attempt hits the lockout-check (even with correct password)
        var sixthResult = await auth.LoginAsync(new LoginRequestDto
        {
            Email    = user.Email,
            Password = correctPassword,
        });

        // Assert — 6th attempt is blocked with HTTP 423
        Assert.False(sixthResult.IsSuccess);
        Assert.Equal(423, sixthResult.StatusCode);
    }
}
