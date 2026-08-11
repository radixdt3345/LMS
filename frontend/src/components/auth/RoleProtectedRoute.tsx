import React from 'react';
import { Navigate } from 'react-router-dom';
import { useAuth } from '../../hooks/useAuth';

export default function RoleProtectedRoute({ children, allowedRoles }: { children: React.ReactElement; allowedRoles: string[] }) {
  const { isAuthenticated, role } = useAuth();
  if (!isAuthenticated) return <Navigate to="/login" replace />;
  if (role == null || !allowedRoles.includes(role)) return <Navigate to="/" replace />;
  return children;
}
