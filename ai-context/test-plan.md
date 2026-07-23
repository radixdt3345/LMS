# Test Plan Bridge — LMS (for Ralph-test, Ralph-e2e)

**Full test plan:** `docs/test-plan.md`
**Totals:** UT-61 | IT-53 | E2E-14 | RT-30

---

## Feature → Test ID Map

| Feature | Unit Tests (UT-) | Integration Tests (IT-) | E2E Tests (E2E-) |
|---------|-----------------|------------------------|-----------------|
| F-01 Auth | UT-1 to UT-10 | IT-1 to IT-6 | E2E-1, E2E-2 |
| F-02 Account Lockout | UT-11, UT-12 | IT-7, IT-8 | E2E-3 |
| F-03 Seeding | UT-13 | IT-45, IT-46 | E2E-14 |
| F-04 Departments | UT-14, UT-15 | IT-9, IT-10 | — |
| F-05 Employees | UT-16 to UT-20 | IT-11 to IT-15 | — |
| F-06 Leave Types | UT-21 | IT-16 | — |
| F-07 Leave Balance | UT-22 to UT-31 | IT-17, IT-18 | E2E-5 |
| F-08 Holiday Calendar | UT-32, UT-33 | IT-19, IT-20 | E2E-10 |
| F-09 Leave Request | UT-34 to UT-42 | IT-21 to IT-32 | E2E-4, E2E-9 |
| F-10 Comp-Off | UT-43 to UT-47 | IT-33 to IT-36 | E2E-8 |
| F-11 Approval Engine | UT-48 to UT-53 | IT-37 to IT-41 | E2E-6, E2E-7 |
| F-12 Notifications | UT-54, UT-55 | IT-42 to IT-44 | E2E-11 |
| F-13 Dashboards | UT-57, UT-58, UT-59 | IT-47, IT-48 | E2E-12 |
| F-14 Audit Trail | UT-56 | IT-49 to IT-51 | E2E-13 |
| RBAC/Security | UT-60, UT-61 | IT-52, IT-53 | E2E-12 |

---

## Critical 100% Coverage Modules

| Module | Tests Required | Covering IDs |
|--------|---------------|-------------|
| SandwichRuleEngine | 5 edge cases | UT-34 to UT-38 |
| LeaveBalanceService (deduct/restore/prorate/lapse) | 10 tests | UT-22 to UT-31 |
| AuthService + TokenService | 10 tests | UT-1 to UT-10 |
| ApprovalService (routing + L1/L2 logic) | 6 tests | UT-48 to UT-53 |
| AccountService (lock/unlock) | 2 tests | UT-11, UT-12 |
| RBAC enforcement (ProtectedRoute + RoleProtectedRoute) | 2 tests | UT-60, UT-61 |

---

## E2E Smoke Suite (@smoke tag)

Must complete in < 5 minutes post-deploy to staging.

| E2E- | PT- | Test Name |
|------|-----|-----------|
| E2E-1 | PT-1 | Local login → Employee Dashboard |
| E2E-4 | PT-4 | Leave application with sandwich rule |
| E2E-6 | PT-6 | No-manager employee → HR Admin approves (L2 skipped) |
| E2E-12 | PT-12 | Role-appropriate dashboards visible |

---

## Key Business Rules to Test (HIL-confirmed)

1. **No-manager overrides retroactive (UT-53):** When HR Admin is L1 (no manager), L2 is skipped even for retroactive requests.
2. **Sandwich rule is single-request scope (UT-38):** Non-working days between two separate requests are never counted.
3. **Google Calendar = company service account (IT-44):** No per-user OAuth2 — single credential writes to shared calendar.
4. **Role auto-derivation is idempotent (UT-18):** Setting Manager on already-Manager user is a no-op.
5. **Unpaid Leave balance exempt (UT-26, IT-25):** Zero balance does not block Unpaid Leave submission.
6. **Audit log is immutable (UT-56, IT-50):** Delete attempt throws exception; no DB call made.

---

## Run Commands

```bash
# Backend unit tests
dotnet test LMS.Tests --filter Category=Unit --collect:"XPlat Code Coverage"

# Backend integration tests (requires lms_test PostgreSQL DB)
dotnet test LMS.Tests --filter Category=Integration

# Frontend unit tests
cd frontend && npm run test

# E2E smoke (post-deploy only — workflow_dispatch)
npx playwright test --grep @smoke

# Full E2E suite (workflow_dispatch only)
npx playwright test
```

---

## Agent Assignments

| Test Type | Agent | When |
|-----------|-------|------|
| UT- (unit) | **Ralph-impl** | Written alongside each implementation task |
| IT- (integration) | **Ralph-test** | After all impl PRs in a domain-stage merge |
| E2E- (Playwright) | **Ralph-e2e** | After staging deploy; `workflow_dispatch` ONLY |
