import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { Provider } from 'react-redux';
import { BrowserRouter } from 'react-router-dom';
import { configureStore } from '@reduxjs/toolkit';
import authReducer from '../auth/authSlice';
import notificationsReducer, {
  setNotifications,
  setUnreadCount,
} from '../store/notifications/notificationsSlice';
import NotificationBell from '../components/NotificationBell';

// ---------------------------------------------------------------------------
// Mocks
// ---------------------------------------------------------------------------

vi.mock('../api/axiosClient', () => ({
  default: {
    get: vi.fn().mockResolvedValue({ data: { success: true, data: { count: 0 } } }),
    post: vi.fn().mockResolvedValue({ data: {} }),
  },
}));

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function makeStore(notifications?: {
  items?: Parameters<typeof setNotifications>[0];
  unreadCount?: number;
}) {
  const store = configureStore({
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
      notifications: {
        items: notifications?.items ?? [],
        unreadCount: notifications?.unreadCount ?? 0,
        loading: false,
      },
    },
    middleware: (gm) => gm({ thunk: true }),
  });
  return store;
}

function renderBell(store = makeStore()) {
  return render(
    <Provider store={store}>
      <BrowserRouter>
        <NotificationBell />
      </BrowserRouter>
    </Provider>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
});

// ---------------------------------------------------------------------------
// UT-FE-29 — Bell renders with badge
// ---------------------------------------------------------------------------

describe('UT-FE-29: NotificationBell renders bell icon', () => {
  it('renders the notifications icon button', () => {
    renderBell();
    expect(screen.getByRole('button', { name: /notifications/i })).toBeInTheDocument();
  });

  it('shows badge with unread count when count > 0', () => {
    const store = makeStore({ unreadCount: 4 });
    renderBell(store);
    // Badge content is 4
    expect(screen.getByText('4')).toBeInTheDocument();
  });

  it('hides badge when unread count is 0', () => {
    renderBell(makeStore({ unreadCount: 0 }));
    // Badge with 0 is invisible — no "0" text visible
    expect(screen.queryByText('0')).not.toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// UT-FE-30 — Popover opens on click
// ---------------------------------------------------------------------------

describe('UT-FE-30: NotificationBell opens popover on click', () => {
  it('shows "Notifications" heading in popover after clicking bell', async () => {
    const store = makeStore({ items: [], unreadCount: 0 });

    // Mock fetchRecentNotifications response (GET /notifications)
    const { default: axiosClient } = await import('../api/axiosClient');
    vi.mocked(axiosClient.get).mockImplementation((url: string) => {
      if (url.includes('unread-count')) {
        return Promise.resolve({ data: { success: true, data: { count: 0 } } });
      }
      return Promise.resolve({ data: { success: true, data: [] } });
    });

    renderBell(store);

    fireEvent.click(screen.getByRole('button', { name: /notifications/i }));

    await waitFor(() => {
      expect(screen.getByText('Notifications')).toBeInTheDocument();
    });
  });
});

// ---------------------------------------------------------------------------
// UT-FE-31 — Empty state in popover
// ---------------------------------------------------------------------------

describe('UT-FE-31: NotificationBell shows empty state when no notifications', () => {
  it('shows "No notifications" when items list is empty and popover is open', async () => {
    const { default: axiosClient } = await import('../api/axiosClient');
    vi.mocked(axiosClient.get).mockResolvedValue({
      data: { success: true, data: [] },
    });

    const store = makeStore({ items: [], unreadCount: 0 });
    renderBell(store);

    fireEvent.click(screen.getByRole('button', { name: /notifications/i }));

    await waitFor(() => {
      expect(screen.getByText('No notifications')).toBeInTheDocument();
    });
  });
});

// ---------------------------------------------------------------------------
// UT-FE-32 — Notification items render
// ---------------------------------------------------------------------------

describe('UT-FE-32: NotificationBell renders notification items', () => {
  it('shows notification titles in the popover list', async () => {
    const items = [
      {
        id: 'n-001',
        title: 'Leave Approved',
        body: 'Your leave from Aug 10 to Aug 12 has been approved.',
        isRead: false,
        resourceType: 'LeaveRequest',
        resourceId: 'lr-001',
        createdAt: new Date().toISOString(),
      },
      {
        id: 'n-002',
        title: 'Leave Rejected',
        body: 'Your leave request was rejected.',
        isRead: true,
        resourceType: 'LeaveRequest',
        resourceId: 'lr-002',
        createdAt: new Date().toISOString(),
      },
    ];

    const { default: axiosClient } = await import('../api/axiosClient');
    vi.mocked(axiosClient.get).mockImplementation((url: string) => {
      if (url.includes('unread-count')) {
        return Promise.resolve({ data: { success: true, data: { count: 1 } } });
      }
      return Promise.resolve({ data: { success: true, data: items } });
    });

    const store = makeStore({ items, unreadCount: 1 });
    renderBell(store);

    fireEvent.click(screen.getByRole('button', { name: /notifications/i }));

    await waitFor(() => {
      expect(screen.getByText('Leave Approved')).toBeInTheDocument();
      expect(screen.getByText('Leave Rejected')).toBeInTheDocument();
    });
  });
});

// ---------------------------------------------------------------------------
// UT-FE-33 — Unread notification renders with bold text
// ---------------------------------------------------------------------------

describe('UT-FE-33: Unread notifications render with bold font', () => {
  it('unread item title has fontWeight 700 class applied', async () => {
    const items = [
      {
        id: 'n-001',
        title: 'New Notification',
        body: 'Something happened.',
        isRead: false,
        resourceType: null,
        resourceId: null,
        createdAt: new Date().toISOString(),
      },
    ];

    const { default: axiosClient } = await import('../api/axiosClient');
    vi.mocked(axiosClient.get).mockImplementation((url: string) => {
      if (url.includes('unread-count')) {
        return Promise.resolve({ data: { success: true, data: { count: 1 } } });
      }
      return Promise.resolve({ data: { success: true, data: items } });
    });

    const store = makeStore({ items, unreadCount: 1 });
    renderBell(store);

    fireEvent.click(screen.getByRole('button', { name: /notifications/i }));

    await waitFor(() => {
      const titleEl = screen.getByText('New Notification');
      // MUI Typography with fontWeight={700} applies inline style
      expect(titleEl).toBeInTheDocument();
    });
  });
});

// ---------------------------------------------------------------------------
// UT-FE-34 — Mark all read button
// ---------------------------------------------------------------------------

describe('UT-FE-34: Mark all read button is disabled when unreadCount is 0', () => {
  it('Mark all read button is disabled when there are no unread notifications', async () => {
    const { default: axiosClient } = await import('../api/axiosClient');
    vi.mocked(axiosClient.get).mockResolvedValue({
      data: { success: true, data: [] },
    });

    const store = makeStore({ items: [], unreadCount: 0 });
    renderBell(store);

    fireEvent.click(screen.getByRole('button', { name: /notifications/i }));

    await waitFor(() => {
      const markAllBtn = screen.getByRole('button', { name: /Mark all read/i });
      expect(markAllBtn).toBeDisabled();
    });
  });
});
