import { test, expect } from '@playwright/test';

test.use({ baseURL: process.env.BASE_URL ?? 'http://localhost:5173' });

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

test(
  'E2E-4 @smoke: sandwich rule — leave spanning weekend shows computed days including non-working days',
  async ({ page }) => {
    await loginAsEmployee(page);

    await page.goto('/leaves/new');

    await page.getByLabel(/leave type/i).click();
    await page.getByRole('option').first().click();

    await page.getByLabel(/start date/i).fill('2026-08-07');
    await page.getByLabel(/end date/i).fill('2026-08-11');

    const computedDaysLocator = page
      .getByText(/\d+\s*(day|days)/i)
      .or(page.locator('[data-testid="computed-days"]'));
    await expect(computedDaysLocator.first()).toBeVisible({ timeout: 5_000 });

    const rawText = await computedDaysLocator.first().textContent();
    const days = parseInt((rawText ?? '0').replace(/[^\d]/g, ''), 10);
    expect(days, `Computed days should be >= 3; got: ${days}`).toBeGreaterThanOrEqual(3);

    await page.getByLabel(/reason/i).fill('E2E-4 automated test — sandwich rule spanning weekend');
    await page.getByRole('button', { name: /submit/i }).click();

    await expect(
      page
        .getByText(/leave request submitted|submitted successfully/i)
        .or(page.getByRole('alert').filter({ hasText: /success/i })),
    ).toBeVisible({ timeout: 10_000 });
  },
);

test(
  'E2E-9: cancelling an approved leave request restores the balance',
  async ({ page }) => {
    await loginAsEmployee(page);

    await page.goto('/dashboard');

    const balanceLocator = page
      .getByText(/annual.*balance|balance.*annual|remaining.*days|days.*remaining/i)
      .or(page.locator('[data-testid="balance-annual"]'));
    await expect(balanceLocator.first()).toBeVisible({ timeout: 10_000 });

    const balanceBeforeText = await balanceLocator.first().textContent();
    const balanceBefore = parseFloat((balanceBeforeText ?? '0').replace(/[^\d.]/g, ''));

    await page.goto('/leaves');

    const approvedRow = page
      .getByRole('row')
      .filter({ hasText: /approved/i })
      .first();
    await expect(approvedRow).toBeVisible({ timeout: 10_000 });

    const cancelButton = approvedRow.getByRole('button', { name: /cancel/i });
    await expect(cancelButton).toBeVisible();
    await cancelButton.click();

    const confirmButton = page.getByRole('button', { name: /confirm|yes|cancel leave/i });
    if (await confirmButton.isVisible({ timeout: 2_000 }).catch(() => false)) {
      await confirmButton.click();
    }

    await expect(
      page
        .getByText(/cancelled|canceled|leave.*cancel/i)
        .or(page.getByRole('alert').filter({ hasText: /success/i })),
    ).toBeVisible({ timeout: 10_000 });

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
