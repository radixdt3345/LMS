import axiosClient from './axiosClient';

// ---------------------------------------------------------------------------
// Types — mirror C# DTOs in LMS.Application.DTOs.CompOff
// ---------------------------------------------------------------------------

export type CompOffStatus = 'Pending' | 'Approved' | 'Rejected';

/** Mirror CreateCompOffRequestDto */
export interface CreateCompOffRequestPayload {
  /** ISO date string 'YYYY-MM-DD' — must be a non-working day */
  workedDate: string;
  /** Must be >= 4. 4h → 0.5 day credit; 8h → 1.0 day credit */
  workedHours: number;
}

/** Mirror CompOffRequestDto */
export interface CompOffRequestDto {
  id: string;
  employeeId: string;
  /** ISO date string */
  workedDate: string;
  workedHours: number;
  status: CompOffStatus;
  createdAt: string;
  updatedAt: string;
}

/** Mirror CompOffCreditDto */
export interface CompOffCreditDto {
  id: string;
  employeeId: string;
  compOffRequestId: string;
  creditDays: number;
  /** ISO date string — expiry date (workedDate + 180 days) */
  expiresAt: string;
  usedDays: number;
  createdAt: string;
}

interface ApiResponse<T> {
  success: boolean;
  data: T;
  total?: number;
}

// ---------------------------------------------------------------------------
// API functions
// ---------------------------------------------------------------------------

/**
 * POST /api/v1/comp-off/requests
 * Submit a comp-off request for the calling user.
 * Returns 201 on success.
 * 422 if workedHours < 4 or date is a regular working day.
 * 409 on duplicate workedDate.
 */
export async function submitCompOffRequest(
  payload: CreateCompOffRequestPayload,
): Promise<CompOffRequestDto> {
  const res = await axiosClient.post<ApiResponse<CompOffRequestDto>>(
    '/api/v1/comp-off/requests',
    payload,
  );
  return res.data.data;
}

/**
 * GET /api/v1/comp-off/requests/me
 * Returns all comp-off requests for the calling user, newest first.
 */
export async function getMyCompOffRequests(): Promise<CompOffRequestDto[]> {
  const res = await axiosClient.get<ApiResponse<CompOffRequestDto[]>>(
    '/api/v1/comp-off/requests/me',
  );
  return res.data.data;
}

/**
 * GET /api/v1/comp-off/credits/me
 * Returns all comp-off credits for the calling user, ordered by expiry desc.
 */
export async function getMyCompOffCredits(): Promise<CompOffCreditDto[]> {
  const res = await axiosClient.get<ApiResponse<CompOffCreditDto[]>>(
    '/api/v1/comp-off/credits/me',
  );
  return res.data.data;
}

/**
 * POST /api/v1/comp-off/requests/{id}/approve
 * Approve a pending comp-off request. Manager or HRAdmin only.
 */
export async function approveCompOffRequest(id: string): Promise<CompOffRequestDto> {
  const res = await axiosClient.post<ApiResponse<CompOffRequestDto>>(
    `/api/v1/comp-off/requests/${id}/approve`,
  );
  return res.data.data;
}

/**
 * POST /api/v1/comp-off/requests/{id}/reject
 * Reject a pending comp-off request. Manager or HRAdmin only.
 */
export async function rejectCompOffRequest(id: string): Promise<CompOffRequestDto> {
  const res = await axiosClient.post<ApiResponse<CompOffRequestDto>>(
    `/api/v1/comp-off/requests/${id}/reject`,
  );
  return res.data.data;
}
