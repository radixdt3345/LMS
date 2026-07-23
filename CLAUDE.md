# LMS V5 — Agent Context (for /ralph, /qmd, /lsp)

## Project
Leave Management System — single-org, monolith, direct-host deployment (no Docker).

## Stack
| Layer | Technology |
|-------|-----------|
| Backend | C# 12, .NET 8, ASP.NET Core Web API, EF Core 8, PostgreSQL 15+ |
| Background Jobs | Hangfire (PostgreSQL storage, `hangfire` schema) |
| Auth | JWT Bearer + Azure AD OAuth2 (MSAL) |
| Email | SendGrid v3 — plain text / inline HTML only. NO template IDs, NO dynamic templates |
| Calendar | Google Calendar API v3 — company-wide service account. NO per-user OAuth2 |
| Frontend | React 17, TypeScript strict, MUI v5, Redux Toolkit + Redux-Saga, Vite |
| Testing | xUnit + Moq + coverlet (backend) | Vitest (frontend) | Playwright (E2E) |
| Deployment | dotnet publish → IIS/systemd | React SPA → Nginx | PostgreSQL native |

## Key Conventions (CONSTITUTION.md Articles II–III)
1. **No raw SQL** — EF Core ORM only; migrations managed via `dotnet ef migrations add`
2. **snake_case** for all DB columns and table names (EF naming convention applied globally)
3. **UTC everywhere** — all timestamps stored as `timestamptz UTC`; IST conversion at application layer only
4. **UUID PKs** — all tables use `Guid` / `uuid` primary keys; no integer auto-increment
5. **Result pattern** — services return `Result<T>` (never throw for expected failures); only infrastructure throws
6. **Tokens in memory only** — JWT access tokens never written to localStorage or sessionStorage
7. **Audit all mutations** — `AuditService.LogAsync()` called in every domain service on state change

## Domain Map
| Domain | Services | Key Constraint |
|--------|----------|---------------|
| AUTH | AuthService, TokenService, AccountService | Rate-limit login (10/min/IP); lockout after 5 failures |
| PEOPLE | EmployeeService, DepartmentService | Role auto-derived from manager_id; idempotent |
| LEAVECORE | LeaveRequestService, ApprovalService, SandwichRuleEngine, LeaveBalanceService, LeaveTypeService | Sandwich rule = single request scope only |
| COMPOFF | CompOffRequestService, CompOffCreditService | Credits expire 180 days; 4h=0.5d, 8h=1d |
| NOTIFICATIONS | NotificationService, EmailService, CalendarService | SendGrid plain-text; Google Calendar service account |
| REPORTING | ReportService, AuditService | Audit log is append-only (no delete) |
| INFRA | SeedService | Idempotent startup seed — runs once |

## Critical Business Rules (HIL-confirmed)
- **No-manager rule is absolute**: employee.manager_id IS NULL → HR Admin is L1 AND L2 is unconditionally skipped, even for retroactive requests (UT-53, IT-40)
- **Sandwich rule — single request scope**: non-working days between TWO separate requests are never counted (UT-38)
- **Google Calendar = company service account**: single credential, shared company calendar; no per-user OAuth2 consent
- **SendGrid = plain text/inline HTML**: no template IDs in config or code (UT-54)
- **Unpaid Leave**: zero balance does NOT block submission (UT-26, IT-25)
- **Audit log immutable**: AuditService.Delete throws; no DB call made (UT-56, IT-50)

## File Structure (Backend)
```
LMS/
  LMS.API/           ← ASP.NET Core project (Controllers, Program.cs, Middleware)
  LMS.Application/   ← Services, DTOs, Interfaces, Validators
  LMS.Domain/        ← Entities, Enums, Domain Services (SandwichRuleEngine)
  LMS.Infrastructure/← EF DbContext, Migrations, EmailService, CalendarService
  LMS.Tests/         ← xUnit tests (Category=Unit / Category=Integration)
frontend/            ← React + Vite SPA
  src/
    components/      ← Shared MUI components
    pages/           ← Route-level page components
    store/           ← Redux slices + sagas
    api/             ← Axios client + interceptors
    hooks/           ← Custom React hooks
```

## API Conventions
- Base: `/api/v1/`
- Auth: `Authorization: Bearer <JWT>`
- Success: `{ "success": true, "data": {...} }`
- Error: `{ "success": false, "error": { "code": "...", "message": "...", "details": [] } }`
- Lists add `total`, `page`, `limit` to response
- Pagination: `?page=1&limit=20` (max 100)

## Test Commands
```bash
# Unit tests
dotnet test LMS.Tests --filter Category=Unit --collect:"XPlat Code Coverage"

# Integration tests (requires lms_test PostgreSQL DB)
dotnet test LMS.Tests --filter Category=Integration

# Frontend unit tests
cd frontend && npm run test

# E2E smoke (post-deploy only — workflow_dispatch)
npx playwright test --grep @smoke

# Full E2E
npx playwright test
```

## Current Phase
Wave 1 — Foundation (T-001 to T-040)

## How to Resume
1. Check `task_status.md` for PENDING tasks with no blockers
2. Read `docs/agile/tasks/T-NNN.md` for the full spec
3. Implement following CONSTITUTION.md + `ai-context/coding-standards.md`
4. Run `/lsp` before committing
5. Run `/ralph` to continue the build loop

## Key Files
| File | Purpose |
|------|---------|
| `task_status.md` | Build progress — updated by /ralph after each task |
| `docs/agile/_registry.md` | Full Epic/Story/Task hierarchy |
| `ai-context/issues.json` | Machine-readable issue manifest |
| `ai-context/architecture.md` | Domain map + service responsibilities |
| `ai-context/testing.md` | Test frameworks + run commands |
| `CONSTITUTION.md` | Non-negotiable engineering standards |
| `docs/prd.md` | FR/AC/PT IDs — source of truth |
| `docs/test-plan.md` | All UT-/IT-/E2E-/RT- test specs |
