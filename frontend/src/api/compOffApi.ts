import axiosClient from './axiosClient';

export type CompOffStatus = 'Pending' | 'Approved' | 'Rejected';

export interface CreateCompOffRequestPayload { workedDate: string; workedHours: number; }
export interface CompOffRequestDto { id: string; employeeId: string; workedDate: string; workedHours: number; status: CompOffStatus; createdAt: string; updatedAt: string; }
export interface CompOffCreditDto { id: string; employeeId: string; compOffRequestId: string; creditDays: number; expiresAt: string; usedDays: number; createdAt: string; }

interface ApiResponse<T> { success: boolean; data: T; total?: number; }

export async function submitCompOffRequest(payload: CreateCompOffRequestPayload): Promise<CompOffRequestDto> {
  const res = await axiosClient.post<ApiResponse<CompOffRequestDto>>('/api/v1/comp-off/requests', payload);
  return res.data.data;
}

export async function getMyCompOffRequests(): Promise<CompOffRequestDto[]> {
  const res = await axiosClient.get<ApiResponse<CompOffRequestDto[]>>('/api/v1/comp-off/requests/me');
  return res.data.data;
}

export async function getMyCompOffCredits(): Promise<CompOffCreditDto[]> {
  const res = await axiosClient.get<ApiResponse<CompOffCreditDto[]>>('/api/v1/comp-off/credits/me');
  return res.data.data;
}

export async function approveCompOffRequest(id: string): Promise<CompOffRequestDto> {
  const res = await axiosClient.post<ApiResponse<CompOffRequestDto>>(`/api/v1/comp-off/requests/${id}/approve`);
  return res.data.data;
}

export async function rejectCompOffRequest(id: string): Promise<CompOffRequestDto> {
  const res = await axiosClient.post<ApiResponse<CompOffRequestDto>>(`/api/v1/comp-off/requests/${id}/reject`);
  return res.data.data;
}
