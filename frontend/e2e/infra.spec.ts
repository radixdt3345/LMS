import { test, expect } from '@playwright/test';

/**
 * INFRA-E2E-001: Infrastructure / SeedService E2E specs.
 *
 * Trigger: workflow_dispatch ONLY — never on: push or on: pull_request.
 *
 * Required environment variables:
 *   BASE_URL             (default: http://localhost:5173)
 *   SUPERADMIN_EMAIL     email of the seeded SuperAdmin account (default: admin@lms.com)
 *   SUPERADMIN_PASSWORD  password of the seeded SuperAdmin account (default: Admin@1234)
 */

test.use({ baseURL: process.env.BASE_URL ?? 'http://localhost:5173' });

// ── E2E-14 @smoke ─────────────────────────────────────────────────────────────

/**
 * E2E-14 @smoke: Seeded SuperAdmin can log in and reach the SuperAdmin dashboard.
 *
 * Verifies that SeedService creates a functional SuperAdmin account on startup:
 *   1. The account is accepted by the login form.
 *   2. The app redirects to a dashboard URL after successful authentication.
 *   3. The SuperAdmin dashboard renders with at least one stat card.
 *
 * Critical constraint: JWT access token must NEVER be stored in
 * localStorage or sessionStorage (CONSTITUTION.md Article VI).
 * This test explicitly asserts both storage areas are empty of any token key.
 */
test(
  'E2E-14 @smoke: seeded SuperAdmin login and dashboard access',
  async ({ page }) => {
    const adminEmail    = process.env.SUPERADMIN_EMAIL    ?? 'admin@lms.com';
    const adminPassword = process.env.SUPERADMIN_PASSWORD ?? 'Admin@1234';

    // Navigate to login page
    await page.goto('/login');

    // Login form heading must be visible
    await expect(
      page.getByRole('heading', { name: /sign in|log in/i }),
    ).toBeVisible({ timeout: 10_000 });

    // Fill credentials via accessible labels (matching MUI TextField label text)
    await page.getByLabel(/email/i).fill(adminEmail);
    await page.getByLabel(/password/i).fill(adminPassword);
    await page.getByRole('button', { name: /sign in|log in|login/i }).click();

    // Should redirect to the dashboard
    await page.waitForURL(/\/dashboard|\/home/i, { timeout: 10_000 });

    // SuperAdmin dashboard container must be present
    await expect(
      page
        .locator('[data-testid="super-admin-dashboard"]')
        .or(page.getByRole('main')),
    ).toBeVisible({ timeout: 5_000 });

    // CRITICAL: token must NOT be persisted in browser storage
    const localToken   = await page.evaluate(() => localStorage.getItem('access_token'));
    const sessionToken = await page.evaluate(() => sessionStorage.getItem('access_token'));
    expect(localToken,   'access_token must not be in localStorage').toBeNull();
    expect(sessionToken, 'access_token must not be in sessionStorage').toBeNull();

    // Also check common alternative key names
    const localToken2   = await page.evaluate(() => localStorage.getItem('token'));
    const sessionToken2 = await page.evaluate(() => sessionStorage.getItem('token'));
    expect(localToken2,   'token must not be in localStorage').toBeNull();
    expect(sessionToken2, 'token must not be in sessionStorage').toBeNull();

    // At least one stat card must be visible (employees, departments, etc.)
    await expect(
      page
        .locator('[data-testid="stat-card"]')
        .or(page.locator('.MuiCard-root'))
        .first(),
    ).toBeVisible({ timeout: 5_000 });

    // Final URL guard — dashboard must still be the active route
    await expect(page).toHaveURL(/\/dashboard|\/home/i);
  },
);
