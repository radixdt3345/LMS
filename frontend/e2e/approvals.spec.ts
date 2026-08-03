import { test, expect } from '@playwright/test';

/**
 * E2E approval flow specs.
 *
 * Trigger: workflow_dispatch ONLY — never on: push or on: pull_request.
 *
 * Required environment variables:
 *   BASE_URL                        (default: http://localhost:5173)
 *   EMPLOYEE_NO_MANAGER_EMAIL       email of an employee with no manager assigned
 *   EMPLOYEE_NO_MANAGER_PASSWORD
 *   MANAGER_EMAIL                   email of a manager (employee with direct reports)
 *   MANAGER_PASSWORD
 *   HRADMIN_EMAIL
 *   HRADMIN_PASSWORD
 */

test.use({ baseURL: process.env.BASE_URL ?? 'http://localhost:5173' });

// ── Helpers ──────────────────────────────────────────────────────────────────

async function loginAs(
  page: ReturnType<typeof test['info']> extends never ? never : Parameters<Parameters<typeof test>[1]>[0]['page'],
  email: string,
  password: string,
): Promise<void> {
  await page.goto('/login');
  await page.getByLabel(/email/i).fill(email);
  await page.getByLabel(/password/i).fill(password);
  await page.getByRole('button', { name: /sign in|log in/i }).click();
  await page.waitForURL(/\/dashboard|\/home|\/leaves/i);
}

// ── E2E-6 @smoke ─────────────────────────────────────────────────────────────

/**
 * E2E-6: No-manager employee submits leave — HR Admin approves in exactly 1 step.
 *
 * Verifies the critical business rule: manager_id IS NULL → L2 unconditionally skipped.
 * The approval UI must show no "L2" or "Step 2" indicator for this employee's request.
 */
test(
  'E2E-6: no-manager employee submits leave — HR Admin approves in single step @smoke',
  async ({ page }) => {
    const employeeEmail    = process.env.EMPLOYEE_NO_MANAGER_EMAIL    ?? '';
    const employeePassword = process.env.EMPLOYEE_NO_MANAGER_PASSWORD ?? '';
    const hrAdminEmail     = process.env.HRADMIN_EMAIL                ?? '';
    const hrAdminPassword  = process.env.HRADMIN_PASSWORD             ?? '';

    // ── Step 1: Employee submits a leave request ──────────────────────────────
    await loginAs(page, employeeEmail, employeePassword);
    await page.goto('/leaves/new');

    // Fill leave type
    await page.getByLabel(/leave type/i).click();
    await page.getByRole('option').first().click();

    // Fill future start date (tomorrow)
    const tomorrow = new Date();
    tomorrow.setDate(tomorrow.getDate() + 1);
    const startStr = tomorrow.toISOString().split('T')[0]; // YYYY-MM-DD
    await page.getByLabel(/start date/i).fill(startStr);

    // Fill future end date (day after tomorrow)
    const dayAfter = new Date();
    dayAfter.setDate(dayAfter.getDate() + 2);
    const endStr = dayAfter.toISOString().split('T')[0];
    await page.getByLabel(/end date/i).fill(endStr);

    // Fill reason
    await page.getByLabel(/reason/i).fill('E2E-6 automated test — no-manager single-step approval');

    // Submit
    await page.getByRole('button', { name: /submit/i }).click();

    // Assert: success toast
    await expect(
      page.getByText(/leave request submitted/i),
    ).toBeVisible({ timeout: 10_000 });

    // Capture the request identifier shown on screen for later cross-checking
    // (accept any text that includes today's date range or the employee name)
    const submittedId = await page
      .getByTestId('leave-request-id')
      .textContent()
      .catch(() => null); // non-fatal; just used for log clarity

    // ── Step 2: HR Admin reviews and approves ─────────────────────────────────
    await loginAs(page, hrAdminEmail, hrAdminPassword);
    await page.goto('/approvals');

    // The submitted request must be visible in the pending queue
    const requestRow = page.getByRole('row').filter({ hasText: /E2E-6 automated test/i });
    await expect(requestRow).toBeVisible({ timeout: 10_000 });

    // CRITICAL: no L2 / Step 2 indicator must be present for this request.
    // A no-manager employee's request should never show a second approval step.
    const stepIndicators = requestRow.getByText(/L2|Step 2/i);
    await expect(stepIndicators).toHaveCount(0);

    // Approve
    await requestRow.getByRole('button', { name: /approve/i }).click();

    // Assert: status changes to Approved
    await expect(
      page.getByText(/approved/i).first(),
    ).toBeVisible({ timeout: 10_000 });

    // Assert: no second approval step appears after approval
    await expect(requestRow.getByText(/L2|Step 2/i)).toHaveCount(0);

    // Optionally log which request ID was processed
    if (submittedId) {
      console.log(`[E2E-6] Processed request ${submittedId} — single-step approval confirmed.`);
    }
  },
);

// ── E2E-7 ────────────────────────────────────────────────────────────────────

/**
 * E2E-7: 3-actor flow — Manager approves L1, HR Admin approves L2.
 *
 * Verifies the full 2-step approval chain for employees who have a manager assigned.
 */
test(
  'E2E-7: 3-actor flow — Manager approves L1, HR Admin approves L2',
  async ({ page }) => {
    const employeeEmail    = process.env.EMPLOYEE_NO_MANAGER_EMAIL    ?? ''; // reuse or override via env
    const employeePassword = process.env.EMPLOYEE_NO_MANAGER_PASSWORD ?? '';
    const managerEmail     = process.env.MANAGER_EMAIL                ?? '';
    const managerPassword  = process.env.MANAGER_PASSWORD             ?? '';
    const hrAdminEmail     = process.env.HRADMIN_EMAIL                ?? '';
    const hrAdminPassword  = process.env.HRADMIN_PASSWORD             ?? '';

    // ── Step 1: Employee (with manager) submits a leave request ───────────────
    // Use a dedicated env var if provided; fall back to the no-manager employee
    // (integration env may not distinguish them).
    const actingEmployeeEmail    = process.env.EMPLOYEE_WITH_MANAGER_EMAIL    ?? employeeEmail;
    const actingEmployeePassword = process.env.EMPLOYEE_WITH_MANAGER_PASSWORD ?? employeePassword;

    await loginAs(page, actingEmployeeEmail, actingEmployeePassword);
    await page.goto('/leaves/new');

    await page.getByLabel(/leave type/i).click();
    await page.getByRole('option').first().click();

    const start = new Date();
    start.setDate(start.getDate() + 3);
    const startStr = start.toISOString().split('T')[0];
    await page.getByLabel(/start date/i).fill(startStr);

    const end = new Date();
    end.setDate(end.getDate() + 4);
    const endStr = end.toISOString().split('T')[0];
    await page.getByLabel(/end date/i).fill(endStr);

    await page.getByLabel(/reason/i).fill('E2E-7 automated test — 3-actor L1+L2 approval flow');
    await page.getByRole('button', { name: /submit/i }).click();
    await expect(page.getByText(/leave request submitted/i)).toBeVisible({ timeout: 10_000 });

    // ── Step 2: Manager (L1) approves ────────────────────────────────────────
    await loginAs(page, managerEmail, managerPassword);
    await page.goto('/approvals');

    const requestRowManager = page.getByRole('row').filter({ hasText: /E2E-7 automated test/i });
    await expect(requestRowManager).toBeVisible({ timeout: 10_000 });

    await requestRowManager.getByRole('button', { name: /approve/i }).click();

    // After L1 approval the request should be awaiting L2 — still not fully Approved.
    await expect(
      page.getByText(/awaiting l2|pending/i).first(),
    ).toBeVisible({ timeout: 10_000 });

    // The request must NOT be in "Approved" final state yet.
    await expect(
      requestRowManager.getByText(/^approved$/i),
    ).toHaveCount(0);

    // ── Step 3: HR Admin (L2) approves ───────────────────────────────────────
    await loginAs(page, hrAdminEmail, hrAdminPassword);
    await page.goto('/approvals');

    const requestRowHR = page.getByRole('row').filter({ hasText: /E2E-7 automated test/i });
    await expect(requestRowHR).toBeVisible({ timeout: 10_000 });

    await requestRowHR.getByRole('button', { name: /approve/i }).click();

    // Final status must be Approved after L2 action.
    await expect(
      page.getByText(/approved/i).first(),
    ).toBeVisible({ timeout: 10_000 });
  },
);
