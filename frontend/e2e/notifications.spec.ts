import { test, expect } from '@playwright/test';

/**
 * E2E notification specs.
 *
 * Trigger: workflow_dispatch ONLY — never on: push or on: pull_request.
 *
 * Required environment variables:
 *   BASE_URL             (default: http://localhost:5173)
 *   EMPLOYEE_EMAIL       email of a standard employee account
 *   EMPLOYEE_PASSWORD
 *   HRADMIN_EMAIL
 *   HRADMIN_PASSWORD
 */

test.use({ baseURL: process.env.BASE_URL ?? 'http://localhost:5173' });

// ── Helpers ──────────────────────────────────────────────────────────────────

async function loginAs(
  page: Parameters<Parameters<typeof test>[1]>[0]['page'],
  email: string,
  password: string,
): Promise<void> {
  await page.goto('/login');
  await page.getByLabel(/email/i).fill(email);
  await page.getByLabel(/password/i).fill(password);
  await page.getByRole('button', { name: /sign in|log in/i }).click();
  await page.waitForURL(/\/dashboard|\/home|\/leaves/i);
}

// ── E2E-11 @smoke ─────────────────────────────────────────────────────────────

/**
 * E2E-11: Notification bell badge increments after a leave request is approved.
 *
 * Flow:
 *   1. Employee logs in and submits a leave request.
 *   2. HR Admin logs in and approves that request at /approvals.
 *   3. Employee logs back in and navigates to /dashboard.
 *   4. Assert: the NotificationBell badge is visible with count >= 1,
 *              OR clicking the bell reveals an approval notification message.
 */
test(
  'E2E-11: notification bell badge increments after leave is approved @smoke',
  async ({ page }) => {
    const employeeEmail    = process.env.EMPLOYEE_EMAIL    ?? '';
    const employeePassword = process.env.EMPLOYEE_PASSWORD ?? '';
    const hrAdminEmail     = process.env.HRADMIN_EMAIL     ?? '';
    const hrAdminPassword  = process.env.HRADMIN_PASSWORD  ?? '';

    // ── Step 1: Employee submits a leave request ──────────────────────────────
    await loginAs(page, employeeEmail, employeePassword);
    await page.goto('/leaves/new');

    // Select first available leave type
    await page.getByLabel(/leave type/i).click();
    await page.getByRole('option').first().click();

    // Use dates 10+ days out to avoid business-day conflicts
    const start = new Date();
    start.setDate(start.getDate() + 10);
    const end = new Date(start);
    end.setDate(end.getDate() + 1);
    const fmt = (d: Date) => d.toISOString().split('T')[0];

    await page.getByLabel(/start date/i).fill(fmt(start));
    await page.getByLabel(/end date/i).fill(fmt(end));
    await page.getByLabel(/reason/i).fill('E2E-11 automated test — notification bell badge');

    await page.getByRole('button', { name: /submit/i }).click();

    // Assert: request was submitted successfully
    await expect(
      page.getByText(/leave request submitted/i),
    ).toBeVisible({ timeout: 10_000 });

    // ── Step 2: HR Admin approves the leave request ───────────────────────────
    await loginAs(page, hrAdminEmail, hrAdminPassword);
    await page.goto('/approvals');

    // Locate the pending row for this specific request and approve it
    const pendingRow = page
      .getByRole('row')
      .filter({ hasText: /E2E-11 automated test/i });

    await expect(pendingRow).toBeVisible({ timeout: 10_000 });
    await pendingRow.getByRole('button', { name: /approve/i }).click();

    // Confirm modal if present
    const confirmBtn = page.getByRole('button', { name: /confirm|yes|ok/i });
    if (await confirmBtn.isVisible({ timeout: 3_000 }).catch(() => false)) {
      await confirmBtn.click();
    }

    await expect(
      page.getByText(/approved/i).first(),
    ).toBeVisible({ timeout: 10_000 });

    // ── Step 3: Employee logs back in and checks notification bell ────────────
    await loginAs(page, employeeEmail, employeePassword);
    await page.goto('/dashboard');

    // The notification bell button — matches common aria-label patterns and MUI IconButton
    const bell = page.getByRole('button', { name: /notification|bell/i });
    await expect(bell).toBeVisible({ timeout: 10_000 });

    // Primary assertion: MUI Badge renders a .MuiBadge-badge element with a count
    const badge = bell.locator('.MuiBadge-badge');
    const badgeVisible = await badge.isVisible({ timeout: 5_000 }).catch(() => false);

    if (badgeVisible) {
      // Badge is visible — assert it shows a positive integer
      const badgeText = await badge.textContent();
      const count = parseInt(badgeText ?? '0', 10);
      expect(count).toBeGreaterThanOrEqual(1);
    } else {
      // Fallback: open the bell dropdown and verify an approval notification appears
      await bell.click();
      await expect(
        page.getByText(/leave.*approved|approved.*leave/i).first(),
      ).toBeVisible({ timeout: 8_000 });
    }
  },
);
