import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { Provider } from 'react-redux';
import { BrowserRouter } from 'react-router-dom';
import { configureStore } from '@reduxjs/toolkit';
import authReducer from '../auth/authSlice';
import notificationsReducer from '../store/notifications/notificationsSlice';
import CompOffPage from '../pages/CompOffPage';
import * as compOffApi from '../api/compOffApi';
import type { CompOffRequestDto, CompOffCreditDto } from '../api/compOffApi';

// ---------------------------------------------------------------------------
// Mocks
// ---------------------------------------------------------------------------

vi.mock('../api/compOffApi', () => ({
  submitCompOffRequest: vi.fn(),
  getMyCompOffRequests: vi.fn(),
  getMyCompOffCredits: vi.fn(),
}));

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

const NOW = new Date().toISOString();

function makeRequest(overrides: Partial<CompOffRequestDto> = {}): CompOffRequestDto {
  return {
    id: 'cor-001',
    employeeId: 'u1',
    workedDate: '2026-08-01',
    workedHours: 8,
    status: 'Pending',
    createdAt: NOW,
    updatedAt: NOW,
    ...overrides,
  };
}

function makeCredit(overrides: Partial<CompOffCreditDto> = {}): CompOffCreditDto {
  return {
    id: 'coc-001',
    employeeId: 'u1',
    compOffRequestId: 'cor-001',
    creditDays: 1,
    usedDays: 0,
    expiresAt: new Date(Date.now() + 90 * 24 * 60 * 60 * 1000).toISOString(),
    createdAt: NOW,
    ...overrides,
  };
}

function makeStore() {
  return configureStore({
    reducer: { auth: authReducer, notifications: notificationsReducer },
    preloadedState: {
      auth: {
        user: { id: 'u1', email: 'emp@example.com', fullName: 'Test User', role: 'Employee' },
        accessToken: 'tok',
        refreshToken: 'ref',
        isAuthenticated: true,
        isLoading: false,
        error: null,
      },
    },
    middleware: (gm) => gm({ thunk: true }),
  });
}

function renderPage() {
  return render(
    <Provider store={makeStore()}>
      <BrowserRouter>
        <CompOffPage />
      </BrowserRouter>
    </Provider>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(compOffApi.getMyCompOffRequests).mockResolvedValue([]);
  vi.mocked(compOffApi.getMyCompOffCredits).mockResolvedValue([]);
});

// ---------------------------------------------------------------------------
// UT-FE-43 — Page renders form fields
// ---------------------------------------------------------------------------

describe('UT-FE-43: CompOffPage renders the submit form', () => {
  it('renders worked date, hours worked, and submit button', async () => {
    renderPage();
    await waitFor(() =>
      expect(compOffApi.getMyCompOffRequests).toHaveBeenCalled(),
    );
    expect(screen.getByTestId('worked-date-input')).toBeInTheDocument();
    expect(screen.getByTestId('worked-hours-input')).toBeInTheDocument();
    expect(screen.getByTestId('submit-compoff-btn')).toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// UT-FE-44 — Load error
// ---------------------------------------------------------------------------

describe('UT-FE-44: CompOffPage shows error when data load fails', () => {
  it('shows load error when API rejects', async () => {
    vi.mocked(compOffApi.getMyCompOffRequests).mockRejectedValueOnce(new Error('net'));
    renderPage();
    await waitFor(() => {
      expect(screen.getByText(/Failed to load comp-off data/i)).toBeInTheDocument();
    });
  });
});

// ---------------------------------------------------------------------------
// UT-FE-45 — Form validation
// ---------------------------------------------------------------------------

describe('UT-FE-45: CompOffPage form validation', () => {
  it('shows error when worked date is missing on submit', async () => {
    renderPage();
    await waitFor(() => expect(compOffApi.getMyCompOffRequests).toHaveBeenCalled());

    fireEvent.click(screen.getByTestId('submit-compoff-btn'));

    await waitFor(() => {
      expect(screen.getByText('Worked date is required.')).toBeInTheDocument();
    });
  });

  it('shows error when worked hours < 4 (FR-39 credit rule)', async () => {
    renderPage();
    await waitFor(() => expect(compOffApi.getMyCompOffRequests).toHaveBeenCalled());

    fireEvent.change(screen.getByTestId('worked-date-input'), {
      target: { value: '2026-08-01' },
    });
    fireEvent.change(screen.getByTestId('worked-hours-input'), {
      target: { value: '2' },
    });

    fireEvent.click(screen.getByTestId('submit-compoff-btn'));

    await waitFor(() => {
      expect(screen.getByText(/Minimum 4 hours required/i)).toBeInTheDocument();
    });
  });

  it('shows "4h = 0.5 day" credit conversion in validation error text', async () => {
    renderPage();
    await waitFor(() => expect(compOffApi.getMyCompOffRequests).toHaveBeenCalled());

    fireEvent.change(screen.getByTestId('worked-date-input'), {
      target: { value: '2026-08-01' },
    });
    fireEvent.change(screen.getByTestId('worked-hours-input'), {
      target: { value: '2' },
    });

    fireEvent.click(screen.getByTestId('submit-compoff-btn'));

    await waitFor(() => {
      expect(screen.getByText(/4h = 0\.5 day/i)).toBeInTheDocument();
    });
  });

  it('does not call submitCompOffRequest when form is invalid', async () => {
    renderPage();
    await waitFor(() => expect(compOffApi.getMyCompOffRequests).toHaveBeenCalled());

    fireEvent.click(screen.getByTestId('submit-compoff-btn'));

    expect(compOffApi.submitCompOffRequest).not.toHaveBeenCalled();
  });
});

// ---------------------------------------------------------------------------
// UT-FE-46 — Submit success
// ---------------------------------------------------------------------------

describe('UT-FE-46: CompOffPage submits request successfully', () => {
  it('calls submitCompOffRequest with workedDate + workedHours and shows success', async () => {
    vi.mocked(compOffApi.submitCompOffRequest).mockResolvedValueOnce(makeRequest());
    vi.mocked(compOffApi.getMyCompOffRequests)
      .mockResolvedValueOnce([]) // initial load
      .mockResolvedValueOnce([makeRequest()]); // refetch after submit

    renderPage();
    await waitFor(() => expect(compOffApi.getMyCompOffRequests).toHaveBeenCalledTimes(1));

    fireEvent.change(screen.getByTestId('worked-date-input'), {
      target: { value: '2026-08-01' },
    });
    fireEvent.change(screen.getByTestId('worked-hours-input'), {
      target: { value: '8' },
    });

    fireEvent.click(screen.getByTestId('submit-compoff-btn'));

    await waitFor(() => {
      expect(compOffApi.submitCompOffRequest).toHaveBeenCalledWith({
        workedDate: '2026-08-01',
        workedHours: 8,
      });
    });

    await waitFor(() => {
      expect(
        screen.getByText(/Comp-off request submitted successfully/i),
      ).toBeInTheDocument();
    });
  });

  it('resets form fields after successful submit', async () => {
    vi.mocked(compOffApi.submitCompOffRequest).mockResolvedValueOnce(makeRequest());
    vi.mocked(compOffApi.getMyCompOffRequests)
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce([makeRequest()]);

    renderPage();
    await waitFor(() => expect(compOffApi.getMyCompOffRequests).toHaveBeenCalledTimes(1));

    fireEvent.change(screen.getByTestId('worked-date-input'), {
      target: { value: '2026-08-01' },
    });
    fireEvent.change(screen.getByTestId('worked-hours-input'), {
      target: { value: '8' },
    });

    fireEvent.click(screen.getByTestId('submit-compoff-btn'));

    await waitFor(() => {
      expect(screen.getByText(/Comp-off request submitted successfully/i)).toBeInTheDocument();
    });

    // After reset, date input should be empty
    expect((screen.getByTestId('worked-date-input') as HTMLInputElement).value).toBe('');
  });
});

// ---------------------------------------------------------------------------
// UT-FE-47 — Submit failure shows error alert
// ---------------------------------------------------------------------------

describe('UT-FE-47: CompOffPage shows error when submit fails', () => {
  it('shows error alert when submitCompOffRequest rejects', async () => {
    vi.mocked(compOffApi.submitCompOffRequest).mockRejectedValueOnce(
      new Error('Server error'),
    );

    renderPage();
    await waitFor(() => expect(compOffApi.getMyCompOffRequests).toHaveBeenCalled());

    fireEvent.change(screen.getByTestId('worked-date-input'), {
      target: { value: '2026-08-01' },
    });
    fireEvent.change(screen.getByTestId('worked-hours-input'), {
      target: { value: '8' },
    });

    fireEvent.click(screen.getByTestId('submit-compoff-btn'));

    await waitFor(() => {
      expect(screen.getByText(/Failed to submit comp-off request/i)).toBeInTheDocument();
    });
  });
});

// ---------------------------------------------------------------------------
// UT-FE-48 — My requests table renders
// ---------------------------------------------------------------------------

describe('UT-FE-48: CompOffPage shows my comp-off requests', () => {
  it('renders a request row with Pending status after data loads', async () => {
    const req = makeRequest({ id: 'cor-001', status: 'Pending' });
    vi.mocked(compOffApi.getMyCompOffRequests).mockResolvedValue([req]);

    renderPage();

    await waitFor(() => {
      expect(screen.getByText('Pending')).toBeInTheDocument();
    });
  });

  it('renders Approved and Rejected status rows', async () => {
    vi.mocked(compOffApi.getMyCompOffRequests).mockResolvedValue([
      makeRequest({ id: 'cor-001', status: 'Approved' }),
      makeRequest({ id: 'cor-002', status: 'Rejected' }),
    ]);

    renderPage();

    await waitFor(() => {
      expect(screen.getByText('Approved')).toBeInTheDocument();
      expect(screen.getByText('Rejected')).toBeInTheDocument();
    });
  });
});

// ---------------------------------------------------------------------------
// UT-FE-49 — Credits panel renders
// ---------------------------------------------------------------------------

describe('UT-FE-49: CompOffPage shows comp-off credits panel', () => {
  it('shows credit days balance when credits exist', async () => {
    vi.mocked(compOffApi.getMyCompOffCredits).mockResolvedValue([
      makeCredit({ creditDays: 1, usedDays: 0 }),
    ]);

    renderPage();

    await waitFor(() => {
      // credit chip or table should have the credit days value
      expect(screen.getAllByText(/1/).length).toBeGreaterThan(0);
    });
  });

  it('shows expiry info for each credit row', async () => {
    const futureExpiry = new Date(Date.now() + 90 * 24 * 60 * 60 * 1000).toISOString();
    vi.mocked(compOffApi.getMyCompOffCredits).mockResolvedValue([
      makeCredit({ creditDays: 1, usedDays: 0, expiresAt: futureExpiry }),
    ]);

    renderPage();

    await waitFor(() => {
      // Should show "Xd left" chip
      expect(screen.getByText(/d left/i)).toBeInTheDocument();
    });
  });

  it('shows Expired chip when credit has passed expiry', async () => {
    const pastExpiry = new Date(Date.now() - 1 * 24 * 60 * 60 * 1000).toISOString();
    vi.mocked(compOffApi.getMyCompOffCredits).mockResolvedValue([
      makeCredit({ creditDays: 1, usedDays: 0, expiresAt: pastExpiry }),
    ]);

    renderPage();

    await waitFor(() => {
      expect(screen.getByText('Expired')).toBeInTheDocument();
    });
  });
});
