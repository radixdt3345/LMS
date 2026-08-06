import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { Provider } from 'react-redux';
import { BrowserRouter, MemoryRouter } from 'react-router-dom';
import { configureStore } from '@reduxjs/toolkit';
import authReducer from '../auth/authSlice';
import notificationsReducer from '../store/notifications/notificationsSlice';
import NewLeavePage from '../pages/leaves/NewLeavePage';
import * as leaveRequestsApi from '../api/leaveRequestsApi';

// ---------------------------------------------------------------------------
// Module mocks
// ---------------------------------------------------------------------------

vi.mock('../api/leaveRequestsApi', () => ({
  getLeaveTypes: vi.fn(),
  createLeaveRequest: vi.fn(),
  submitLeaveRequest: vi.fn(),
  previewLeaveDays: vi.fn(),
}));

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual<typeof import('react-router-dom')>('react-router-dom');
  return { ...actual, useNavigate: () => vi.fn() };
});

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

const LEAVE_TYPES: leaveRequestsApi.LeaveTypeDto[] = [
  { id: 'lt-annual', name: 'Annual Leave', requiresDocument: false, isUnpaid: false },
  { id: 'lt-sick', name: 'Sick Leave', requiresDocument: true, isUnpaid: false },
];

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
        <NewLeavePage />
      </BrowserRouter>
    </Provider>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(leaveRequestsApi.getLeaveTypes).mockResolvedValue(LEAVE_TYPES);
  vi.mocked(leaveRequestsApi.previewLeaveDays).mockResolvedValue({ computed_days: 1 });
});

// ---------------------------------------------------------------------------
// UT-FE-01 — Page renders all required fields
// ---------------------------------------------------------------------------

describe('UT-FE-01: NewLeavePage renders required form fields', () => {
  it('renders start date, end date, leave type select, reason, and submit button', async () => {
    renderPage();

    expect(screen.getByTestId('start-date-input')).toBeInTheDocument();
    expect(screen.getByTestId('end-date-input')).toBeInTheDocument();
    expect(screen.getByTestId('reason-textarea')).toBeInTheDocument();
    expect(screen.getByTestId('submit-button')).toBeInTheDocument();

    // Leave types load in dropdown
    await waitFor(() => {
      expect(screen.getByLabelText(/Leave Type/i)).toBeInTheDocument();
    });
  });
});

// ---------------------------------------------------------------------------
// UT-FE-02 — Leave type dropdown loads options
// ---------------------------------------------------------------------------

describe('UT-FE-02: Leave type dropdown populates from API', () => {
  it('shows leave type options after API resolves', async () => {
    renderPage();
    await waitFor(() => {
      expect(leaveRequestsApi.getLeaveTypes).toHaveBeenCalledOnce();
    });
  });

  it('shows error alert when getLeaveTypes fails', async () => {
    vi.mocked(leaveRequestsApi.getLeaveTypes).mockRejectedValueOnce(new Error('net'));
    renderPage();
    await waitFor(() => {
      expect(screen.getByText(/Failed to load leave types/i)).toBeInTheDocument();
    });
  });
});

// ---------------------------------------------------------------------------
// UT-FE-03 — Validation fires on empty submit
// ---------------------------------------------------------------------------

describe('UT-FE-03: Validation errors shown on empty submit', () => {
  it('shows validation errors for all required fields when submit clicked without data', async () => {
    renderPage();
    await waitFor(() => expect(leaveRequestsApi.getLeaveTypes).toHaveBeenCalled());

    fireEvent.click(screen.getByTestId('submit-button'));

    await waitFor(() => {
      expect(screen.getByText('Start date is required.')).toBeInTheDocument();
      expect(screen.getByText('End date is required.')).toBeInTheDocument();
      expect(screen.getByText('Please select a leave type.')).toBeInTheDocument();
      expect(screen.getByText('Reason is required.')).toBeInTheDocument();
    });
  });

  it('does not call createLeaveRequest when form is invalid', async () => {
    renderPage();
    await waitFor(() => expect(leaveRequestsApi.getLeaveTypes).toHaveBeenCalled());

    fireEvent.click(screen.getByTestId('submit-button'));
    expect(leaveRequestsApi.createLeaveRequest).not.toHaveBeenCalled();
  });
});

// ---------------------------------------------------------------------------
// UT-FE-04 — Half-day toggle only visible when single day selected (FR-39)
// ---------------------------------------------------------------------------

describe('UT-FE-04: Half-day toggle (FR-39)', () => {
  it('half-day switch is NOT shown when no dates are set', async () => {
    renderPage();
    await waitFor(() => expect(leaveRequestsApi.getLeaveTypes).toHaveBeenCalled());
    expect(screen.queryByTestId('half-day-switch')).not.toBeInTheDocument();
  });

  it('half-day switch appears when start == end (single day)', async () => {
    renderPage();
    await waitFor(() => expect(leaveRequestsApi.getLeaveTypes).toHaveBeenCalled());

    fireEvent.change(screen.getByTestId('start-date-input'), {
      target: { value: '2026-08-10' },
    });
    fireEvent.change(screen.getByTestId('end-date-input'), {
      target: { value: '2026-08-10' },
    });

    await waitFor(() => {
      expect(screen.getByTestId('half-day-switch')).toBeInTheDocument();
    });
  });

  it('half-day switch disappears when end date becomes different from start', async () => {
    renderPage();
    await waitFor(() => expect(leaveRequestsApi.getLeaveTypes).toHaveBeenCalled());

    fireEvent.change(screen.getByTestId('start-date-input'), {
      target: { value: '2026-08-10' },
    });
    fireEvent.change(screen.getByTestId('end-date-input'), {
      target: { value: '2026-08-10' },
    });

    await waitFor(() => {
      expect(screen.getByTestId('half-day-switch')).toBeInTheDocument();
    });

    // Change end date to make it multi-day → switch disappears
    fireEvent.change(screen.getByTestId('end-date-input'), {
      target: { value: '2026-08-12' },
    });

    await waitFor(() => {
      expect(screen.queryByTestId('half-day-switch')).not.toBeInTheDocument();
    });
  });

  it('toggling half-day switch updates computed-days chip to 0.5', async () => {
    vi.mocked(leaveRequestsApi.previewLeaveDays).mockResolvedValue({ computed_days: 1 });
    renderPage();
    await waitFor(() => expect(leaveRequestsApi.getLeaveTypes).toHaveBeenCalled());

    fireEvent.change(screen.getByTestId('start-date-input'), {
      target: { value: '2026-08-10' },
    });
    fireEvent.change(screen.getByTestId('end-date-input'), {
      target: { value: '2026-08-10' },
    });

    // Wait for the switch and chip
    await waitFor(() => {
      expect(screen.getByTestId('half-day-switch')).toBeInTheDocument();
    });
    await waitFor(() => {
      expect(screen.getByTestId('computed-days-chip')).toBeInTheDocument();
    });

    // Toggle half-day ON
    const switchInput = screen.getByTestId('half-day-switch');
    fireEvent.click(switchInput);

    await waitFor(() => {
      expect(screen.getByTestId('computed-days-chip')).toHaveTextContent('0.5');
    });
  });
});

// ---------------------------------------------------------------------------
// UT-FE-05 — Document URL field shows only when leave type requires it
// ---------------------------------------------------------------------------

describe('UT-FE-05: Document URL field conditional visibility', () => {
  it('document URL input NOT shown when requiresDocument is false', async () => {
    renderPage();
    await waitFor(() => expect(leaveRequestsApi.getLeaveTypes).toHaveBeenCalled());
    expect(screen.queryByTestId('document-url-input')).not.toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// UT-FE-06 — Submit flow: create draft → submit → navigate
// ---------------------------------------------------------------------------

describe('UT-FE-06: Submit flow calls create then submit', () => {
  it('calls createLeaveRequest and submitLeaveRequest with correct args on valid form', async () => {
    const draftId = 'draft-uuid-123';
    vi.mocked(leaveRequestsApi.createLeaveRequest).mockResolvedValueOnce({
      id: draftId,
      leaveTypeId: 'lt-annual',
      leaveTypeName: 'Annual Leave',
      startDate: '2026-08-10',
      endDate: '2026-08-12',
      computedDays: 3,
      status: 'Draft',
      reason: 'Family vacation',
      documentUrl: null,
      isRetroactive: false,
      isHalfDay: false,
      createdAt: new Date().toISOString(),
    });
    vi.mocked(leaveRequestsApi.submitLeaveRequest).mockResolvedValueOnce({
      id: draftId,
      leaveTypeId: 'lt-annual',
      leaveTypeName: 'Annual Leave',
      startDate: '2026-08-10',
      endDate: '2026-08-12',
      computedDays: 3,
      status: 'Pending',
      reason: 'Family vacation',
      documentUrl: null,
      isRetroactive: false,
      isHalfDay: false,
      createdAt: new Date().toISOString(),
    });

    renderPage();
    await waitFor(() => expect(leaveRequestsApi.getLeaveTypes).toHaveBeenCalled());

    fireEvent.change(screen.getByTestId('start-date-input'), {
      target: { value: '2026-08-10' },
    });
    fireEvent.change(screen.getByTestId('end-date-input'), {
      target: { value: '2026-08-12' },
    });
    fireEvent.change(screen.getByTestId('reason-textarea'), {
      target: { value: 'Family vacation' },
    });

    fireEvent.click(screen.getByTestId('submit-button'));

    await waitFor(() => {
      expect(leaveRequestsApi.createLeaveRequest).toHaveBeenCalledWith(
        expect.objectContaining({
          startDate: '2026-08-10',
          endDate: '2026-08-12',
          reason: 'Family vacation',
          isHalfDay: false,
        }),
      );
    });

    await waitFor(() => {
      expect(leaveRequestsApi.submitLeaveRequest).toHaveBeenCalledWith(draftId);
    });
  });

  it('passes isHalfDay: true when half-day toggle is on for single-day request', async () => {
    const draftId = 'half-day-draft';
    vi.mocked(leaveRequestsApi.createLeaveRequest).mockResolvedValueOnce({
      id: draftId,
      leaveTypeId: 'lt-annual',
      leaveTypeName: 'Annual Leave',
      startDate: '2026-08-10',
      endDate: '2026-08-10',
      computedDays: 0.5,
      status: 'Draft',
      reason: 'Short errand',
      documentUrl: null,
      isRetroactive: false,
      isHalfDay: true,
      createdAt: new Date().toISOString(),
    });
    vi.mocked(leaveRequestsApi.submitLeaveRequest).mockResolvedValue({
      id: draftId,
      leaveTypeId: 'lt-annual',
      leaveTypeName: 'Annual Leave',
      startDate: '2026-08-10',
      endDate: '2026-08-10',
      computedDays: 0.5,
      status: 'Pending',
      reason: 'Short errand',
      documentUrl: null,
      isRetroactive: false,
      isHalfDay: true,
      createdAt: new Date().toISOString(),
    });

    renderPage();
    await waitFor(() => expect(leaveRequestsApi.getLeaveTypes).toHaveBeenCalled());

    fireEvent.change(screen.getByTestId('start-date-input'), {
      target: { value: '2026-08-10' },
    });
    fireEvent.change(screen.getByTestId('end-date-input'), {
      target: { value: '2026-08-10' },
    });
    fireEvent.change(screen.getByTestId('reason-textarea'), {
      target: { value: 'Short errand' },
    });

    await waitFor(() => expect(screen.getByTestId('half-day-switch')).toBeInTheDocument());
    fireEvent.click(screen.getByTestId('half-day-switch'));

    fireEvent.click(screen.getByTestId('submit-button'));

    await waitFor(() => {
      expect(leaveRequestsApi.createLeaveRequest).toHaveBeenCalledWith(
        expect.objectContaining({ isHalfDay: true }),
      );
    });
  });

  it('shows error alert when createLeaveRequest throws', async () => {
    vi.mocked(leaveRequestsApi.createLeaveRequest).mockRejectedValueOnce(new Error('500'));

    renderPage();
    await waitFor(() => expect(leaveRequestsApi.getLeaveTypes).toHaveBeenCalled());

    fireEvent.change(screen.getByTestId('start-date-input'), {
      target: { value: '2026-08-10' },
    });
    fireEvent.change(screen.getByTestId('end-date-input'), {
      target: { value: '2026-08-12' },
    });
    fireEvent.change(screen.getByTestId('reason-textarea'), {
      target: { value: 'Vacation' },
    });

    fireEvent.click(screen.getByTestId('submit-button'));

    await waitFor(() => {
      expect(
        screen.getByText(/Failed to submit leave request/i),
      ).toBeInTheDocument();
    });
  });
});
