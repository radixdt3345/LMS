import { test, expect } from '@playwright/test';

/**
 * E2E-13: HR Admin views the audit trail and sees a leave approval entry.
 *
 * Verifies:
 * - The audit log page is accessible to HR Admin users.
 * - The table/grid renders at least one row with EntityType="LeaveRequest"
 *   and an action containing "Approved".
 * - Expanding a row reveals a detail panel with leave request information.
 * - There is NO Delete button anywhere on the page (audit log is immutable).
 *
 * Credentials and base URL come exclusively from environment variables — no
 * hardcoded values. Set before running:
 *   BASE_URL          (default: http://localhost:5173)
 *   HRADMIN_EMAIL
 *   HRADMIN_PASSWORD
 *
 * Run (post-deploy, manual trigger only — never wired into on:push CI):
 *   npx playwright test frontend/e2e/audit-trail.spec.ts
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

    // Navigate to login page and authenticate as HR Admin
    await page.goto(`${BASE_URL}/login`);
    await page.getByLabel(/email/i).fill(HRADMIN_EMAIL);
    await page.getByLabel(/password/i).fill(HRADMIN_PASSWORD);
    await page.getByRole('button', { name: /sign in|log in/i }).click();

    // Wait for successful redirect away from login
    await page.waitForURL((url) => !url.pathname.includes('/login'), {
      timeout: 10_000,
    });
  });

  test(
    'E2E-13: HR Admin can view audit trail and see leave approval entry',
    async ({ page }) => {
      // ── Navigate to audit trail ──────────────────────────────────────────
      await page.goto(`${BASE_URL}/admin/audit`);
      await page.waitForLoadState('networkidle');

      // ── Assert: audit log table / grid is visible ────────────────────────
      // The table may be a MUI DataGrid, a plain <table>, or a role="grid".
      const auditTable = page
        .getByRole('grid')
        .or(page.getByRole('table'))
        .first();
      await expect(auditTable).toBeVisible({ timeout: 10_000 });

      // ── Filter for LeaveRequest entity type ─────────────────────────────
      // Attempt to use a visible filter / search control; fall back gracefully
      // if none is present (the table should already show all entity types).
      const entityTypeFilter = page.getByLabel(/entity.?type/i);
      if (await entityTypeFilter.isVisible()) {
        await entityTypeFilter.fill('LeaveRequest');
        // Allow the table to re-render after filtering
        await page.waitForTimeout(500);
      }

      // ── Assert: at least one row with action containing "Approved" ────────
      const approvedRow = page
        .getByRole('row')
        .filter({ hasText: /approved/i })
        .first();
      await expect(approvedRow).toBeVisible({ timeout: 10_000 });

      // ── Click the row to expand its detail panel ─────────────────────────
      await approvedRow.click();

      // ── Assert: detail panel / JSON diff becomes visible ─────────────────
      // The panel may surface as a drawer, dialog, or inline expansion.
      const detailPanel = page
        .getByRole('dialog')
        .or(page.locator('[data-testid="audit-detail"]'))
        .or(page.locator('.audit-detail'))
        .first();
      await expect(detailPanel).toBeVisible({ timeout: 5_000 });

      // The detail panel must contain leave request related information
      await expect(detailPanel).toContainText(/leave/i);

      // ── Assert: NO Delete button exists anywhere on the page ─────────────
      // The audit log is immutable; the UI must not expose a delete action.
      const deleteButton = page.getByRole('button', { name: /delete/i });
      await expect(deleteButton).toHaveCount(0);
    }
  );
});
