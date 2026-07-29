import axios, { AxiosError } from 'axios';

const API_BASE = import.meta.env.VITE_API_BASE_URL as string;

export const ACCRUAL_TYPE_LABELS: Record<number, string> = {
  0: 'Annual',
  1: 'OneTime',
  2: 'Unlimited',
};

export interface LeaveTypeDto {
  id: string;
  name: string;
  maxDaysPerYear: number | null;
  accrualType: number; // 0=Annual, 1=OneTime, 2=Unlimited
  requiresDocument: boolean;
  isActive: boolean;
}

export interface CreateLeaveTypePayload {
  name: string;
  maxDaysPerYear: number | null;
  accrualType: number;
  requiresDocument: boolean;
}

export type UpdateLeaveTypePayload = CreateLeaveTypePayload;

function authHeader(token: string) {
  return { Authorization: `Bearer ${token}` };
}

export async function fetchLeaveTypes(token: string): Promise<LeaveTypeDto[]> {
  const res = await axios.get<{ success: boolean; data: LeaveTypeDto[] }>(
    `${API_BASE}/api/v1/leave-types`,
    { headers: authHeader(token) },
  );
  return res.data.data;
}

export async function createLeaveType(
  token: string,
  payload: CreateLeaveTypePayload,
): Promise<LeaveTypeDto> {
  const res = await axios.post<{ success: boolean; data: LeaveTypeDto }>(
    `${API_BASE}/api/v1/leave-types`,
    payload,
    { headers: authHeader(token) },
  );
  return res.data.data;
}

export async function updateLeaveType(
  token: string,
  id: string,
  payload: UpdateLeaveTypePayload,
): Promise<LeaveTypeDto> {
  const res = await axios.put<{ success: boolean; data: LeaveTypeDto }>(
    `${API_BASE}/api/v1/leave-types/${id}`,
    payload,
    { headers: authHeader(token) },
  );
  return res.data.data;
}

/**
 * Returns true on success, false on 409 (balances exist).
 * All other errors are re-thrown.
 */
export async function deleteLeaveType(
  token: string,
  id: string,
): Promise<boolean> {
  try {
    await axios.delete(`${API_BASE}/api/v1/leave-types/${id}`, {
      headers: authHeader(token),
    });
    return true;
  } catch (err) {
    const axiosErr = err as AxiosError;
    if (axiosErr.response?.status === 409) {
      return false;
    }
    throw err;
  }
}
