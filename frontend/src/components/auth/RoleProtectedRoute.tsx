import React from 'react'
import { Navigate } from 'react-router-dom'
import { useAuth } from '../../hooks/useAuth'

interface RoleProtectedRouteProps {
  children: React.ReactElement
  allowedRoles: string[]
}

/**
 * Redirects unauthenticated users to /login.
 * Redirects authenticated users whose role is not in allowedRoles to /.
 * Tokens are never read from localStorage/sessionStorage — role comes from Redux state only.
 */
function RoleProtectedRoute({
  children,
  allowedRoles,
}: RoleProtectedRouteProps): JSX.Element {
  const { isAuthenticated, role } = useAuth()

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />
  }

  if (role == null || !allowedRoles.includes(role)) {
    return <Navigate to="/" replace />
  }

  return children
}

export default RoleProtectedRoute
