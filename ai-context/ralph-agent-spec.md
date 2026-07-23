# Ralph Agent Specification — Leave Management System (LMS)

## Model
All agents: `claude-sonnet-4-6`

## Agent Types

### Ralph-impl (Implementation Agent)
- **Responsibility**: Implements DB, API, UI, and INT issues
- **Branch convention**: `feat/[ISSUE-ID]-[slug]`
- **PR target**: `main`
- **Max turns**: 60
- **Parallelization**:
  - Tier 1: One agent per domain (max 4 concurrent)
  - Tier 2: One agent per issue within a domain-stage (max 3 concurrent), only for issues with no inter-dependency
- **Worktree convention**: `../lms-v5-wt-[domain]` (domain level), `../lms-v5-wt-[domain]-[issue-id]` (issue level)
- **Reads per task**: `CLAUDE.md`, `CONSTITUTION.md`, `docs/agile/tasks/T-[N].md`, `docs/06_rbac.md`, `ai-context/architecture.md`, `ai-context/coding-standards.md`
- **Failure handling**: If LSP diagnostics fail → fix before committing. If tests fail → fix before PR. If blocked → open issue comment describing blocker, mark task BLOCKED_ERROR.

### Ralph-test (Integration Test Agent)
- **Responsibility**: Writes IT- integration tests covering acceptance criteria
- **Branch convention**: `test/[ISSUE-ID]-[slug]`
- **PR target**: `main`
- **Max turns**: 40
- **Trigger**: Runs after all sibling Ralph-impl issues in the same domain-stage close
- **Reads**: `docs/agile/tasks/T-[N].md`, `ai-context/test-plan.md`, `ai-context/testing.md`
- **Completion signal**: All IT- test cases in the task's test matrix pass; PR merged

### Ralph-e2e (End-to-End Test Agent)
- **Responsibility**: Writes E2E- Playwright tests for critical user flows
- **Branch convention**: `e2e/[ISSUE-ID]-[slug]`
- **PR target**: `main`
- **Max turns**: 50
- **Trigger**: Runs after Ralph-test issues close for the critical path
- **Staging URL**: Configured via env var `E2E_BASE_URL`
- **Completion signal**: All E2E- Playwright tests pass against staging; PR merged

## Branch Strategy
```
main (base + deployable)
  ├── feat/[ISSUE-ID]-[slug]    # Ralph-impl
  ├── test/[ISSUE-ID]-[slug]    # Ralph-test
  └── e2e/[ISSUE-ID]-[slug]     # Ralph-e2e
```

## PR Conventions
- **Title**: `[ISSUE-ID] [issue title]`
- **Description**: Links to GitHub issue + test results summary
- **Merge strategy**: Squash merge
- **CI gate**: Must pass (build + lint + unit tests + LSP) before merge

## Failure Handling
| Failure Type | Action |
|-------------|--------|
| LSP errors | Fix all errors before commit — zero tolerance |
| Failing unit tests | Fix before opening PR |
| Failing integration tests | Ralph-test fixes, does not merge broken tests |
| Blocked by external dependency | Comment on issue: "BLOCKED: [reason]", set task status BLOCKED_ERROR |
| Merge conflict | Rebase on latest main, resolve conflicts, re-run validation |

## Constitution Reference
Every agent reads `CONSTITUTION.md` before any code generation. All generated code must comply with all 8 articles. Non-compliant code is never committed.

## Issue Dependency Graph
Read from `ai-context/issues.json`. An issue is "unblocked" when all its `depends_on` issue IDs have status = MERGED in the PMS.
