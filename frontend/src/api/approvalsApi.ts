import axiosClient from './axiosClient';
import type { LeaveStatus } from './leaveRequestsApi';

interface ApiResponse<T> { success: boolean; data: T; }
interface PaginatedData<T> { items: T[]; total: number; page: number; limit: number; }
export type PaginatedResponse<T> = PaginatedData<T>;

export interface LeaveRequestDto {
  id: string;
  employeeName: string;
  leaveTypeName: string;
  startDate: string;
  endDate: string;
  computedDays: number;
  isRetroactive: boolean;
  status: LeaveStatus;
  documentUrl: string | null;
  reason: string;
}

export async function getPendingApprovals(page: number, limit: number): Promise<PaginatedResponse<LeaveRequestDto>> {
  const response = await axiosClient.get<ApiResponse<PaginatedData<LeaveRequestDto>>>('/api/v1/approvals/pending', { params: { page, limit } });
  return response.data.data;
}

export async function approveRequest(requestId: string): Promise<void> {
  await axiosClient.post(`/api/v1/approvals/${requestId}/approve`);
}

export async function rejectRequest(requestId: string, comment: string): Promise<void> {
  await axiosClient.post(`/api/v1/approvals/${requestId}/reject`, { comment });
}
