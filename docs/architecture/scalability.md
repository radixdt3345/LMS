# Scalability — Leave Management System (LMS)

## Expected Load (from PRD NFRs)

| Metric | Target |
|--------|--------|
| Concurrent authenticated users | 500 |
| CRUD endpoint p50 response time | < 200ms |
| CRUD endpoint p99 response time | < 500ms |
| Report endpoint p99 response time | < 2,000ms |
| System uptime | 99.9% |
| API rate limit | 100 req/min per user |

LMS is an internal enterprise tool for a single organization. 500 concurrent users is the Phase 1 ceiling. The architecture does not need to support horizontal scaling in Phase 1 but is designed to enable it in Phase 2 without rewrites.

## Horizontal Scaling Approach

**Phase 1**: Single API instance + single PostgreSQL instance (Docker Compose on a server).

**Phase 2 readiness** (designed in, not implemented):
- The API is stateless: JWT validation requires only the signing key (env var), not session state
- Refresh tokens stored in DB (not in-memory): multiple API instances can validate them
- No in-process caching that would create split-brain issues
- Hangfire distributed lock (PostgreSQL-based) prevents duplicate job execution if multiple API instances are added

To scale horizontally in Phase 2: add API instances behind an Nginx load balancer (round-robin). No code changes required.

## Caching Strategy

| What | Where | TTL | Invalidation |
|------|-------|-----|-------------|
| Holiday list (current year) | In-memory (IMemoryCache) | 1 hour | Invalidated on any holiday create/update/delete |
| Leave type list (active) | In-memory (IMemoryCache) | 30 minutes | Invalidated on any leave type create/update/deactivate |
| JWT claims (role, dept) | JWT payload (client-side) | 24h (token expiry) | Role changes take effect on next token refresh |
| Leave balances | No server-side cache — read from DB on each request (BAL-09) | — | N/A |
| Notification unread count | Frontend polls every 60s | 60s (polling interval) | N/A (poll-based) |
| Department list | In-memory (IMemoryCache) | 30 minutes | Invalidated on dept CRUD |

**No Redis in Phase 1** — IMemoryCache is sufficient for 500 concurrent users and a single API instance. Redis is the Phase 2 upgrade path.

**Frontend caching**: Redux store holds leave balances, leave types, notifications, holidays. Invalidated on relevant mutations (FE-NFR-08).

## Connection Pooling

- **PostgreSQL**: Npgsql built-in connection pool
  - Min pool size: 5
  - Max pool size: 100 (configurable via connection string)
  - Connection timeout: 30s
  - Command timeout: 60s (30s for CRUD, 60s for report queries)
- **Hangfire**: uses its own connection pool from the Hangfire connection string

## Async Job Queue (Hangfire)

| Job | Queue | Workers | Concurrency |
|-----|-------|---------|-------------|
| EmailDispatchJob | `email` | 2 workers | 2 concurrent emails |
| CalendarSyncJob / CalendarDeleteJob | `calendar` | 1 worker | 1 at a time |
| EscalationJob | `scheduled` | 1 worker | 1 (daily) |
| CompOffExpiryJob | `scheduled` | 1 worker | 1 (daily) |
| YearEndLapseJob / NewYearCreditJob | `scheduled` | 1 worker | 1 (yearly) |

Hangfire server configuration:
- Worker count: 5 total (split across queues above)
- Heartbeat interval: 30s
- Job retry: per-job configuration (email: 5x, calendar: 3x, scheduled: 1x with alert on failure)

## CDN

**Phase 1**: No CDN. React SPA served by Nginx from the container.

**Phase 2**: Serve the React SPA from a CDN (CloudFront / Azure Front Door) for reduced latency to distributed users.

## Database Query Optimization

- All hot-path queries must use indexed columns (see database_strategy.md indexing rules)
- Report queries (utilization, trends): use `GROUP BY` + aggregate functions; avoid loading full datasets into memory
- Pagination enforced on all list endpoints (max 100 per page)
- Report CSV export: stream response using `IAsyncEnumerable` + `Response.Body` streaming — never load all rows into memory
- Sandwich rule: computed in-process using holiday lookup from IMemoryCache (no additional DB round-trip during submission)
