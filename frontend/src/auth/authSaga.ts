import { call, put, select, takeLatest } from 'redux-saga/effects';
import type { PayloadAction } from '@reduxjs/toolkit';
import axios from 'axios';
import { loginRequest, loginSuccess, loginFailure, ssoCallbackRequest, refreshRequest, refreshSuccess, refreshFailure, logout, type LoginPayload, type AuthSuccessPayload } from './authSlice';
import type { RootState } from '../store';

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000';

interface LoginResponse { success: boolean; data: { accessToken: string; refreshToken: string; user: AuthSuccessPayload['user'] }; }
interface RefreshResponse { success: boolean; data: { accessToken: string; refreshToken: string; user: AuthSuccessPayload['user'] }; }

function* loginSaga(action: PayloadAction<LoginPayload>) {
  try {
    const response: { data: LoginResponse } = yield call([axios, axios.post], `${API_BASE}/api/v1/auth/login`, action.payload);
    const { accessToken, refreshToken, user } = response.data.data;
    yield put(loginSuccess({ accessToken, refreshToken, user }));
  } catch (err: unknown) {
    const message = axios.isAxiosError(err) && err.response?.data?.error?.message
      ? (err.response.data.error.message as string)
      : 'Login failed. Please check your credentials.';
    yield put(loginFailure(message));
  }
}

function* ssoCallbackSaga(action: PayloadAction<string>) {
  try {
    const response: { data: LoginResponse } = yield call([axios, axios.get], `${API_BASE}/api/v1/auth/sso/callback`, { params: { code: action.payload } });
    const { accessToken, refreshToken, user } = response.data.data;
    yield put(loginSuccess({ accessToken, refreshToken, user }));
  } catch (err: unknown) {
    const message = axios.isAxiosError(err) && err.response?.data?.error?.message
      ? (err.response.data.error.message as string) : 'SSO login failed.';
    yield put(loginFailure(message));
  }
}

function* refreshSaga() {
  try {
    const refreshToken: string | null = yield select((state: RootState) => state.auth.refreshToken);
    if (!refreshToken) { yield put(refreshFailure()); return; }
    const response: { data: RefreshResponse } = yield call([axios, axios.post], `${API_BASE}/api/v1/auth/refresh`, { refreshToken });
    const { accessToken, refreshToken: newRefreshToken, user } = response.data.data;
    yield put(refreshSuccess({ accessToken, refreshToken: newRefreshToken, user }));
  } catch { yield put(refreshFailure()); yield put(logout()); }
}

export function* rootAuthSaga() {
  yield takeLatest(loginRequest.type, loginSaga);
  yield takeLatest(ssoCallbackRequest.type, ssoCallbackSaga);
  yield takeLatest(refreshRequest.type, refreshSaga);
}
