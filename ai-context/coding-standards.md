# Coding Standards — LMS (Article II Quick Reference for Ralph-impl)

## Backend (C# 12 / .NET 8)

### Tooling
- Formatter: `dotnet format` (EditorConfig: 4-space indent, LF, no trailing whitespace)
- Linter: Roslyn analyzers + SonarAnalyzer.CSharp
- Run before every commit: `dotnet format --verify-no-changes`

### Naming Conventions
| Element | Convention | Example |
|---------|-----------|---------|
| Namespace | `LMS.[Layer].[Domain]` | `LMS.Application.Leave` |
| Class | PascalCase | `LeaveRequestService` |
| Interface | `I` + PascalCase | `ILeaveRequestService` |
| Method | PascalCase | `SubmitLeaveRequestAsync` |
| Private field | `_camelCase` | `_leaveRepository` |
| Local variable | camelCase | `leaveRequest` |
| DTO | `[Entity][Action]Dto` | `LeaveRequestCreateDto` |
| Controller | `[Domain]Controller` | `LeaveRequestsController` |
| EF entity (class) | Singular PascalCase | `LeaveRequest` |
| DB table (EF config) | Plural snake_case | `leave_requests` |
| DB column (EF config) | snake_case | `start_date`, `created_at` |

### Forbidden Patterns — Backend
| Pattern | Why Forbidden |
|---------|--------------|
| `dynamic` type | Bypasses compile-time safety |
| `!` (null suppression) without comment | Hides null reference bugs |
| `Console.WriteLine` / `Console.Write` | Use Serilog only |
| Hardcoded secrets/URLs/credentials | Security — use env vars |
| `Thread.Sleep` / `Task.Delay` in prod | Blocks threads; use async |
| Direct `DbContext` in controllers | Violates layered architecture |
| Raw SQL string concatenation | SQL injection risk; use EF LINQ or parameterized `FromSqlRaw` |
| Empty `catch` blocks | Silently swallows errors |
| Returning password hashes, raw tokens in responses | Security — never expose |

### Layer Rules
- **Controllers**: Only receive request, call one service method, return response. No logic.
- **Services**: Business logic only. Depend on interfaces. No EF Core directly.
- **Repositories**: EF Core queries only. No business logic.
- **Hangfire Jobs**: Call application services only. No repository access.
- **Domain Entities**: Properties + domain invariants. No infrastructure dependencies.

### XML Documentation
All public classes, interfaces, and methods must have:
```csharp
/// <summary>
/// [What it does]
/// </summary>
```

---

## Frontend (React 17 / TypeScript)

### Tooling
- Formatter: Prettier (`.prettierrc`: 2-space indent, single quotes, trailing commas)
- Linter: ESLint (`@typescript-eslint/recommended` + `plugin:react-hooks/recommended`)
- Run before every commit: `npm run lint` (zero errors), `npm run format:check`

### Naming Conventions
| Element | Convention | Example |
|---------|-----------|---------|
| Component file | PascalCase | `LeaveBalanceCard.tsx` |
| Hook file | camelCase | `useLeaveBalances.ts` |
| Redux slice | camelCase | `leaveRequestsSlice.ts` |
| Saga file | camelCase | `leaveRequestsSaga.ts` |
| Utility | camelCase | `sandwichRuleUtils.ts` |
| SCSS module | camelCase | `leaveBalanceCard.module.scss` |
| Constant | UPPER_SNAKE_CASE | `MAX_FILE_SIZE_MB` |
| Type/Interface | PascalCase | `LeaveRequest`, `ILeaveBalance` |

### Forbidden Patterns — Frontend
| Pattern | Why Forbidden |
|---------|--------------|
| `any` type | Defeats TypeScript; use `unknown` + type guard |
| `console.log` / `console.error` in prod | Remove before commit |
| Hardcoded API URLs | Use `VITE_API_BASE_URL` env var |
| JWT in `localStorage` / `sessionStorage` | XSS risk; use memory only |
| Direct Redux state mutation | Use Redux Toolkit immer pattern only |
| `useEffect` with wrong deps | Causes infinite loops or stale closures |
| Non-memoized callbacks to heavy children | Performance regression |
| `dangerouslySetInnerHTML` | XSS risk |

### Component Rules
- Functional components + hooks only (no class components)
- Props must be typed with an explicit interface
- Redux state accessed via typed `useAppSelector` hook (not raw `useSelector`)
- API calls only via Axios instance with JWT interceptor (never raw `fetch`)
- All forms use React Hook Form with explicit validation schema

### Import Order (ESLint enforced)
1. React
2. Third-party libraries (alphabetical)
3. Internal: components → store → utils → types
(Blank line between each group)

---

## PR Checklist (all agents must verify before opening PR)

- [ ] `dotnet format --verify-no-changes` passes
- [ ] `npm run lint` returns zero errors
- [ ] All unit tests pass: `dotnet test` + `npm run test`
- [ ] Coverage ≥ 80% project-wide (CI reports this)
- [ ] Zero new Roslyn analyzer warnings in modified files
- [ ] No forbidden patterns introduced (self-checked against list above)
- [ ] XML doc / JSDoc added to all new public members
- [ ] PR title: `[ISSUE-ID] description`
- [ ] PR description: ACs satisfied + link to test results
- [ ] `docs/06_rbac.md` updated if permissions changed
- [ ] `README.md` updated if new env var or endpoint added
