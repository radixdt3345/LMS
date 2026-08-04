import { apiClient } from './axiosClient';

// ---------------------------------------------------------------------------
// Response envelope — mirrors backend ApiResponse<T>
// ---------------------------------------------------------------------------
interface ApiEnvelope<T> {
  success: boolean;
  data: T;
}

// ---------------------------------------------------------------------------
// Shared sub-types (mirror C# records, camelCase per ASP.NET Core defaults)
// ---------------------------------------------------------------------------

export interface LeaveBalanceSummary {
  leaveTypeName: string;
  totalDays: number;
  usedDays: number;
  remainingDays: number;
}

/** DateOnly fields arrive as 'YYYY-MM-DD' strings. */
export interface RecentLeaveRequestSummary {
  requestId: string;
  leaveTypeName: string;
  startDate: string;
  endDate: string;
  status: string;
}

export interface DeptUtilizationRow {
  departmentName: string;
  totalLeaveDays: number;
  averageLeaveDays: number;
}

export interface UtilizationReportDto {
  rows: DeptUtilizationRow[];
}

export interface ComplianceReportDto {
  totalEmployees: number;
  employeesWithRequests: number;
  complianceRate: number;
}

export interface MonthTrendRow {
  year: number;
  month: number;
  monthLabel: string;
  approvedCount: number;
  rejectedCount: number;
}

export interface TrendsReportDto {
  rows: MonthTrendRow[];
}

// ---------------------------------------------------------------------------
// Dashboard DTOs (one per role)
// ---------------------------------------------------------------------------

export interface EmployeeDashboardDto {
  balances: LeaveBalanceSummary[];
  recentRequests: RecentLeaveRequestSummary[];
}

export interface ManagerDashboardDto {
  pendingApprovals: number;
  teamRecentRequests: RecentLeaveRequestSummary[];
  teamUtilization: DeptUtilizationRow[];
}

export interface HrDashboardDto {
  totalEmployees: number;
  totalPendingApprovals: number;
  utilization: UtilizationReportDto;
  compliance: ComplianceReportDto;
}

export interface SuperAdminDashboardDto {
  totalEmployees: number;
  totalDepartments: number;
  lockedAccountCount: number;
  recentAuditEventCount: number;
}

// ---------------------------------------------------------------------------
// API surface
// ---------------------------------------------------------------------------

export const dashboardApi = {
  getEmployeeDashboard: (): Promise<EmployeeDashboardDto> =>
    apiClient
      .get<ApiEnvelope<EmployeeDashboardDto>>('/api/v1/dashboard/employee')
      .then(r => r.data.data),

  getManagerDashboard: (): Promise<ManagerDashboardDto> =>
    apiClient
      .get<ApiEnvelope<ManagerDashboardDto>>('/api/v1/dashboard/manager')
      .then(r => r.data.data),

  getHrDashboard: (): Promise<HrDashboardDto> =>
    apiClient
      .get<ApiEnvelope<HrDashboardDto>>('/api/v1/dashboard/hr')
      .then(r => r.data.data),

  getSuperAdminDashboard: (): Promise<SuperAdminDashboardDto> =>
    apiClient
      .get<ApiEnvelope<SuperAdminDashboardDto>>('/api/v1/dashboard/super-admin')
      .then(r => r.data.data),

  getTrends: (): Promise<TrendsReportDto> =>
    apiClient
      .get<ApiEnvelope<TrendsReportDto>>('/api/v1/reports/trends')
      .then(r => r.data.data),

  /**
   * Streaming CSV — responseType blob, caller creates a Blob URL and triggers
   * a synthetic <a> click. The axios interceptor still attaches the Bearer token.
   */
  exportCsv: () =>
    apiClient.get('/api/v1/reports/export', { responseType: 'blob' }),
};
