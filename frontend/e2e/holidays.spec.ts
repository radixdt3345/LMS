import { test, expect } from '@playwright/test';
import path from 'path';

test.describe('Holiday Management — CSV Import @smoke', () => {
  test.beforeEach(async ({ page }) => {
    const hrEmail = process.env.E2E_HR_ADMIN_EMAIL ?? 'hr@lms.local';
    const hrPassword = process.env.E2E_HR_ADMIN_PASSWORD ?? 'HrAdmin!2026';

    await page.goto('/login');
    await page.getByLabel(/email/i).fill(hrEmail);
    await page.getByLabel(/password/i).fill(hrPassword);
    await page.getByRole('button', { name: /login|sign in/i }).click();
    await page.waitForURL('**/dashboard', { timeout: 10_000 });
  });

  test('E2E-10 @smoke: Import CSV uploads new holidays to the calendar', async ({ page }) => {
    await page.goto('/holidays');
    await expect(page.getByRole('heading', { name: /holiday/i })).toBeVisible();

    const importButton = page.getByRole('button', { name: /import csv/i });
    await expect(importButton).toBeVisible();

    const csvPath = path.join(__dirname, 'fixtures', 'holidays-sample.csv');
    const [fileChooser] = await Promise.all([
      page.waitForEvent('filechooser'),
      importButton.click(),
    ]);
    await fileChooser.setFiles(csvPath);

    const confirmButton = page.getByRole('button', { name: /upload|confirm|import/i });
    if (await confirmButton.isVisible({ timeout: 2_000 }).catch(() => false)) {
      await confirmButton.click();
    }

    await expect(
      page.getByText(/republic day|independence day|import.*success/i)
    ).toBeVisible({ timeout: 15_000 });
  });
});
