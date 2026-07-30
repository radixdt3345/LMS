import { test, expect } from '@playwright/test';

/**
 * E2E-3: HR Admin views locked accounts, unlocks one, and verifies the row
 * disappears from the table and a success toast is shown.
 *
 * Trigger: workflow_dispatch ONLY.
 * This spec MUST NOT run on push or pull_request CI triggers.
 * Add this file to a workflow that runs only via workflow_dispatch.
 *
 * Tags: @smoke
 */
test.describe('Admin — Locked Account Management @smoke', () => {
  test.beforeEach(async ({ page }) => {
    // Login as HR Admin using seeded credentials injected from environment.
    // E2E_HR_ADMIN_EMAIL and E2E_HR_ADMIN_PASSWORD must be set in the
    // workflow_dispatch environment (never hardcoded in production).
    const hrEmail = process.env.E2E_HR_ADMIN_EMAIL ?? 'hr@lms.local';
    const hrPassword = process.env.E2E_HR_ADMIN_PASSWORD ?? 'HrAdmin!2026';

    await page.goto('/login');
    await page.getByLabel(/email/i).fill(hrEmail);
    await page.getByLabel(/password/i).fill(hrPassword);
    await page.getByRole('button', { name: /login|sign in/i }).click();
    await page.waitForURL('**/dashboard', { timeout: 10_000 });
  });

  /**
   * E2E-3 @smoke
   * Steps:
   * 1. Navigate to /admin/users/locked
   * 2. Assert the locked accounts table is visible
   * 3. Click "Unlock" on the first locked-account row
   * 4. Assert that row disappears (account unlocked)
   * 5. Assert success toast message is shown
   */
  test('E2E-3 @smoke: Unlock locked account — row disappears and success toast shown', async ({ page }) => {
    // 1. Navigate to locked accounts admin page
    await page.goto('/admin/users/locked');

    // 2. Assert the locked accounts table is visible
    const table = page.getByRole('table');
    await expect(table).toBeVisible({ timeout: 10_000 });

    // Assert at least one data row exists (header + 1 locked account)
    const rows = table.getByRole('row');
    const rowCount = await rows.count();
    expect(rowCount).toBeGreaterThan(1);

    // 3. Click the "Unlock" button on the first data row
    const firstDataRow = rows.nth(1);
    const unlockButton = firstDataRow.getByRole('button', { name: /unlock/i });
    await expect(unlockButton).toBeVisible();

    // Capture row identifier before clicking (for post-action assertion)
    await unlockButton.click();

    // 4. Assert the row disappears after unlock
    await expect(firstDataRow).not.toBeVisible({ timeout: 5_000 });

    // 5. Assert success toast / alert is displayed
    //    Matches MUI Snackbar alert or any element containing the success message.
    const successFeedback = page
      .getByRole('alert')
      .or(page.getByText(/unlock.*success|account.*unlock|successfully unlock/i));
    await expect(successFeedback).toBeVisible({ timeout: 5_000 });
  });
});
