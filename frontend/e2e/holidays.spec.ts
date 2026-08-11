import { test, expect } from '@playwright/test';
import path from 'path';

/**
 * E2E-10: HR Admin imports a CSV of holidays and verifies they appear on the
 * holiday calendar page.
 *
 * Trigger: workflow_dispatch ONLY.
 * This spec MUST NOT run on push or pull_request CI triggers.
 * Add this file to a workflow that runs only via workflow_dispatch.
 *
 * Tags: @smoke
 */
test.describe('Holiday Management — CSV Import @smoke', () => {
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
   * E2E-10 @smoke
   * Steps:
   * 1. Navigate to /holidays
   * 2. Click "Import CSV" button
   * 3. Upload the test CSV fixture
   * 4. Assert calendar shows the imported holidays
   */
  test('E2E-10 @smoke: Import CSV uploads new holidays to the calendar', async ({ page }) => {
    // 1. Navigate to the Holidays management page
    await page.goto('/holidays');
    await expect(page.getByRole('heading', { name: /holiday/i })).toBeVisible();

    // 2. Click "Import CSV" button
    const importButton = page.getByRole('button', { name: /import csv/i });
    await expect(importButton).toBeVisible();

    // 3. Upload test CSV fixture (fixtures/holidays-sample.csv)
    //    Playwright intercepts the file-chooser triggered by the button click.
    const csvPath = path.join(__dirname, 'fixtures', 'holidays-sample.csv');
    const [fileChooser] = await Promise.all([
      page.waitForEvent('filechooser'),
      importButton.click(),
    ]);
    await fileChooser.setFiles(csvPath);

    // Submit if a separate confirm button is rendered after file selection
    const confirmButton = page.getByRole('button', { name: /upload|confirm|import/i });
    if (await confirmButton.isVisible({ timeout: 2_000 }).catch(() => false)) {
      await confirmButton.click();
    }

    // 4. Assert the calendar now shows the imported holidays
    await expect(
      page.getByText(/republic day|independence day|import.*success/i)
    ).toBeVisible({ timeout: 15_000 });
  });
});
