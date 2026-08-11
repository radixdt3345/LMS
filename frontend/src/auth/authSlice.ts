import { createSlice, type PayloadAction } from '@reduxjs/toolkit';

export interface AuthUser { id: string; email: string; fullName: string; role: string; }

export interface AuthState {
  user: AuthUser | null;
  accessToken: string | null;
  refreshToken: string | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  error: string | null;
}

const initialState: AuthState = { user: null, accessToken: null, refreshToken: null, isAuthenticated: false, isLoading: false, error: null };

export interface LoginPayload { email: string; password: string; }
export interface AuthSuccessPayload { user: AuthUser; accessToken: string; refreshToken: string; }

const authSlice = createSlice({
  name: 'auth',
  initialState,
  reducers: {
    loginRequest: (state, _action: PayloadAction<LoginPayload>) => { state.isLoading = true; state.error = null; },
    loginSuccess: (state, action: PayloadAction<AuthSuccessPayload>) => { state.isLoading = false; state.isAuthenticated = true; state.user = action.payload.user; state.accessToken = action.payload.accessToken; state.refreshToken = action.payload.refreshToken; state.error = null; },
    loginFailure: (state, action: PayloadAction<string>) => { state.isLoading = false; state.isAuthenticated = false; state.error = action.payload; },
    ssoCallbackRequest: (state, _action: PayloadAction<string>) => { state.isLoading = true; state.error = null; },
    refreshRequest: (state) => { state.isLoading = true; },
    refreshSuccess: (state, action: PayloadAction<AuthSuccessPayload>) => { state.isLoading = false; state.accessToken = action.payload.accessToken; state.refreshToken = action.payload.refreshToken; state.user = action.payload.user; },
    refreshFailure: (state) => { state.isLoading = false; state.isAuthenticated = false; state.user = null; state.accessToken = null; state.refreshToken = null; },
    logout: (state) => { state.isAuthenticated = false; state.user = null; state.accessToken = null; state.refreshToken = null; state.isLoading = false; state.error = null; },
    clearError: (state) => { state.error = null; },
    tokenRefreshSuccess: (state, action: PayloadAction<{ accessToken: string; refreshToken: string }>) => { state.accessToken = action.payload.accessToken; state.refreshToken = action.payload.refreshToken; },
  },
});

export const { loginRequest, loginSuccess, loginFailure, ssoCallbackRequest, refreshRequest, refreshSuccess, refreshFailure, logout, clearError, tokenRefreshSuccess } = authSlice.actions;
export default authSlice.reducer;
