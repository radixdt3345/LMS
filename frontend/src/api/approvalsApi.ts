import axiosClient from './axiosClient';
import type { LeaveStatus } from './leaveRequestsApi';

// ---------------------------------------------------------------------------
// Shared API response envelope
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

export type PaginatedResponse<T> = PaginatedData<T>;

// ---------------------------------------------------------------------------
// Domain types
// ---------------------------------------------------------------------------

export interface LeaveRequestDto {
  id: string;
  employeeName: string;
  leaveTypeName: string;
  /** ISO-8601 date string (YYYY-MM-DD) */
  startDate: string;
  /** ISO-8601 date string (YYYY-MM-DD) */
  endDate: string;
  computedDays: number;
  isRetroactive: boolean;
  status: LeaveStatus;
  documentUrl: string | null;
  reason: string;
}

// ---------------------------------------------------------------------------
// Approvals API
// ---------------------------------------------------------------------------

/**
 * GET /api/v1/approvals/pending
 * Returns paginated leave requests pending the caller's approval.
 * Manager: only their direct reports; HR Admin / Super Admin: all.
 */
export async function getPendingApprovals(
  page: number,
  limit: number,
): Promise<PaginatedResponse<LeaveRequestDto>> {
  const response = await axiosClient.get<
    ApiResponse<PaginatedData<LeaveRequestDto>>
  >('/api/v1/approvals/pending', { params: { page, limit } });
  return response.data.data;
}

/**
 * POST /api/v1/approvals/{requestId}/approve
 * Approves the given leave request. Returns 204 on success.
 */
export async function approveRequest(requestId: string): Promise<void> {
  await axiosClient.post(`/api/v1/approvals/${requestId}/approve`);
}

/**
 * POST /api/v1/approvals/{requestId}/reject
 * Rejects the given leave request with a mandatory comment.
 */
export async function rejectRequest(
  requestId: string,
  comment: string,
): Promise<void> {
  await axiosClient.post(`/api/v1/approvals/${requestId}/reject`, { comment });
}
