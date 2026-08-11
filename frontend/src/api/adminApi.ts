import axiosClient from './axiosClient';

export interface LockedAccount {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  lockoutUntil: string | null;
  failedLoginCount: number;
}

interface PaginatedData<T> { items: T[]; total: number; page: number; limit: number; }
interface ApiResponse<T> { success: boolean; data: T; }

export async function fetchLockedAccounts(page = 1, limit = 20): Promise<PaginatedData<LockedAccount>> {
  const response = await axiosClient.get<ApiResponse<PaginatedData<LockedAccount>>>('/api/v1/auth/accounts', { params: { page, limit, locked: true } });
  return response.data.data;
}

export async function unlockAccount(id: string): Promise<void> {
  await axiosClient.post<ApiResponse<null>>(`/api/v1/auth/accounts/${id}/unlock`);
}
