# Deployment — Leave Management System (LMS)

> **Amendment (HIL 2):** No Docker in Phase 1. Direct host deployment.

## Environment Model

| Environment | Purpose | Deployment Method | Access |
|-------------|---------|-------------------|--------|
| Local (dev) | Developer machine | `dotnet run` (API) + `npm run dev` (frontend) + native PostgreSQL | Developer only |
| Staging | QA, UAT, integration tests, E2E | Manual deploy to staging server (IIS / systemd service) | QA team + stakeholders |
| Production | Live system | Manual deploy to production server (GitHub Actions → publish artifacts → restart service) | End users |

## Deployment Strategy (No Docker)

### Backend (ASP.NET Core API)

**Build**:
```bash
dotnet publish -c Release -o ./publish
```

**Run** (choose one per environment):
- **Windows**: IIS with ASP.NET Core module (reverse proxy from IIS to Kestrel), or Windows Service via `UseWindowsService()`
- **Linux**: systemd service running `dotnet LMS.API.dll`

**Configuration**: `appsettings.json` (non-secret, committed) + environment variables or `appsettings.Production.json` (git-ignored) for secrets.

**Migrations**: Run `dotnet ef database update` as part of the deployment script before starting the API.

**Seeding**: Idempotent seed runs automatically on startup (`app.SeedDatabase()` in `Program.cs`).

### Frontend (React SPA)

**Build**:
```bash
npm run build   # outputs to ./dist
```

**Serve**:
- Copy `./dist` to Nginx `html` root or IIS static site folder
- Nginx/IIS must serve `index.html` for all unmatched routes (SPA fallback)

**Nginx config snippet**:
```nginx
server {
    listen 80;
    root /var/www/lms;
    index index.html;

    location /api/ {
        proxy_pass http://localhost:5000;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Request-ID $request_id;
    }

    location / {
        try_files $uri $uri/ /index.html;
    }
}
```

### PostgreSQL

- Install PostgreSQL 15+ natively on the server
- Create database `lms` and user `lms_user`
- Connection string provided via environment variable
- Backup: `pg_dump` scheduled via cron (daily, retained 7 days)

## CI/CD Pipeline (GitHub Actions)

### `.github/workflows/ci.yml` — on every push / PR

```
1. Checkout code
2. Setup .NET 8 SDK
3. dotnet restore
4. dotnet build --no-restore -c Release
5. dotnet test --collect:"XPlat Code Coverage"
6. Check coverage ≥ 80%
7. dotnet format --verify-no-changes
8. Setup Node 20
9. npm ci (frontend)
10. npm run lint (zero errors)
11. npm run test (Vitest)
12. npm run build
```

### `.github/workflows/deploy-staging.yml` — on push to `main`

```
1. Run CI (all steps above)
2. dotnet publish -c Release -o ./publish
3. npm run build → ./dist
4. SSH to staging server
5. Copy ./publish → /opt/lms/api/
6. Copy ./dist → /var/www/lms/
7. Run: dotnet ef database update (or migration script)
8. Restart API service (systemctl restart lms-api / iisreset)
9. GET /health → HTTP 200 (retry 3x, 10s interval)
10. Smoke test key endpoints
```

### `.github/workflows/deploy-prod.yml` — manual trigger with approval

```
1. Require manual approval (GitHub Environments: production)
2. Same publish + copy + migrate + restart steps as staging
3. POST-deploy health check
4. Tag release: git tag v{version}
```

## Rollback Procedure

**Staging**: Re-deploy previous `main` commit via GitHub Actions (re-run workflow on prior SHA).

**Production**:
```bash
# On production server:
# Keep previous publish in /opt/lms/api-previous/
cp -r /opt/lms/api/ /opt/lms/api-previous/
# On rollback:
cp -r /opt/lms/api-previous/ /opt/lms/api/
systemctl restart lms-api
```

Retain the last 2 published builds on the server for instant rollback.

## Infrastructure-as-Code Conventions

| File | Purpose |
|------|---------|
| `.github/workflows/ci.yml` | CI pipeline |
| `.github/workflows/deploy-staging.yml` | Staging CD |
| `.github/workflows/deploy-prod.yml` | Prod CD (manual) |
| `nginx.conf` | Nginx reverse proxy + SPA serving |
| `scripts/deploy.sh` | Deployment helper script (copy, migrate, restart) |
| `.env.example` | Template with all required env var names (no values) |
| `appsettings.json` | Non-secret config (committed) |
| `appsettings.Production.json` | Production secrets (git-ignored; set on server) |

## SSL / TLS

- Local dev: HTTP only
- Staging: Let's Encrypt via Certbot + Nginx
- Production: Valid TLS cert; HTTPS enforced; HTTP → HTTPS redirect in Nginx

## Startup Sequence (API)

1. Validate required env vars (fail fast if missing)
2. `dotnet ef database update` (run by deploy script before service restart)
3. `app.SeedDatabase()` — idempotent seed on startup
4. Register Hangfire recurring jobs (`AddOrUpdate` — idempotent)
5. Start Kestrel / IIS, accept requests

## Attachment Storage

- Local filesystem: `/opt/lms/uploads/` (Linux) or `C:\lms\uploads\` (Windows)
- Configured via `Storage__AttachmentPath` env var
- Backed up with the server's regular backup schedule
- Path must be readable and writable by the API process user
