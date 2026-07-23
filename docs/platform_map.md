# Platform Map — LMS V5

## Repository
Platform: GitHub
Owner: radixdt3345
Repo: LMS
URL: https://github.com/radixdt3345/LMS
Visibility: public
Default branch: main

## CI/CD
| Workflow | File | Trigger | Purpose |
|----------|------|---------|---------|
| CI | `.github/workflows/ci.yml` | PR → main | Build + unit + integration tests + coverage gate (80%) |
| E2E | `.github/workflows/e2e.yml` | workflow_dispatch ONLY | Smoke (@smoke tag) or full Playwright suite |

## Project Board
URL: https://github.com/radixdt3345/LMS/projects
(Created after /push-to-pms runs)

## Milestones (one per Epic)
| Epic ID | Milestone Title | GitHub Milestone |
|---------|----------------|-----------------|
| E-01 | User Authentication | (created by /push-to-pms) |
| E-02 | Account Lockout Management | (created by /push-to-pms) |
| E-03 | Initial Data Seeding | (created by /push-to-pms) |
| E-04 | Department Management | (created by /push-to-pms) |
| E-05 | Employee Management | (created by /push-to-pms) |
| E-06 | Leave Types and Policies | (created by /push-to-pms) |
| E-07 | Leave Balance Management | (created by /push-to-pms) |
| E-08 | Public Holiday Calendar | (created by /push-to-pms) |
| E-09 | Leave Request Workflow | (created by /push-to-pms) |
| E-10 | Comp-Off Request Workflow | (created by /push-to-pms) |
| E-11 | Approval Engine | (created by /push-to-pms) |
| E-12 | Notifications | (created by /push-to-pms) |
| E-13 | Dashboards and Reporting | (created by /push-to-pms) |
| E-14 | Audit Trail | (created by /push-to-pms) |

## Credentials
GitHub PAT: stored in `.env.local.txt` (local only — never committed; listed in .gitignore)
CI secrets required:
- `TEST_DB_CONNECTION` — PostgreSQL connection string for integration tests
- `E2E_USER_EMAIL` — test user email for Playwright
- `E2E_USER_PASSWORD` — test user password for Playwright
CI variables required:
- `STAGING_URL` — base URL for E2E runs
