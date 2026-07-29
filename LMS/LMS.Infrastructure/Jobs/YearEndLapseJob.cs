using LMS.Application.Interfaces;

namespace LMS.Infrastructure.Jobs;

/// <summary>
/// Hangfire recurring job: lapses (zeroes) all Annual and OneTime leave balances
/// for the current year at year-end. No carry-forward per org policy POL-06.
/// Cron: <c>0 18 31 12 *</c> — 31 Dec 18:00 UTC = 31 Dec 23:30 IST.
/// Runs 30 minutes BEFORE <see cref="NewYearCreditJob"/> to ensure clean slate before crediting.
/// Registered in Program.cs via <c>RecurringJob.AddOrUpdate</c>.
/// </summary>
public class YearEndLapseJob
{
    private readonly ILeaveBalanceService _balanceService;

    public YearEndLapseJob(ILeaveBalanceService balanceService)
    {
        _balanceService = balanceService;
    }

    /// <summary>
    /// Zeroes all Annual and OneTime leave balances for the current calendar year.
    /// </summary>
    public async Task Execute()
    {
        var currentYear = DateTime.UtcNow.Year;
        await _balanceService.YearEndLapse(currentYear);
    }
}