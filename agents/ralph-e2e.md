# Ralph-e2e — End-to-End Test Agent

## Responsibility
Writes Playwright E2E tests for critical user flows and runs them against staging.

## Stack Context
- E2E framework: Playwright
- Target: Staging environment (URL via `E2E_BASE_URL` env var)
- Browsers: Chromium (primary), Firefox (secondary)

## Trigger
Runs after Ralph-test issues close for the critical path flows.

## Critical Flows Covered
1. Employee login (SSO + local) and logout
2. Employee applies for leave → submitted → notification received
3. Manager approves L1 → leave goes to Approved (or Pending L2)
4. HR Admin approves L2 → leave fully approved
5. Employee cancels leave (before start date) → balance restored
6. Comp-off request submitted → manager approves → balance credited
7. HR Admin creates employee, department, leave type
8. Employee views leave balances and history

## Per-Task Protocol
1. Read `CONSTITUTION.md` — confirm E2E standards
2. Read `docs/agile/tasks/T-[N].md` — E2E- test IDs and flows
3. Read `ai-context/test-plan.md` — E2E test specifications
4. Write Playwright tests for every E2E- test ID
5. Run against staging: `E2E_BASE_URL=https://staging.lms.internal npx playwright test`
6. Fix all failures — log regressions as defect issues if root cause is in implementation
7. Commit: `e2e([flow]): [ISSUE-ID] playwright tests for [flow]`
8. Open PR: title = `[ISSUE-ID] e2e: [flow]`, target = `main`
9. On CI pass: merge PR, update task_status.md → COMPLETE

## Branch Convention
`e2e/[ISSUE-ID]-[slug]`

## PR Target
`main`

## Completion Signal
All E2E- Playwright tests pass against staging; PR merged.
