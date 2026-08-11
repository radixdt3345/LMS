import { test, expect } from '@playwright/test';

test.use({ baseURL: process.env.BASE_URL ?? 'http://localhost:5173' });

test(
  'E2E-8: manager logs 8-hour comp-off — 1.0-day credit visible with expiry date',
  async ({ page }) => {
    const email    = process.env.MANAGER_EMAIL    ?? 'manager@lms.local';
    const password = process.env.MANAGER_PASSWORD ?? 'Manager@123';

    await page.goto('/login');
    await page.getByLabel(/email/i).fill(email);
    await page.getByLabel(/password/i).fill(password);
    await page.getByRole('button', { name: /sign in|log in|login/i }).click();
    await page.waitForURL(/\/dashboard|\/home/i, { timeout: 10_000 });

    await page.goto('/compoff');

    const newCompOffButton = page.getByRole('button', { name: /new|add|log comp.?off|request/i });
    await expect(newCompOffButton).toBeVisible({ timeout: 10_000 });
    await newCompOffButton.click();

    await page.getByLabel(/work.*date|date.*worked/i).fill('2026-07-25');
    await page.getByLabel(/hours/i).fill('8');

    const employeeSelect = page.getByLabel(/employee/i);
    if (await employeeSelect.isVisible({ timeout: 1_000 }).catch(() => false)) {
      await employeeSelect.click();
      await page.getByRole('option').first().click();
    }

    await page.getByRole('button', { name: /submit|save|create/i }).click();

    await expect(
      page
        .getByText(/comp.?off.*submitted|comp.?off.*created|credit.*added|success/i)
        .or(page.getByRole('alert').filter({ hasText: /success/i })),
    ).toBeVisible({ timeout: 10_000 });

    await page.goto('/compoff');

    const creditRows = page.getByRole('row').filter({ hasText: /1(\.0)?\s*(day|d)?/i });
    await expect(creditRows.first()).toBeVisible({ timeout: 10_000 });

    const creditRowText = await creditRows.first().textContent();
    expect(creditRowText).toMatch(/1(\.0)?/);

    const expiryLocator = creditRows.first().getByText(/expir|expires/i)
      .or(creditRows.first().locator('[data-testid="expiry-date"]'));
    await expect(
      expiryLocator.first(),
    ).toBeVisible({ timeout: 5_000 });
  },
);
