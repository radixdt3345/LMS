# Project Constitution — LMS (Bridge Summary)

Synthesized from CONSTITUTION.md for downstream skills. All 8 articles apply.

## Immutable Principles (Article I)
1. No secrets in source code — environment variables only
2. Every authenticated endpoint enforces RBAC — [Authorize] on every action
3. Audit trail is append-only — no UPDATE/DELETE on audit_logs ever
4. Sandwich rule and balance validation enforced server-side on every leave submission
5. No force-push to main. No merge without green CI (build + tests + LSP)

## Code Standards (Article II)
- Backend: C# 12, nullable enabled, `dotnet format` + Roslyn analyzers
- Frontend: React 17, TypeScript strict, Prettier + ESLint (@typescript-eslint)
- Backend forbidden: `dynamic`, `!` suppression without comment, `Console.Write*`, hardcoded secrets, `Thread.Sleep`, direct DbContext in controllers, raw SQL string concat, empty catch, returning sensitive data
- Frontend forbidden: `any`, `console.log` in prod, hardcoded API URLs, JWT in localStorage, direct state mutation outside Redux
- Backend naming: PascalCase classes/methods, `_camelCase` private fields, `I` prefix interfaces, snake_case DB columns/tables
- Frontend naming: PascalCase components, camelCase hooks/slices/sagas, UPPER_SNAKE constants
- PR: references issue ID, states ACs satisfied, zero new warnings, coverage ≥ 80%, self-review checklist

## Testing Policy (Article III)
- Gate: 80% coverage (CI blocks merge below this)
- Target: 90%
- 100% required: Auth domain, RBAC layer, leave balance calculation, sandwich rule algorithm
- Frameworks: xUnit + Vitest + Playwright
- Tests co-located with source
- Forbidden: Thread.Sleep in tests, external service calls (mock all), shared mutable state, skipped tests committed, log-output-only assertions

## Security Rules (Article IV)
- Auth: [Authorize] globally; JWT contains user_id, role, department_id
- RBAC: per docs/06_rbac.md Permission Matrix; Manager endpoints filter by reporting_manager_id = current user
- Data classes: Secret (env only, never logged) | PII (TLS + anonymize on GDPR request) | Sensitive (hashed, never returned)
- Secrets: env vars only; Serilog masks password/token/secret/apikey/connectionstring fields
- Validation: FluentValidation on all public endpoints; file uploads server-side (MIME + size)

## Architecture Rules (Article V)
- 4 layers: API → Application → Domain ← Infrastructure
- No cross-layer DB access; Hangfire jobs call application services only
- Error envelope: `{ "success": false, "error": { "code": "...", "message": "...", "details": [] } }`
- Success envelope: `{ "success": true, "data": {...} }` (lists add total/page/limit)
- Logging: Serilog JSON; fields: timestamp, level, message, request_id, user_id
- DB: UUID PKs, UTC timestamps, soft deletes, index all FK + hot-path WHERE columns

## AI Agent Rules (Article VI)
- Agents CANNOT modify: CONSTITUTION.md, ai-context/issues.json, docs/prd.md, ai-context/* bridge files
- Pre-commit gates: zero LSP Errors, all tests pass, ≥80% coverage, dotnet format clean, zero ESLint errors
- Commit format: `[type]([domain]): [ISSUE-ID] [description]`
- Blocking conditions: unresolvable LSP errors, missing credentials, unmerged blocking dependency, AC contradicts Constitution

## Documentation Rules (Article VII)
- README updated on API/env changes
- XML doc on all public C# members; JSDoc on React components
- docs/06_rbac.md updated with every permission change in same PR

## Amendment Rule (Article VIII)
- Only /constitution amend (human-initiated). No agent may amend CONSTITUTION.md.
