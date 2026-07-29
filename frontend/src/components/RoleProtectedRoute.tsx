import { type ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { useSelector } from 'react-redux';
import type { RootState } from '../store';

interface RoleProtectedRouteProps {
  children: ReactNode;
  allowedRoles: string[];
}

/**
 * RoleProtectedRoute — guards a route by authentication AND role membership.
 *
 * - Unauthenticated users are redirected to /login (preserving attempted path).
 * - Authenticated users whose role is NOT in allowedRoles are redirected to
 *   /dashboard (forbidden — no dedicated 403 page in this sprint).
 */
export default function RoleProtectedRoute({
  children,
  allowedRoles,
}: RoleProtectedRouteProps) {
  const { isAuthenticated, user } = useSelector(
    (state: RootState) => state.auth,
  );
  const location = useLocation();

  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  if (!user || !allowedRoles.includes(user.role)) {
    return <Navigate to="/dashboard" replace />;
  }

  return <>{children}</>;
}
