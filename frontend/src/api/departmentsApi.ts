import { apiClient } from './axiosClient'

export interface Department {
  id: string
  name: string
  description: string | null
  isActive: boolean
  employeeCount: number
  createdAt: string
  updatedAt: string
}

export interface CreateDepartmentPayload {
  name: string
  description?: string
}

export interface UpdateDepartmentPayload {
  name?: string
  description?: string
  isActive?: boolean
}

const BASE = '/api/v1/departments'

export const departmentsApi = {
  /**
   * GET /api/v1/departments?includeInactive=true
   * Returns all departments including inactive ones for the admin view.
   */
  getAll: async (includeInactive = true): Promise<Department[]> => {
    const response = await apiClient.get<{ success: boolean; data: Department[] }>(
      `${BASE}?includeInactive=${String(includeInactive)}`
    )
    return response.data.data
  },

  /**
   * POST /api/v1/departments
   * Creates a new department. Throws on 409 (name conflict).
   */
  create: async (payload: CreateDepartmentPayload): Promise<Department> => {
    const response = await apiClient.post<{ success: boolean; data: Department }>(BASE, payload)
    return response.data.data
  },

  /**
   * PUT /api/v1/departments/{id}
   * Updates a department. Throws on 409 (name conflict).
   */
  update: async (id: string, payload: UpdateDepartmentPayload): Promise<Department> => {
    const response = await apiClient.put<{ success: boolean; data: Department }>(
      `${BASE}/${id}`,
      payload
    )
    return response.data.data
  },

  /**
   * DELETE /api/v1/departments/{id}
   * Soft-deletes a department. Throws 409 if employees are still assigned.
   */
  delete: async (id: string): Promise<void> => {
    await apiClient.delete(`${BASE}/${id}`)
  },
}
