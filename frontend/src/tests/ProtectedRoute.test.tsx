import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { Provider } from 'react-redux';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { configureStore } from '@reduxjs/toolkit';
import authReducer from '../auth/authSlice';
import notificationsReducer from '../store/notifications/notificationsSlice';
import ProtectedRoute from '../components/ProtectedRoute';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function makeStore(isAuthenticated: boolean) {
  return configureStore({
    reducer: { auth: authReducer, notifications: notificationsReducer },
    preloadedState: {
      auth: {
        user: isAuthenticated
          ? { id: 'u1', email: 'emp@example.com', fullName: 'Test User', role: 'Employee' }
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

function renderWithRoute(isAuthenticated: boolean) {
  return render(
    <Provider store={makeStore(isAuthenticated)}>
      <MemoryRouter initialEntries={['/protected']}>
        <Routes>
          <Route path="/login" element={<div data-testid="login-page">Login</div>} />
          <Route
            path="/protected"
            element={
              <ProtectedRoute>
                <div data-testid="protected-content">Protected Content</div>
              </ProtectedRoute>
            }
          />
        </Routes>
      </MemoryRouter>
    </Provider>,
  );
}

// ---------------------------------------------------------------------------
// UT-FE-15 — ProtectedRoute redirects unauthenticated users
// ---------------------------------------------------------------------------

describe('UT-FE-15: ProtectedRoute redirects unauthenticated users to /login', () => {
  it('redirects to /login when not authenticated', () => {
    renderWithRoute(false);
    expect(screen.getByTestId('login-page')).toBeInTheDocument();
    expect(screen.queryByTestId('protected-content')).not.toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// UT-FE-16 — ProtectedRoute renders children for authenticated users
// ---------------------------------------------------------------------------

describe('UT-FE-16: ProtectedRoute renders children for authenticated users', () => {
  it('renders protected content when authenticated', () => {
    renderWithRoute(true);
    expect(screen.getByTestId('protected-content')).toBeInTheDocument();
    expect(screen.queryByTestId('login-page')).not.toBeInTheDocument();
  });
});
