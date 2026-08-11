import { apiClient } from './axiosClient';

export interface AuditLog {
  id: string; action: string; entityType: string; entityId: string;
  actorId: string; oldValue: string | null; newValue: string | null; createdAt: string;
}

export interface AuditLogFilters {
  entity_type?: string; entity_id?: string; actor_id?: string;
  from?: string; to?: string; page?: number; limit?: number;
}

export interface AuditLogListResponse {
  success: boolean; data: AuditLog[]; total: number; page: number; limit: number;
}

export async function fetchAuditLogs(filters: AuditLogFilters): Promise<AuditLogListResponse> {
  const response = await apiClient.get<AuditLogListResponse>('/api/v1/audit-logs', { params: filters });
  return response.data;
}
