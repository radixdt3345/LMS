import { test, expect } from '@playwright/test';

/**
 * COMPOFF-E2E-001: Comp-off credit workflow E2E spec.
 *
 * Trigger: workflow_dispatch ONLY — never on: push or on: pull_request.
 *
 * Required environment variables:
 *   BASE_URL           (default: http://localhost:5173)
 *   MANAGER_EMAIL      email of a seeded manager account
 *   MANAGER_PASSWORD
 */

test.use({ baseURL: process.env.BASE_URL ?? 'http://localhost:5173' });

// ── E2E-8 ────────────────────────────────────────────────────────────────────

/**
 * E2E-8: Manager (or HR Admin) logs 8 hours of comp-off for a past working day.
 *
 * Business rules validated (CONSTITUTION.md / critical-rules):
 *   - 8 hours of extra work = 1.0 comp-off day credit
 *   - 4 hours = 0.5 day (not tested here, but rule documented)
 *   - Credits expire 180 days from the work date
 *
 * After submission the test asserts:
 *   1. A comp-off credit row is visible in the list
 *   2. The credit amount shows 1.0 (or "1 day")
 *   3. An expiry date is displayed on the credit row
 */
test(
  'E2E-8: manager logs 8-hour comp-off — 1.0-day credit visible with expiry date',
  async ({ page }) => {
    const email    = process.env.MANAGER_EMAIL    ?? 'manager@lms.local';
    const password = process.env.MANAGER_PASSWORD ?? 'Manager@123';

    // ── Step 1: log in as manager ─────────────────────────────────────────────
    await page.goto('/login');
    await page.getByLabel(/email/i).fill(email);
    await page.getByLabel(/password/i).fill(password);
    await page.getByRole('button', { name: /sign in|log in|login/i }).click();
    await page.waitForURL(/\/dashboard|\/home/i, { timeout: 10_000 });

    // ── Step 2: navigate to comp-off section ─────────────────────────────────
    await page.goto('/compoff');

    // Open the new comp-off request form
    const newCompOffButton = page.getByRole('button', { name: /new|add|log comp.?off|request/i });
    await expect(newCompOffButton).toBeVisible({ timeout: 10_000 });
    await newCompOffButton.click();

    // ── Step 3: fill in comp-off details ─────────────────────────────────────

    // Work date — a past Saturday (2026-07-25)
    await page.getByLabel(/work.*date|date.*worked/i).fill('2026-07-25');

    // Hours worked — 8 (= 1.0 day)
    await page.getByLabel(/hours/i).fill('8');

    // If the form includes an employee selector (manager view), pick the first option
    const employeeSelect = page.getByLabel(/employee/i);
    if (await employeeSelect.isVisible({ timeout: 1_000 }).catch(() => false)) {
      await employeeSelect.click();
      await page.getByRole('option').first().click();
    }

    // Submit the comp-off request
    await page.getByRole('button', { name: /submit|save|create/i }).click();

    // Assert success feedback
    await expect(
      page
        .getByText(/comp.?off.*submitted|comp.?off.*created|credit.*added|success/i)
        .or(page.getByRole('alert').filter({ hasText: /success/i })),
    ).toBeVisible({ timeout: 10_000 });

    // ── Step 4: verify credit row appears in the list ────────────────────────
    // Navigate or wait for the list to refresh
    await page.goto('/compoff');

    const creditRows = page.getByRole('row').filter({ hasText: /1(\.0)?\s*(day|d)?/i });
    await expect(creditRows.first()).toBeVisible({ timeout: 10_000 });

    // Assert credit amount is 1.0 day (8 hours / 8 = 1.0)
    const creditRowText = await creditRows.first().textContent();
    expect(
      creditRowText,
      'Credit row should display 1 or 1.0 days for 8 hours worked.',
    ).toMatch(/1(\.0)?/);

    // Assert an expiry date is shown (credits expire after 180 days)
    const expiryLocator = creditRows.first().getByText(/expir|expires/i)
      .or(creditRows.first().locator('[data-testid="expiry-date"]'));
    await expect(
      expiryLocator.first(),
      'Expiry date must be displayed on the credit row.',
    ).toBeVisible({ timeout: 5_000 });
  },
);
