namespace LMS.Application.DTOs.Reporting;

public record DeptUtilizationRow(
    string DeptName,
    int TotalEmployees,
    decimal TotalLeaveDays,
    decimal AvgLeaveDaysPerEmployee);

public record UtilizationReportDto(
    int Year,
    Guid? DepartmentId,
    List<DeptUtilizationRow> Rows);

public record MonthTrendRow(
    string YearMonth,
    int TotalRequests,
    int ApprovedCount,
    int RejectedCount);

public record TrendsReportDto(int Months, List<MonthTrendRow> Rows);

public record ComplianceReportDto(
    decimal SubmissionRatePercent,
    int TotalEmployees,
    int EmployeesWithAtLeastOneRequest);

public record LeaveBalanceSummary(
    string LeaveTypeName,
    decimal Allocated,
    decimal Used,
    decimal Available);

public record RecentLeaveRequestSummary(
    Guid Id,
    string LeaveTypeName,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status);

public record EmployeeDashboardDto(
    List<LeaveBalanceSummary> Balances,
    List<RecentLeaveRequestSummary> RecentRequests,
    int PendingCount);

public record ManagerDashboardDto(
    List<RecentLeaveRequestSummary> TeamPendingRequests,
    int TeamSize);

public record HrDashboardDto(
    int PendingApprovals,
    int TotalEmployees,
    int ActiveLeaveToday,
    List<RecentLeaveRequestSummary> RecentActivity);

public record SuperAdminDashboardDto(
    int TotalEmployees,
    int TotalDepartments,
    int ActiveLeaveToday,
    int PendingApprovals,
    decimal SystemLeaveUtilizationPercent);
