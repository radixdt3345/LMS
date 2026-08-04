using LMS.Application.DTOs.Reporting;
using LMS.Application.Interfaces;
using LMS.Domain.Common;
using LMS.Domain.Enums;
using LMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LMS.Infrastructure.Services;

/// <summary>
/// Read-only reporting service. No audit calls (reads only — no state mutations).
/// All data fetched via EF Core LINQ — no raw SQL.
/// </summary>
public class ReportService : IReportService
{
    private readonly LmsDbContext _db;

    public ReportService(LmsDbContext db) => _db = db;

    // ── Utilization ─────────────────────────────────────────────────────────

    public async Task<Result<UtilizationReportDto>> GetUtilizationAsync(
        int year, Guid? departmentId = null)
    {
        // Two-query approach: project leave+user data first, then look up dept names.
        // Avoids nullable Guid join issues on both InMemory and PostgreSQL providers.
        var requestData = await (
            from lr in _db.LeaveRequests
            join u in _db.Users on lr.EmployeeId equals u.Id
            where lr.StartDate.Year == year
                && lr.Status == LeaveRequestStatus.Approved
                && u.DepartmentId.HasValue
            select new { lr.EmployeeId, lr.ComputedDays, DeptId = u.DepartmentId!.Value }
        ).ToListAsync();

        if (departmentId.HasValue)
            requestData = requestData
                .Where(x => x.DeptId == departmentId.Value)
                .ToList();

        var deptIds = requestData.Select(x => x.DeptId).Distinct().ToList();
        var deptNames = await _db.Departments
            .Where(d => deptIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Name);

        var rows = requestData
            .GroupBy(x => x.DeptId)
            .Select(g =>
            {
                var name     = deptNames.TryGetValue(g.Key, out var n) ? n : string.Empty;
                var total    = g.Sum(x => x.ComputedDays);
                var empCount = g.Select(x => x.EmployeeId).Distinct().Count();
                var avg      = empCount > 0 ? Math.Round(total / empCount, 2) : 0m;
                return new DeptUtilizationRow(name, empCount, total, avg);
            })
            .ToList();

        return Result<UtilizationReportDto>.Success(
            new UtilizationReportDto(year, departmentId, rows));
    }

    // ── Trends ──────────────────────────────────────────────────────────────

    public async Task<Result<TrendsReportDto>> GetTrendsAsync(int months)
    {
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-months);

        var grouped = await _db.LeaveRequests
            .Where(lr => lr.StartDate >= cutoff)
            .GroupBy(lr => new { lr.StartDate.Year, lr.StartDate.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                TotalRequests = g.Count(),
                ApprovedCount = g.Count(lr => lr.Status == LeaveRequestStatus.Approved),
                RejectedCount = g.Count(lr => lr.Status == LeaveRequestStatus.Rejected)
            })
            .OrderBy(g => g.Year).ThenBy(g => g.Month)
            .ToListAsync();

        var rows = grouped
            .Select(g => new MonthTrendRow(
                $"{g.Year}-{g.Month:D2}",
                g.TotalRequests,
                g.ApprovedCount,
                g.RejectedCount))
            .ToList();

        return Result<TrendsReportDto>.Success(new TrendsReportDto(months, rows));
    }

    // ── Compliance ──────────────────────────────────────────────────────────

    public async Task<Result<ComplianceReportDto>> GetComplianceAsync()
    {
        var totalEmployees = await _db.Users.CountAsync(u => u.IsActive);

        var withRequests = await _db.LeaveRequests
            .Select(lr => lr.EmployeeId)
            .Distinct()
            .CountAsync();

        var rate = totalEmployees > 0
            ? Math.Round(withRequests * 100m / totalEmployees, 2)
            : 0m;

        return Result<ComplianceReportDto>.Success(
            new ComplianceReportDto(rate, totalEmployees, withRequests));
    }

    // ── CSV Export (streaming via IAsyncEnumerable + yield) ─────────────────

    public async IAsyncEnumerable<string> ExportCsvAsync(string type)
    {
        if (type.Equals("utilization", StringComparison.OrdinalIgnoreCase))
        {
            yield return "Department,TotalEmployees,TotalLeaveDays,AvgLeaveDaysPerEmployee";

            var year = DateTime.UtcNow.Year;
            var data = await (
                from lr in _db.LeaveRequests
                join u in _db.Users on lr.EmployeeId equals u.Id
                where lr.StartDate.Year == year
                    && lr.Status == LeaveRequestStatus.Approved
                    && u.DepartmentId.HasValue
                select new { lr.EmployeeId, lr.ComputedDays, DeptId = u.DepartmentId!.Value }
            ).ToListAsync();

            var deptIds   = data.Select(x => x.DeptId).Distinct().ToList();
            var deptNames = await _db.Departments
                .Where(d => deptIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, d => d.Name);

            foreach (var g in data.GroupBy(x => x.DeptId))
            {
                var name     = deptNames.TryGetValue(g.Key, out var n) ? n : string.Empty;
                var total    = g.Sum(x => x.ComputedDays);
                var empCount = g.Select(x => x.EmployeeId).Distinct().Count();
                var avg      = empCount > 0 ? Math.Round(total / empCount, 2) : 0m;
                yield return $"{name},{empCount},{total},{avg}";
            }
        }
        else if (type.Equals("trends", StringComparison.OrdinalIgnoreCase))
        {
            yield return "YearMonth,TotalRequests,ApprovedCount,RejectedCount";

            var cutoff = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-6);
            var data   = await _db.LeaveRequests
                .Where(lr => lr.StartDate >= cutoff)
                .GroupBy(lr => new { lr.StartDate.Year, lr.StartDate.Month })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    TotalRequests = g.Count(),
                    ApprovedCount = g.Count(lr => lr.Status == LeaveRequestStatus.Approved),
                    RejectedCount = g.Count(lr => lr.Status == LeaveRequestStatus.Rejected)
                })
                .OrderBy(g => g.Year).ThenBy(g => g.Month)
                .ToListAsync();

            foreach (var r in data)
                yield return $"{r.Year}-{r.Month:D2},{r.TotalRequests},{r.ApprovedCount},{r.RejectedCount}";
        }
    }

    // ── Dashboards ──────────────────────────────────────────────────────────

    public async Task<Result<EmployeeDashboardDto>> GetEmployeeDashboardAsync(Guid employeeId)
    {
        var year = (short)DateTime.UtcNow.Year;

        var balances = await (
            from lb in _db.LeaveBalances
            join lt in _db.LeaveTypes on lb.LeaveTypeId equals lt.Id
            where lb.UserId == employeeId && lb.Year == year
            select new LeaveBalanceSummary(
                lt.Name, lb.AllocatedDays, lb.UsedDays, lb.AllocatedDays - lb.UsedDays)
        ).ToListAsync();

        var recentRequests = await (
            from lr in _db.LeaveRequests
            join lt in _db.LeaveTypes on lr.LeaveTypeId equals lt.Id
            where lr.EmployeeId == employeeId
            orderby lr.CreatedAt descending
            select new RecentLeaveRequestSummary(
                lr.Id, lt.Name, lr.StartDate, lr.EndDate, lr.Status.ToString())
        ).Take(10).ToListAsync();

        var pendingCount = await _db.LeaveRequests
            .CountAsync(lr => lr.EmployeeId == employeeId
                && lr.Status == LeaveRequestStatus.Pending);

        return Result<EmployeeDashboardDto>.Success(
            new EmployeeDashboardDto(balances, recentRequests, pendingCount));
    }

    public async Task<Result<ManagerDashboardDto>> GetManagerDashboardAsync(Guid managerId)
    {
        var reportIds = await _db.Users
            .Where(u => u.ManagerId == managerId && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync();

        var teamPending = await (
            from lr in _db.LeaveRequests
            join lt in _db.LeaveTypes on lr.LeaveTypeId equals lt.Id
            where reportIds.Contains(lr.EmployeeId)
                && lr.Status == LeaveRequestStatus.Pending
            orderby lr.StartDate
            select new RecentLeaveRequestSummary(
                lr.Id, lt.Name, lr.StartDate, lr.EndDate, lr.Status.ToString())
        ).ToListAsync();

        return Result<ManagerDashboardDto>.Success(
            new ManagerDashboardDto(teamPending, reportIds.Count));
    }

    public async Task<Result<HrDashboardDto>> GetHrDashboardAsync()
    {
        var pendingApprovals = await _db.LeaveRequests
            .CountAsync(lr => lr.Status == LeaveRequestStatus.Pending);

        var totalEmployees = await _db.Users.CountAsync(u => u.IsActive);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var activeLeaveToday = await _db.LeaveRequests
            .CountAsync(lr => lr.Status == LeaveRequestStatus.Approved
                && lr.StartDate <= today && lr.EndDate >= today);

        var recentActivity = await (
            from lr in _db.LeaveRequests
            join lt in _db.LeaveTypes on lr.LeaveTypeId equals lt.Id
            orderby lr.UpdatedAt descending
            select new RecentLeaveRequestSummary(
                lr.Id, lt.Name, lr.StartDate, lr.EndDate, lr.Status.ToString())
        ).Take(20).ToListAsync();

        return Result<HrDashboardDto>.Success(
            new HrDashboardDto(pendingApprovals, totalEmployees, activeLeaveToday, recentActivity));
    }

    public async Task<Result<SuperAdminDashboardDto>> GetSuperAdminDashboardAsync()
    {
        var totalEmployees   = await _db.Users.CountAsync(u => u.IsActive);
        var totalDepartments = await _db.Departments.CountAsync(d => d.IsActive);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var activeLeaveToday = await _db.LeaveRequests
            .CountAsync(lr => lr.Status == LeaveRequestStatus.Approved
                && lr.StartDate <= today && lr.EndDate >= today);

        var pendingApprovals = await _db.LeaveRequests
            .CountAsync(lr => lr.Status == LeaveRequestStatus.Pending);

        var utilization = totalEmployees > 0
            ? Math.Round(activeLeaveToday * 100m / totalEmployees, 2)
            : 0m;

        return Result<SuperAdminDashboardDto>.Success(
            new SuperAdminDashboardDto(
                totalEmployees, totalDepartments, activeLeaveToday,
                pendingApprovals, utilization));
    }
}
