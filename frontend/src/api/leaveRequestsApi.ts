import axiosClient from './axiosClient';

// ---------------------------------------------------------------------------
// Shared API response envelope (mirrors adminApi.ts)
// ---------------------------------------------------------------------------

interface ApiResponse<T> {
  success: boolean;
  data: T;
}

interface PaginatedData<T> {
  items: T[];
  total: number;
  page: number;
  limit: number;
}

// ---------------------------------------------------------------------------
// Domain types
// ---------------------------------------------------------------------------

export type LeaveStatus =
  | 'Draft'
  | 'Pending'
  | 'Approved'
  | 'Rejected'
  | 'Cancelled'
  | 'Revoked';

export interface LeaveRequestDto {
  id: string;
  leaveTypeId: string;
  leaveTypeName: string;
  /** ISO-8601 date string (YYYY-MM-DD) */
  startDate: string;
  /** ISO-8601 date string (YYYY-MM-DD) */
  endDate: string;
  computedDays: number;
  status: LeaveStatus;
  reason: string;
  documentUrl: string | null;
  isRetroactive: boolean;
  isHalfDay: boolean;
  /** ISO-8601 UTC timestamp */
  createdAt: string;
}

export interface CreateLeaveRequestDto {
  leaveTypeId: string;
  startDate: string;
  endDate: string;
  reason: string;
  documentUrl?: string | null;
  /** FR-39: half-day request — only valid when startDate === endDate */
  isHalfDay?: boolean;
}

export interface PreviewLeaveResponse {
  computed_days: number;
}

export interface LeaveTypeDto {
  id: string;
  name: string;
  /** When true the employee must supply a document URL */
  requiresDocument: boolean;
  /** When true zero balance does not block submission */
  isUnpaid: boolean;
}

// ---------------------------------------------------------------------------
// Paginated leave request list (used by admin endpoint)
// ---------------------------------------------------------------------------

export type { PaginatedData };

// ---------------------------------------------------------------------------
// Leave type helpers
// ---------------------------------------------------------------------------

/**
 * GET /api/v1/leave-types
 * Returns the list of active leave types for the select dropdown.
 */
export async function getLeaveTypes(): Promise<LeaveTypeDto[]> {
  const response = await axiosClient.get<ApiResponse<LeaveTypeDto[]>>(
    '/api/v1/leave-types',
  );
  return response.data.data;
}

// ---------------------------------------------------------------------------
// Leave request CRUD
// ---------------------------------------------------------------------------

/**
 * POST /api/v1/leave-requests
 * Creates a new leave request in Draft state.
 */
export async function createLeaveRequest(
  dto: CreateLeaveRequestDto,
): Promise<LeaveRequestDto> {
  const response = await axiosClient.post<ApiResponse<LeaveRequestDto>>(
    '/api/v1/leave-requests',
    dto,
  );
  return response.data.data;
}

/**
 * POST /api/v1/leave-requests/{id}/submit
 * Transitions the leave request from Draft → Pending.
 */
export async function submitLeaveRequest(id: string): Promise<LeaveRequestDto> {
  const response = await axiosClient.post<ApiResponse<LeaveRequestDto>>(
    `/api/v1/leave-requests/${id}/submit`,
  );
  return response.data.data;
}

/**
 * POST /api/v1/leave-requests/{id}/cancel
 * Cancels a Draft or Pending leave request.
 */
export async function cancelLeaveRequest(id: string): Promise<LeaveRequestDto> {
  const response = await axiosClient.post<ApiResponse<LeaveRequestDto>>(
    `/api/v1/leave-requests/${id}/cancel`,
  );
  return response.data.data;
}

/**
 * POST /api/v1/leave-requests/{id}/revoke
 * Revokes an Approved leave request. HR Admin + Super Admin only.
 */
export async function revokeLeaveRequest(id: string): Promise<LeaveRequestDto> {
  const response = await axiosClient.post<ApiResponse<LeaveRequestDto>>(
    `/api/v1/leave-requests/${id}/revoke`,
  );
  return response.data.data;
}

/**
 * GET /api/v1/leave-requests
 * Returns the authenticated employee's own leave requests (paginated).
 */
export async function getMyLeaveRequests(
  page = 1,
  limit = 10,
): Promise<PaginatedData<LeaveRequestDto>> {
  const response = await axiosClient.get<
    ApiResponse<PaginatedData<LeaveRequestDto>>
  >('/api/v1/leave-requests', {
    params: { page, limit },
  });
  return response.data.data;
}

/**
 * GET /api/v1/leave-requests/admin
 * Returns all employees' leave requests (paginated). HR Admin + Super Admin only.
 */
export async function getAllLeaveRequests(
  page = 1,
  limit = 10,
): Promise<PaginatedData<LeaveRequestDto & { employeeName: string }>> {
  const response = await axiosClient.get<
    ApiResponse<PaginatedData<LeaveRequestDto & { employeeName: string }>>
  >('/api/v1/leave-requests/admin', {
    params: { page, limit },
  });
  return response.data.data;
}

/**
 * GET /api/v1/leave-requests/preview
 * Computes the number of working days for the given date range + leave type.
 * Returns { computed_days: number }.
 */
export async function previewLeaveDays(
  startDate: string,
  endDate: string,
  leaveTypeId: string,
): Promise<PreviewLeaveResponse> {
  const response = await axiosClient.get<ApiResponse<PreviewLeaveResponse>>(
    '/api/v1/leave-requests/preview',
    {
      params: { start: startDate, end: endDate, leave_type_id: leaveTypeId },
    },
  );
  return response.data.data;
}
