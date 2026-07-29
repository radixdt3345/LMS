import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { Provider } from 'react-redux';
import { BrowserRouter } from 'react-router-dom';
import { MsalProvider } from '@azure/msal-react';
import { PublicClientApplication } from '@azure/msal-browser';
import { configureStore } from '@reduxjs/toolkit';
import authReducer from '../auth/authSlice';
import { msalConfig } from '../auth/msalConfig';
import LockedAccountsPage from '../pages/admin/LockedAccountsPage';
import RoleProtectedRoute from '../components/RoleProtectedRoute';
import * as adminApi from '../api/adminApi';
import type { LockedAccount } from '../api/adminApi';

// Mock the entire adminApi module
vi.mock('../api/adminApi', () => ({
  fetchLockedAccounts: vi.fn(),
  unlockAccount: vi.fn(),
}));

const msalInstance = new PublicClientApplication(msalConfig);

const MOCK_ACCOUNTS: LockedAccount[] = [
  {
    id: 'uuid-001',
    firstName: 'Alice',
    lastName: 'Smith',
    email: 'alice@example.com',
    lockoutUntil: '2026-07-29T10:00:00Z',
    failedLoginCount: 5,
  },
  {
    id: 'uuid-002',
    firstName: 'Bob',
    lastName: 'Jones',
    email: 'bob@example.com',
    lockoutUntil: null,
    failedLoginCount: 3,
  },
];

function makeAdminStore() {
  return configureStore({
    reducer: { auth: authReducer },
    preloadedState: {
      auth: {
        user: {
          id: 'admin-1',
          email: 'hr@example.com',
          fullName: 'HR Admin',
          role: 'HRAdmin',
        },
        accessToken: 'test-token',
        refreshToken: 'refresh-token',
        isAuthenticated: true,
        isLoading: false,
        error: null,
      },
    },
    middleware: (getDefaultMiddleware) =>
      getDefaultMiddleware({ thunk: true }),
  });
}

function makeEmployeeStore() {
  return configureStore({
    reducer: { auth: authReducer },
    preloadedState: {
      auth: {
        user: {
          id: 'emp-1',
          email: 'emp@example.com',
          fullName: 'Regular Employee',
          role: 'Employee',
        },
        accessToken: 'emp-token',
        refreshToken: 'emp-refresh',
        isAuthenticated: true,
        isLoading: false,
        error: null,
      },
    },
    middleware: (getDefaultMiddleware) =>
      getDefaultMiddleware({ thunk: true }),
  });
}

function renderPage(store = makeAdminStore()) {
  return render(
    <MsalProvider instance={msalInstance}>
      <Provider store={store}>
        <BrowserRouter>
          <LockedAccountsPage />
        </BrowserRouter>
      </Provider>
    </MsalProvider>,
  );
}

function renderWithRoleGuard(store = makeAdminStore()) {
  return render(
    <MsalProvider instance={msalInstance}>
      <Provider store={store}>
        <BrowserRouter>
          <RoleProtectedRoute allowedRoles={['HRAdmin', 'SuperAdmin']}>
            <LockedAccountsPage />
          </RoleProtectedRoute>
        </BrowserRouter>
      </Provider>
    </MsalProvider>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
});

/**
 * UT-63 — LockedAccountsPage renders the accounts table with correct columns.
 */
describe('UT-63: LockedAccountsPage renders accounts table', () => {
  it('shows a row for each locked account after loading', async () => {
    vi.mocked(adminApi.fetchLockedAccounts).mockResolvedValueOnce({
      items: MOCK_ACCOUNTS,
      total: 2,
      page: 1,
      limit: 20,
    });

    renderPage();

    // Loading spinner shown initially
    expect(screen.getByRole('progressbar')).toBeInTheDocument();

    // Table appears once data loads
    await waitFor(() => {
      expect(screen.getByTestId('accounts-table')).toBeInTheDocument();
    });

    expect(screen.getByText('Alice Smith')).toBeInTheDocument();
    expect(screen.getByText('alice@example.com')).toBeInTheDocument();
    expect(screen.getByText('Bob Jones')).toBeInTheDocument();
    expect(screen.getByText('bob@example.com')).toBeInTheDocument();
  });

  it('renders an Unlock button for each row', async () => {
    vi.mocked(adminApi.fetchLockedAccounts).mockResolvedValueOnce({
      items: MOCK_ACCOUNTS,
      total: 2,
      page: 1,
      limit: 20,
    });

    renderPage();

    await waitFor(() => {
      expect(screen.getByTestId('unlock-btn-uuid-001')).toBeInTheDocument();
      expect(screen.getByTestId('unlock-btn-uuid-002')).toBeInTheDocument();
    });
  });
});

/**
 * UT-64 — LockedAccountsPage shows empty state when list is empty.
 */
describe('UT-64: Empty state is displayed when no accounts are locked', () => {
  it('shows "No locked accounts" when the API returns an empty list', async () => {
    vi.mocked(adminApi.fetchLockedAccounts).mockResolvedValueOnce({
      items: [],
      total: 0,
      page: 1,
      limit: 20,
    });

    renderPage();

    await waitFor(() => {
      expect(screen.getByTestId('empty-state')).toBeInTheDocument();
      expect(screen.getByText('No locked accounts')).toBeInTheDocument();
    });

    expect(screen.queryByTestId('accounts-table')).not.toBeInTheDocument();
  });
});

/**
 * UT-65 — Unlock button removes the row (optimistic update) on success.
 */
describe('UT-65: Unlock action performs optimistic row removal', () => {
  it('removes the unlocked row immediately after clicking Unlock', async () => {
    vi.mocked(adminApi.fetchLockedAccounts).mockResolvedValueOnce({
      items: MOCK_ACCOUNTS,
      total: 2,
      page: 1,
      limit: 20,
    });
    vi.mocked(adminApi.unlockAccount).mockResolvedValueOnce(undefined);

    renderPage();

    await waitFor(() => {
      expect(screen.getByTestId('row-uuid-001')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId('unlock-btn-uuid-001'));

    // Row should be gone immediately (optimistic)
    await waitFor(() => {
      expect(screen.queryByTestId('row-uuid-001')).not.toBeInTheDocument();
    });

    // Other row remains
    expect(screen.getByTestId('row-uuid-002')).toBeInTheDocument();

    expect(adminApi.unlockAccount).toHaveBeenCalledWith('uuid-001');
  });
});

/**
 * UT-66 — Unlock failure shows Snackbar error and re-fetches list.
 */
describe('UT-66: Unlock failure shows error Snackbar', () => {
  it('shows a Snackbar error message when unlockAccount rejects', async () => {
    vi.mocked(adminApi.fetchLockedAccounts)
      .mockResolvedValueOnce({
        items: MOCK_ACCOUNTS,
        total: 2,
        page: 1,
        limit: 20,
      })
      // Second call (rollback re-fetch)
      .mockResolvedValueOnce({
        items: MOCK_ACCOUNTS,
        total: 2,
        page: 1,
        limit: 20,
      });

    vi.mocked(adminApi.unlockAccount).mockRejectedValueOnce(
      new Error('Network error'),
    );

    renderPage();

    await waitFor(() => {
      expect(screen.getByTestId('row-uuid-001')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId('unlock-btn-uuid-001'));

    await waitFor(() => {
      expect(
        screen.getByText('Failed to unlock account. Please try again.'),
      ).toBeInTheDocument();
    });
  });
});

/**
 * UT-67 — RoleProtectedRoute blocks Employee role from the locked accounts page.
 */
describe('UT-67: RoleProtectedRoute denies access to Employee role', () => {
  it('redirects an Employee away from the locked accounts page', () => {
    vi.mocked(adminApi.fetchLockedAccounts).mockResolvedValue({
      items: [],
      total: 0,
      page: 1,
      limit: 20,
    });

    renderWithRoleGuard(makeEmployeeStore());

    // LockedAccountsPage heading must NOT appear
    expect(
      screen.queryByText('Locked Accounts'),
    ).not.toBeInTheDocument();
  });

  it('renders the page for an HRAdmin', async () => {
    vi.mocked(adminApi.fetchLockedAccounts).mockResolvedValueOnce({
      items: [],
      total: 0,
      page: 1,
      limit: 20,
    });

    renderWithRoleGuard(makeAdminStore());

    await waitFor(() => {
      expect(screen.getByText('Locked Accounts')).toBeInTheDocument();
    });
  });
});
