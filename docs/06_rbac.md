# Role Registry & Permission Matrix — Leave Management System (LMS)

## Roles

| Role | Description | User Count Estimate | Source |
|------|-------------|--------------------|----|
| Employee | Any staff member — applies for leave, views own balance/history | Majority of users | Auto-assigned on account creation; default for unmatched SSO users |
| Manager | Team lead — approves L1 leave/comp-off, views team calendar, team balances | Subset of employees with direct reports | Auto-derived: set when first direct report linked; reverted when last direct report removed |
| HR Admin | HR team — L2 approvals, employee/department/leave-type/holiday management, reporting, locked account management | Small team (2–5) | Assigned by Super Admin |
| Super Admin | Full system access — all data, all audit logs, system-wide metrics | 1–2 | Assigned during setup |

## Role Hierarchy & Inheritance

```
Super Admin
  └── HR Admin (inherits: department read, employee read, leave read, approvals L2, reports)
       └── Manager (inherits: employee read own team, leave read own team, approvals L1)
            └── Employee (base: own leave, own profile, own notifications)
```

**Exception:** Personal leave screens are Employee + Manager only:
- Apply for Leave
- Employee Dashboard
- My Leave Balances
- My Leave History
- My Profile
- Cancel Leave Request
- Comp-Off Request

HR Admin and Super Admin **cannot apply for or cancel leave** in this system.

## Permission Matrix

| Resource / Action | Employee | Manager | HR Admin | Super Admin |
|-------------------|----------|---------|----------|-------------|
| **Auth** | | | | |
| Login (SSO + local) | ✅ | ✅ | ✅ | ✅ |
| Refresh token | ✅ | ✅ | ✅ | ✅ |
| Logout | ✅ | ✅ | ✅ | ✅ |
| Unlock locked account | ❌ | ❌ | ✅ | ✅ |
| View locked accounts | ❌ | ❌ | ✅ | ✅ |
| **Employees** | | | | |
| View own profile | ✅ | ✅ | ❌ | ❌ |
| Edit own profile (name, phone) | ✅ | ✅ | ❌ | ❌ |
| View own team (direct reports) | ❌ | ✅ (if has subordinates) | ✅ | ✅ |
| View any employee | ❌ | ✅ (own team only) | ✅ | ✅ |
| Create employee | ❌ | ❌ | ✅ | ✅ |
| Edit employee | ❌ | ❌ | ✅ | ✅ |
| Deactivate employee (soft delete) | ❌ | ❌ | ✅ | ✅ |
| **Departments** | | | | |
| List / read departments | 👁 | 👁 | ✅ | ✅ |
| Create department | ❌ | ❌ | ✅ | ✅ |
| Edit department | ❌ | ❌ | ✅ | ✅ |
| Deactivate department | ❌ | ❌ | ✅ | ✅ |
| **Leave Types** | | | | |
| List / read leave types | ✅ | ✅ | ✅ | ✅ |
| Create / edit leave type | ❌ | ❌ | ✅ | ✅ |
| Deactivate leave type | ❌ | ❌ | ✅ | ✅ |
| **Leave Balances** | | | | |
| View own balances | ✅ | ✅ | ❌ | ❌ |
| View own team's balances | ❌ | ✅ (direct reports) | ✅ | ✅ |
| View department balances | ❌ | ❌ | ✅ | ✅ |
| **Leave Requests** | | | | |
| Submit leave request | ✅ | ✅ | ❌ | ❌ |
| View own leave requests | ✅ | ✅ | ❌ | ❌ |
| View leave request detail (own) | ✅ | ✅ | ✅ | ✅ |
| View leave request detail (team) | ❌ | ✅ (own team) | ✅ | ✅ |
| Cancel leave request (before start date) | ✅ | ✅ | ❌ | ❌ |
| Revoke leave (before start date) | ❌ | ❌ | ✅ | ✅ |
| **Approvals** | | | | |
| View pending approvals (L1) | ❌ | ✅ (own team only) | ✅ | ✅ |
| Approve L1 | ❌ | ✅ (own team) | ✅ (when acting as L1 for no-manager employees) | ❌ |
| Reject L1 | ❌ | ✅ (own team) | ✅ (when acting as L1 for no-manager employees) | ❌ |
| View pending approvals (L2) | ❌ | ❌ | ✅ | ✅ |
| Approve L2 | ❌ | ❌ | ✅ | ❌ |
| Reject L2 | ❌ | ❌ | ✅ | ❌ |
| **Comp-Off Requests** | | | | |
| Submit comp-off request | ✅ | ✅ | ❌ | ❌ |
| View own comp-off requests | ✅ | ✅ | ❌ | ❌ |
| View pending comp-off approvals | ❌ | ✅ (own team) | ✅ | ✅ |
| Approve comp-off | ❌ | ✅ (own team) | ✅ (for no-manager employees) | ❌ |
| Reject comp-off | ❌ | ✅ (own team) | ✅ (for no-manager employees) | ❌ |
| **Holidays** | | | | |
| View holidays | ✅ | ✅ | ✅ | ✅ |
| Create / edit / delete holiday | ❌ | ❌ | ✅ | ✅ |
| Bulk import holidays (CSV) | ❌ | ❌ | ✅ | ✅ |
| **Notifications** | | | | |
| View own notifications | ✅ | ✅ | ✅ | ✅ |
| Mark read / mark all read | ✅ | ✅ | ✅ | ✅ |
| **Reports & Dashboards** | | | | |
| Employee dashboard | ✅ | ✅ | ❌ | ❌ |
| Manager dashboard (team calendar, pending count) | ❌ | ✅ (if has subordinates) | ❌ | ❌ |
| HR dashboard (dept utilization, trends, compliance) | ❌ | ❌ | ✅ | ✅ |
| Super Admin dashboard (system-wide metrics) | ❌ | ❌ | ❌ | ✅ |
| Team calendar | ❌ | ✅ | ✅ | ✅ |
| Department reports + CSV export | ❌ | ❌ | ✅ | ✅ |
| **Audit Trail** | | | | |
| View audit log (all entries) | ❌ | ❌ | ✅ | ✅ |
| Search / filter audit log | ❌ | ❌ | ✅ | ✅ |

## RBAC Implementation Notes

### Auth Mechanism
- **Primary**: Azure AD OAuth2 Authorization Code Flow (MSAL)
- **Fallback**: Local email + password (BCrypt hashed)
- **Session**: JWT access token (24h, memory-only in browser) + refresh token (7d, HttpOnly cookie, stored in DB)

### JWT Claims
```json
{
  "user_id": "<uuid>",
  "role": "Employee | Manager | HR Admin | Super Admin",
  "department_id": "<uuid>",
  "exp": <unix_timestamp>
}
```
Role changes take effect on next token refresh (not immediately mid-session).

### Enforcement Points
- **Backend**: ASP.NET Core `[Authorize(Roles = "...")]` attribute on every controller action
- **Middleware**: JWT validation middleware on all routes except `/health` and `/api/v1/auth/*`
- **Scoped queries**: Manager endpoints filter by `reporting_manager_id = current_user_id`
- **Frontend**: `ProtectedRoute` (login check) + `RoleProtectedRoute` (role-based screen access)
- **Frontend nav**: Menu items not accessible to current role are not rendered (FE-NFR-05)

### Multi-Tenancy Model
None — single-organization deployment.

### Role Auto-Derivation Rules
1. When employee `reporting_manager_id` = User X is saved → if User X.role == Employee → set User X.role = Manager
2. When the **last** direct report of User X is removed/reassigned → set User X.role = Employee
3. Role is **not** a manually editable form field — system-derived only
4. If SSO user's AD group does not match any configured mapping → default role: Employee
