import { describe, it, expect, beforeEach } from 'vitest'
import {
  authReducer,
  loginRequest,
  loginSuccess,
  loginFailure,
  tokenRefreshSuccess,
  logout,
  type AuthState,
} from '../store/slices/authSlice'

// UT-60: JWT tokens stored in Redux memory only — never localStorage/sessionStorage
describe('authSlice — UT-60: in-memory token storage', () => {
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

  const mockTokens = {
    accessToken: 'eyJhbGciOiJSUzI1NiJ9.access.token',
    refreshToken: 'eyJhbGciOiJSUzI1NiJ9.refresh.token',
    userId: '550e8400-e29b-41d4-a716-446655440000',
    email: 'user@example.com',
    role: 'Employee',
  }

  beforeEach(() => {
    localStorage.clear()
    sessionStorage.clear()
  })

  it('UT-60a: loginSuccess stores accessToken and refreshToken in Redux state', () => {
    const state = authReducer(initialState, loginSuccess(mockTokens))
    expect(state.accessToken).toBe(mockTokens.accessToken)
    expect(state.refreshToken).toBe(mockTokens.refreshToken)
    expect(state.isAuthenticated).toBe(true)
    expect(state.email).toBe(mockTokens.email)
    expect(state.role).toBe(mockTokens.role)
  })

  it('UT-60b: tokens are never written to localStorage after loginSuccess', () => {
    authReducer(initialState, loginSuccess(mockTokens))
    expect(localStorage.getItem('accessToken')).toBeNull()
    expect(localStorage.getItem('refreshToken')).toBeNull()
    expect(localStorage.getItem('token')).toBeNull()
    expect(localStorage.length).toBe(0)
  })

  it('UT-60c: tokens are never written to sessionStorage after loginSuccess', () => {
    authReducer(initialState, loginSuccess(mockTokens))
    expect(sessionStorage.getItem('accessToken')).toBeNull()
    expect(sessionStorage.getItem('refreshToken')).toBeNull()
    expect(sessionStorage.getItem('token')).toBeNull()
    expect(sessionStorage.length).toBe(0)
  })

  it('UT-60d: logout clears accessToken, refreshToken, and isAuthenticated', () => {
    const loggedInState = authReducer(initialState, loginSuccess(mockTokens))
    const state = authReducer(loggedInState, logout())
    expect(state.accessToken).toBeNull()
    expect(state.refreshToken).toBeNull()
    expect(state.isAuthenticated).toBe(false)
    expect(state.userId).toBeNull()
  })

  it('UT-60e: tokenRefreshSuccess updates tokens in Redux state; storage remains empty', () => {
    const loggedInState = authReducer(initialState, loginSuccess(mockTokens))
    const refreshed = {
      accessToken: 'eyJhbGciOiJSUzI1NiJ9.new-access',
      refreshToken: 'eyJhbGciOiJSUzI1NiJ9.new-refresh',
    }
    const state = authReducer(loggedInState, tokenRefreshSuccess(refreshed))
    expect(state.accessToken).toBe(refreshed.accessToken)
    expect(state.refreshToken).toBe(refreshed.refreshToken)
    expect(localStorage.length).toBe(0)
    expect(sessionStorage.length).toBe(0)
  })

  it('UT-60f: loginRequest sets isLoading without writing tokens', () => {
    const state = authReducer(
      initialState,
      loginRequest({ email: 'user@example.com', password: 'Password1!' })
    )
    expect(state.isLoading).toBe(true)
    expect(state.accessToken).toBeNull()
    expect(localStorage.length).toBe(0)
  })

  it('UT-60g: loginFailure sets error; no tokens stored anywhere', () => {
    const state = authReducer(
      initialState,
      loginFailure('Invalid email or password')
    )
    expect(state.accessToken).toBeNull()
    expect(state.refreshToken).toBeNull()
    expect(state.isAuthenticated).toBe(false)
    expect(state.error).toBe('Invalid email or password')
    expect(localStorage.length).toBe(0)
    expect(sessionStorage.length).toBe(0)
  })
})
