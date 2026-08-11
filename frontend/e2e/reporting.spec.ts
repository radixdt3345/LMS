import { test, expect } from '@playwright/test';

test.use({ baseURL: process.env.BASE_URL ?? 'http://localhost:5173' });

test(
  'E2E-12 @smoke — Employee sees Employee dashboard',
  async ({ page }) => {
    const email    = process.env.EMPLOYEE_EMAIL    ?? 'employee@test.com';
    const password = process.env.EMPLOYEE_PASSWORD ?? 'Test@1234';

    await page.goto('/login');
    await page.getByLabel(/email/i).fill(email);
    await page.getByLabel(/password/i).fill(password);
    await page.getByRole('button', { name: /sign in|log in/i }).click();
    await page.waitForURL(/\/dashboard/, { timeout: 10_000 });

    await expect(
      page
        .locator('[data-testid="employee-dashboard"]')
        .or(page.locator('[data-testid="balance-section"]'))
        .or(page.getByText(/Leave Balance/i))
        .first(),
    ).toBeVisible({ timeout: 5_000 });

    await page.goto('/dashboard/hr');
    const url = page.url();
    expect(url, 'Employee must be redirected away from /dashboard/hr').not.toContain('/dashboard/hr');
  },
);

test(
  'E2E-12 @smoke — HR Admin sees HR dashboard',
  async ({ page }) => {
    const email    = process.env.HRADMIN_EMAIL    ?? 'hradmin@test.com';
    const password = process.env.HRADMIN_PASSWORD ?? 'Test@1234';

    await page.goto('/login');
    await page.getByLabel(/email/i).fill(email);
    await page.getByLabel(/password/i).fill(password);
    await page.getByRole('button', { name: /sign in|log in/i }).click();
    await page.waitForURL(/\/dashboard/, { timeout: 10_000 });

    await expect(
      page
        .locator('[data-testid="hr-dashboard"]')
        .or(page.locator('[data-testid="hr-stats"]'))
        .or(page.locator('.MuiCard-root'))
        .first(),
    ).toBeVisible({ timeout: 5_000 });
  },
);

test(
  'E2E-5 — SuperAdmin can access reports and utilization page',
  async ({ page }) => {
    const email    = process.env.SUPERADMIN_EMAIL    ?? 'admin@lms.com';
    const password = process.env.SUPERADMIN_PASSWORD ?? 'Admin@1234';

    await page.goto('/login');
    await page.getByLabel(/email/i).fill(email);
    await page.getByLabel(/password/i).fill(password);
    await page.getByRole('button', { name: /sign in|log in/i }).click();
    await page.waitForURL(/\/dashboard/, { timeout: 10_000 });

    await page.goto('/reports/utilization');
    await expect(page).toHaveURL(/reports\/utilization/);

    await expect(
      page
        .locator('canvas')
        .or(page.locator('table'))
        .or(page.locator('[data-testid="utilization-chart"]'))
        .or(page.locator('[data-testid="utilization-table"]'))
        .first(),
    ).toBeVisible({ timeout: 8_000 });

    await expect(
      page
        .getByRole('button', { name: /export|csv|download/i })
        .or(page.locator('[data-testid="export-csv-btn"]'))
        .first(),
    ).toBeVisible({ timeout: 5_000 });

    await page.goto('/reports/trends');
    await expect(page).toHaveURL(/reports\/trends/);

    await expect(
      page
        .locator('canvas')
        .or(page.locator('[data-testid="trends-chart"]'))
        .first(),
    ).toBeVisible({ timeout: 8_000 });
  },
);
