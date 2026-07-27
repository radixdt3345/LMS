using LMS.Domain.Entities;

namespace LMS.Application.Interfaces;

/// <summary>Issues JWT access tokens and manages refresh token lifecycle.</summary>
public interface ITokenService
{
    string IssueAccessToken(User user);
    Task<string> IssueRefreshTokenAsync(Guid userId, CancellationToken ct = default);
}
