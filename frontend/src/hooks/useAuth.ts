import { useSelector, useDispatch } from 'react-redux'
import type { RootState, AppDispatch } from '../store'
import {
  loginRequest,
  logout,
  clearError,
  type LoginCredentials,
} from '../store/slices/authSlice'

export function useAuth() {
  const dispatch = useDispatch<AppDispatch>()
  const auth = useSelector((state: RootState) => state.auth)

  return {
    isAuthenticated: auth.isAuthenticated,
    isLoading: auth.isLoading,
    error: auth.error,
    email: auth.email,
    role: auth.role,
    login: (credentials: LoginCredentials) =>
      dispatch(loginRequest(credentials)),
    logout: () => dispatch(logout()),
    clearError: () => dispatch(clearError()),
  }
}
