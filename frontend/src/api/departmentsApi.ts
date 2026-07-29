import axiosClient from './axiosClient';

export interface Department {
  id: string;
  name: string;
  description: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateDepartmentDto {
  name: string;
  description?: string | null;
}

export interface UpdateDepartmentDto {
  name: string;
  description?: string | null;
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
 * GET /api/v1/departments — paginated list of active departments.
 */
export async function fetchDepartments(
  page = 1,
  limit = 100,
): Promise<PaginatedData<Department>> {
  const response = await axiosClient.get<ApiResponse<PaginatedData<Department>>>(
    '/api/v1/departments',
    { params: { page, limit } },
  );
  return response.data.data;
}

/**
 * GET /api/v1/departments/{id}
 */
export async function getDepartment(id: string): Promise<Department> {
  const response = await axiosClient.get<ApiResponse<Department>>(
    `/api/v1/departments/${id}`,
  );
  return response.data.data;
}

/**
 * POST /api/v1/departments
 */
export async function createDepartment(
  dto: CreateDepartmentDto,
): Promise<Department> {
  const response = await axiosClient.post<ApiResponse<Department>>(
    '/api/v1/departments',
    dto,
  );
  return response.data.data;
}

/**
 * PUT /api/v1/departments/{id}
 */
export async function updateDepartment(
  id: string,
  dto: UpdateDepartmentDto,
): Promise<Department> {
  const response = await axiosClient.put<ApiResponse<Department>>(
    `/api/v1/departments/${id}`,
    dto,
  );
  return response.data.data;
}

/**
 * DELETE /api/v1/departments/{id} — soft delete (returns 204).
 */
export async function deleteDepartment(id: string): Promise<void> {
  await axiosClient.delete(`/api/v1/departments/${id}`);
}
