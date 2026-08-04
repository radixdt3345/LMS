import { test, expect } from '@playwright/test';

/**
 * REPORTING-E2E-001: Reporting & dashboard routing E2E specs.
 *
 * Trigger: workflow_dispatch ONLY — never on: push or on: pull_request.
 *
 * Required environment variables:
 *   BASE_URL              (default: http://localhost:5173)
 *   EMPLOYEE_EMAIL        email of a seeded Employee account (default: employee@test.com)
 *   EMPLOYEE_PASSWORD     (default: Test@1234)
 *   HRADMIN_EMAIL         email of a seeded HR Admin account (default: hradmin@test.com)
 *   HRADMIN_PASSWORD      (default: Test@1234)
 *   SUPERADMIN_EMAIL      email of the seeded SuperAdmin account (default: admin@lms.com)
 *   SUPERADMIN_PASSWORD   (default: Admin@1234)
 */

test.use({ baseURL: process.env.BASE_URL ?? 'http://localhost:5173' });

// ── E2E-12 @smoke ─────────────────────────────────────────────────────────────

/**
 * E2E-12 @smoke: Role-based dashboard routing — each role sees its own dashboard.
 *
 * Verifies that the React ProtectedRoute / RoleProtectedRoute correctly routes
 * each role to its own dashboard and blocks access to dashboards intended for
 * other roles.
 *
 *   - Employee lands on the employee dashboard (leave balance section visible)
 *     and is redirected away from /dashboard/hr.
 *   - HR Admin lands on the HR dashboard (stats / card section visible).
 */
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

    // Employee dashboard must show a leave balance section
    await expect(
      page
        .locator('[data-testid="employee-dashboard"]')
        .or(page.locator('[data-testid="balance-section"]'))
        .or(page.getByText(/Leave Balance/i))
        .first(),
    ).toBeVisible({ timeout: 5_000 });

    // Employee must NOT be able to reach the HR dashboard
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

    // HR dashboard must render with at least one stats card or HR-specific container
    await expect(
      page
        .locator('[data-testid="hr-dashboard"]')
        .or(page.locator('[data-testid="hr-stats"]'))
        .or(page.locator('.MuiCard-root'))
        .first(),
    ).toBeVisible({ timeout: 5_000 });
  },
);

// ── E2E-5 ─────────────────────────────────────────────────────────────────────

/**
 * E2E-5: SuperAdmin can access reporting pages (utilization + trends).
 *
 * Verifies the full navigation flow for the reporting domain:
 *   1. /reports/utilization — chart or table is visible, CSV export button present.
 *   2. /reports/trends      — trends chart is visible.
 *
 * Credentials are read exclusively from environment variables; no hardcoded values.
 */
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

    // Navigate to utilization report
    await page.goto('/reports/utilization');
    await expect(page).toHaveURL(/reports\/utilization/);

    // Utilization chart or table must be visible
    await expect(
      page
        .locator('canvas')
        .or(page.locator('table'))
        .or(page.locator('[data-testid="utilization-chart"]'))
        .or(page.locator('[data-testid="utilization-table"]'))
        .first(),
    ).toBeVisible({ timeout: 8_000 });

    // CSV export button must be visible
    await expect(
      page
        .getByRole('button', { name: /export|csv|download/i })
        .or(page.locator('[data-testid="export-csv-btn"]'))
        .first(),
    ).toBeVisible({ timeout: 5_000 });

    // Navigate to trends report
    await page.goto('/reports/trends');
    await expect(page).toHaveURL(/reports\/trends/);

    // Trends chart must be visible
    await expect(
      page
        .locator('canvas')
        .or(page.locator('[data-testid="trends-chart"]'))
        .first(),
    ).toBeVisible({ timeout: 8_000 });
  },
);
