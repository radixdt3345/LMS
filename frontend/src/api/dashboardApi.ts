import { apiClient } from './axiosClient';

interface ApiEnvelope<T> { success: boolean; data: T; }

export interface LeaveBalanceSummary { leaveTypeName: string; allocated: number; used: number; available: number; }
export interface RecentLeaveRequestSummary { id: string; leaveTypeName: string; startDate: string; endDate: string; status: string; }
export interface DeptUtilizationRow { deptName: string; totalEmployees: number; totalLeaveDays: number; avgLeaveDaysPerEmployee: number; }
export interface UtilizationReportDto { year: number; departmentId: string | null; rows: DeptUtilizationRow[]; }
export interface ComplianceReportDto { submissionRatePercent: number; totalEmployees: number; employeesWithAtLeastOneRequest: number; }
export interface MonthTrendRow { yearMonth: string; totalRequests: number; approvedCount: number; rejectedCount: number; }
export interface TrendsReportDto { months: number; rows: MonthTrendRow[]; }
export interface EmployeeDashboardDto { balances: LeaveBalanceSummary[]; recentRequests: RecentLeaveRequestSummary[]; pendingCount: number; }
export interface ManagerDashboardDto { teamPendingRequests: RecentLeaveRequestSummary[]; teamSize: number; }
export interface HrDashboardDto { pendingApprovals: number; totalEmployees: number; activeLeaveToday: number; recentActivity: RecentLeaveRequestSummary[]; }
export interface SuperAdminDashboardDto { totalEmployees: number; totalDepartments: number; activeLeaveToday: number; pendingApprovals: number; systemLeaveUtilizationPercent: number; }

export const dashboardApi = {
  getEmployeeDashboard: () => apiClient.get<ApiEnvelope<EmployeeDashboardDto>>('/api/v1/dashboard/employee').then(r => r.data.data),
  getManagerDashboard: () => apiClient.get<ApiEnvelope<ManagerDashboardDto>>('/api/v1/dashboard/manager').then(r => r.data.data),
  getHrDashboard: () => apiClient.get<ApiEnvelope<HrDashboardDto>>('/api/v1/dashboard/hr').then(r => r.data.data),
  getSuperAdminDashboard: () => apiClient.get<ApiEnvelope<SuperAdminDashboardDto>>('/api/v1/dashboard/super-admin').then(r => r.data.data),
  getUtilization: () => apiClient.get<ApiEnvelope<UtilizationReportDto>>('/api/v1/reports/utilization').then(r => r.data.data),
  getCompliance: () => apiClient.get<ApiEnvelope<ComplianceReportDto>>('/api/v1/reports/compliance').then(r => r.data.data),
  getTrends: () => apiClient.get<ApiEnvelope<TrendsReportDto>>('/api/v1/reports/trends').then(r => r.data.data),
  exportCsv: () => apiClient.get('/api/v1/reports/export', { responseType: 'blob' }),
};
