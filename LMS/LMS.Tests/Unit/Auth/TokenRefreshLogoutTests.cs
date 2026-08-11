using System.Security.Cryptography;
using System.Text;
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

[Trait("Category", "Unit")]
public class TokenRefreshLogoutTests
{
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

    private static AuthService BuildService(LmsDbContext db, ITokenService tokenSvc)
    {
        var msalProvider = new Mock<IMsalAuthProvider>().Object;
        return new AuthService(db, tokenSvc, DefaultJwtOptions(), msalProvider);
    }

    private static string HashToken(string raw) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    private static User MakeActiveUser() => new()
    {
        Id = Guid.NewGuid(),
        Email = "user@example.com",
        Role = UserRole.Employee,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private static RefreshToken MakeValidToken(Guid userId, string rawToken) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        TokenHash = HashToken(rawToken),
        ExpiresAt = DateTime.UtcNow.AddDays(7),
        CreatedAt = DateTime.UtcNow,
    };

    [Fact]
    public async Task RefreshTokenAsync_ValidToken_RotatesToken()
    {
        await using var db = CreateInMemoryDb();

        const string rawToken = "raw-refresh-token-abc";
        var user = MakeActiveUser();
        var storedToken = MakeValidToken(user.Id, rawToken);

        db.Users.Add(user);
        db.RefreshTokens.Add(storedToken);
        await db.SaveChangesAsync();

        var tokenSvc = new Mock<ITokenService>();
        tokenSvc.Setup(t => t.IssueAccessToken(It.IsAny<User>())).Returns("new.access.token");
        tokenSvc.Setup(t => t.IssueRefreshTokenAsync(It.IsAny<Guid>(), default))
                .ReturnsAsync("new-refresh-token");

        var svc = BuildService(db, tokenSvc.Object);
        var result = await svc.RefreshTokenAsync(rawToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("new.access.token", result.Value!.AccessToken);
        Assert.Equal("new-refresh-token", result.Value.RefreshToken);

        var oldToken = await db.RefreshTokens.FindAsync(storedToken.Id);
        Assert.NotNull(oldToken!.RevokedAt);
    }

    [Fact]
    public async Task RefreshTokenAsync_ExpiredToken_Returns401()
    {
        await using var db = CreateInMemoryDb();

        const string rawToken = "expired-token";
        var user = MakeActiveUser();
        var expiredToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashToken(rawToken),
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow.AddDays(-8),
        };

        db.Users.Add(user);
        db.RefreshTokens.Add(expiredToken);
        await db.SaveChangesAsync();

        var tokenSvc = new Mock<ITokenService>();
        var svc = BuildService(db, tokenSvc.Object);
        var result = await svc.RefreshTokenAsync(rawToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task RefreshTokenAsync_RevokedToken_Returns401()
    {
        await using var db = CreateInMemoryDb();

        const string rawToken = "revoked-token";
        var user = MakeActiveUser();
        var revokedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashToken(rawToken),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            RevokedAt = DateTime.UtcNow.AddHours(-1),
            CreatedAt = DateTime.UtcNow,
        };

        db.Users.Add(user);
        db.RefreshTokens.Add(revokedToken);
        await db.SaveChangesAsync();

        var tokenSvc = new Mock<ITokenService>();
        var svc = BuildService(db, tokenSvc.Object);
        var result = await svc.RefreshTokenAsync(rawToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task LogoutAsync_ValidToken_SetsRevokedAt()
    {
        await using var db = CreateInMemoryDb();

        const string rawToken = "logout-token";
        var user = MakeActiveUser();
        var activeToken = MakeValidToken(user.Id, rawToken);

        db.Users.Add(user);
        db.RefreshTokens.Add(activeToken);
        await db.SaveChangesAsync();

        var tokenSvc = new Mock<ITokenService>();
        var svc = BuildService(db, tokenSvc.Object);
        var result = await svc.LogoutAsync(rawToken);

        Assert.True(result.IsSuccess);

        var updatedToken = await db.RefreshTokens.FindAsync(activeToken.Id);
        Assert.NotNull(updatedToken!.RevokedAt);
        Assert.True(updatedToken.RevokedAt <= DateTime.UtcNow);
    }

    [Fact]
    public async Task LogoutAsync_UnknownToken_Returns400()
    {
        await using var db = CreateInMemoryDb();

        var tokenSvc = new Mock<ITokenService>();
        var svc = BuildService(db, tokenSvc.Object);
        var result = await svc.LogoutAsync("completely-unknown-token");

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
    }
}
