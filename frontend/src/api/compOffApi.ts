import axiosClient from './axiosClient';

export type CompOffRequestStatus = 'Pending' | 'Approved' | 'Rejected';

export interface CreateCompOffRequestDto {
  /** ISO date string (YYYY-MM-DD). Must be a past working day. */
  workedDate: string;
  /** Minimum 4 hours, step 0.5. Credit: 4 h → 0.5 d, 8 h → 1.0 d. */
  hoursWorked: number;
  reason: string;
}

export interface CompOffRequestDto {
  id: string;
  employeeId: string;
  employeeName?: string;
  workedDate: string;
  hoursWorked: number;
  /** Backend-derived: hoursWorked / 8, rounded to nearest 0.5. */
  creditDays: number;
  reason: string;
  status: CompOffRequestStatus;
  createdAt: string;
  approvedAt?: string | null;
  approvedByName?: string | null;
}

interface ApiResponse<T> {
  success: boolean;
  data: T;
  total?: number;
}

/**
 * POST /api/v1/comp-off/requests
 * Submit a comp-off request for the calling user.
 * Returns 201 on success, 409 on duplicate date, 422 on invalid hours or working day.
 */
export async function submitCompOffRequest(
  dto: CreateCompOffRequestDto,
): Promise<CompOffRequestDto> {
  const res = await axiosClient.post<ApiResponse<CompOffRequestDto>>(
    '/api/v1/comp-off/requests',
    dto,
  );
  return res.data.data;
}

/**
 * GET /api/v1/comp-off/requests/me
 * Returns all comp-off requests for the calling user, newest first.
 */
export async function fetchMyCompOffRequests(): Promise<CompOffRequestDto[]> {
  const res = await axiosClient.get<ApiResponse<CompOffRequestDto[]>>(
    '/api/v1/comp-off/requests/me',
  );
  return res.data.data;
}

/**
 * POST /api/v1/comp-off/requests/{id}/approve
 * Approve a pending comp-off request. Requires Manager or HRAdmin role.
 */
export async function approveCompOffRequest(id: string): Promise<CompOffRequestDto> {
  const res = await axiosClient.post<ApiResponse<CompOffRequestDto>>(
    `/api/v1/comp-off/requests/${id}/approve`,
  );
  return res.data.data;
}

/**
 * POST /api/v1/comp-off/requests/{id}/reject
 * Reject a pending comp-off request. Requires Manager or HRAdmin role.
 */
export async function rejectCompOffRequest(id: string): Promise<CompOffRequestDto> {
  const res = await axiosClient.post<ApiResponse<CompOffRequestDto>>(
    `/api/v1/comp-off/requests/${id}/reject`,
  );
  return res.data.data;
}
