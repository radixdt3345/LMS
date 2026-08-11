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

vi.mock('../api/compOffApi', () => ({ submitCompOffRequest: vi.fn(), getMyCompOffRequests: vi.fn(), getMyCompOffCredits: vi.fn() }));

const NOW = new Date().toISOString();
function makeRequest(overrides: Partial<CompOffRequestDto> = {}): CompOffRequestDto {
  return { id: 'cor-001', employeeId: 'u1', workedDate: '2026-08-01', workedHours: 8, status: 'Pending', createdAt: NOW, updatedAt: NOW, ...overrides };
}
function makeCredit(overrides: Partial<CompOffCreditDto> = {}): CompOffCreditDto {
  return { id: 'coc-001', employeeId: 'u1', compOffRequestId: 'cor-001', creditDays: 1, usedDays: 0, expiresAt: new Date(Date.now() + 90 * 24 * 60 * 60 * 1000).toISOString(), createdAt: NOW, ...overrides };
}
function makeStore() {
  return configureStore({
    reducer: { auth: authReducer, notifications: notificationsReducer },
    preloadedState: { auth: { user: { id: 'u1', email: 'emp@example.com', fullName: 'Test User', role: 'Employee' }, accessToken: 'tok', refreshToken: 'ref', isAuthenticated: true, isLoading: false, error: null } },
    middleware: (gm) => gm({ thunk: true }),
  });
}
function renderPage() { return render(<Provider store={makeStore()}><BrowserRouter><CompOffPage /></BrowserRouter></Provider>); }
beforeEach(() => { vi.clearAllMocks(); vi.mocked(compOffApi.getMyCompOffRequests).mockResolvedValue([]); vi.mocked(compOffApi.getMyCompOffCredits).mockResolvedValue([]); });

describe('UT-FE-43: CompOffPage renders the submit form', () => {
  it('renders worked date, hours worked, and submit button', async () => {
    renderPage();
    await waitFor(() => expect(compOffApi.getMyCompOffRequests).toHaveBeenCalled());
    expect(screen.getByTestId('worked-date-input')).toBeInTheDocument();
    expect(screen.getByTestId('worked-hours-input')).toBeInTheDocument();
    expect(screen.getByTestId('submit-compoff-btn')).toBeInTheDocument();
  });
});

describe('UT-FE-44: CompOffPage shows error when data load fails', () => {
  it('shows load error when API rejects', async () => {
    vi.mocked(compOffApi.getMyCompOffRequests).mockRejectedValueOnce(new Error('net'));
    renderPage();
    await waitFor(() => { expect(screen.getByText(/Failed to load comp-off data/i)).toBeInTheDocument(); });
  });
});

describe('UT-FE-45: CompOffPage form validation', () => {
  it('shows error when worked date is missing on submit', async () => {
    renderPage();
    await waitFor(() => expect(compOffApi.getMyCompOffRequests).toHaveBeenCalled());
    fireEvent.click(screen.getByTestId('submit-compoff-btn'));
    await waitFor(() => { expect(screen.getByText('Worked date is required.')).toBeInTheDocument(); });
  });
  it('shows error when worked hours < 4 (FR-39 credit rule)', async () => {
    renderPage();
    await waitFor(() => expect(compOffApi.getMyCompOffRequests).toHaveBeenCalled());
    fireEvent.change(screen.getByTestId('worked-date-input'), { target: { value: '2026-08-01' } });
    fireEvent.change(screen.getByTestId('worked-hours-input'), { target: { value: '2' } });
    fireEvent.click(screen.getByTestId('submit-compoff-btn'));
    await waitFor(() => { expect(screen.getByText(/Minimum 4 hours required/i)).toBeInTheDocument(); });
  });
});

describe('UT-FE-46: CompOffPage submits request successfully', () => {
  it('calls submitCompOffRequest with workedDate + workedHours and shows success', async () => {
    vi.mocked(compOffApi.submitCompOffRequest).mockResolvedValueOnce(makeRequest());
    vi.mocked(compOffApi.getMyCompOffRequests).mockResolvedValueOnce([]).mockResolvedValueOnce([makeRequest()]);
    renderPage();
    await waitFor(() => expect(compOffApi.getMyCompOffRequests).toHaveBeenCalledTimes(1));
    fireEvent.change(screen.getByTestId('worked-date-input'), { target: { value: '2026-08-01' } });
    fireEvent.change(screen.getByTestId('worked-hours-input'), { target: { value: '8' } });
    fireEvent.click(screen.getByTestId('submit-compoff-btn'));
    await waitFor(() => { expect(compOffApi.submitCompOffRequest).toHaveBeenCalledWith({ workedDate: '2026-08-01', workedHours: 8 }); });
    await waitFor(() => { expect(screen.getByText(/Comp-off request submitted successfully/i)).toBeInTheDocument(); });
  });
});

describe('UT-FE-49: CompOffPage shows comp-off credits panel', () => {
  it('shows expiry info for each credit row', async () => {
    vi.mocked(compOffApi.getMyCompOffCredits).mockResolvedValue([makeCredit({ creditDays: 1, usedDays: 0 })]);
    renderPage();
    await waitFor(() => { expect(screen.getByText(/d left/i)).toBeInTheDocument(); });
  });
  it('shows Expired chip when credit has passed expiry', async () => {
    const pastExpiry = new Date(Date.now() - 1 * 24 * 60 * 60 * 1000).toISOString();
    vi.mocked(compOffApi.getMyCompOffCredits).mockResolvedValue([makeCredit({ creditDays: 1, usedDays: 0, expiresAt: pastExpiry })]);
    renderPage();
    await waitFor(() => { expect(screen.getByText('Expired')).toBeInTheDocument(); });
  });
});
