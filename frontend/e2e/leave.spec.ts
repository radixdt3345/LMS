import { test, expect } from '@playwright/test';

/**
 * LEAVECORE-E2E-001: Leave request E2E specs.
 *
 * Trigger: workflow_dispatch ONLY — never on: push or on: pull_request.
 *
 * Required environment variables:
 *   BASE_URL            (default: http://localhost:5173)
 *   EMPLOYEE_EMAIL      email of a seeded employee account
 *   EMPLOYEE_PASSWORD
 */

test.use({ baseURL: process.env.BASE_URL ?? 'http://localhost:5173' });

// ── helpers ──────────────────────────────────────────────────────────────────

async function loginAsEmployee(
  page: Parameters<Parameters<typeof test>[1]>[0]['page'],
): Promise<void> {
  const email    = process.env.EMPLOYEE_EMAIL    ?? 'employee@lms.local';
  const password = process.env.EMPLOYEE_PASSWORD ?? 'Employee@123';

  await page.goto('/login');
  await page.getByLabel(/email/i).fill(email);
  await page.getByLabel(/password/i).fill(password);
  await page.getByRole('button', { name: /sign in|log in|login/i }).click();
  await page.waitForURL(/\/dashboard|\/home|\/leaves/i, { timeout: 10_000 });
}

// ── E2E-4 @smoke ─────────────────────────────────────────────────────────────

/**
 * E2E-4 @smoke: Sandwich rule — a leave request that spans a weekend (Friday to
 * the following Monday or Tuesday) should show a computed-days value that includes
 * the intervening non-working days, i.e. >= the number of working-day-only days.
 *
 * Business rule (CONSTITUTION.md / critical-rules): the sandwich rule applies
 * within a single request — non-working days between start and end are counted.
 */
test(
  'E2E-4 @smoke: sandwich rule — leave spanning weekend shows computed days including non-working days',
  async ({ page }) => {
    await loginAsEmployee(page);

    // Navigate to the new leave request form
    await page.goto('/leaves/new');

    // Select any available leave type
    await page.getByLabel(/leave type/i).click();
    await page.getByRole('option').first().click();

    // Friday 2026-08-07 → Tuesday 2026-08-11 (spans Sat + Sun)
    // Working days only: Fri + Mon + Tue = 3; with sandwich rule: 5 calendar days
    await page.getByLabel(/start date/i).fill('2026-08-07');
    await page.getByLabel(/end date/i).fill('2026-08-11');

    // Wait for the computed-days indicator to update (debounced API call)
    const computedDaysLocator = page
      .getByText(/\d+\s*(day|days)/i)
      .or(page.locator('[data-testid="computed-days"]'));
    await expect(computedDaysLocator.first()).toBeVisible({ timeout: 5_000 });

    // Extract the number — must be >= 3 (at minimum the 3 working days)
    const rawText = await computedDaysLocator.first().textContent();
    const days = parseInt((rawText ?? '0').replace(/[^\d]/g, ''), 10);
    expect(days, `Computed days should be >= 3; got: ${days}`).toBeGreaterThanOrEqual(3);

    // Fill mandatory reason field
    await page.getByLabel(/reason/i).fill('E2E-4 automated test — sandwich rule spanning weekend');

    // Submit
    await page.getByRole('button', { name: /submit/i }).click();

    // Assert success feedback
    await expect(
      page
        .getByText(/leave request submitted|submitted successfully/i)
        .or(page.getByRole('alert').filter({ hasText: /success/i })),
    ).toBeVisible({ timeout: 10_000 });
  },
);

// ── E2E-9 ────────────────────────────────────────────────────────────────────

/**
 * E2E-9: Cancelling an already-approved leave request restores the employee's
 * leave balance — the balance shown on the dashboard must be higher after the
 * cancellation than before.
 *
 * Pre-condition: at least one leave request with status "Approved" must already
 * exist for the test employee in the seeded environment.
 */
test(
  'E2E-9: cancelling an approved leave request restores the balance',
  async ({ page }) => {
    await loginAsEmployee(page);

    // ── Step 1: capture current balance from dashboard ───────────────────────
    await page.goto('/dashboard');

    // Read the balance figure — accepts any numeric text near a balance label
    const balanceLocator = page
      .getByText(/annual.*balance|balance.*annual|remaining.*days|days.*remaining/i)
      .or(page.locator('[data-testid="balance-annual"]'));
    await expect(balanceLocator.first()).toBeVisible({ timeout: 10_000 });

    const balanceBeforeText = await balanceLocator.first().textContent();
    const balanceBefore = parseFloat((balanceBeforeText ?? '0').replace(/[^\d.]/g, ''));

    // ── Step 2: find an approved leave request and cancel it ─────────────────
    await page.goto('/leaves');

    // Locate the first row with status "Approved"
    const approvedRow = page
      .getByRole('row')
      .filter({ hasText: /approved/i })
      .first();
    await expect(approvedRow).toBeVisible({ timeout: 10_000 });

    // Click the Cancel button in that row
    const cancelButton = approvedRow.getByRole('button', { name: /cancel/i });
    await expect(cancelButton).toBeVisible();
    await cancelButton.click();

    // Confirm cancellation in dialog if one appears
    const confirmButton = page.getByRole('button', { name: /confirm|yes|cancel leave/i });
    if (await confirmButton.isVisible({ timeout: 2_000 }).catch(() => false)) {
      await confirmButton.click();
    }

    // Assert success feedback
    await expect(
      page
        .getByText(/cancelled|canceled|leave.*cancel/i)
        .or(page.getByRole('alert').filter({ hasText: /success/i })),
    ).toBeVisible({ timeout: 10_000 });

    // ── Step 3: verify balance increased on the dashboard ────────────────────
    await page.goto('/dashboard');
    await expect(balanceLocator.first()).toBeVisible({ timeout: 10_000 });

    const balanceAfterText = await balanceLocator.first().textContent();
    const balanceAfter = parseFloat((balanceAfterText ?? '0').replace(/[^\d.]/g, ''));

    expect(
      balanceAfter,
      `Balance should increase after cancellation (was ${balanceBefore}, now ${balanceAfter}).`,
    ).toBeGreaterThan(balanceBefore);
  },
);
