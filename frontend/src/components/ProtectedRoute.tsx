import { type ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { useSelector } from 'react-redux';
import type { RootState } from '../store';

interface ProtectedRouteProps {
  children: ReactNode;
}

/**
 * ProtectedRoute — redirects unauthenticated users to /login.
 * Preserves the attempted path in location state so LoginPage can
 * redirect back after successful authentication.
 */
export default function ProtectedRoute({ children }: ProtectedRouteProps) {
  const isAuthenticated = useSelector(
    (state: RootState) => state.auth.isAuthenticated,
  );
  const location = useLocation();

  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  return <>{children}</>;
}
