import { createSlice, type PayloadAction } from '@reduxjs/toolkit';
import type {
  EmployeeDashboardDto,
  ManagerDashboardDto,
  HrDashboardDto,
  SuperAdminDashboardDto,
} from '../../api/dashboardApi';

interface AsyncSlot<T> {
  data: T | null;
  loading: boolean;
  error: string | null;
}

const emptySlot = <T>(): AsyncSlot<T> => ({
  data: null,
  loading: false,
  error: null,
});

export interface DashboardState {
  employee: AsyncSlot<EmployeeDashboardDto>;
  manager: AsyncSlot<ManagerDashboardDto>;
  hr: AsyncSlot<HrDashboardDto>;
  superAdmin: AsyncSlot<SuperAdminDashboardDto>;
}

const initialState: DashboardState = {
  employee: emptySlot<EmployeeDashboardDto>(),
  manager: emptySlot<ManagerDashboardDto>(),
  hr: emptySlot<HrDashboardDto>(),
  superAdmin: emptySlot<SuperAdminDashboardDto>(),
};

const dashboardSlice = createSlice({
  name: 'dashboard',
  initialState,
  reducers: {
    // --- Employee ---
    fetchEmployeeDashboard: (state) => {
      state.employee.loading = true;
      state.employee.error = null;
    },
    fetchEmployeeDashboardSuccess: (
      state,
      action: PayloadAction<EmployeeDashboardDto>,
    ) => {
      state.employee.loading = false;
      state.employee.data = action.payload;
    },
    fetchEmployeeDashboardFailure: (state, action: PayloadAction<string>) => {
      state.employee.loading = false;
      state.employee.error = action.payload;
    },

    // --- Manager ---
    fetchManagerDashboard: (state) => {
      state.manager.loading = true;
      state.manager.error = null;
    },
    fetchManagerDashboardSuccess: (
      state,
      action: PayloadAction<ManagerDashboardDto>,
    ) => {
      state.manager.loading = false;
      state.manager.data = action.payload;
    },
    fetchManagerDashboardFailure: (state, action: PayloadAction<string>) => {
      state.manager.loading = false;
      state.manager.error = action.payload;
    },

    // --- HR Admin ---
    fetchHrDashboard: (state) => {
      state.hr.loading = true;
      state.hr.error = null;
    },
    fetchHrDashboardSuccess: (
      state,
      action: PayloadAction<HrDashboardDto>,
    ) => {
      state.hr.loading = false;
      state.hr.data = action.payload;
    },
    fetchHrDashboardFailure: (state, action: PayloadAction<string>) => {
      state.hr.loading = false;
      state.hr.error = action.payload;
    },

    // --- Super Admin ---
    fetchSuperAdminDashboard: (state) => {
      state.superAdmin.loading = true;
      state.superAdmin.error = null;
    },
    fetchSuperAdminDashboardSuccess: (
      state,
      action: PayloadAction<SuperAdminDashboardDto>,
    ) => {
      state.superAdmin.loading = false;
      state.superAdmin.data = action.payload;
    },
    fetchSuperAdminDashboardFailure: (state, action: PayloadAction<string>) => {
      state.superAdmin.loading = false;
      state.superAdmin.error = action.payload;
    },
  },
});

export const {
  fetchEmployeeDashboard,
  fetchEmployeeDashboardSuccess,
  fetchEmployeeDashboardFailure,
  fetchManagerDashboard,
  fetchManagerDashboardSuccess,
  fetchManagerDashboardFailure,
  fetchHrDashboard,
  fetchHrDashboardSuccess,
  fetchHrDashboardFailure,
  fetchSuperAdminDashboard,
  fetchSuperAdminDashboardSuccess,
  fetchSuperAdminDashboardFailure,
} = dashboardSlice.actions;

export const dashboardReducer = dashboardSlice.reducer;
