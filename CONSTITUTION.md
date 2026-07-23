# CONSTITUTION.md — Leave Management System (LMS)

**Version**: 1.0
**Ratified**: 2026-07-22
**Status**: Active — Locked after ratification. Only `/constitution amend` can change this document.

---

## Article I — Core Principles

These principles are immutable. No task, PR, or agent output may violate them.

1. **No secrets in source code.** All credentials, API keys, connection strings, and signing keys live exclusively in environment variables. No secret may appear in any `.cs`, `.ts`, `.json`, `.yml`, or any tracked file.

2. **Every authenticated endpoint enforces RBAC.** Every API endpoint except `/health` and `/api/v1/auth/*` requires a valid JWT. Every controller action is decorated with `[Authorize(Roles = "...")]` or an equivalent policy. No endpoint is left open by omission.

3. **The audit trail is append-only and inviolable.** No `UPDATE` or `DELETE` may be issued against the `audit_logs` table by any application code, migration, or agent. Audit log rows are facts — they cannot be amended.

4. **The sandwich rule and balance validation are non-negotiable enforcement points.** The sandwich rule algorithm (FR-42) and the balance check (FR-37) must be executed server-side on every leave submission. Client-side calculation is display-only and never trusted for enforcement.

5. **No force-push to `main`. No merge without green CI.** The `main` branch is always deployable. CI (build + tests + LSP diagnostics) must pass before any PR is merged.

---

## Article II — Code Quality Standards

### 2.1 Language Versions & Tooling

| Layer | Language | Version | Formatter | Linter |
|-------|----------|---------|-----------|--------|
| Backend | C# | 12 (nullable enabled) | `dotnet format` (EditorConfig) | Roslyn analyzers (SonarAnalyzer.CSharp) |
| Frontend | TypeScript/JavaScript | React 17, TS strict mode | Prettier (`.prettierrc`) | ESLint (`@typescript-eslint` + `plugin:react-hooks/recommended`) |
| Database | SQL (PostgreSQL dialect) | 15+ | Manual | N/A |

EditorConfig (`.editorconfig`) governs indentation (4 spaces — backend; 2 spaces — frontend), line endings (LF), and trailing whitespace.

### 2.2 Forbidden Patterns

**Backend (C#):**
- ❌ `dynamic` type — use explicit types
- ❌ Nullable suppression operator (`!`) without comment justifying why
- ❌ `Console.WriteLine` / `Console.Write` anywhere (use Serilog)
- ❌ Hardcoded connection strings, passwords, API keys, or URLs in source
- ❌ `Thread.Sleep` or `Task.Delay` in production code (use proper async patterns)
- ❌ Direct `DbContext` access in controllers — always go through service layer
- ❌ Raw SQL strings (use EF Core LINQ; raw SQL only via parameterized `FromSqlRaw` with explicit justification comment)
- ❌ `catch (Exception e)` with empty body or only `throw` — either handle or wrap with context
- ❌ Returning sensitive data (passwords, raw tokens) in API responses

**Frontend (React/TypeScript):**
- ❌ `any` type — use explicit types or `unknown` with type guard
- ❌ `console.log` / `console.error` in production builds (remove before commit)
- ❌ Inline hardcoded API URLs — always use `VITE_API_BASE_URL`
- ❌ JWT access token in `localStorage` or `sessionStorage` — memory only
- ❌ Synchronous state mutation outside Redux reducers
- ❌ `useEffect` with missing or incorrect dependency arrays
- ❌ Non-memoized callbacks passed to child components that do heavy rendering

### 2.3 Naming Conventions

**Backend (C#):**
| Element | Convention | Example |
|---------|-----------|---------|
| Namespace | `LMS.[Layer].[Domain]` | `LMS.Application.Leave` |
| Class | PascalCase | `LeaveRequestService` |
| Interface | `I` prefix + PascalCase | `ILeaveRequestService` |
| Method | PascalCase | `SubmitLeaveRequestAsync` |
| Private field | `_camelCase` | `_leaveRepository` |
| Local variable | camelCase | `leaveRequest` |
| DTO | `[Entity][Action]Dto` | `LeaveRequestCreateDto` |
| Controller | `[Domain]Controller` | `LeaveRequestsController` |
| EF Core entity | Singular PascalCase | `LeaveRequest` |
| DB table (EF) | Plural snake_case | `leave_requests` |
| DB column (EF) | snake_case | `start_date` |

**Frontend (React/TypeScript):**
| Element | Convention | Example |
|---------|-----------|---------|
| Component file | PascalCase | `LeaveBalanceCard.tsx` |
| Hook file | camelCase | `useLeaveBalances.ts` |
| Redux slice file | camelCase | `leaveRequestsSlice.ts` |
| Saga file | camelCase | `leaveRequestsSaga.ts` |
| Utility file | camelCase | `sandwichRuleUtils.ts` |
| CSS/SCSS module | camelCase | `leaveBalanceCard.module.scss` |
| Constant | UPPER_SNAKE_CASE | `MAX_FILE_SIZE_MB` |
| Type/Interface | PascalCase | `LeaveRequest`, `ILeaveBalance` |

### 2.4 Import Order

**Backend**: System → Microsoft → third-party NuGet → internal LMS namespaces (each group separated by blank line, enforced by `.editorconfig`).

**Frontend**: React → third-party libraries → internal modules (components → store → utils → types) — enforced by ESLint `import/order`.

### 2.5 PR Requirements
- Every PR must reference a GitHub issue ID in the title: `[ISSUE-ID] description`
- PR description must include: what changed, which acceptance criteria are satisfied, link to test results
- Zero ESLint/Roslyn warnings in newly added/modified files
- Coverage must not drop below 80% (CI gate enforces this)
- Self-review checklist completed before requesting review

---

## Article III — Testing Policy

### 3.1 Coverage Gates

| Tier | Minimum Coverage | Enforcement |
|------|-----------------|-------------|
| Project-wide | **80%** | CI gate — build fails below this |
| Target | **90%** | Aspirational; tracked in PRs |
| Auth domain | **100%** | CI gate — enforced per-domain |
| RBAC enforcement layer | **100%** | CI gate — enforced per-domain |
| Leave balance calculation | **100%** | CI gate — enforced per-domain |
| Sandwich rule algorithm | **100%** | CI gate — enforced per-domain |

### 3.2 Test Types Required

| Type | Framework | When Written |
|------|-----------|-------------|
| Unit tests | xUnit (backend) / Vitest (frontend) | With every implementation task (Ralph-impl) |
| Integration tests | xUnit + TestContainers (backend) | By Ralph-test after impl PRs merge |
| E2E tests | Playwright | By Ralph-e2e for critical user flows |

### 3.3 Unit Test Rules
- Test files co-located with source: `[ClassName]Tests.cs` beside `[ClassName].cs` (backend); `[Component].test.tsx` beside `[Component].tsx` (frontend)
- Each test: one assertion per logical outcome (AAA pattern: Arrange / Act / Assert)
- Test names: `[MethodName]_[Scenario]_[ExpectedResult]` e.g. `SubmitLeaveRequest_InsufficientBalance_Returns422`
- No shared mutable state between tests
- No test order dependency — tests must be runnable in any order

### 3.4 Forbidden Test Patterns
- ❌ `Thread.Sleep` / `Task.Delay` in tests — use async properly or mock time
- ❌ Tests that hit external services (SendGrid, Azure AD, Google Calendar) — mock them all
- ❌ Tests that depend on execution order or shared DB state — each test must set up its own data
- ❌ Skipped tests checked in (`[Skip]` / `.skip(`) — fix or delete
- ❌ Tests asserting on log output only — test the actual behaviour

---

## Article IV — Security Baseline

### 4.1 Authentication Enforcement
- All non-auth, non-health endpoints: `[Authorize]` required
- JWT validation middleware registered globally in `Program.cs`
- JWT must contain: `user_id`, `role`, `department_id` (enforced on issue)
- Access token: 24h, in-memory (frontend); Refresh token: 7d, DB-stored (hashed SHA-256) + HttpOnly SameSite=Strict cookie
- Account lock: 3 consecutive failed local logins → locked; unlock only by HR Admin / Super Admin

### 4.2 RBAC Enforcement
- Source of truth: `docs/06_rbac.md` Permission Matrix
- Every controller action must have an explicit role or policy: `[Authorize(Roles = "Manager,HR Admin,Super Admin")]`
- Manager-scoped endpoints must filter queries by `reporting_manager_id = current_user_id` (never trust client-supplied user_id for scoping)
- Super Admin does not automatically get all Manager/HR permissions for personal leave screens — role exclusions from Section 3.3 of discovery are enforced

### 4.3 Data Classification

| Class | Examples | Handling |
|-------|----------|---------|
| **Secret** | JWT signing key, DB connection string, SendGrid API key, Google OAuth secret | Env vars only; never logged; never in response bodies |
| **PII** | Name, email, phone, date of joining, leave history | Encrypted in transit (TLS); soft-delete with anonymization on GDPR Article 17 request |
| **Sensitive** | Password hash, refresh token hash, audit log | BCrypt/SHA-256 hashed; never returned in API responses |
| **Internal** | Leave balances, approval statuses, department data | Auth-required; RBAC-scoped |
| **Public** | Health check response | No auth required |

### 4.4 Secrets Rules
- All secrets in environment variables only (never `appsettings.json` values)
- Serilog destructuring policies must mask fields named: `password`, `token`, `secret`, `apikey`, `connectionstring`
- Never log JWT payload, refresh token value, or SendGrid/Google credentials

### 4.5 Input Validation
- All public-facing API request bodies validated with FluentValidation before reaching service layer
- File uploads validated server-side: MIME type (PDF/JPG/PNG only), size ≤ 5MB
- SQL injection: prevented by EF Core parameterization; no raw string concatenation in queries
- XSS: all text fields HTML-encoded before rendering in frontend (MUI handles this; no `dangerouslySetInnerHTML`)

---

## Article V — Architecture Conventions

### 5.1 Service Boundaries
- No direct cross-domain DB access — all inter-domain communication via service interfaces
- Domain layer: no EF Core or infrastructure dependencies
- Application layer: no direct HTTP calls — use registered service interfaces only
- Hangfire jobs: treated as application-layer consumers; they call application services, not repositories directly

### 5.2 Layered Architecture (Backend)
```
LMS.API          → Controllers, Middleware, Filters (no business logic)
LMS.Application  → Services, DTOs, Validators, Interfaces (business logic)
LMS.Domain       → Entities, Value Objects, Domain Interfaces, Business Rules
LMS.Infrastructure → EF Core DbContext, Repositories, Hangfire Jobs, SendGrid, Google Calendar
LMS.Tests        → xUnit Unit + Integration tests
```
Dependency direction: API → Application → Domain ← Infrastructure

### 5.3 API Error Envelope
All API errors must use this standard envelope:
```json
{
  "success": false,
  "error": {
    "code": "INSUFFICIENT_BALANCE",
    "message": "You do not have enough Casual Leave balance.",
    "details": []
  }
}
```
Success responses: `{ "success": true, "data": {...} }` (list endpoints add `total`, `page`, `limit`).
HTTP status codes: 200 OK, 201 Created, 204 No Content, 400 Bad Request, 401 Unauthorized, 403 Forbidden, 404 Not Found, 409 Conflict, 422 Unprocessable Entity, 423 Locked, 429 Too Many Requests, 500 Internal Server Error.

### 5.4 Logging Standards
- Structured JSON logging via Serilog
- Required fields on every log event: `timestamp`, `level`, `message`, `request_id`, `user_id` (where available)
- Log levels: DEBUG (dev only), INFO (business events), WARN (recoverable issues), ERROR (failures), FATAL (system crashes)
- Never log PII beyond what is needed for debugging (name: OK, full profile: not OK)

### 5.5 Database Conventions
- All tables use UUID primary keys (`gen_random_uuid()`)
- All tables include `created_at TIMESTAMPTZ DEFAULT NOW()` and `updated_at TIMESTAMPTZ`
- Soft deletes via `status` column (Active/Inactive) or `is_active` (bool) — never hard delete application data
- All timestamps stored as UTC in the database; IST conversion handled in application/frontend layer
- Foreign keys always have explicit cascade rules defined
- Index every foreign key column and every column used in `WHERE` clauses in hot paths

---

## Article VI — AI Governance

### 6.1 Agent Permissions

| Action | Ralph-impl | Ralph-test | Ralph-e2e |
|--------|-----------|-----------|----------|
| Modify source code (`src/`) | ✅ | ✅ (test files only) | ✅ (e2e files only) |
| Modify `docs/agile/` task files | Read only | Read only | Read only |
| Modify `ai-context/` bridge files | ❌ | ❌ | ❌ |
| Modify `CONSTITUTION.md` | ❌ | ❌ | ❌ |
| Modify `docs/prd.md` | ❌ | ❌ | ❌ |
| Modify `ai-context/issues.json` | ❌ | ❌ | ❌ |
| Commit to `main` directly | ❌ | ❌ | ❌ |
| Open PRs targeting `main` | ✅ | ✅ | ✅ |
| Force-push any branch | ❌ | ❌ | ❌ |

### 6.2 Pre-Commit Gates (all agents)
1. `/lsp` diagnostics — **zero Errors tolerance** (Warnings allowed but must be noted in PR)
2. `dotnet test` — all tests pass
3. `npm run test` — all tests pass
4. Coverage ≥ 80% project-wide (CI enforces)
5. `dotnet format --verify-no-changes` — no formatting drift
6. `npm run lint` — zero ESLint errors

### 6.3 Commit Message Format
```
[type]([domain]): [ISSUE-ID] [brief description]

Types: feat | fix | test | e2e | chore | refactor | docs
Example: feat(leave): DB-003 add leave_requests table migration
```

### 6.4 Blocking Conditions
If any of the following occur, the agent must halt and set task status to `BLOCKED_ERROR`:
- LSP reports Errors that cannot be resolved without architectural decisions
- A required external credential is missing (Azure AD, SendGrid, Google Calendar)
- A dependency issue is not yet merged and cannot be stubbed safely
- The task's acceptance criteria contradict the Constitution

---

## Article VII — Documentation Standards

### 7.1 README
- `README.md` must be updated whenever: a new public API endpoint is added, a new environment variable is required, or the getting-started/run-tests flow changes
- README must always have accurate "Getting Started" and "Running Tests" sections post-build

### 7.2 Inline Documentation
- All public C# classes, methods, and interfaces: XML doc comments (`/// <summary>`)
- All React components: JSDoc comment on the component function
- All Redux sagas: comment describing the flow it handles
- All Hangfire jobs: comment describing trigger, frequency, and failure behaviour

### 7.3 RBAC Changes
- Any change to role permissions must update `docs/06_rbac.md` in the same PR
- Any new screen must add a row to the Screen Visibility Matrix in the requirements

### 7.4 API Changes
- Any new or modified endpoint must be reflected in `docs/prd.md` Section 9 and/or architecture API standards doc in the same PR

---

## Article VIII — Amendment Process

All amendments must:
1. Be initiated via `/constitution amend [article] [proposed change]`
2. Include a rationale (why is this change needed?)
3. Be confirmed by the user (HIL checkpoint)
4. Be logged in the Appendix below with date, article changed, and rationale
5. Trigger regeneration of `ai-context/project-constitution.md` and `ai-context/coding-standards.md`

**No agent may amend this document.** Only `/constitution amend` invoked by a human can change it.

---

## Appendix — Amendment Log

| # | Date | Article | Change | Rationale |
|---|------|---------|--------|-----------|
| — | — | — | — | Initial ratification |
