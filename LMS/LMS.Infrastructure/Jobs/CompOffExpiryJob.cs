using LMS.Application.Interfaces;

namespace LMS.Infrastructure.Jobs;

/// <summary>
/// Hangfire recurring job: marks expired comp-off credits as fully consumed.
/// A credit is expired when <c>expires_at &lt;= today</c> and <c>used_days &lt; credit_days</c>.
/// Cron: <c>30 18 * * *</c> — daily 18:30 UTC = 00:00 IST.
/// Registered in Program.cs via <c>RecurringJob.AddOrUpdate</c>.
/// </summary>
public class CompOffExpiryJob
{
    private readonly ILeaveBalanceService _balanceService;

    public CompOffExpiryJob(ILeaveBalanceService balanceService)
    {
        _balanceService = balanceService;
    }

    /// <summary>
    /// Expires all comp-off credits whose expiry date has passed and still
    /// have unredeemed days (sets <c>used_days = credit_days</c>).
    /// </summary>
    public async Task Execute()
    {
        await _balanceService.ExpireCompOffCredits();
    }
}