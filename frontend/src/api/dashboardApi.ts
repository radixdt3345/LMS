import { apiClient } from './axiosClient';

// ---------------------------------------------------------------------------
// Response envelope — mirrors backend ApiResponse<T>
// ---------------------------------------------------------------------------
interface ApiEnvelope<T> {
  success: boolean;
  data: T;
}

// ---------------------------------------------------------------------------
// Shared sub-types — mirror C# records (camelCase per ASP.NET Core defaults)
// ---------------------------------------------------------------------------

/** Mirrors LMS.Application.DTOs.Reporting.LeaveBalanceSummary */
export interface LeaveBalanceSummary {
  leaveTypeName: string;
  allocated: number;
  used: number;
  available: number;
}

/** Mirrors LMS.Application.DTOs.Reporting.RecentLeaveRequestSummary */
export interface RecentLeaveRequestSummary {
  id: string;
  leaveTypeName: string;
  /** DateOnly serialised as 'YYYY-MM-DD' */
  startDate: string;
  endDate: string;
  status: string;
}

/** Mirrors LMS.Application.DTOs.Reporting.DeptUtilizationRow */
export interface DeptUtilizationRow {
  deptName: string;
  totalEmployees: number;
  totalLeaveDays: number;
  avgLeaveDaysPerEmployee: number;
}

/** Mirrors LMS.Application.DTOs.Reporting.UtilizationReportDto */
export interface UtilizationReportDto {
  year: number;
  departmentId: string | null;
  rows: DeptUtilizationRow[];
}

/** Mirrors LMS.Application.DTOs.Reporting.ComplianceReportDto */
export interface ComplianceReportDto {
  submissionRatePercent: number;
  totalEmployees: number;
  employeesWithAtLeastOneRequest: number;
}

/** Mirrors LMS.Application.DTOs.Reporting.MonthTrendRow */
export interface MonthTrendRow {
  /** 'YYYY-MM' string e.g. '2025-01' */
  yearMonth: string;
  totalRequests: number;
  approvedCount: number;
  rejectedCount: number;
}

/** Mirrors LMS.Application.DTOs.Reporting.TrendsReportDto */
export interface TrendsReportDto {
  months: number;
  rows: MonthTrendRow[];
}

// ---------------------------------------------------------------------------
// Dashboard DTOs (one per role) — mirror C# records exactly
// ---------------------------------------------------------------------------

/** GET /api/v1/dashboard/employee */
export interface EmployeeDashboardDto {
  balances: LeaveBalanceSummary[];
  recentRequests: RecentLeaveRequestSummary[];
  pendingCount: number;
}

/** GET /api/v1/dashboard/manager */
export interface ManagerDashboardDto {
  teamPendingRequests: RecentLeaveRequestSummary[];
  teamSize: number;
}

/** GET /api/v1/dashboard/hr */
export interface HrDashboardDto {
  pendingApprovals: number;
  totalEmployees: number;
  activeLeaveToday: number;
  recentActivity: RecentLeaveRequestSummary[];
}

/** GET /api/v1/dashboard/super-admin */
export interface SuperAdminDashboardDto {
  totalEmployees: number;
  totalDepartments: number;
  activeLeaveToday: number;
  pendingApprovals: number;
  systemLeaveUtilizationPercent: number;
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

  getUtilization: (): Promise<UtilizationReportDto> =>
    apiClient
      .get<ApiEnvelope<UtilizationReportDto>>('/api/v1/reports/utilization')
      .then(r => r.data.data),

  getCompliance: (): Promise<ComplianceReportDto> =>
    apiClient
      .get<ApiEnvelope<ComplianceReportDto>>('/api/v1/reports/compliance')
      .then(r => r.data.data),

  getTrends: (): Promise<TrendsReportDto> =>
    apiClient
      .get<ApiEnvelope<TrendsReportDto>>('/api/v1/reports/trends')
      .then(r => r.data.data),

  /**
   * Streaming CSV — responseType blob. Caller creates a Blob URL and triggers
   * a synthetic <a> click.
   */
  exportCsv: () =>
    apiClient.get('/api/v1/reports/export', { responseType: 'blob' }),
};
