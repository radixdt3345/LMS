import { createSlice, type PayloadAction } from '@reduxjs/toolkit';

export interface AuthUser {
  id: string;
  email: string;
  fullName: string;
  role: string;
}

export interface AuthState {
  user: AuthUser | null;
  /**
   * Access token stored in Redux state (in-memory) ONLY.
   * CONSTITUTION Rule 6: NEVER written to localStorage or sessionStorage.
   */
  accessToken: string | null;
  refreshToken: string | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  error: string | null;
}

const initialState: AuthState = {
  user: null,
  accessToken: null,
  refreshToken: null,
  isAuthenticated: false,
  isLoading: false,
  error: null,
};

export interface LoginPayload {
  email: string;
  password: string;
}

export interface AuthSuccessPayload {
  user: AuthUser;
  accessToken: string;
  refreshToken: string;
}

const authSlice = createSlice({
  name: 'auth',
  initialState,
  reducers: {
    /** Dispatched by LoginPage form submission — saga intercepts. */
    loginRequest: (state, _action: PayloadAction<LoginPayload>) => {
      state.isLoading = true;
      state.error = null;
    },
    /** Dispatched by sagas on successful local login or SSO callback. */
    loginSuccess: (state, action: PayloadAction<AuthSuccessPayload>) => {
      state.isLoading = false;
      state.isAuthenticated = true;
      state.user = action.payload.user;
      state.accessToken = action.payload.accessToken;
      state.refreshToken = action.payload.refreshToken;
      state.error = null;
    },
    loginFailure: (state, action: PayloadAction<string>) => {
      state.isLoading = false;
      state.isAuthenticated = false;
      state.error = action.payload;
    },
    /** Dispatched when MSAL SSO redirect is detected. */
    ssoCallbackRequest: (state, _action: PayloadAction<string>) => {
      state.isLoading = true;
      state.error = null;
    },
    /** Dispatched by axios interceptor when a 401 is encountered. */
    refreshRequest: (state) => {
      state.isLoading = true;
    },
    refreshSuccess: (state, action: PayloadAction<AuthSuccessPayload>) => {
      state.isLoading = false;
      state.accessToken = action.payload.accessToken;
      state.refreshToken = action.payload.refreshToken;
      state.user = action.payload.user;
    },
    refreshFailure: (state) => {
      // Refresh failed — force logout
      state.isLoading = false;
      state.isAuthenticated = false;
      state.user = null;
      state.accessToken = null;
      state.refreshToken = null;
    },
    logout: (state) => {
      state.isAuthenticated = false;
      state.user = null;
      state.accessToken = null;
      state.refreshToken = null;
      state.isLoading = false;
      state.error = null;
    },
    clearError: (state) => {
      state.error = null;
    },
  },
});

export const {
  loginRequest,
  loginSuccess,
  loginFailure,
  ssoCallbackRequest,
  refreshRequest,
  refreshSuccess,
  refreshFailure,
  logout,
  clearError,
} = authSlice.actions;

export default authSlice.reducer;
