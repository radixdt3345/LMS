import { test, expect } from '@playwright/test';

test.use({ baseURL: process.env.BASE_URL ?? 'http://localhost:5173' });

test(
  'E2E-1 @smoke: local login redirects to dashboard and token is absent from browser storage',
  async ({ page }) => {
    const email    = process.env.EMPLOYEE_EMAIL    ?? 'employee@lms.local';
    const password = process.env.EMPLOYEE_PASSWORD ?? 'Employee@123';

    await page.goto('/login');

    const loginForm = page
      .getByRole('form')
      .or(page.locator('form'));
    await expect(loginForm.first()).toBeVisible({ timeout: 10_000 });

    await page.getByLabel(/email/i).fill(email);
    await page.getByLabel(/password/i).fill(password);
    await page.getByRole('button', { name: /sign in|log in|login/i }).click();

    await page.waitForURL(/\/dashboard|\/home/i, { timeout: 10_000 });

    const localToken   = await page.evaluate(() => localStorage.getItem('access_token'));
    const sessionToken = await page.evaluate(() => sessionStorage.getItem('access_token'));
    expect(localToken,   'access_token must not be in localStorage').toBeNull();
    expect(sessionToken, 'access_token must not be in sessionStorage').toBeNull();

    const localToken2   = await page.evaluate(() => localStorage.getItem('token'));
    const sessionToken2 = await page.evaluate(() => sessionStorage.getItem('token'));
    expect(localToken2,   'token must not be in localStorage').toBeNull();
    expect(sessionToken2, 'token must not be in sessionStorage').toBeNull();

    await expect(
      page.getByRole('main').or(page.getByText(/dashboard|welcome/i).first()),
    ).toBeVisible({ timeout: 5_000 });
  },
);

test(
  'E2E-2: SSO login button is visible on login page',
  async ({ page }) => {
    await page.goto('/login');

    const ssoButton = page
      .getByRole('button', { name: /sign in with microsoft|azure ad|sso|single sign.on/i })
      .or(page.getByText(/sign in with microsoft|azure ad/i));

    await expect(ssoButton.first()).toBeVisible({ timeout: 10_000 });

    const azureUser = process.env.AZURE_TEST_USER;
    const azurePass = process.env.AZURE_TEST_PASS;

    if (azureUser && azurePass) {
      await ssoButton.first().click();

      await page.waitForURL(
        /\/dashboard|\/home|microsoftonline\.com|login\.microsoft/i,
        { timeout: 15_000 },
      );

      if (/\/dashboard|\/home/i.test(page.url())) {
        const localToken = await page.evaluate(() => localStorage.getItem('access_token'));
        expect(localToken, 'SSO: access_token must not be in localStorage').toBeNull();
      }
    }
  },
);
