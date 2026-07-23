# Tech Stack — LMS (Quick Reference)

| Layer | Technology | Version | Notes |
|-------|-----------|---------|-------|
| Backend language | C# | 12 | Nullable enabled |
| Backend runtime | .NET | 8 | ASP.NET Core Web API |
| ORM | EF Core | 8.x | Code-First, Fluent API, Npgsql provider |
| Database | PostgreSQL | 15+ | Primary data + Hangfire storage (hangfire schema) |
| Job queue | Hangfire | 1.8.x | PostgreSQL storage, recurring + enqueued jobs |
| Auth — SSO | Azure AD (MSAL) | Microsoft.Identity.Web 2.x | OAuth2 Authorization Code Flow |
| Auth — local | BCrypt | BCrypt.Net-Next 4.x | Cost factor 12 |
| Auth — tokens | JWT | System.IdentityModel.Tokens.Jwt 7.x | HS256, 24h access / 7d refresh |
| Email | SendGrid | SendGrid SDK 9.x | v3 API, plain text / inline HTML (no dynamic templates) |
| Calendar | Google Calendar | Google.Apis.Calendar.v3 | Per-user OAuth2 consent |
| Validation | FluentValidation | 11.x | All request DTOs |
| Logging | Serilog | 8.x | Structured JSON, Console + File sinks |
| Caching | IMemoryCache | Built-in | Holiday list, leave types, dept list |
| Testing (backend) | xUnit | 2.x | + Moq 4.x + coverlet |
| Frontend framework | React | 17.x | Functional components + Hooks |
| UI library | MUI (Material-UI) | 5.x | Includes @mui/x-date-pickers 6.x |
| State management | Redux Toolkit + Redux-Saga | 1.x + 1.x | Store, slices, sagas |
| React-Redux | react-redux | 8.x | useAppSelector, useAppDispatch |
| HTTP client | Axios | 1.x | JWT interceptor + 401 → refresh |
| Frontend auth | MSAL React | @azure/msal-react 2.x | Azure AD SSO |
| Routing | React Router | v6 | ProtectedRoute + RoleProtectedRoute |
| Forms | React Hook Form | 7.x | All forms |
| Charts | Chart.js + react-chartjs-2 | 4.x + 5.x | Dashboard charts |
| Calendar UI | FullCalendar | @fullcalendar/react 6.x | Team leave calendar |
| Build tool | Vite | 5.x | Frontend build + dev server |
| Web server | Nginx | latest stable | SPA serving + API reverse proxy |
| CI/CD | GitHub Actions | — | Build, test, deploy |
| TypeScript | TypeScript | strict mode | Frontend only |
| Formatter (BE) | dotnet format | Built-in | EditorConfig |
| Formatter (FE) | Prettier | — | .prettierrc |
| Linter (BE) | Roslyn analyzers + SonarAnalyzer | — | |
| Linter (FE) | ESLint | @typescript-eslint | plugin:react-hooks/recommended |
| Test (FE) | Vitest | — | Component + unit tests |
| E2E | Playwright | — | Critical user flows |

## Deployment (No Docker)
- API: Published via `dotnet publish -c Release`, hosted on IIS (Windows) or systemd (Linux)
- Frontend: Built via `npm run build`, served by Nginx from static `dist/` folder
- PostgreSQL: Native install on server
- No container orchestration in Phase 1
