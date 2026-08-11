import { test, expect } from '@playwright/test';

test.use({ baseURL: process.env.BASE_URL ?? 'http://localhost:5173' });

test(
  'E2E-14 @smoke: seeded SuperAdmin login and dashboard access',
  async ({ page }) => {
    const adminEmail    = process.env.SUPERADMIN_EMAIL    ?? 'admin@lms.com';
    const adminPassword = process.env.SUPERADMIN_PASSWORD ?? 'Admin@1234';

    await page.goto('/login');

    await expect(
      page.getByRole('heading', { name: /sign in|log in/i }),
    ).toBeVisible({ timeout: 10_000 });

    await page.getByLabel(/email/i).fill(adminEmail);
    await page.getByLabel(/password/i).fill(adminPassword);
    await page.getByRole('button', { name: /sign in|log in|login/i }).click();

    await page.waitForURL(/\/dashboard|\/home/i, { timeout: 10_000 });

    await expect(
      page
        .locator('[data-testid="super-admin-dashboard"]')
        .or(page.getByRole('main')),
    ).toBeVisible({ timeout: 5_000 });

    const localToken   = await page.evaluate(() => localStorage.getItem('access_token'));
    const sessionToken = await page.evaluate(() => sessionStorage.getItem('access_token'));
    expect(localToken,   'access_token must not be in localStorage').toBeNull();
    expect(sessionToken, 'access_token must not be in sessionStorage').toBeNull();

    const localToken2   = await page.evaluate(() => localStorage.getItem('token'));
    const sessionToken2 = await page.evaluate(() => sessionStorage.getItem('token'));
    expect(localToken2,   'token must not be in localStorage').toBeNull();
    expect(sessionToken2, 'token must not be in sessionStorage').toBeNull();

    await expect(
      page
        .locator('[data-testid="stat-card"]')
        .or(page.locator('.MuiCard-root'))
        .first(),
    ).toBeVisible({ timeout: 5_000 });

    await expect(page).toHaveURL(/\/dashboard|\/home/i);
  },
);
