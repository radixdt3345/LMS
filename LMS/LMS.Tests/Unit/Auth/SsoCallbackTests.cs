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

namespace LMS.Tests.Unit.Auth;

/// <summary>
/// UT-6: Unit tests for AuthService.SsoCallbackAsync — Azure AD SSO callback logic.
/// MSAL token exchange is mocked via IMsalAuthProvider so no real Azure AD calls are made.
/// </summary>
[Trait("Category", "Unit")]
public class SsoCallbackTests
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

    private static AuthService BuildService(
        LmsDbContext db,
        IMsalAuthProvider msalProvider,
        ITokenService? tokenSvc = null)
    {
        tokenSvc ??= new Mock<ITokenService>().Object;
        return new AuthService(db, tokenSvc, DefaultJwtOptions(), msalProvider);
    }

    private static Mock<ITokenService> DefaultTokenMock()
    {
        var mock = new Mock<ITokenService>();
        mock.Setup(t => t.IssueAccessToken(It.IsAny<User>())).Returns("access.token");
        mock.Setup(t => t.IssueRefreshTokenAsync(It.IsAny<Guid>(), default)).ReturnsAsync("refresh.token");
        return mock;
    }

    // ── UT-6a: MSAL exchange throws → 400 returned ───────────────────────────

    [Fact]
    public async Task SsoCallbackAsync_MsalFailure_Returns400()
    {
        await using var db = CreateInMemoryDb();

        var msalProvider = new Mock<IMsalAuthProvider>();
        msalProvider
            .Setup(m => m.ExchangeCodeAsync(It.IsAny<string>(), default))
            .ThrowsAsync(new Exception("MSAL: invalid_grant"));

        var svc = BuildService(db, msalProvider.Object);
        var result = await svc.SsoCallbackAsync("bad-code");

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
    }

    // ── UT-6b: brand-new OID + email → auto-provisioned as Employee ──────────

    [Fact]
    public async Task SsoCallbackAsync_NewUser_AutoProvisionedAsEmployee()
    {
        await using var db = CreateInMemoryDb();

        var msalProvider = new Mock<IMsalAuthProvider>();
        msalProvider
            .Setup(m => m.ExchangeCodeAsync(It.IsAny<string>(), default))
            .ReturnsAsync(("brand-new-oid", "newuser@company.com"));

        var svc = BuildService(db, msalProvider.Object, DefaultTokenMock().Object);
        var result = await svc.SsoCallbackAsync("valid-code");

        Assert.True(result.IsSuccess);
        Assert.Equal("access.token", result.Value!.AccessToken);

        var createdUser = await db.Users.FirstOrDefaultAsync(u => u.AzureAdOid == "brand-new-oid");
        Assert.NotNull(createdUser);
        Assert.Equal(UserRole.Employee, createdUser!.Role);
        Assert.Equal("newuser@company.com", createdUser.Email);
        Assert.True(createdUser.IsActive);
    }

    // ── UT-6c: existing user found by OID → tokens issued without DB write ───

    [Fact]
    public async Task SsoCallbackAsync_ExistingUserByOid_IssuesTokens()
    {
        await using var db = CreateInMemoryDb();

        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "manager@company.com",
            AzureAdOid = "known-oid",
            Role = UserRole.Manager,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Users.Add(existingUser);
        await db.SaveChangesAsync();

        var msalProvider = new Mock<IMsalAuthProvider>();
        msalProvider
            .Setup(m => m.ExchangeCodeAsync(It.IsAny<string>(), default))
            .ReturnsAsync(("known-oid", "manager@company.com"));

        var svc = BuildService(db, msalProvider.Object, DefaultTokenMock().Object);
        var result = await svc.SsoCallbackAsync("valid-code");

        Assert.True(result.IsSuccess);
        Assert.Equal("access.token", result.Value!.AccessToken);
    }

    // ── UT-6d: no OID match but email match → OID linked to existing account ─

    [Fact]
    public async Task SsoCallbackAsync_EmailFallback_LinksOidToLocalAccount()
    {
        await using var db = CreateInMemoryDb();

        var localUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "localonly@company.com",
            PasswordHash = "hashed",
            AzureAdOid = null, // not yet linked
            Role = UserRole.Employee,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Users.Add(localUser);
        await db.SaveChangesAsync();

        var msalProvider = new Mock<IMsalAuthProvider>();
        msalProvider
            .Setup(m => m.ExchangeCodeAsync(It.IsAny<string>(), default))
            .ReturnsAsync(("fresh-oid-for-local", "localonly@company.com"));

        var svc = BuildService(db, msalProvider.Object, DefaultTokenMock().Object);
        var result = await svc.SsoCallbackAsync("valid-code");

        Assert.True(result.IsSuccess);

        var updatedUser = await db.Users.FindAsync(localUser.Id);
        Assert.Equal("fresh-oid-for-local", updatedUser!.AzureAdOid);
    }

    // ── UT-6e: inactive user returned by SSO → 403 ──────────────────────────

    [Fact]
    public async Task SsoCallbackAsync_InactiveUser_Returns403()
    {
        await using var db = CreateInMemoryDb();

        var inactiveUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "inactive@company.com",
            AzureAdOid = "inactive-oid",
            Role = UserRole.Employee,
            IsActive = false, // deactivated
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Users.Add(inactiveUser);
        await db.SaveChangesAsync();

        var msalProvider = new Mock<IMsalAuthProvider>();
        msalProvider
            .Setup(m => m.ExchangeCodeAsync(It.IsAny<string>(), default))
            .ReturnsAsync(("inactive-oid", "inactive@company.com"));

        var svc = BuildService(db, msalProvider.Object);
        var result = await svc.SsoCallbackAsync("valid-code");

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.StatusCode);
    }
}
