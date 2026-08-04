using LMS.Application.DTOs.Reporting;
using LMS.Domain.Common;

namespace LMS.Application.Interfaces;

public interface IReportService
{
    Task<Result<UtilizationReportDto>> GetUtilizationAsync(int year, Guid? departmentId = null);
    Task<Result<TrendsReportDto>> GetTrendsAsync(int months);
    Task<Result<ComplianceReportDto>> GetComplianceAsync();
    IAsyncEnumerable<string> ExportCsvAsync(string type);
    Task<Result<EmployeeDashboardDto>> GetEmployeeDashboardAsync(Guid employeeId);
    Task<Result<ManagerDashboardDto>> GetManagerDashboardAsync(Guid managerId);
    Task<Result<HrDashboardDto>> GetHrDashboardAsync();
    Task<Result<SuperAdminDashboardDto>> GetSuperAdminDashboardAsync();
}
