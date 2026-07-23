# Database Guidelines — LMS (Quick Reference for Ralph-impl)

## Technology
PostgreSQL 15+. ORM: EF Core 8 (Code-First, Fluent API). Migrations: `dotnet ef migrations add`.

## Naming
- Tables: plural snake_case → `leave_requests`, `comp_off_credits`, `audit_logs`
- Columns: snake_case → `start_date`, `reporting_manager_id`, `created_at`
- PKs: `id UUID PRIMARY KEY DEFAULT gen_random_uuid()`
- FKs: `{ref_table_singular}_id` → `user_id`, `leave_type_id`
- Indexes: `ix_{table}_{column(s)}` → `ix_leave_requests_user_id`
- Unique constraints: `uq_{table}_{column(s)}` → `uq_departments_name`

## EF Core Rules
- Entity configs in `LMS.Infrastructure.Persistence.Configurations/[Entity]Configuration.cs`
- Always `builder.ToTable("snake_case_plural_name")`
- Always `builder.Property(x => x.PropName).HasColumnName("snake_case_name")`
- Decimal precision: `HasPrecision(5, 1)` for leave balance columns
- Enum columns: store as `string` (varchar + CHECK), not integer
- FK cascade: `DeleteBehavior.Restrict` by default; only use Cascade when intentional
- **No lazy loading** — use `.Include()` or projection (Select) explicitly
- Global query filters for soft-deleted entities (e.g. `u.Status == "Active"`)

## Soft Delete
- Users / Departments: `status VARCHAR(20)` (Active / Inactive)
- Leave types: `is_active BOOLEAN`
- CompOffCredits: `status VARCHAR(20)` (Active / Used / Expired)
- Never `DELETE` application rows — set status to Inactive

## Audit Trail
- Table: `audit_logs` — append-only, **no UPDATE or DELETE ever**
- All state-changing operations call `AuditService.LogAsync()` before returning
- `old_value` / `new_value`: JSONB

## ID Strategy
- All PKs: UUID (`gen_random_uuid()`)
- Never use integer sequences as PKs

## Timestamps
- All tables: `created_at TIMESTAMPTZ DEFAULT NOW()` and `updated_at TIMESTAMPTZ`
- Store UTC in DB; apply IST conversion in application logic

## Required Indexes
Index ALL of the following:
- Every FK column
- `leave_requests`: status, start_date, end_date, (user_id + status) composite
- `approval_steps`: (leave_request_id + level)
- `audit_logs`: user_id, action, created_at
- `notifications`: (user_id + read)
- `comp_off_credits`: (user_id + status + expiry_date)
- `holidays`: date
- `departments`: name (unique), code (unique)

## Migration Rules
- One migration per logical change; descriptive names: `AddLeaveRequestsTable`
- Never hand-edit committed migration files — add a corrective migration instead
- `dotnet ef database update` runs in deploy script before API restart
- Seed runs automatically in `Program.cs` via idempotent `SeedDatabase()` on startup

## Connection
- Via env var: `ConnectionStrings__DefaultConnection`
- Hangfire: `ConnectionStrings__HangfireConnection` (same DB, `hangfire` schema)
- TLS required in staging/prod: `sslmode=require`
- Local dev: `sslmode=disable` acceptable
