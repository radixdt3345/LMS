# Technical Stack — Leave Management System (LMS)

## Runtime & Languages

| Layer | Language / Runtime |
|-------|-------------------|
| Backend API | C# 12, .NET 8 |
| Frontend SPA | JavaScript (ES2020+), React 17 |
| Database scripts | SQL (PostgreSQL dialect) |
| Infrastructure | Bash, YAML (Docker Compose, GitHub Actions) |

## Frameworks & Libraries

### Backend
| Package | Version | Purpose |
|---------|---------|---------|
| ASP.NET Core Web API | .NET 8 | REST API server |
| Entity Framework Core | 8.x | ORM + migrations |
| Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | EF Core PostgreSQL provider |
| Hangfire | 1.8.x | Background jobs + recurring scheduler |
| Hangfire.PostgreSql | 1.x | Hangfire PostgreSQL storage |
| Microsoft.Identity.Web | 2.x | Azure AD MSAL integration |
| System.IdentityModel.Tokens.Jwt | 7.x | JWT creation + validation |
| SendGrid | 9.x | Email delivery (SendGrid API v3) |
| Google.Apis.Calendar.v3 | latest stable | Google Calendar event sync |
| BCrypt.Net-Next | 4.x | Password hashing (local login) |
| FluentValidation.AspNetCore | 11.x | Request validation |
| Serilog.AspNetCore | 8.x | Structured logging |
| xUnit | 2.x | Unit + integration tests |
| Moq | 4.x | Mocking in tests |
| coverlet.collector | 6.x | Test coverage |

### Frontend
| Package | Version | Purpose |
|---------|---------|---------|
| React | 17.x | SPA framework |
| React DOM | 17.x | DOM rendering |
| @mui/material | 5.x | UI component library |
| @mui/x-date-pickers | 6.x | Date range picker |
| @reduxjs/toolkit | 1.x | Redux store + slices |
| redux-saga | 1.x | Side-effect middleware |
| react-redux | 8.x | React-Redux bindings |
| react-router-dom | 6.x | SPA routing + protected routes |
| axios | 1.x | HTTP client + interceptors |
| @azure/msal-react | 2.x | Azure AD SSO (MSAL React) |
| @azure/msal-browser | 3.x | MSAL browser runtime |
| chart.js | 4.x | Charts engine |
| react-chartjs-2 | 5.x | React wrapper for Chart.js |
| @fullcalendar/react | 6.x | Team calendar view |
| @fullcalendar/daygrid | 6.x | Month/day grid plugin |
| react-hook-form | 7.x | Form management + validation |
| sass | 1.x | SCSS styling |
| vite | 5.x | Frontend build tool |

## Infrastructure

| Component | Technology |
|-----------|-----------|
| Web server (frontend) | Nginx (static SPA serving) |
| Backend runtime | Docker + Docker Compose |
| CI/CD | GitHub Actions |
| Container registry | Docker Hub (or GitHub Container Registry) |
| Reverse proxy | Nginx (routes /api/* → backend, /* → frontend SPA) |
| Local dev | Docker Compose (backend + postgres + hangfire) |

## Databases

| Database | Purpose | Technology |
|----------|---------|-----------|
| Primary data store | All application data | PostgreSQL 15+ |
| Job persistence | Hangfire background jobs | PostgreSQL 15+ (shared instance, separate schema) |

**Key conventions:**
- EF Core Code-First migrations for schema management
- Soft deletes via `status` / `is_active` flags (never hard-delete)
- Audit trail is append-only (no UPDATE/DELETE on audit_logs table)
- All timestamps in UTC internally; IST display in frontend

## CI/CD

| Stage | Tool / Config |
|-------|--------------|
| Build & test | GitHub Actions — `dotnet build`, `dotnet test`, `npm run build` |
| Code coverage | coverlet → threshold ≥80% |
| Docker image | `docker build` on merge to main |
| Deployment | `docker compose up -d` on target server |
| Environment promotion | dev → staging → prod (manual gate at prod) |

## Key Architectural Constraints

- Single-organization deployment — no multi-tenancy
- IST timezone for all business logic (Phase 1); UTC in DB
- REST API versioned under `/api/v1/`
- JWT access token: 24h expiry, stored in memory (frontend)
- Refresh token: 7d expiry, stored in DB + HttpOnly cookie
- API rate limiting: 100 requests/minute per authenticated user
- Attachment storage: local filesystem (Phase 1); max 5MB, PDF/JPG/PNG only
- p50 < 200ms, p99 < 500ms for CRUD endpoints
- p99 < 2s for report generation
- 500 concurrent authenticated users
- Minimum supported browsers: Chrome 120+, Edge 120+, Firefox 121+ (desktop only)
