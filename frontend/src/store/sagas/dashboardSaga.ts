import { call, put, takeLatest } from 'redux-saga/effects';
import {
  dashboardApi,
  type EmployeeDashboardDto,
  type ManagerDashboardDto,
  type HrDashboardDto,
  type SuperAdminDashboardDto,
} from '../../api/dashboardApi';
import {
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
} from '../slices/dashboardSlice';

function* handleFetchEmployeeDashboard(): Generator<unknown, void, EmployeeDashboardDto> {
  try {
    const data = yield call([dashboardApi, dashboardApi.getEmployeeDashboard]);
    yield put(fetchEmployeeDashboardSuccess(data));
  } catch (error: unknown) {
    const message =
      error instanceof Error ? error.message : 'Failed to load employee dashboard.';
    yield put(fetchEmployeeDashboardFailure(message));
  }
}

function* handleFetchManagerDashboard(): Generator<unknown, void, ManagerDashboardDto> {
  try {
    const data = yield call([dashboardApi, dashboardApi.getManagerDashboard]);
    yield put(fetchManagerDashboardSuccess(data));
  } catch (error: unknown) {
    const message =
      error instanceof Error ? error.message : 'Failed to load manager dashboard.';
    yield put(fetchManagerDashboardFailure(message));
  }
}

function* handleFetchHrDashboard(): Generator<unknown, void, HrDashboardDto> {
  try {
    const data = yield call([dashboardApi, dashboardApi.getHrDashboard]);
    yield put(fetchHrDashboardSuccess(data));
  } catch (error: unknown) {
    const message =
      error instanceof Error ? error.message : 'Failed to load HR dashboard.';
    yield put(fetchHrDashboardFailure(message));
  }
}

function* handleFetchSuperAdminDashboard(): Generator<unknown, void, SuperAdminDashboardDto> {
  try {
    const data = yield call([dashboardApi, dashboardApi.getSuperAdminDashboard]);
    yield put(fetchSuperAdminDashboardSuccess(data));
  } catch (error: unknown) {
    const message =
      error instanceof Error ? error.message : 'Failed to load super admin dashboard.';
    yield put(fetchSuperAdminDashboardFailure(message));
  }
}

export function* dashboardSaga(): Generator {
  yield takeLatest(fetchEmployeeDashboard.type, handleFetchEmployeeDashboard);
  yield takeLatest(fetchManagerDashboard.type, handleFetchManagerDashboard);
  yield takeLatest(fetchHrDashboard.type, handleFetchHrDashboard);
  yield takeLatest(fetchSuperAdminDashboard.type, handleFetchSuperAdminDashboard);
}
