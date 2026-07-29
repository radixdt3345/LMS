import React from 'react'
import { Routes, Route, Navigate } from 'react-router-dom'
import LoginPage from './components/auth/LoginPage'
import ProtectedRoute from './components/auth/ProtectedRoute'
import RoleProtectedRoute from './components/auth/RoleProtectedRoute'
import DepartmentsPage from './pages/admin/DepartmentsPage'

function App(): JSX.Element {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route
        path="/"
        element={
          <ProtectedRoute>
            <div>Dashboard (placeholder)</div>
          </ProtectedRoute>
        }
      />
      <Route
        path="/admin/departments"
        element={
          <RoleProtectedRoute allowedRoles={['HRAdmin', 'SuperAdmin']}>
            <DepartmentsPage />
          </RoleProtectedRoute>
        }
      />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}

export default App
