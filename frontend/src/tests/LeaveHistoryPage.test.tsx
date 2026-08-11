import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { Provider } from 'react-redux';
import { BrowserRouter } from 'react-router-dom';
import { configureStore } from '@reduxjs/toolkit';
import authReducer from '../auth/authSlice';
import notificationsReducer from '../store/notifications/notificationsSlice';
import LeaveHistoryPage from '../pages/leaves/LeaveHistoryPage';
import * as leaveRequestsApi from '../api/leaveRequestsApi';
import type { LeaveRequestDto } from '../api/leaveRequestsApi';

vi.mock('../api/leaveRequestsApi', () => ({ getMyLeaveRequests: vi.fn(), cancelLeaveRequest: vi.fn() }));

const NOW = new Date().toISOString();
function makeRow(overrides: Partial<LeaveRequestDto> = {}): LeaveRequestDto {
  return { id: 'lr-001', leaveTypeId: 'lt-annual', leaveTypeName: 'Annual Leave', startDate: '2026-08-10', endDate: '2026-08-12', computedDays: 3, status: 'Pending', reason: 'Vacation', documentUrl: null, isRetroactive: false, isHalfDay: false, createdAt: NOW, ...overrides };
}
const EMPTY_PAGE = { items: [], total: 0, page: 1, limit: 10 };
function oneRowPage(row: LeaveRequestDto) { return { items: [row], total: 1, page: 1, limit: 10 }; }
function makeStore() {
  return configureStore({
    reducer: { auth: authReducer, notifications: notificationsReducer },
    preloadedState: { auth: { user: { id: 'u1', email: 'emp@example.com', fullName: 'Test User', role: 'Employee' }, accessToken: 'tok', refreshToken: 'ref', isAuthenticated: true, isLoading: false, error: null } },
    middleware: (gm) => gm({ thunk: true }),
  });
}
function renderPage() { return render(<Provider store={makeStore()}><BrowserRouter><LeaveHistoryPage /></BrowserRouter></Provider>); }
beforeEach(() => { vi.clearAllMocks(); });

describe('UT-FE-07: LeaveHistoryPage shows loading spinner initially', () => {
  it('renders progress indicator while fetching', () => {
    vi.mocked(leaveRequestsApi.getMyLeaveRequests).mockReturnValue(new Promise(() => {}));
    renderPage();
    expect(screen.getByRole('progressbar')).toBeInTheDocument();
  });
});

describe('UT-FE-08: LeaveHistoryPage shows empty state when no requests', () => {
  it('renders "No leave requests found" and hides the table', async () => {
    vi.mocked(leaveRequestsApi.getMyLeaveRequests).mockResolvedValue(EMPTY_PAGE);
    renderPage();
    await waitFor(() => { expect(screen.getByTestId('empty-state')).toBeInTheDocument(); });
    expect(screen.queryByTestId('leave-history-table')).not.toBeInTheDocument();
  });
});

describe('UT-FE-09: LeaveHistoryPage renders leave request rows', () => {
  it('shows table with correct data after loading', async () => {
    vi.mocked(leaveRequestsApi.getMyLeaveRequests).mockResolvedValue(oneRowPage(makeRow()));
    renderPage();
    await waitFor(() => { expect(screen.getByTestId('leave-history-table')).toBeInTheDocument(); });
    expect(screen.getByText('Annual Leave')).toBeInTheDocument();
    expect(screen.getByTestId('leave-row-lr-001')).toBeInTheDocument();
  });
});

describe('UT-FE-10: Cancel button visibility by status', () => {
  it('shows cancel button for Pending row', async () => {
    vi.mocked(leaveRequestsApi.getMyLeaveRequests).mockResolvedValue(oneRowPage(makeRow({ status: 'Pending' })));
    renderPage();
    await waitFor(() => { expect(screen.getByTestId('cancel-btn-lr-001')).toBeInTheDocument(); });
  });
  it('hides cancel button for Approved row', async () => {
    vi.mocked(leaveRequestsApi.getMyLeaveRequests).mockResolvedValue(oneRowPage(makeRow({ status: 'Approved' })));
    renderPage();
    await waitFor(() => { expect(screen.getByTestId('leave-history-table')).toBeInTheDocument(); });
    expect(screen.queryByTestId('cancel-btn-lr-001')).not.toBeInTheDocument();
  });
});

describe('UT-FE-11: Cancel request — success flow', () => {
  it('calls cancelLeaveRequest and shows success snackbar', async () => {
    vi.mocked(leaveRequestsApi.getMyLeaveRequests).mockResolvedValueOnce(oneRowPage(makeRow({ status: 'Pending' }))).mockResolvedValueOnce(EMPTY_PAGE);
    vi.mocked(leaveRequestsApi.cancelLeaveRequest).mockResolvedValue(makeRow({ status: 'Cancelled' }));
    renderPage();
    await waitFor(() => { expect(screen.getByTestId('cancel-btn-lr-001')).toBeInTheDocument(); });
    fireEvent.click(screen.getByTestId('cancel-btn-lr-001'));
    await waitFor(() => { expect(leaveRequestsApi.cancelLeaveRequest).toHaveBeenCalledWith('lr-001'); });
    await waitFor(() => { expect(screen.getByText('Leave request cancelled.')).toBeInTheDocument(); });
  });
});

describe('UT-FE-13: Fetch error shows error alert', () => {
  it('shows error alert when getMyLeaveRequests rejects', async () => {
    vi.mocked(leaveRequestsApi.getMyLeaveRequests).mockRejectedValueOnce(new Error('Network error'));
    renderPage();
    await waitFor(() => { expect(screen.getByText('Failed to load leave history. Please try again.')).toBeInTheDocument(); });
  });
});
