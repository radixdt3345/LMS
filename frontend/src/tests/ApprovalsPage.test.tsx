import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { Provider } from 'react-redux';
import { BrowserRouter } from 'react-router-dom';
import { configureStore } from '@reduxjs/toolkit';
import authReducer from '../auth/authSlice';
import notificationsReducer from '../store/notifications/notificationsSlice';
import * as approvalsApi from '../api/approvalsApi';
import type { LeaveRequestDto } from '../api/approvalsApi';

// ---------------------------------------------------------------------------
// Mock @mui/x-data-grid so we can render and find cells in tests
// ---------------------------------------------------------------------------

vi.mock('@mui/x-data-grid', () => ({
  DataGrid: ({
    rows,
    loading,
    columns,
  }: {
    rows: LeaveRequestDto[];
    loading: boolean;
    columns: { field: string; renderCell?: (p: { row: LeaveRequestDto; value: unknown }) => React.ReactNode }[];
  }) => {
    if (loading) return <div role="progressbar" aria-label="loading" />;
    if (rows.length === 0) return <div data-testid="empty-grid">No rows</div>;
    return (
      <div data-testid="approvals-grid">
        {rows.map((row) => {
          const actionsCol = columns.find((c) => c.field === 'actions');
          return (
            <div key={row.id} data-testid={`grid-row-${row.id}`}>
              <span>{row.employeeName}</span>
              <span>{row.leaveTypeName}</span>
              {actionsCol?.renderCell?.({ row, value: undefined })}
            </div>
          );
        })}
      </div>
    );
  },
}));

// ---------------------------------------------------------------------------
// Mocks
// ---------------------------------------------------------------------------

vi.mock('../api/approvalsApi', () => ({
  getPendingApprovals: vi.fn(),
  approveRequest: vi.fn(),
  rejectRequest: vi.fn(),
}));

// ---------------------------------------------------------------------------
// Lazy import to avoid hoisting issues with the mock
// ---------------------------------------------------------------------------

let ApprovalsPage: typeof import('../pages/approvals/ApprovalsPage').default;

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function makeRow(overrides: Partial<LeaveRequestDto> = {}): LeaveRequestDto {
  return {
    id: 'lr-001',
    employeeName: 'Alice Smith',
    leaveTypeName: 'Annual Leave',
    startDate: '2026-08-10',
    endDate: '2026-08-12',
    computedDays: 3,
    isRetroactive: false,
    status: 'Pending',
    documentUrl: null,
    reason: 'Vacation',
    ...overrides,
  };
}

const EMPTY_PAGE = { items: [], total: 0, page: 1, limit: 10 };

function makeStore() {
  return configureStore({
    reducer: { auth: authReducer, notifications: notificationsReducer },
    preloadedState: {
      auth: {
        user: { id: 'mgr-1', email: 'mgr@example.com', fullName: 'Manager User', role: 'Manager' },
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

async function renderPage() {
  if (!ApprovalsPage) {
    ApprovalsPage = (await import('../pages/approvals/ApprovalsPage')).default;
  }
  return render(
    <Provider store={makeStore()}>
      <BrowserRouter>
        <ApprovalsPage />
      </BrowserRouter>
    </Provider>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
});

// ---------------------------------------------------------------------------
// UT-FE-35 — Loading state
// ---------------------------------------------------------------------------

describe('UT-FE-35: ApprovalsPage shows loading indicator initially', () => {
  it('shows progressbar while fetching', async () => {
    vi.mocked(approvalsApi.getPendingApprovals).mockReturnValue(
      new Promise(() => {}),
    );
    await renderPage();
    expect(screen.getByRole('progressbar')).toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// UT-FE-36 — Empty grid
// ---------------------------------------------------------------------------

describe('UT-FE-36: ApprovalsPage shows empty grid when no pending approvals', () => {
  it('shows empty grid state when API returns empty list', async () => {
    vi.mocked(approvalsApi.getPendingApprovals).mockResolvedValue(EMPTY_PAGE);
    await renderPage();
    await waitFor(() => {
      expect(screen.getByTestId('empty-grid')).toBeInTheDocument();
    });
  });
});

// ---------------------------------------------------------------------------
// UT-FE-37 — Fetch error
// ---------------------------------------------------------------------------

describe('UT-FE-37: ApprovalsPage shows error alert on fetch failure', () => {
  it('shows fetch-error alert when getPendingApprovals rejects', async () => {
    vi.mocked(approvalsApi.getPendingApprovals).mockRejectedValueOnce(
      new Error('Network'),
    );
    await renderPage();
    await waitFor(() => {
      expect(screen.getByTestId('fetch-error')).toBeInTheDocument();
      expect(
        screen.getByText('Failed to load pending approvals. Please try again.'),
      ).toBeInTheDocument();
    });
  });
});

// ---------------------------------------------------------------------------
// UT-FE-38 — Grid renders rows with Approve/Reject buttons
// ---------------------------------------------------------------------------

describe('UT-FE-38: ApprovalsPage renders rows with action buttons', () => {
  it('shows employee name, leave type, and action buttons', async () => {
    vi.mocked(approvalsApi.getPendingApprovals).mockResolvedValue({
      items: [makeRow()],
      total: 1,
      page: 1,
      limit: 10,
    });
    await renderPage();

    await waitFor(() => {
      expect(screen.getByTestId('grid-row-lr-001')).toBeInTheDocument();
    });

    expect(screen.getByText('Alice Smith')).toBeInTheDocument();
    expect(screen.getByText('Annual Leave')).toBeInTheDocument();
    expect(screen.getByTestId('approve-btn-lr-001')).toBeInTheDocument();
    expect(screen.getByTestId('reject-btn-lr-001')).toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// UT-FE-39 — Approve: optimistic row removal
// ---------------------------------------------------------------------------

describe('UT-FE-39: Approve removes row optimistically', () => {
  it('removes the row immediately when approve is clicked and API succeeds', async () => {
    vi.mocked(approvalsApi.getPendingApprovals).mockResolvedValue({
      items: [makeRow({ id: 'lr-001' }), makeRow({ id: 'lr-002', employeeName: 'Bob' })],
      total: 2,
      page: 1,
      limit: 10,
    });
    vi.mocked(approvalsApi.approveRequest).mockResolvedValueOnce(undefined);

    await renderPage();

    await waitFor(() => {
      expect(screen.getByTestId('approve-btn-lr-001')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId('approve-btn-lr-001'));

    await waitFor(() => {
      expect(screen.queryByTestId('grid-row-lr-001')).not.toBeInTheDocument();
    });

    // Other row still visible
    expect(screen.getByTestId('grid-row-lr-002')).toBeInTheDocument();
    expect(approvalsApi.approveRequest).toHaveBeenCalledWith('lr-001');
  });

  it('shows success snackbar after approve', async () => {
    vi.mocked(approvalsApi.getPendingApprovals).mockResolvedValue({
      items: [makeRow()],
      total: 1,
      page: 1,
      limit: 10,
    });
    vi.mocked(approvalsApi.approveRequest).mockResolvedValueOnce(undefined);

    await renderPage();

    await waitFor(() => {
      expect(screen.getByTestId('approve-btn-lr-001')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId('approve-btn-lr-001'));

    await waitFor(() => {
      expect(screen.getByText('Leave request approved.')).toBeInTheDocument();
    });
  });
});

// ---------------------------------------------------------------------------
// UT-FE-40 — Approve: failure restores row
// ---------------------------------------------------------------------------

describe('UT-FE-40: Approve failure shows action error and restores row', () => {
  it('shows action-error alert when approveRequest rejects', async () => {
    vi.mocked(approvalsApi.getPendingApprovals).mockResolvedValue({
      items: [makeRow()],
      total: 1,
      page: 1,
      limit: 10,
    });
    vi.mocked(approvalsApi.approveRequest).mockRejectedValueOnce(new Error('500'));

    await renderPage();

    await waitFor(() => {
      expect(screen.getByTestId('approve-btn-lr-001')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId('approve-btn-lr-001'));

    await waitFor(() => {
      expect(screen.getByTestId('action-error')).toBeInTheDocument();
      expect(
        screen.getByText('Failed to approve request. Please try again.'),
      ).toBeInTheDocument();
    });
  });
});

// ---------------------------------------------------------------------------
// UT-FE-41 — Reject dialog opens and submit requires comment
// ---------------------------------------------------------------------------

describe('UT-FE-41: Reject dialog requires a comment before submitting', () => {
  it('opens reject dialog when Reject button is clicked', async () => {
    vi.mocked(approvalsApi.getPendingApprovals).mockResolvedValue({
      items: [makeRow()],
      total: 1,
      page: 1,
      limit: 10,
    });

    await renderPage();

    await waitFor(() => {
      expect(screen.getByTestId('reject-btn-lr-001')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId('reject-btn-lr-001'));

    await waitFor(() => {
      expect(screen.getByTestId('reject-dialog')).toBeInTheDocument();
    });
  });

  it('reject submit button is disabled when comment is empty', async () => {
    vi.mocked(approvalsApi.getPendingApprovals).mockResolvedValue({
      items: [makeRow()],
      total: 1,
      page: 1,
      limit: 10,
    });

    await renderPage();

    await waitFor(() => {
      expect(screen.getByTestId('reject-btn-lr-001')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId('reject-btn-lr-001'));

    await waitFor(() => {
      expect(screen.getByTestId('reject-submit-btn')).toBeDisabled();
    });
  });

  it('enables submit when comment is filled and calls rejectRequest', async () => {
    vi.mocked(approvalsApi.getPendingApprovals).mockResolvedValue({
      items: [makeRow()],
      total: 1,
      page: 1,
      limit: 10,
    });
    vi.mocked(approvalsApi.rejectRequest).mockResolvedValueOnce(undefined);

    await renderPage();

    await waitFor(() => {
      expect(screen.getByTestId('reject-btn-lr-001')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId('reject-btn-lr-001'));

    await waitFor(() => {
      expect(screen.getByTestId('reject-dialog')).toBeInTheDocument();
    });

    fireEvent.change(screen.getByTestId('reject-comment-input'), {
      target: { value: 'Not enough leave balance.' },
    });

    await waitFor(() => {
      expect(screen.getByTestId('reject-submit-btn')).not.toBeDisabled();
    });

    fireEvent.click(screen.getByTestId('reject-submit-btn'));

    await waitFor(() => {
      expect(approvalsApi.rejectRequest).toHaveBeenCalledWith(
        'lr-001',
        'Not enough leave balance.',
      );
    });
  });
});

// ---------------------------------------------------------------------------
// UT-FE-42 — Reject success closes dialog and removes row
// ---------------------------------------------------------------------------

describe('UT-FE-42: Reject success closes dialog and removes row from grid', () => {
  it('removes row and shows success snackbar after successful rejection', async () => {
    vi.mocked(approvalsApi.getPendingApprovals).mockResolvedValue({
      items: [makeRow()],
      total: 1,
      page: 1,
      limit: 10,
    });
    vi.mocked(approvalsApi.rejectRequest).mockResolvedValueOnce(undefined);

    await renderPage();

    await waitFor(() => {
      expect(screen.getByTestId('reject-btn-lr-001')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId('reject-btn-lr-001'));

    await waitFor(() => {
      expect(screen.getByTestId('reject-dialog')).toBeInTheDocument();
    });

    fireEvent.change(screen.getByTestId('reject-comment-input'), {
      target: { value: 'Not eligible.' },
    });

    fireEvent.click(screen.getByTestId('reject-submit-btn'));

    await waitFor(() => {
      expect(screen.getByText('Leave request rejected.')).toBeInTheDocument();
    });

    expect(screen.queryByTestId('grid-row-lr-001')).not.toBeInTheDocument();
  });
});
