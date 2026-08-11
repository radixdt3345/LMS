import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { Provider } from 'react-redux';
import { BrowserRouter } from 'react-router-dom';
import { MsalProvider } from '@azure/msal-react';
import { PublicClientApplication } from '@azure/msal-browser';
import { configureStore } from '@reduxjs/toolkit';
import authReducer from '../auth/authSlice';
import LoginPage from '../pages/LoginPage';
import { msalConfig } from '../auth/msalConfig';

vi.mock('@azure/msal-react', async () => {
  const actual = await vi.importActual<typeof import('@azure/msal-react')>(
    '@azure/msal-react',
  );
  return {
    ...actual,
    useMsal: () => ({
      instance: { loginRedirect: vi.fn().mockResolvedValue(undefined) },
      accounts: [],
      inProgress: 'none',
    }),
  };
});

function makeStore() {
  return configureStore({
    reducer: { auth: authReducer },
    middleware: (getDefaultMiddleware) =>
      getDefaultMiddleware({ thunk: true }),
  });
}

const msalInstance = new PublicClientApplication(msalConfig);

function renderLoginPage(store = makeStore()) {
  return render(
    <MsalProvider instance={msalInstance}>
      <Provider store={store}>
        <BrowserRouter>
          <LoginPage />
        </BrowserRouter>
      </Provider>
    </MsalProvider>,
  );
}

describe('UT-60: LoginPage renders correctly', () => {
  it('renders email field, password field, submit button, and SSO button', () => {
    renderLoginPage();
    expect(screen.getByTestId('login-form')).toBeInTheDocument();
    expect(screen.getByTestId('email-input')).toBeInTheDocument();
    expect(screen.getByTestId('password-input')).toBeInTheDocument();
    expect(screen.getByTestId('submit-button')).toBeInTheDocument();
    expect(screen.getByTestId('sso-button')).toBeInTheDocument();
  });

  it('shows validation errors when submitted empty', async () => {
    renderLoginPage();
    fireEvent.click(screen.getByTestId('submit-button'));
    await waitFor(() => {
      expect(screen.getByText('Email is required')).toBeInTheDocument();
      expect(screen.getByText('Password is required')).toBeInTheDocument();
    });
  });
});

describe('UT-61: Form submission dispatches loginRequest', () => {
  it('dispatches loginRequest with email and password on valid submit', async () => {
    const store = makeStore();
    const dispatchSpy = vi.spyOn(store, 'dispatch');
    renderLoginPage(store);

    fireEvent.change(screen.getByTestId('email-input'), {
      target: { value: 'user@example.com' },
    });
    fireEvent.change(screen.getByTestId('password-input'), {
      target: { value: 'Password1!' },
    });
    fireEvent.click(screen.getByTestId('submit-button'));

    await waitFor(() => {
      const actions = dispatchSpy.mock.calls.map((c) => c[0]);
      const loginAction = actions.find(
        (a) =>
          typeof a === 'object' &&
          a !== null &&
          'type' in a &&
          (a as { type: string }).type === 'auth/loginRequest',
      );
      expect(loginAction).toBeDefined();
      expect(loginAction).toMatchObject({
        type: 'auth/loginRequest',
        payload: { email: 'user@example.com', password: 'Password1!' },
      });
    });
  });
});

describe('UT-62: Error message is displayed when auth fails', () => {
  it('shows error alert when auth state has an error', () => {
    const store = configureStore({
      reducer: { auth: authReducer },
      preloadedState: {
        auth: {
          user: null,
          accessToken: null,
          refreshToken: null,
          isAuthenticated: false,
          isLoading: false,
          error: 'Invalid email or password',
        },
      },
      middleware: (getDefaultMiddleware) =>
        getDefaultMiddleware({ thunk: true }),
    });

    renderLoginPage(store);
    expect(screen.getByRole('alert')).toBeInTheDocument();
    expect(screen.getByText('Invalid email or password')).toBeInTheDocument();
  });
});
