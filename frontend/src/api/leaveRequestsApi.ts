import axiosClient from './axiosClient';

interface ApiResponse<T> { success: boolean; data: T; }
interface PaginatedData<T> { items: T[]; total: number; page: number; limit: number; }

export type LeaveStatus = 'Draft' | 'Pending' | 'Approved' | 'Rejected' | 'Cancelled' | 'Revoked';

export interface LeaveRequestDto {
  id: string; leaveTypeId: string; leaveTypeName: string;
  startDate: string; endDate: string; computedDays: number;
  status: LeaveStatus; reason: string; documentUrl: string | null;
  isRetroactive: boolean; createdAt: string;
}

export interface CreateLeaveRequestDto {
  leaveTypeId: string; startDate: string; endDate: string;
  reason: string; documentUrl?: string | null; isHalfDay?: boolean;
}

export interface PreviewLeaveResponse { computed_days: number; }
export interface LeaveTypeDto { id: string; name: string; requiresDocument: boolean; isUnpaid: boolean; }
export type { PaginatedData };

export async function getLeaveTypes(): Promise<LeaveTypeDto[]> {
  const response = await axiosClient.get<ApiResponse<LeaveTypeDto[]>>('/api/v1/leave-types');
  return response.data.data;
}

export async function createLeaveRequest(dto: CreateLeaveRequestDto): Promise<LeaveRequestDto> {
  const response = await axiosClient.post<ApiResponse<LeaveRequestDto>>('/api/v1/leave-requests', dto);
  return response.data.data;
}

export async function submitLeaveRequest(id: string): Promise<LeaveRequestDto> {
  const response = await axiosClient.post<ApiResponse<LeaveRequestDto>>(`/api/v1/leave-requests/${id}/submit`);
  return response.data.data;
}

export async function cancelLeaveRequest(id: string): Promise<LeaveRequestDto> {
  const response = await axiosClient.post<ApiResponse<LeaveRequestDto>>(`/api/v1/leave-requests/${id}/cancel`);
  return response.data.data;
}

export async function revokeLeaveRequest(id: string): Promise<LeaveRequestDto> {
  const response = await axiosClient.post<ApiResponse<LeaveRequestDto>>(`/api/v1/leave-requests/${id}/revoke`);
  return response.data.data;
}

export async function getMyLeaveRequests(page = 1, limit = 10): Promise<PaginatedData<LeaveRequestDto>> {
  const response = await axiosClient.get<ApiResponse<PaginatedData<LeaveRequestDto>>>('/api/v1/leave-requests', { params: { page, limit } });
  return response.data.data;
}

export async function getAllLeaveRequests(page = 1, limit = 10): Promise<PaginatedData<LeaveRequestDto & { employeeName: string }>> {
  const response = await axiosClient.get<ApiResponse<PaginatedData<LeaveRequestDto & { employeeName: string }>>>('/api/v1/leave-requests/admin', { params: { page, limit } });
  return response.data.data;
}

export async function previewLeaveDays(startDate: string, endDate: string, leaveTypeId: string): Promise<PreviewLeaveResponse> {
  const response = await axiosClient.get<ApiResponse<PreviewLeaveResponse>>('/api/v1/leave-requests/preview', { params: { start: startDate, end: endDate, leave_type_id: leaveTypeId } });
  return response.data.data;
}
