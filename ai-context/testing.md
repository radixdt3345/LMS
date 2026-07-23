# Testing Standards — LMS (Bridge for Ralph-impl, Ralph-test, Ralph-e2e)

## Unit Tests (L1)

**Framework (Backend)**: xUnit 2.x + Moq 4.x + coverlet
**Framework (Frontend)**: Vitest

**Location**: Co-located with source
- Backend: `LMS.Tests/Unit/[Domain]/[ClassName]Tests.cs`
- Frontend: `[Component].test.tsx` beside `[Component].tsx`

**Naming**: `[MethodName]_[Scenario]_[ExpectedResult]`
e.g. `ComputeLeaveDays_IsolatedHoliday_NotCounted`

**Coverage gate**: 80% (project-wide CI gate)
**Coverage target**: 90%
**100% required**: Auth domain, RBAC enforcement layer, SandwichRuleEngine, LeaveBalanceService (deduct/restore/prorate/lapse), ApprovalService (routing logic)

**Run command (backend)**: `dotnet test LMS.Tests`
**Run command (frontend)**: `cd frontend && npm run test`

**Mocking rules**: All external dependencies mocked (no real DB, no real SendGrid, no real Google/Azure calls in unit tests). Use Moq for backend interfaces. Use MSW for frontend API calls.

---

## Integration Tests (L2/L3/L4)

**Framework**: xUnit + real PostgreSQL (`lms_test` DB)
**Owner**: Ralph-test
**Location**: `LMS.Tests/Integration/[Domain]/`

**Test DB setup**:
- Local: native PostgreSQL, `lms_test` DB, connection via `ConnectionStrings__TestConnection`
- CI: PostgreSQL GitHub Actions service, same connection pattern

**Run command**: `dotnet test LMS.Tests --filter Category=Integration`

**Key integration tests required** (write these — do not skip):
- Full leave submission pipeline (all validation: balance, overlap, sandwich, team limit, weekend/holiday)
- L1 and L2 approval flows (including no-manager routing, HR Admin as L1 = skip L2)
- Leave cancel with balance restoration
- Leave revoke with balance restoration
- Comp-off submit → approve → balance credited
- Year-end lapse job
- Comp-off expiry job
- Audit log written on every state change
- Account lock (3 failures) and unlock

**Test data**: Use `TestDataBuilder` helpers. Each test class sets up and tears down its own data. No shared mutable state.

---

## E2E Tests (L7 — Playwright)

**Config**: `tests/e2e/playwright.config.ts`
**Base URL**: `E2E_BASE_URL` env var (staging: `https://staging.lms.internal`)
**Trigger**: `workflow_dispatch` ONLY — NEVER on PR push/pull_request triggers
**Run command**: `npx playwright test` (full suite) or `npx playwright test --grep @smoke` (smoke only)

**Smoke suite** (`@smoke` tag — runs post-deploy to staging, must complete in < 5 min):
- Login with local credentials → Employee Dashboard visible
- Submit leave request → status = Pending L1
- Manager approves L1 → status updates
- HR Dashboard loads with chart
- Notification center returns results

**Critical E2E flows** (PT-1 to PT-14 from docs/prd.md Section 15):
- PT-1: Local login flow
- PT-4: Leave application with full validation (sandwich rule, balance display)
- PT-5: Zero balance blocks submit
- PT-6: No-manager employee → HR Admin as L1 approver
- PT-7: Two-level approval for Sick Leave
- PT-8: Comp-off submit → manager approve → balance credited
- PT-9: Cancel leave / revoke after start date blocked
- PT-10: HR Admin manages holidays + calendar disables them
- PT-11: Leave approval triggers notifications + Google Calendar

---

## CI Gate Summary

| Gate | Trigger | Must Pass |
|------|---------|----------|
| L1 unit tests | Every PR | Yes — blocks merge |
| Coverage ≥ 80% | Every PR | Yes — blocks merge |
| L2-L4 integration + API | Every PR | Yes — blocks merge |
| L7 smoke (Playwright @smoke) | `workflow_dispatch` post-deploy to staging | Yes — blocks staging promotion |
| L9 full regression | Every PR to `main` | Yes — blocks merge |
| L5 performance (k6) | Weekly scheduled | No (informational; alert on regression) |
| L6 security scan (OWASP ZAP + audit) | On staging deploy | Yes — HIGH/CRITICAL vulns block |

---

## Test Data Strategy

**Unit tests**: In-memory only; mocked dependencies; no DB.

**Integration tests**:
- Local: `lms_test` DB; fresh schema per test fixture; `TestDataBuilder` for all test entities
- CI: ephemeral PostgreSQL service; same approach

**Staging (E2E)**:
- 5 permanent test accounts seeded (superadmin, hradmin, manager1, employee1, employee2)
- Playwright tests use `employee1` / `employee2` to avoid polluting admin views
- QA team cleans up test leave requests after each E2E / UAT run

**Test data never uses production DB.** CI uses separate GitHub Secrets for test DB credentials.
