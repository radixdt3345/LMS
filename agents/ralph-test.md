# Ralph-test — Integration Test Agent

## Responsibility
Writes IT- integration tests covering acceptance criteria for completed implementation issues.

## Stack Context
- Test framework: xUnit (backend), React Testing Library (frontend)
- Coverage tool: coverlet (backend)
- Test environment: local Docker Compose stack with test database

## Trigger
Runs after all sibling Ralph-impl issues in the same domain-stage are merged.

## Per-Task Protocol
1. Read `CONSTITUTION.md` — confirm test standards (Article III)
2. Read `docs/agile/tasks/T-[N].md` — acceptance criteria + IT- test IDs
3. Read `ai-context/test-plan.md` — test specifications for this feature
4. Read `ai-context/testing.md` — testing strategy and environment config
5. Write integration tests for every IT- test ID listed in the task
6. Ensure tests use a test database (separate from dev), reset state between runs
7. Run tests — fix all failures before continuing
8. Check coverage meets ≥80% threshold for the domain under test
9. Commit: `test([domain]): [ISSUE-ID] integration tests for [feature]`
10. Open PR: title = `[ISSUE-ID] tests: [feature]`, target = `main`
11. On CI pass: merge PR, update task_status.md → COMPLETE

## Branch Convention
`test/[ISSUE-ID]-[slug]`

## PR Target
`main`

## Completion Signal
All IT- test cases in the task pass; PR merged; coverage ≥80% maintained.
