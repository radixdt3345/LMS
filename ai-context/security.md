# Security — LMS (Quick Reference for Ralph-impl)

## Auth Model
- **Primary**: Azure AD OAuth2 Authorization Code Flow (MSAL) → JWT issued by LMS API
- **Fallback**: Local email + BCrypt (cost 12) password
- **Access token**: JWT (HS256), 24h, stored in browser memory only (never localStorage)
- **Refresh token**: 7d, SHA-256 hashed in DB, transmitted via HttpOnly SameSite=Strict cookie
- **JWT claims**: `user_id`, `role`, `department_id`, `exp`
- **Account lock**: 3 failed local logins → locked; unlock via HR Admin / Super Admin only

## RBAC Enforcement
- Source of truth: `docs/06_rbac.md` Permission Matrix
- Every controller action: `[Authorize(Roles = "...")]` or named policy
- JWT middleware registered globally in `Program.cs`; only `/health` and `/api/v1/auth/*` are anonymous
- Manager-scoped queries: always filter `WHERE reporting_manager_id = current_user_id` — never trust client-supplied user_id for scoping
- Role changes take effect on next token refresh

## Data Classification

| Class | Examples | Rules |
|-------|----------|-------|
| Secret | JWT signing key, DB conn string, SendGrid API key, Google OAuth secret | Env vars only; never log; never in response |
| PII | Name, email, phone, DOJ, leave history | TLS in transit; anonymize on GDPR deletion request |
| Sensitive | Password hash, refresh token hash | BCrypt/SHA-256; never return in API response |
| Internal | Leave balances, approval status, dept data | Auth required; RBAC scoped |
| Public | /health response | No auth required |

## Secrets Rules
- All secrets: environment variables only (never `appsettings.json` values)
- Serilog masks fields: `password`, `token`, `secret`, `api_key`, `connection_string`, `client_secret`, `password_hash` → `[REDACTED]`
- Never log JWT payload, refresh token value, SendGrid key, Google credentials

## Input Validation
- All request bodies: FluentValidation before service layer
- File uploads (server-side only): MIME type (PDF/JPG/PNG), size ≤ 5MB
- No raw SQL string concatenation — EF Core LINQ or parameterized `FromSqlRaw` only
- No `dangerouslySetInnerHTML` in React (MUI handles encoding)

## Transport Security
- All DB connections: `sslmode=require` in staging/prod connection strings
- API: HTTPS enforced in staging/prod; HTTP → HTTPS redirect in Nginx
- CORS: allowed origins from `Cors__AllowedOrigins` env var; `AllowCredentials()` for refresh token cookie

## Rate Limiting
- 100 requests/min per authenticated user (keyed on `user_id`)
- HTTP 429 + `Retry-After` header on limit exceeded
- ASP.NET Core rate limiting middleware in `Program.cs`

## Audit Enforcement
- `audit_logs` is append-only — no UPDATE/DELETE ever issued by application code
- Every state-changing action calls `AuditService.LogAsync()` with old_value + new_value + IP
