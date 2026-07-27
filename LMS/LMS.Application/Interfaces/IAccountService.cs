using LMS.Application.DTOs.Auth;
using LMS.Domain.Common;

namespace LMS.Application.Interfaces;

/// <summary>
/// Account management operations: list accounts with lockout status, unlock a user.
/// Intended for HRAdmin and SuperAdmin callers only — enforced at the controller level.
/// FR-10, FR-11.
/// </summary>
public interface IAccountService
{
    /// <summary>
    /// Returns a paginated list of all user accounts with their current lockout status.
    /// page is 1-based; limit is clamped to 1–100. FR-10.
    /// </summary>
    Task<Result<PagedResult<AccountDto>>> GetAccountsAsync(
        int page, int limit, CancellationToken ct = default);

    /// <summary>
    /// Clears FailedLoginCount and LockoutUntil for the specified user. FR-11.
    /// Returns 404 if the user does not exist.
    /// </summary>
    Task<Result<bool>> UnlockAccountAsync(Guid userId, CancellationToken ct = default);
}
