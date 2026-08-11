import { useSelector, useDispatch } from 'react-redux'
import type { RootState, AppDispatch } from '../store'
import {
  loginRequest,
  logout,
  clearError,
} from '../auth/authSlice'
import type { LoginPayload } from '../auth/authSlice'

/**
 * useAuth — convenience hook for accessing auth state and dispatching auth actions.
 * The auth reducer comes from auth/authSlice; state.auth.user holds the user object.
 */
export function useAuth() {
  const dispatch = useDispatch<AppDispatch>()
  const auth = useSelector((state: RootState) => state.auth)

  return {
    isAuthenticated: auth.isAuthenticated,
    isLoading: auth.isLoading,
    error: auth.error,
    user: auth.user,
    email: auth.user?.email ?? null,
    role: auth.user?.role ?? null,
    accessToken: auth.accessToken,
    login: (credentials: LoginPayload) =>
      dispatch(loginRequest(credentials)),
    logout: () => dispatch(logout()),
    clearError: () => dispatch(clearError()),
  }
}
