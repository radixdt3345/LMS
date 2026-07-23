# API Standards — Leave Management System (LMS)

## API Style

REST (Representational State Transfer). All endpoints under `/api/v1/` prefix.

## URL Structure

```
/api/v1/{resource}                  → Collection
/api/v1/{resource}/{id}             → Single resource
/api/v1/{resource}/{id}/{action}    → Action on resource (e.g. /submit, /approve, /cancel)
/api/v1/{resource}/{id}/{sub}       → Sub-resource (rare)
```

**Rules:**
- Resource names: **plural, kebab-case**: `leave-requests`, `comp-off-requests`, `leave-types`
- Action suffixes: lowercase verb: `/submit`, `/approve`, `/reject`, `/cancel`, `/revoke`, `/unlock`
- No verbs in the resource path (use HTTP method + action suffix instead)
- Query string for filtering, sorting, pagination: `?status=Pending&page=1&limit=20`

## HTTP Status Codes

| Code | When Used |
|------|-----------|
| 200 OK | Successful GET, successful action (approve, cancel, etc.) |
| 201 Created | Successful POST that creates a resource |
| 204 No Content | Successful DELETE or logout |
| 400 Bad Request | Malformed request body / invalid JSON |
| 401 Unauthorized | Missing or invalid JWT |
| 403 Forbidden | Valid JWT but insufficient role/scope |
| 404 Not Found | Resource does not exist |
| 409 Conflict | Duplicate resource (email, dept name/code) |
| 422 Unprocessable Entity | Validation error (business rule violation: insufficient balance, overlap, etc.) |
| 423 Locked | Account locked after 3 failed login attempts |
| 429 Too Many Requests | Rate limit exceeded (100 req/min per user) |
| 500 Internal Server Error | Unhandled server-side exception |
| 503 Service Unavailable | DB or critical dependency down |

## Error Envelope Format

All errors return the same envelope:

```json
{
  "success": false,
  "error": {
    "code": "INSUFFICIENT_BALANCE",
    "message": "You do not have enough Casual Leave balance to submit this request.",
    "details": [
      {
        "field": "leave_type_id",
        "message": "Balance available: 0.5 days, requested: 2.0 days."
      }
    ]
  }
}
```

**Error code conventions**: `UPPER_SNAKE_CASE`, domain-prefixed where helpful:
- `AUTH_INVALID_CREDENTIALS`, `AUTH_ACCOUNT_LOCKED`, `AUTH_TOKEN_EXPIRED`
- `LEAVE_INSUFFICIENT_BALANCE`, `LEAVE_OVERLAP_CONFLICT`, `LEAVE_TEAM_OVERLAP_LIMIT`
- `LEAVE_WEEKEND_OR_HOLIDAY`, `LEAVE_CANNOT_CANCEL_STARTED`, `LEAVE_CANNOT_REVOKE_STARTED`
- `COMP_OFF_INVALID_DATE`, `COMP_OFF_INSUFFICIENT_HOURS`
- `VALIDATION_ERROR` (generic; use `details` array for field-level messages)
- `NOT_FOUND`, `CONFLICT`, `FORBIDDEN`, `INTERNAL_ERROR`

## Success Envelope Format

Single resource:
```json
{
  "success": true,
  "data": { ... }
}
```

Collection:
```json
{
  "success": true,
  "data": [ ... ],
  "total": 142,
  "page": 1,
  "limit": 20
}
```

Action responses (approve, cancel, etc.) that don't return a body:
```json
{
  "success": true,
  "data": null
}
```

## Authentication

- **Header**: `Authorization: Bearer {access_token}` on every protected request
- **Refresh**: Sent automatically via HttpOnly cookie `refresh_token` on POST `/api/v1/auth/refresh`
- **Unauthenticated routes**: `/health`, `/api/v1/auth/login`, `/api/v1/auth/sso/login`, `/api/v1/auth/sso/callback`, `/api/v1/auth/refresh`

## Versioning

- URL path versioning: `/api/v1/`
- Version bump to `v2` only for breaking changes
- `v1` remains available for minimum 6 months after `v2` ships
- No header-based versioning in Phase 1

## Rate Limiting

- 100 requests per minute per authenticated user (keyed on `user_id` from JWT)
- Response on limit exceeded: HTTP 429 with header `Retry-After: {seconds}`
- Rate limiting applied after JWT validation (unauthenticated requests handled by auth middleware first)

## Pagination

All list endpoints support:
```
?page=1&limit=20
```
- Default `limit`: 20
- Maximum `limit`: 100 (server enforces; requests above 100 clamped to 100)
- Response always includes `total`, `page`, `limit` in envelope

## Filtering & Sorting

- Filter parameters: query string, snake_case names matching column names: `?status=Approved&leave_type_id=uuid&date_from=2026-01-01&date_to=2026-12-31`
- Sort: `?sort_by=created_at&sort_dir=desc` (default: `created_at desc` on most endpoints)

## Content Type

- All requests: `Content-Type: application/json` (except file upload: `multipart/form-data`)
- All responses: `Content-Type: application/json` (except CSV export: `Content-Type: text/csv`)
- File upload: attachment on leave request — field name `attachment`, `multipart/form-data`
- CSV bulk import: holiday import — field name `file`, `multipart/form-data`

## API Versioning Header (optional, for debugging)

Include `X-API-Version: 1.0` in all responses for client-side version detection.

## CORS

- Allowed origins: configured via `Cors__AllowedOrigins` env var (comma-separated)
- Allowed methods: `GET, POST, PUT, DELETE, OPTIONS`
- Allowed headers: `Authorization, Content-Type, X-Request-ID`
- Allow credentials: `true` (required for HttpOnly cookie refresh token)
- Preflight cache: `max-age=86400`

## Request Tracing

- Every request assigned a `X-Request-ID` (generated by middleware if not provided by client)
- `X-Request-ID` included in all response headers
- Logged on every request for correlation across logs and Hangfire jobs
