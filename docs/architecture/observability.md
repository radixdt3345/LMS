# Observability — Leave Management System (LMS)

## Logging

### Framework
Serilog with structured JSON output (`Serilog.Sinks.Console` + `Serilog.Sinks.File`).

### Required Fields on Every Log Event

| Field | Type | Description |
|-------|------|-------------|
| `timestamp` | ISO 8601 UTC | When the event occurred |
| `level` | string | DEBUG / INFO / WARN / ERROR / FATAL |
| `service` | string | Always `"lms-api"` |
| `trace_id` | string | X-Request-ID from request (or Hangfire job ID for background jobs) |
| `user_id` | UUID / null | Authenticated user's ID; null for unauthenticated requests |
| `message` | string | Human-readable log message |
| `exception` | object / null | Exception details on ERROR/FATAL; null otherwise |

### Log Levels

| Level | When Used | Examples |
|-------|-----------|---------|
| DEBUG | Development only — never in staging/prod | EF Core query SQL, every DB parameter |
| INFO | Normal business events | "Leave request submitted", "L1 approval completed", "Hangfire job started" |
| WARN | Recoverable issues, degraded functionality | "SendGrid retry attempt 2/5", "Calendar sync failed — retrying", "Rate limit near threshold for user {id}" |
| ERROR | Failures that need attention | "SendGrid all retries exhausted for notification {id}", "Google Calendar OAuth token revoked for user {id}", "DB command timeout on report query" |
| FATAL | System cannot continue | "DB connection pool exhausted", "EF Core migration failed on startup" |

### Sensitive Field Masking
Serilog destructuring policies must mask (replace with `[REDACTED]`):
- Any field named: `password`, `token`, `secret`, `api_key`, `connection_string`, `client_secret`, `password_hash`
- JWT payload: never log the full JWT string
- Refresh token value: log only the token's DB ID, never the value

### Log Output Destinations

| Environment | Destination |
|-------------|-------------|
| Local dev | Console (colored output) |
| Staging | Console (JSON) + file (`/app/logs/lms-api-{date}.json`, 7-day retention) |
| Production | Console (JSON) + file (`/app/logs/lms-api-{date}.json`, 30-day retention) |

**Phase 2**: Ship to centralized log aggregation (e.g. Azure Monitor / Seq / ELK). Console JSON output in Phase 1 is compatible with log shipper agents.

### Hangfire Job Logging
- Each job logs: `INFO "Job started: {JobName} {JobId}"`, `INFO "Job completed: {JobName} {JobId} duration={ms}ms"`, `ERROR "Job failed: {JobName} {JobId} attempt={n}"` 
- `trace_id` for jobs: use Hangfire job ID

## Distributed Tracing

**Phase 1**: No distributed tracing system (single service, no microservices). `X-Request-ID` header provides per-request correlation across logs.

**Phase 2**: OpenTelemetry + Azure Monitor Application Insights (or Jaeger).

## Metrics

### Health Check Endpoint
`GET /health` (unauthenticated)

Response:
```json
{
  "status": "healthy",
  "checks": {
    "database": "healthy",
    "hangfire": "healthy"
  },
  "timestamp": "2026-07-22T06:30:00Z"
}
```

Returns HTTP 200 if all checks pass; HTTP 503 if any check fails.

### Key Metrics to Track (Phase 1 — manual monitoring via logs)

| Metric | How Measured | Alert Threshold |
|--------|-------------|-----------------|
| API p99 response time | Log request duration | > 500ms sustained for 5 min |
| Report p99 response time | Log request duration for /reports/* | > 2s sustained |
| Failed login attempts | Log count per user | > 3 consecutive (auto-lock triggers) |
| SendGrid delivery failures | Log email_status = failed count | > 5 per hour |
| Google Calendar sync failures | Log calendar_status = failed count | > 10 per day |
| Hangfire job failures | Hangfire dashboard / log ERROR | Any scheduled job failure |
| DB connection pool saturation | Log pool wait time | > 1s wait |

**Phase 2**: Prometheus metrics endpoint + Grafana dashboards.

## Alerting Thresholds (Phase 1 — manual log review)

| Condition | Severity | Action |
|-----------|----------|--------|
| API returns 5xx > 1% of requests in 5 min | HIGH | Page on-call; check DB and Hangfire |
| YearEndLapseJob fails on Dec 31 | CRITICAL | Manual run + audit correction |
| NewYearCreditJob fails on Jan 1 | CRITICAL | Manual run + audit correction |
| SendGrid all retries exhausted | MEDIUM | Check SendGrid account status |
| Google Calendar OAuth token revoked for user | LOW | Notify user to re-authorize |
| DB disk usage > 80% | HIGH | Plan PostgreSQL volume expansion |

## Request Tracing Convention

```
Incoming request → Middleware assigns X-Request-ID (or uses client-provided)
  → All log events in this request scope carry trace_id = X-Request-ID
  → X-Request-ID echoed in response headers
  → Hangfire jobs enqueued from this request carry parent_trace_id = X-Request-ID
```
