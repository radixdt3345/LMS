import { createSlice, type PayloadAction } from '@reduxjs/toolkit'

// SECURITY: JWT access tokens and refresh tokens are stored in Redux
// (in-process memory) ONLY. They are NEVER written to localStorage
// or sessionStorage. This is enforced by UT-60.
export interface AuthState {
  accessToken: string | null
  refreshToken: string | null
  userId: string | null
  email: string | null
  role: string | null
  isAuthenticated: boolean
  isLoading: boolean
  error: string | null
}

const initialState: AuthState = {
  accessToken: null,
  refreshToken: null,
  userId: null,
  email: null,
  role: null,
  isAuthenticated: false,
  isLoading: false,
  error: null,
}

export interface LoginCredentials {
  email: string
  password: string
}

export interface AuthTokens {
  accessToken: string
  refreshToken: string
  userId: string
  email: string
  role: string
}

const authSlice = createSlice({
  name: 'auth',
  initialState,
  reducers: {
    loginRequest: (state, _action: PayloadAction<LoginCredentials>) => {
      state.isLoading = true
      state.error = null
    },
    loginSuccess: (state, action: PayloadAction<AuthTokens>) => {
      // Tokens stored in Redux memory ONLY — never localStorage/sessionStorage
      state.accessToken = action.payload.accessToken
      state.refreshToken = action.payload.refreshToken
      state.userId = action.payload.userId
      state.email = action.payload.email
      state.role = action.payload.role
      state.isAuthenticated = true
      state.isLoading = false
      state.error = null
    },
    loginFailure: (state, action: PayloadAction<string>) => {
      state.isLoading = false
      state.error = action.payload
      state.isAuthenticated = false
    },
    tokenRefreshSuccess: (
      state,
      action: PayloadAction<{ accessToken: string; refreshToken: string }>
    ) => {
      // Refreshed tokens stay in Redux memory only
      state.accessToken = action.payload.accessToken
      state.refreshToken = action.payload.refreshToken
    },
    logout: (state) => {
      state.accessToken = null
      state.refreshToken = null
      state.userId = null
      state.email = null
      state.role = null
      state.isAuthenticated = false
      state.isLoading = false
      state.error = null
    },
    clearError: (state) => {
      state.error = null
    },
  },
})

export const {
  loginRequest,
  loginSuccess,
  loginFailure,
  tokenRefreshSuccess,
  logout,
  clearError,
} = authSlice.actions

export const authReducer = authSlice.reducer
