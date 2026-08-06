import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { Provider } from 'react-redux';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { configureStore } from '@reduxjs/toolkit';
import authReducer from '../auth/authSlice';
import notificationsReducer from '../store/notifications/notificationsSlice';
import RoleProtectedRoute from '../components/RoleProtectedRoute';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

interface StoreOptions {
  isAuthenticated: boolean;
  role?: string;
}

function makeStore({ isAuthenticated, role = 'Employee' }: StoreOptions) {
  return configureStore({
    reducer: { auth: authReducer, notifications: notificationsReducer },
    preloadedState: {
      auth: {
        user: isAuthenticated
          ? { id: 'u1', email: 'emp@example.com', fullName: 'Test User', role }
          : null,
        accessToken: isAuthenticated ? 'tok' : null,
        refreshToken: isAuthenticated ? 'ref' : null,
        isAuthenticated,
        isLoading: false,
        error: null,
      },
    },
    middleware: (gm) => gm({ thunk: true }),
  });
}

function renderWithRoles(opts: StoreOptions, allowedRoles: string[]) {
  return render(
    <Provider store={makeStore(opts)}>
      <MemoryRouter initialEntries={['/admin']}>
        <Routes>
          <Route path="/login" element={<div data-testid="login-page">Login</div>} />
          <Route path="/dashboard" element={<div data-testid="dashboard-page">Dashboard</div>} />
          <Route
            path="/admin"
            element={
              <RoleProtectedRoute allowedRoles={allowedRoles}>
                <div data-testid="admin-content">Admin Content</div>
              </RoleProtectedRoute>
            }
          />
        </Routes>
      </MemoryRouter>
    </Provider>,
  );
}

// ---------------------------------------------------------------------------
// UT-FE-17 — Unauthenticated → /login
// ---------------------------------------------------------------------------

describe('UT-FE-17: RoleProtectedRoute redirects unauthenticated users to /login', () => {
  it('redirects to /login when not authenticated', () => {
    renderWithRoles({ isAuthenticated: false }, ['HRAdmin', 'SuperAdmin']);
    expect(screen.getByTestId('login-page')).toBeInTheDocument();
    expect(screen.queryByTestId('admin-content')).not.toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// UT-FE-18 — Wrong role → /dashboard
// ---------------------------------------------------------------------------

describe('UT-FE-18: RoleProtectedRoute redirects unauthorised role to /dashboard', () => {
  it('redirects Employee to /dashboard when only HRAdmin is allowed', () => {
    renderWithRoles(
      { isAuthenticated: true, role: 'Employee' },
      ['HRAdmin', 'SuperAdmin'],
    );
    expect(screen.getByTestId('dashboard-page')).toBeInTheDocument();
    expect(screen.queryByTestId('admin-content')).not.toBeInTheDocument();
  });

  it('redirects Manager to /dashboard when only HRAdmin and SuperAdmin are allowed', () => {
    renderWithRoles(
      { isAuthenticated: true, role: 'Manager' },
      ['HRAdmin', 'SuperAdmin'],
    );
    expect(screen.getByTestId('dashboard-page')).toBeInTheDocument();
    expect(screen.queryByTestId('admin-content')).not.toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// UT-FE-19 — Correct role → renders children
// ---------------------------------------------------------------------------

describe('UT-FE-19: RoleProtectedRoute renders children for authorised role', () => {
  it('renders admin content for HRAdmin', () => {
    renderWithRoles(
      { isAuthenticated: true, role: 'HRAdmin' },
      ['HRAdmin', 'SuperAdmin'],
    );
    expect(screen.getByTestId('admin-content')).toBeInTheDocument();
    expect(screen.queryByTestId('login-page')).not.toBeInTheDocument();
  });

  it('renders admin content for SuperAdmin', () => {
    renderWithRoles(
      { isAuthenticated: true, role: 'SuperAdmin' },
      ['HRAdmin', 'SuperAdmin'],
    );
    expect(screen.getByTestId('admin-content')).toBeInTheDocument();
  });

  it('renders content for Manager when Manager is in allowedRoles', () => {
    renderWithRoles(
      { isAuthenticated: true, role: 'Manager' },
      ['Manager', 'HRAdmin', 'SuperAdmin'],
    );
    expect(screen.getByTestId('admin-content')).toBeInTheDocument();
  });
});
