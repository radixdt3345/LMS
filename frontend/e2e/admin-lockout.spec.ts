import { test, expect } from '@playwright/test';

test.describe('Admin — Locked Account Management @smoke', () => {
  test.beforeEach(async ({ page }) => {
    const hrEmail = process.env.E2E_HR_ADMIN_EMAIL ?? 'hr@lms.local';
    const hrPassword = process.env.E2E_HR_ADMIN_PASSWORD ?? 'HrAdmin!2026';
    await page.goto('/login');
    await page.getByLabel(/email/i).fill(hrEmail);
    await page.getByLabel(/password/i).fill(hrPassword);
    await page.getByRole('button', { name: /login|sign in/i }).click();
    await page.waitForURL('**/dashboard', { timeout: 10_000 });
  });

  test('E2E-3 @smoke: Unlock locked account — row disappears and success toast shown', async ({ page }) => {
    await page.goto('/admin/users/locked');
    const table = page.getByRole('table');
    await expect(table).toBeVisible({ timeout: 10_000 });
    const rows = table.getByRole('row');
    const rowCount = await rows.count();
    expect(rowCount).toBeGreaterThan(1);
    const firstDataRow = rows.nth(1);
    const unlockButton = firstDataRow.getByRole('button', { name: /unlock/i });
    await expect(unlockButton).toBeVisible();
    await unlockButton.click();
    await expect(firstDataRow).not.toBeVisible({ timeout: 5_000 });
    const successFeedback = page
      .getByRole('alert')
      .or(page.getByText(/unlock.*success|account.*unlock|successfully unlock/i));
    await expect(successFeedback).toBeVisible({ timeout: 5_000 });
  });
});
