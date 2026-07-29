using LMS.Application.Interfaces;

namespace LMS.Infrastructure.Jobs;

/// <summary>
/// Hangfire recurring job: credits the next calendar year's leave balances for all active employees.
/// Cron: <c>30 18 31 12 *</c> — 31 Dec 18:30 UTC = 01 Jan 00:00 IST.
/// Registered in Program.cs via <c>RecurringJob.AddOrUpdate</c>.
/// </summary>
public class NewYearCreditJob
{
    private readonly ILeaveBalanceService _balanceService;

    public NewYearCreditJob(ILeaveBalanceService balanceService)
    {
        _balanceService = balanceService;
    }

    /// <summary>
    /// Credits all active employees for the incoming new year
    /// (DateTime.UtcNow.Year + 1, since the job fires on 31 Dec UTC).
    /// </summary>
    public async Task Execute()
    {
        var nextYear = DateTime.UtcNow.Year + 1;
        await _balanceService.CreditAnnual(nextYear);
    }
}