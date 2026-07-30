import { apiClient } from './axiosClient';

// ── Types ────────────────────────────────────────────────────────────────────

export interface AuditLog {
  id: string;
  action: string;
  entityType: string;
  entityId: string;
  actorId: string;
  oldValue: string | null;
  newValue: string | null;
  createdAt: string;
}

export interface AuditLogFilters {
  entity_type?: string;
  entity_id?: string;
  actor_id?: string;
  from?: string;
  to?: string;
  page?: number;
  limit?: number;
}

/** Shape returned by GET /api/v1/audit-logs */
export interface AuditLogListResponse {
  success: boolean;
  /** Array of audit log records, ordered newest-first. */
  data: AuditLog[];
  total: number;
  page: number;
  limit: number;
}

// ── API functions ─────────────────────────────────────────────────────────────

/**
 * GET /api/v1/audit-logs
 * Returns a paginated, filterable audit trail.
 * All filter parameters are optional.
 * Requires HRAdmin or SuperAdmin JWT claim (enforced by AuditController).
 */
export async function fetchAuditLogs(
  filters: AuditLogFilters,
): Promise<AuditLogListResponse> {
  const response = await apiClient.get<AuditLogListResponse>(
    '/api/v1/audit-logs',
    { params: filters },
  );
  return response.data;
}
