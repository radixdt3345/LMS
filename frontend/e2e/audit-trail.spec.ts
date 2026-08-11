import { test, expect } from '@playwright/test';

/**
 * E2E-13: HR Admin views the audit trail and sees a leave approval entry.
 */

const BASE_URL = process.env.BASE_URL ?? 'http://localhost:5173';
const HRADMIN_EMAIL = process.env.HRADMIN_EMAIL;
const HRADMIN_PASSWORD = process.env.HRADMIN_PASSWORD;

test.describe('Audit Trail — HR Admin', () => {
  test.beforeEach(async ({ page }) => {
    if (!HRADMIN_EMAIL || !HRADMIN_PASSWORD) {
      throw new Error(
        'HRADMIN_EMAIL and HRADMIN_PASSWORD environment variables must be set before running E2E tests.'
      );
    }

    await page.goto(`${BASE_URL}/login`);
    await page.getByLabel(/email/i).fill(HRADMIN_EMAIL);
    await page.getByLabel(/password/i).fill(HRADMIN_PASSWORD);
    await page.getByRole('button', { name: /sign in|log in/i }).click();

    await page.waitForURL((url) => !url.pathname.includes('/login'), {
      timeout: 10_000,
    });
  });

  test(
    'E2E-13: HR Admin can view audit trail and see leave approval entry',
    async ({ page }) => {
      await page.goto(`${BASE_URL}/admin/audit`);
      await page.waitForLoadState('networkidle');

      const auditTable = page
        .getByRole('grid')
        .or(page.getByRole('table'))
        .first();
      await expect(auditTable).toBeVisible({ timeout: 10_000 });

      const entityTypeFilter = page.getByLabel(/entity.?type/i);
      if (await entityTypeFilter.isVisible()) {
        await entityTypeFilter.fill('LeaveRequest');
        await page.waitForTimeout(500);
      }

      const approvedRow = page
        .getByRole('row')
        .filter({ hasText: /approved/i })
        .first();
      await expect(approvedRow).toBeVisible({ timeout: 10_000 });

      await approvedRow.click();

      const detailPanel = page
        .getByRole('dialog')
        .or(page.locator('[data-testid="audit-detail"]'))
        .or(page.locator('.audit-detail'))
        .first();
      await expect(detailPanel).toBeVisible({ timeout: 5_000 });

      await expect(detailPanel).toContainText(/leave/i);

      const deleteButton = page.getByRole('button', { name: /delete/i });
      await expect(deleteButton).toHaveCount(0);
    }
  );
});
