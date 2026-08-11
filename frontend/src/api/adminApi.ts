import axiosClient from './axiosClient';

/** Shape of a single locked account returned by GET /api/v1/auth/accounts */
export interface LockedAccount {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  /** ISO-8601 UTC timestamp, or null when account is unlocked */
  lockoutUntil: string | null;
  failedLoginCount: number;
}

interface PaginatedData<T> {
  items: T[];
  total: number;
  page: number;
  limit: number;
}

interface ApiResponse<T> {
  success: boolean;
  data: T;
}

/**
 * Fetches the paginated list of currently locked accounts.
 * GET /api/v1/auth/accounts?page=&limit=&locked=true
 */
export async function fetchLockedAccounts(
  page = 1,
  limit = 20,
): Promise<PaginatedData<LockedAccount>> {
  const response = await axiosClient.get<
    ApiResponse<PaginatedData<LockedAccount>>
  >('/api/v1/auth/accounts', {
    params: { page, limit, locked: true },
  });
  return response.data.data;
}

/**
 * Unlocks a specific account by ID.
 * POST /api/v1/auth/accounts/{id}/unlock
 */
export async function unlockAccount(id: string): Promise<void> {
  await axiosClient.post<ApiResponse<null>>(
    `/api/v1/auth/accounts/${id}/unlock`,
  );
}
