import { test, expect } from '@playwright/test';

/**
 * AUTH-E2E-001: Authentication E2E specs.
 *
 * Trigger: workflow_dispatch ONLY — never on: push or on: pull_request.
 *
 * Required environment variables:
 *   BASE_URL           (default: http://localhost:5173)
 *   EMPLOYEE_EMAIL     email of a seeded employee account
 *   EMPLOYEE_PASSWORD
 *   AZURE_TEST_USER    (optional) Azure AD test user — SSO flow only executed if set
 *   AZURE_TEST_PASS    (optional)
 */

test.use({ baseURL: process.env.BASE_URL ?? 'http://localhost:5173' });

// ── E2E-1 @smoke ─────────────────────────────────────────────────────────────

/**
 * E2E-1 @smoke: Local (email/password) login redirects to the dashboard.
 *
 * Critical constraint: JWT access token must NEVER be stored in
 * localStorage or sessionStorage (CONSTITUTION.md Article VI).
 * This test explicitly asserts both storage areas are empty of any token key.
 */
test(
  'E2E-1 @smoke: local login redirects to dashboard and token is absent from browser storage',
  async ({ page }) => {
    const email    = process.env.EMPLOYEE_EMAIL    ?? 'employee@lms.local';
    const password = process.env.EMPLOYEE_PASSWORD ?? 'Employee@123';

    // Navigate to login page
    await page.goto('/login');

    // Login form must be visible
    const loginForm = page
      .getByRole('form')
      .or(page.locator('form'));
    await expect(loginForm.first()).toBeVisible({ timeout: 10_000 });

    // Fill credentials via accessible labels (matching MUI TextField label text)
    await page.getByLabel(/email/i).fill(email);
    await page.getByLabel(/password/i).fill(password);
    await page.getByRole('button', { name: /sign in|log in|login/i }).click();

    // Should redirect to dashboard
    await page.waitForURL(/\/dashboard|\/home/i, { timeout: 10_000 });

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

    // Dashboard should render meaningful content
    await expect(
      page.getByRole('main').or(page.getByText(/dashboard|welcome/i).first()),
    ).toBeVisible({ timeout: 5_000 });
  },
);

// ── E2E-2 ────────────────────────────────────────────────────────────────────

/**
 * E2E-2: The SSO (Azure AD) login option is visible on the login page.
 *
 * If AZURE_TEST_USER + AZURE_TEST_PASS env vars are provided, the test also
 * clicks the SSO button and verifies either a successful dashboard redirect
 * or a Microsoft OIDC handshake. If those vars are absent the test validates
 * only that the button is present and clickable.
 */
test(
  'E2E-2: SSO login button is visible on login page',
  async ({ page }) => {
    await page.goto('/login');

    // SSO button — match common text labels used for Azure AD / Microsoft login
    const ssoButton = page
      .getByRole('button', { name: /sign in with microsoft|azure ad|sso|single sign.on/i })
      .or(page.getByText(/sign in with microsoft|azure ad/i));

    await expect(ssoButton.first()).toBeVisible({ timeout: 10_000 });

    // Optional: full SSO flow when test credentials are available
    const azureUser = process.env.AZURE_TEST_USER;
    const azurePass = process.env.AZURE_TEST_PASS;

    if (azureUser && azurePass) {
      await ssoButton.first().click();

      // Wait for Microsoft OIDC redirect or a mocked dashboard landing
      await page.waitForURL(
        /\/dashboard|\/home|microsoftonline\.com|login\.microsoft/i,
        { timeout: 15_000 },
      );

      // If we landed on the dashboard (mock/stub env), re-assert token is not stored
      if (/\/dashboard|\/home/i.test(page.url())) {
        const localToken = await page.evaluate(() => localStorage.getItem('access_token'));
        expect(localToken, 'SSO: access_token must not be in localStorage').toBeNull();
      }
    }
    // When Azure credentials are absent, SSO presence alone is sufficient for this spec.
  },
);
