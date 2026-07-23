# Testing Strategy — Leave Management System (LMS)

## Overview

10-layer test pyramid spanning unit, integration, contract, API functional, performance, security, smoke, UAT, regression, and rollback validation. Tests are distributed between Ralph-impl (writes unit tests with every implementation task), Ralph-test (writes integration tests after impl PRs merge), and Ralph-e2e (writes Playwright E2E for critical flows).

---

## Layer 1 — Unit Tests

| Property | Value |
|----------|-------|
| **Framework (Backend)** | xUnit 2.x + Moq 4.x + coverlet |
| **Framework (Frontend)** | Vitest (via `npm run test`) |
| **Owner** | Ralph-impl (written alongside implementation) |
| **Trigger** | Every PR (CI gate) |
| **Environment** | No external dependencies — all mocked |

### Backend (xUnit)
- **Location**: Co-located with source — `[ClassName]Tests.cs` in the same folder as `[ClassName].cs`, inside `LMS.Tests/Unit/`
- **Naming**: `[ClassName]Tests.cs` → `LeaveRequestServiceTests.cs`, `SandwichRuleEngineTests.cs`
- **Test method naming**: `[MethodName]_[Scenario]_[ExpectedResult]`
  - e.g. `SubmitLeaveRequest_InsufficientBalance_Returns422`
  - e.g. `ComputeLeaveDays_IsolatedHolidayBetweenLeaves_NotCounted`
- **Pattern**: AAA (Arrange / Act / Assert); one logical assertion per test
- **Mocks**: All external dependencies mocked via Moq (`ILeaveRepository`, `IEmailService`, `ICalendarService`, `IAuditService`)
- **Config file**: `LMS.Tests/LMS.Tests.csproj`
- **Run command**: `dotnet test LMS.Tests --collect:"XPlat Code Coverage"`
- **Coverage tool**: coverlet → reports in `./TestResults/`

### Frontend (Vitest)
- **Location**: Co-located — `[Component].test.tsx` beside `[Component].tsx`
- **Naming**: `LeaveBalanceCard.test.tsx`, `useLeaveBalances.test.ts`
- **Config file**: `frontend/vitest.config.ts`
- **Run command**: `npm run test` (from `frontend/`)
- **Mocks**: MSW (Mock Service Worker) for API calls; Redux store wrapped with `renderWithProviders` helper

### Pass/Fail Gate
- ≥ 80% line coverage project-wide (CI fails below this)
- Auth domain: 100% (CI enforces)
- RBAC enforcement layer: 100%
- `SandwichRuleEngine`: 100%
- `LeaveBalanceService` (deduct, restore, prorate, lapse): 100%

---

## Layer 2 — Integration Tests

| Property | Value |
|----------|-------|
| **Framework** | xUnit + TestContainers.PostgreSql (or local test DB) |
| **Owner** | Ralph-test |
| **Trigger** | After all sibling Ralph-impl PRs in a domain-stage merge |
| **Environment** | Real PostgreSQL (ephemeral test database; seeded fresh per test run) |

- **Location**: `LMS.Tests/Integration/[Domain]/`
- **Naming**: `[Feature]IntegrationTests.cs` → `LeaveRequestSubmissionTests.cs`, `ApprovalEngineTests.cs`
- **Database**: Separate test DB (`lms_test`) created and migrated before each test run; seeded with minimal test data; dropped after
- **Pattern**: Each test class uses `IClassFixture<LmsTestFixture>` for DB setup/teardown
- **Run command**: `dotnet test LMS.Tests --filter Category=Integration`
- **Config file**: `LMS.Tests/Integration/LmsTestFixture.cs`

### What Integration Tests Must Cover
- Leave request submission through full validation pipeline (balance, overlap, sandwich rule, team limit, weekend/holiday)
- L1 and L2 approval flows (including no-manager routing, skip-L2 on HR Admin as L1)
- Leave cancel and revoke with balance restoration
- Comp-off request submit → approve → credit flow
- Year-end lapse Hangfire job
- Comp-off expiry job
- Escalation job (reminder email enqueue)
- Audit log entries on every state-changing action
- JWT issuance, refresh, and invalidation on logout
- Account lock after 3 failed attempts; unlock by HR Admin

---

## Layer 3 — Contract Tests

| Property | Value |
|----------|-------|
| **Tool** | OpenAPI schema validation (Swashbuckle generates `openapi.json`; tests validate response shapes against it) |
| **Owner** | Ralph-test |
| **Trigger** | After impl PRs merge (same run as L2) |
| **Environment** | Same test DB as L2 |

- **Location**: `LMS.Tests/Contract/`
- **What it validates**: Every API endpoint's response shape matches the declared OpenAPI spec. Error envelopes match the standard format. No undocumented fields in responses.
- **Run command**: `dotnet test LMS.Tests --filter Category=Contract`

---

## Layer 4 — API Functional Tests

| Property | Value |
|----------|-------|
| **Tool** | xUnit (HTTP-level tests using `WebApplicationFactory<Program>`) |
| **Owner** | Ralph-test |
| **Trigger** | After impl PRs merge |
| **Environment** | In-memory test server + test PostgreSQL |

- **Location**: `LMS.Tests/Api/[Domain]/`
- **Naming**: `[Resource]ApiTests.cs` → `LeaveRequestsApiTests.cs`
- **Covers**: Full HTTP round-trip — request headers, status codes, response bodies, error envelopes
- **Includes**: RBAC enforcement tests (each endpoint tested with each role's JWT to verify correct 200/403 responses)
- **Run command**: `dotnet test LMS.Tests --filter Category=Api`

---

## Layer 5 — Performance Tests

| Property | Value |
|----------|-------|
| **Tool** | k6 |
| **Owner** | DevOps / QA team (not generated by Ralph agents) |
| **Trigger** | Weekly scheduled CI run (not on every PR) |
| **Environment** | Staging (persistent) |

- **Location**: `tests/performance/`
- **Scenarios**:
  - 500 concurrent users: mix of leave balance reads, leave request submissions, approval actions
  - Report generation: 50 concurrent HR Admin report requests
- **Targets** (from NFR-1, NFR-2, NFR-3):
  - p50 < 200ms, p99 < 500ms (CRUD)
  - p99 < 2,000ms (/reports/* endpoints)
  - < 5% error rate at 500 concurrent users
- **Config file**: `tests/performance/k6.config.js`
- **Run command**: `k6 run tests/performance/scenarios.js`

---

## Layer 6 — Security Scans

| Property | Value |
|----------|-------|
| **Tools** | OWASP ZAP (DAST), `dotnet list package --vulnerable` (dependency audit), `npm audit` |
| **Owner** | CI pipeline |
| **Trigger** | On every deploy to staging |
| **Environment** | Staging |

- **Dependency audit**: `dotnet list package --vulnerable` in CI — fail on HIGH or CRITICAL severity
- **npm audit**: `npm audit --audit-level=high` — fail on HIGH or CRITICAL
- **OWASP ZAP**: Baseline scan against staging URL post-deploy; fail on MEDIUM+ alerts
- **Location**: `.github/workflows/security-scan.yml`

---

## Layer 7 — Smoke Tests

| Property | Value |
|----------|-------|
| **Tool** | Playwright (subset of E2E — tagged `@smoke`) |
| **Owner** | Ralph-e2e |
| **Trigger** | `workflow_dispatch` ONLY — never on PR push. Run post-deploy to staging. |
| **Environment** | Staging (persistent, seeded with test accounts) |

- **Location**: `tests/e2e/smoke/`
- **Scope**: Critical path only (≤ 10 tests; must run in < 5 minutes)
  - Login (local) → Employee Dashboard visible
  - Submit leave request → status = Pending L1
  - Manager approves L1 → status = Approved (or Pending L2)
  - HR Dashboard loads with chart data
  - Notification center returns results
- **Config file**: `tests/e2e/playwright.config.ts`
- **Run command**: `npx playwright test --grep @smoke`
- **Base URL**: `E2E_BASE_URL` env var (pointing to staging)

---

## Layer 8 — UAT (User Acceptance Testing)

| Property | Value |
|----------|-------|
| **Method** | Manual testing + Playwright-guided test scripts for HR/Manager roles |
| **Owner** | HR team + QA lead (human) |
| **Trigger** | Pre-production gate (manual) |
| **Environment** | Staging with production-like data |

- **UAT checklist** covers all 22 screens per the Screen Visibility Matrix (docs/prd.md Section 10)
- Playwright-guided scripts (not automated) help HR/Manager testers follow test scenarios PT-1 to PT-14
- Sign-off required from HR Admin and Manager stakeholders before production deploy

---

## Layer 9 — Regression Suite

| Property | Value |
|----------|-------|
| **Scope** | Full L1 + L2 + L3 + L4 suite |
| **Owner** | CI pipeline |
| **Trigger** | Every PR targeting `main`; pre-production deploy |
| **Environment** | CI ephemeral test DB |

- **Run command**: `dotnet test LMS.Tests && npm run test`
- Any regression failure blocks the PR merge
- Coverage gate enforced on every regression run

---

## Layer 10 — Rollback Validation

| Property | Value |
|----------|-------|
| **Method** | Manual smoke test (L7 subset) + DB integrity check |
| **Owner** | DevOps / QA lead (human) |
| **Trigger** | After every production rollback |
| **Environment** | Production |

### DB Integrity Checks Post-Rollback
- `audit_logs` table row count has not decreased
- `leave_balances` totals match expected (spot-check 5 employees)
- No orphaned `approval_steps` (leave_request_id FK references valid rows)
- Hangfire schema jobs table is accessible and scheduler is running

---

## Test File Structure

```
LMS.Tests/
  Unit/
    Auth/
      AuthServiceTests.cs
      TokenServiceTests.cs
    LeaveCore/
      SandwichRuleEngineTests.cs
      LeaveBalanceServiceTests.cs
      LeaveRequestServiceTests.cs
      ApprovalServiceTests.cs
    People/
      EmployeeServiceTests.cs
    CompOff/
      CompOffRequestServiceTests.cs
    Reporting/
      AuditServiceTests.cs
  Integration/
    Auth/
      AuthIntegrationTests.cs
    LeaveCore/
      LeaveRequestSubmissionTests.cs
      ApprovalEngineTests.cs
      YearEndLapseJobTests.cs
    CompOff/
      CompOffApprovalTests.cs
    Notifications/
      EscalationJobTests.cs
  Contract/
    OpenApiContractTests.cs
  Api/
    Auth/
      AuthApiTests.cs
    LeaveCore/
      LeaveRequestsApiTests.cs
      ApprovalsApiTests.cs
    People/
      EmployeesApiTests.cs
    Reports/
      ReportsApiTests.cs
  Helpers/
    LmsTestFixture.cs
    TestDataBuilder.cs
    JwtTestHelper.cs

frontend/
  src/
    modules/leave/components/
      LeaveBalanceCard.test.tsx
      ApplyLeaveForm.test.tsx
    store/slices/
      leaveRequestsSlice.test.ts
    utils/
      sandwichRuleUtils.test.ts

tests/
  e2e/
    playwright.config.ts
    smoke/
      login.spec.ts
      leaveSubmission.spec.ts
    critical/
      approvalFlow.spec.ts
      compOffFlow.spec.ts
      notifications.spec.ts
  performance/
    k6.config.js
    scenarios.js
```
