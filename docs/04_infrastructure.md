# Infrastructure — Leave Management System (LMS)

## Environment Model

| Environment | Purpose | Access |
|-------------|---------|--------|
| Local (dev) | Developer machine — Docker Compose | Developer only |
| Staging | QA and UAT — mirrors prod config | QA team + Stakeholders |
| Production | Live system | End users |

### Docker Compose Services (local + staging)
- `lms-api` — ASP.NET Core Web API container
- `lms-frontend` — Nginx serving the React SPA
- `lms-postgres` — PostgreSQL 15 container
- `lms-nginx` — Reverse proxy (routes `/api/*` → lms-api, `/*` → lms-frontend)

## Required External Services & APIs

| Service | Purpose | Phase |
|---------|---------|-------|
| Azure AD (Entra ID) | SSO + OAuth2 authorization | Phase 1 |
| SendGrid | Transactional email delivery | Phase 1 |
| Google Calendar API v3 | Leave event sync per-user | Phase 1 |
| PostgreSQL 15+ | Primary data store + Hangfire persistence | Phase 1 |
| Docker Hub / GitHub Container Registry | Container image hosting | Phase 1 |
| GitHub Actions | CI/CD pipeline | Phase 1 |

## Environment Variable Catalogue

### Backend (`lms-api`)

| Variable | Environment | Sensitivity | Description |
|----------|-------------|-------------|-------------|
| `ASPNETCORE_ENVIRONMENT` | All | Low | Development / Staging / Production |
| `ConnectionStrings__DefaultConnection` | All | **Secret** | PostgreSQL connection string |
| `ConnectionStrings__HangfireConnection` | All | **Secret** | Hangfire PostgreSQL connection string (may be same DB, different schema) |
| `Jwt__Secret` | All | **Secret** | JWT signing key (min 32 chars) |
| `Jwt__Issuer` | All | Low | Token issuer (e.g. `lms-api`) |
| `Jwt__Audience` | All | Low | Token audience (e.g. `lms-client`) |
| `Jwt__AccessTokenExpiryHours` | All | Low | Default: 24 |
| `Jwt__RefreshTokenExpiryDays` | All | Low | Default: 7 |
| `AzureAd__TenantId` | All | Medium | Azure AD tenant GUID |
| `AzureAd__ClientId` | All | Medium | App registration client ID |
| `AzureAd__ClientSecret` | All | **Secret** | App registration client secret |
| `AzureAd__CallbackUrl` | All | Low | OAuth2 redirect URI |
| `AzureAd__RoleMappings__Employee` | All | Low | AD group name → Employee role |
| `AzureAd__RoleMappings__Manager` | All | Low | AD group name → Manager role |
| `AzureAd__RoleMappings__HRAdmin` | All | Low | AD group name → HR Admin role |
| `AzureAd__RoleMappings__SuperAdmin` | All | Low | AD group name → Super Admin role |
| `SendGrid__ApiKey` | All | **Secret** | SendGrid API v3 key |
| `SendGrid__FromEmail` | All | Low | Sender email address |
| `SendGrid__FromName` | All | Low | Sender display name |
| `SendGrid__EmailStyle` | All | Low | `plaintext` or `html` (default: `html`) — emails use inline HTML bodies, no dynamic templates |
| `GoogleCalendar__ClientId` | All | Medium | Google OAuth2 client ID |
| `GoogleCalendar__ClientSecret` | All | **Secret** | Google OAuth2 client secret |
| `GoogleCalendar__RedirectUri` | All | Low | Google OAuth2 redirect URI |
| `Storage__AttachmentPath` | All | Low | Local filesystem path for attachments (e.g. `/app/uploads`) |
| `RateLimit__RequestsPerMinute` | All | Low | Default: 100 |
| `Hangfire__DashboardEnabled` | All | Low | true/false |
| `Cors__AllowedOrigins` | All | Low | Comma-separated frontend origins |

### Frontend (`lms-frontend` — build-time env, Vite)

| Variable | Environment | Description |
|----------|-------------|-------------|
| `VITE_API_BASE_URL` | All | Backend API base URL (e.g. `https://api.lms.internal`) |
| `VITE_AZURE_CLIENT_ID` | All | Azure AD app client ID (MSAL) |
| `VITE_AZURE_TENANT_ID` | All | Azure AD tenant ID (MSAL) |
| `VITE_AZURE_REDIRECT_URI` | All | MSAL redirect URI |

## Secrets Management Approach

- **Local development**: `.env` files (git-ignored) + Docker Compose `env_file`
- **Staging / Production**: Environment variables injected at container runtime (not baked into image)
- **CI/CD (GitHub Actions)**: GitHub Encrypted Secrets for build-time values
- **Never commit**: Connection strings, API keys, client secrets, JWT signing key
- All secrets at rest: encrypted via OS-level disk encryption on production server
- Passwords in DB: BCrypt-hashed; never stored or logged in plaintext

## Infrastructure-as-Code Conventions

- `docker-compose.yml` — full local dev stack
- `docker-compose.staging.yml` — staging overrides
- `Dockerfile` (backend) — multi-stage: build → publish → runtime (mcr.microsoft.com/dotnet/aspnet:8.0)
- `nginx.conf` — SPA serving + reverse proxy rules
- GitHub Actions `.github/workflows/ci.yml` — build, test, coverage check, Docker build
- GitHub Actions `.github/workflows/deploy-staging.yml` — deploy to staging on push to `main`

## Volume Mounts (Docker)

| Mount | Purpose |
|-------|---------|
| `./uploads:/app/uploads` | Attachment file storage (local dev + staging) |
| `postgres-data:/var/lib/postgresql/data` | PostgreSQL data persistence |
