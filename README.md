# Leave Management System (LMS)

Enterprise web application that replaces manual leave tracking with an automated, policy-driven system covering the full leave lifecycle — from employee application through multi-level approval, balance tracking, and compliance reporting.

## Stack

| Layer | Technology |
|-------|-----------|
| Backend API | C# 12, .NET 8, ASP.NET Core Web API |
| Database | PostgreSQL 15+ |
| Job Queue | Hangfire (PostgreSQL storage) |
| Auth | JWT + Azure AD OAuth2 (MSAL) |
| Email | SendGrid API v3 |
| Calendar | Google Calendar API v3 |
| Frontend | React 17, MUI v5, Redux Toolkit + Redux-Saga |
| Build | Vite + Nginx |

## Getting Started

*Will be updated as the build progresses.*

## Running Tests

*Will be updated as the build progresses.*

## Environment Setup

Copy `.env.example` to `.env` and fill in required values (see `docs/04_infrastructure.md` for the full variable catalogue).

```bash
docker compose up -d
```

## Architecture

See `docs/` for full architecture documentation:
- `docs/00_discovery.md` — Project discovery and requirements summary
- `docs/02_technical.md` — Full tech stack
- `docs/03_orchestration.md` — Workflows and job patterns
- `docs/04_infrastructure.md` — Infrastructure and env vars
- `docs/06_rbac.md` — Role registry and permission matrix
- `CONSTITUTION.md` — Engineering standards (generated next)
