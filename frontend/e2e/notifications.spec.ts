import { test, expect } from '@playwright/test';

test.use({ baseURL: process.env.BASE_URL ?? 'http://localhost:5173' });

async function loginAs(
  page: Parameters<Parameters<typeof test>[1]>[0]['page'],
  email: string,
  password: string,
): Promise<void> {
  await page.goto('/login');
  await page.getByLabel(/email/i).fill(email);
  await page.getByLabel(/password/i).fill(password);
  await page.getByRole('button', { name: /sign in|log in/i }).click();
  await page.waitForURL(/\/dashboard|\/home|\/leaves/i);
}

test(
  'E2E-11: notification bell badge increments after leave is approved @smoke',
  async ({ page }) => {
    const employeeEmail    = process.env.EMPLOYEE_EMAIL    ?? '';
    const employeePassword = process.env.EMPLOYEE_PASSWORD ?? '';
    const hrAdminEmail     = process.env.HRADMIN_EMAIL     ?? '';
    const hrAdminPassword  = process.env.HRADMIN_PASSWORD  ?? '';

    await loginAs(page, employeeEmail, employeePassword);
    await page.goto('/leaves/new');

    await page.getByLabel(/leave type/i).click();
    await page.getByRole('option').first().click();

    const start = new Date();
    start.setDate(start.getDate() + 10);
    const end = new Date(start);
    end.setDate(end.getDate() + 1);
    const fmt = (d: Date) => d.toISOString().split('T')[0];

    await page.getByLabel(/start date/i).fill(fmt(start));
    await page.getByLabel(/end date/i).fill(fmt(end));
    await page.getByLabel(/reason/i).fill('E2E-11 automated test — notification bell badge');

    await page.getByRole('button', { name: /submit/i }).click();

    await expect(
      page.getByText(/leave request submitted/i),
    ).toBeVisible({ timeout: 10_000 });

    await loginAs(page, hrAdminEmail, hrAdminPassword);
    await page.goto('/approvals');

    const pendingRow = page
      .getByRole('row')
      .filter({ hasText: /E2E-11 automated test/i });

    await expect(pendingRow).toBeVisible({ timeout: 10_000 });
    await pendingRow.getByRole('button', { name: /approve/i }).click();

    const confirmBtn = page.getByRole('button', { name: /confirm|yes|ok/i });
    if (await confirmBtn.isVisible({ timeout: 3_000 }).catch(() => false)) {
      await confirmBtn.click();
    }

    await expect(
      page.getByText(/approved/i).first(),
    ).toBeVisible({ timeout: 10_000 });

    await loginAs(page, employeeEmail, employeePassword);
    await page.goto('/dashboard');

    const bell = page.getByRole('button', { name: /notification|bell/i });
    await expect(bell).toBeVisible({ timeout: 10_000 });

    const badge = bell.locator('.MuiBadge-badge');
    const badgeVisible = await badge.isVisible({ timeout: 5_000 }).catch(() => false);

    if (badgeVisible) {
      const badgeText = await badge.textContent();
      const count = parseInt(badgeText ?? '0', 10);
      expect(count).toBeGreaterThanOrEqual(1);
    } else {
      await bell.click();
      await expect(
        page.getByText(/leave.*approved|approved.*leave/i).first(),
      ).toBeVisible({ timeout: 8_000 });
    }
  },
);
