import axiosClient from './axiosClient';

/** Mirrors LMS.Domain.Enums.AccrualType. */
export enum AccrualType {
  Annual    = 0,
  OneTime   = 1,
  Unlimited = 2,
}

export interface BalanceItem {
  leaveTypeId:   string;
  leaveTypeName: string;
  accrualType:   AccrualType;
  allocatedDays: number;
  usedDays:      number;
  availableDays: number;
  year:          number;
}

export interface CompOffCredit {
  id:               string;
  employeeId:       string;
  compOffRequestId: string;
  creditDays:       number;
  expiresAt:        string;  // ISO date string (DateOnly serialised as "YYYY-MM-DD")
  usedDays:         number;
  createdAt:        string;
}

interface ApiResponse<T> {
  success: boolean;
  data:    T;
  total?:  number;
}

/**
 * GET /api/v1/leave-balances/me?year=YYYY
 * Returns the calling user's leave balances for the given year
 * (defaults to the current calendar year when year is omitted).
 */
export async function fetchMyLeaveBalances(year?: number): Promise<BalanceItem[]> {
  const params = year !== undefined ? { year } : {};
  const res = await axiosClient.get<ApiResponse<BalanceItem[]>>(
    '/api/v1/leave-balances/me',
    { params },
  );
  return res.data.data;
}

/**
 * GET /api/v1/comp-off/credits/me
 * Returns all comp-off credits for the calling user.
 */
export async function fetchMyCompOffCredits(): Promise<CompOffCredit[]> {
  const res = await axiosClient.get<ApiResponse<CompOffCredit[]>>(
    '/api/v1/comp-off/credits/me',
  );
  return res.data.data;
}
