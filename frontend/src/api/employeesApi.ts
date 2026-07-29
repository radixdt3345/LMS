import axiosClient from './axiosClient';

/** Employee response shape from GET /api/v1/employees */
export interface Employee {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  employeeCode: string;
  role: 'Employee' | 'Manager' | 'HRAdmin' | 'SuperAdmin';
  departmentId: string | null;
  departmentName: string | null;
  managerId: string | null;
  managerName: string | null;
  phone: string | null;
  isActive: boolean;
}

export interface CreateEmployeeDto {
  firstName: string;
  lastName: string;
  email: string;
  employeeCode: string;
  phone?: string | null;
  departmentId?: string | null;
  managerId?: string | null;
}

export interface UpdateEmployeeDto {
  firstName: string;
  lastName: string;
  email?: string;
  employeeCode?: string;
  phone?: string | null;
  departmentId?: string | null;
  managerId?: string | null;
}

export interface UpdateOwnProfileDto {
  firstName: string;
  lastName: string;
  phone?: string | null;
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
 * GET /api/v1/employees — paginated list; optional ?role filter.
 */
export async function fetchEmployees(
  page = 1,
  limit = 20,
  role?: string,
): Promise<PaginatedData<Employee>> {
  const response = await axiosClient.get<ApiResponse<PaginatedData<Employee>>>(
    '/api/v1/employees',
    { params: { page, limit, ...(role ? { role } : {}) } },
  );
  return response.data.data;
}

/**
 * GET /api/v1/employees/{id}
 */
export async function getEmployee(id: string): Promise<Employee> {
  const response = await axiosClient.get<ApiResponse<Employee>>(
    `/api/v1/employees/${id}`,
  );
  return response.data.data;
}

/**
 * POST /api/v1/employees — create a new employee.
 */
export async function createEmployee(
  dto: CreateEmployeeDto,
): Promise<Employee> {
  const response = await axiosClient.post<ApiResponse<Employee>>(
    '/api/v1/employees',
    dto,
  );
  return response.data.data;
}

/**
 * PUT /api/v1/employees/{id} — update employee (HR Admin).
 */
export async function updateEmployee(
  id: string,
  dto: UpdateEmployeeDto,
): Promise<Employee> {
  const response = await axiosClient.put<ApiResponse<Employee>>(
    `/api/v1/employees/${id}`,
    dto,
  );
  return response.data.data;
}

/**
 * DELETE /api/v1/employees/{id} — soft deactivate; returns 204.
 */
export async function deactivateEmployee(id: string): Promise<void> {
  await axiosClient.delete(`/api/v1/employees/${id}`);
}

/**
 * GET /api/v1/employees/me — own profile (all authenticated users).
 */
export async function getOwnProfile(): Promise<Employee> {
  const response = await axiosClient.get<ApiResponse<Employee>>(
    '/api/v1/employees/me',
  );
  return response.data.data;
}

/**
 * PUT /api/v1/employees/me — update own profile: firstName, lastName, phone only.
 */
export async function updateOwnProfile(
  dto: UpdateOwnProfileDto,
): Promise<Employee> {
  const response = await axiosClient.put<ApiResponse<Employee>>(
    '/api/v1/employees/me',
    dto,
  );
  return response.data.data;
}

/**
 * GET /api/v1/employees/{id}/team — direct reports for manager.
 */
export async function fetchTeam(
  userId: string,
): Promise<PaginatedData<Employee>> {
  const response = await axiosClient.get<ApiResponse<PaginatedData<Employee>>>(
    `/api/v1/employees/${userId}/team`,
  );
  return response.data.data;
}
