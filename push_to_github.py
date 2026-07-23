"""
push_to_github.py — /push-to-pms for LMS V5 (ralph-loop-v5 Forge pipeline)

Reads ai-context/issues.json and creates:
  - 14 GitHub Milestones (one per Epic)
  - Labels: layer:*, wave:*, domain:*, agent:*
  - 77 GitHub Issues with milestone, labels, body (ACs + depends_on links)

Writes:
  - ai-context/pms-map.json  (issue_id → github number + url)
  - task_status.md            (GitHub Issue column populated)

Run from project root:
  python push_to_github.py

Requirements: Python 3.8+ with 'requests' (pip install requests)
"""

import json
import os
import re
import time
import sys
from pathlib import Path

try:
    import requests
except ImportError:
    print("ERROR: 'requests' not installed. Run: pip install requests")
    sys.exit(1)

# ---------------------------------------------------------------------------
# Config — read PAT from .env.local.txt
# ---------------------------------------------------------------------------
BASE_DIR = Path(__file__).parent
ENV_FILE = BASE_DIR / ".env.local.txt"

def load_env():
    env = {}
    if ENV_FILE.exists():
        for line in ENV_FILE.read_text().splitlines():
            line = line.strip()
            if "=" in line and not line.startswith("#"):
                k, _, v = line.partition("=")
                env[k.strip()] = v.strip()
    return env

env = load_env()
PAT   = env.get("GITHUB_PAT", "")
OWNER = "radixdt3345"
REPO  = "LMS"

if not PAT:
    print("ERROR: GITHUB_PAT not found in .env.local.txt")
    sys.exit(1)

HEADERS = {
    "Authorization": f"Bearer {PAT}",
    "Accept": "application/vnd.github+json",
    "X-GitHub-Api-Version": "2022-11-28",
}
BASE_URL = f"https://api.github.com/repos/{OWNER}/{REPO}"

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------
def gh(method, path, **kwargs):
    """GitHub API call with rate-limit handling."""
    url = BASE_URL + path if path.startswith("/") else path
    for attempt in range(3):
        resp = requests.request(method, url, headers=HEADERS, **kwargs)
        if resp.status_code == 429 or (resp.status_code == 403 and "rate limit" in resp.text.lower()):
            wait = int(resp.headers.get("Retry-After", 60))
            print(f"  Rate limited — waiting {wait}s...")
            time.sleep(wait)
            continue
        return resp
    return resp

def ok(resp, expected=(200, 201)):
    return resp.status_code in expected

# ---------------------------------------------------------------------------
# Step 1 — Create labels
# ---------------------------------------------------------------------------
LABELS = [
    # Layer labels
    ("layer:DB",    "0075ca", "Database migration / EF Core"),
    ("layer:API",   "e4e669", "API / service layer"),
    ("layer:UI",    "d93f0b", "React frontend"),
    ("layer:INT",   "0e8a16", "Integration wiring / tests"),
    ("layer:TEST",  "5319e7", "Unit + integration test files"),
    ("layer:E2E",   "f9d0c4", "Playwright end-to-end tests"),
    # Wave labels
    ("wave:1",      "bfd4f2", "Wave 1 — Foundation"),
    ("wave:2",      "c5def5", "Wave 2 — Core workflows"),
    ("wave:3",      "fef2c0", "Wave 3 — Dashboards"),
    # Domain labels
    ("domain:AUTH",          "1d76db", "Authentication domain"),
    ("domain:INFRA",         "e11d48", "Infrastructure / seeding"),
    ("domain:PEOPLE",        "16a34a", "People / HR domain"),
    ("domain:LEAVECORE",     "dc2626", "Leave core domain"),
    ("domain:COMPOFF",       "7c3aed", "Comp-off domain"),
    ("domain:NOTIFICATIONS", "0284c7", "Notifications domain"),
    ("domain:REPORTING",     "b45309", "Reporting / audit domain"),
    # Agent labels
    ("agent:ralph-impl",  "6e40c9", "Implemented by Ralph-impl"),
    ("agent:ralph-test",  "2ea44f", "Written by Ralph-test"),
    ("agent:ralph-e2e",   "f97316", "Written by Ralph-e2e"),
]

def create_labels():
    print("\n── Creating labels ─────────────────────────────")
    existing = {l["name"] for l in gh("GET", "/labels", params={"per_page": 100}).json()}
    for name, color, desc in LABELS:
        if name in existing:
            print(f"  SKIP  {name}")
            continue
        r = gh("POST", "/labels", json={"name": name, "color": color, "description": desc})
        status = "OK  " if ok(r, (200, 201, 422)) else f"FAIL({r.status_code})"
        print(f"  {status}  {name}")
        time.sleep(0.2)

# ---------------------------------------------------------------------------
# Step 2 — Create milestones (one per epic)
# ---------------------------------------------------------------------------
EPIC_TO_FEATURE = {
    "EPIC-01": "F-01", "EPIC-02": "F-02", "EPIC-03": "F-03", "EPIC-04": "F-04",
    "EPIC-05": "F-05", "EPIC-06": "F-06", "EPIC-07": "F-07", "EPIC-08": "F-08",
    "EPIC-09": "F-09", "EPIC-10": "F-10", "EPIC-11": "F-11", "EPIC-12": "F-12",
    "EPIC-13": "F-13", "EPIC-14": "F-14",
}

def create_milestones(epics):
    print("\n── Creating milestones ──────────────────────────")
    existing = {m["title"]: m for m in gh("GET", "/milestones", params={"state": "open", "per_page": 100}).json()}
    milestone_map = {}  # epic_id → milestone_number
    for epic in epics:
        title = f"[{epic['id']}] {epic['title']}"
        if title in existing:
            milestone_map[epic["id"]] = existing[title]["number"]
            print(f"  SKIP  {title}")
            continue
        r = gh("POST", "/milestones", json={
            "title": title,
            "description": f"Feature {EPIC_TO_FEATURE[epic['id']]} — {epic['title']} (Wave {epic['wave']}, {epic['domain']})"
        })
        if ok(r, (200, 201)):
            n = r.json()["number"]
            milestone_map[epic["id"]] = n
            print(f"  OK    #{n}  {title}")
        else:
            print(f"  FAIL({r.status_code})  {title}: {r.text[:120]}")
        time.sleep(0.3)
    return milestone_map

# ---------------------------------------------------------------------------
# Step 3 — Determine labels per issue
# ---------------------------------------------------------------------------
LAYER_TO_AGENT = {
    "DB": "agent:ralph-impl",
    "API": "agent:ralph-impl",
    "UI": "agent:ralph-impl",
    "INT": "agent:ralph-impl",
    "TEST": "agent:ralph-test",
    "E2E": "agent:ralph-e2e",
}

EPIC_TO_DOMAIN = {
    "EPIC-01": "domain:AUTH",    "EPIC-02": "domain:AUTH",
    "EPIC-03": "domain:INFRA",
    "EPIC-04": "domain:PEOPLE",  "EPIC-05": "domain:PEOPLE",
    "EPIC-06": "domain:LEAVECORE", "EPIC-07": "domain:LEAVECORE",
    "EPIC-08": "domain:LEAVECORE", "EPIC-09": "domain:LEAVECORE",
    "EPIC-10": "domain:COMPOFF",
    "EPIC-11": "domain:LEAVECORE",
    "EPIC-12": "domain:NOTIFICATIONS",
    "EPIC-13": "domain:REPORTING", "EPIC-14": "domain:REPORTING",
}

def issue_labels(issue):
    return [
        f"layer:{issue['layer']}",
        f"wave:{issue['wave']}",
        EPIC_TO_DOMAIN.get(issue["epic"], ""),
        LAYER_TO_AGENT.get(issue["layer"], "agent:ralph-impl"),
    ]

# ---------------------------------------------------------------------------
# Step 4 — Build issue body
# ---------------------------------------------------------------------------
def build_body(issue, dep_numbers):
    acs = "\n".join(f"- [ ] {ac}" for ac in issue.get("acceptance_criteria", []))
    deps = ""
    if dep_numbers:
        links = ", ".join(f"#{n}" for n in dep_numbers)
        deps = f"\n\n**Depends on:** {links}"
    return f"""\
## {issue['id']} — {issue['title']}

**Layer:** `{issue['layer']}` | **Wave:** {issue['wave']} | **Epic:** {issue['epic']}

### Description
{issue['description']}

### Acceptance Criteria
{acs}
{deps}

---
*Generated by Forge /push-to-pms — ralph-loop-v5*
"""

# ---------------------------------------------------------------------------
# Step 5 — Create issues in dependency order
# ---------------------------------------------------------------------------
def create_issues(issues, milestone_map):
    print("\n── Creating issues ──────────────────────────────")
    # id → github number
    id_to_number = {}
    # existing issues by title prefix
    existing = {}
    page = 1
    while True:
        r = gh("GET", "/issues", params={"state": "all", "per_page": 100, "page": page})
        batch = r.json()
        if not batch:
            break
        for iss in batch:
            existing[iss["title"]] = iss["number"]
        page += 1

    pms_issues = {}
    failed = []

    # Sort by dependency order (issues with no deps first)
    ordered = sorted(issues, key=lambda x: len(x.get("depends_on", [])))

    for issue in ordered:
        title = f"[{issue['id']}] {issue['title']}"

        # Skip if already exists
        if title in existing:
            n = existing[title]
            id_to_number[issue["id"]] = n
            pms_issues[issue["id"]] = {"number": n, "url": f"https://github.com/{OWNER}/{REPO}/issues/{n}", "state": "open"}
            print(f"  SKIP  #{n}  {issue['id']}")
            continue

        # Resolve dependency issue numbers
        dep_numbers = [id_to_number[d] for d in issue.get("depends_on", []) if d in id_to_number]

        payload = {
            "title": title,
            "body": build_body(issue, dep_numbers),
            "labels": [l for l in issue_labels(issue) if l],
        }
        ms = milestone_map.get(issue["epic"])
        if ms:
            payload["milestone"] = ms

        r = gh("POST", "/issues", json=payload)
        if ok(r, (200, 201)):
            n = r.json()["number"]
            url = r.json()["html_url"]
            id_to_number[issue["id"]] = n
            pms_issues[issue["id"]] = {"number": n, "url": url, "state": "open"}
            print(f"  OK    #{n}  {issue['id']}")
        else:
            print(f"  FAIL({r.status_code})  {issue['id']}: {r.text[:120]}")
            failed.append(issue["id"])

        time.sleep(0.4)  # Stay well within rate limits

    return pms_issues, failed

# ---------------------------------------------------------------------------
# Step 6 — Write pms-map.json
# ---------------------------------------------------------------------------
def write_pms_map(milestone_map, pms_issues, epics):
    epic_map = {}
    for epic in epics:
        n = milestone_map.get(epic["id"])
        if n:
            epic_map[epic["id"]] = {
                "number": n,
                "url": f"https://github.com/{OWNER}/{REPO}/milestone/{n}"
            }

    pms_map = {
        "platform": "github",
        "repo": f"{OWNER}/{REPO}",
        "created": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "milestones": epic_map,
        "issues": pms_issues,
    }
    out = BASE_DIR / "ai-context" / "pms-map.json"
    out.write_text(json.dumps(pms_map, indent=2))
    print(f"\n── Wrote {out}")
    return pms_map

# ---------------------------------------------------------------------------
# Step 7 — Update task_status.md
# ---------------------------------------------------------------------------
TASK_TO_ISSUE = {
    "T-001": "AUTH-DB-001",   "T-002": "AUTH-API-001",  "T-003": "AUTH-API-002",
    "T-004": "AUTH-API-003",  "T-005": "AUTH-UI-001",   "T-006": "AUTH-INT-001",
    "T-007": "AUTH-TEST-001", "T-008": "AUTH-E2E-001",  "T-009": "AUTH-API-004",
    "T-010": "AUTH-UI-002",   "T-011": "AUTH-INT-002",  "T-012": "AUTH-TEST-002",
    "T-013": "INFRA-DB-001",  "T-014": "INFRA-TEST-001","T-015": "INFRA-E2E-001",
    "T-016": "PEOPLE-DB-001", "T-017": "PEOPLE-API-001","T-018": "PEOPLE-UI-001",
    "T-019": "PEOPLE-INT-001","T-020": "PEOPLE-TEST-001",
    "T-021": "PEOPLE-DB-002", "T-022": "PEOPLE-API-002","T-023": "PEOPLE-UI-002",
    "T-024": "PEOPLE-INT-002","T-025": "PEOPLE-TEST-002",
    "T-026": "LEAVECORE-DB-001","T-027": "LEAVECORE-API-001","T-028": "LEAVECORE-UI-001",
    "T-029": "LEAVECORE-INT-001","T-030": "LEAVECORE-TEST-001",
    "T-031": "LEAVECORE-DB-002","T-032": "LEAVECORE-API-002","T-033": "LEAVECORE-UI-002",
    "T-034": "LEAVECORE-INT-002","T-035": "LEAVECORE-TEST-002",
    "T-036": "LEAVECORE-DB-003","T-037": "LEAVECORE-API-003","T-038": "LEAVECORE-UI-003",
    "T-039": "LEAVECORE-INT-003","T-040": "LEAVECORE-TEST-003",
    "T-041": "LEAVECORE-DB-004","T-042": "LEAVECORE-API-004","T-043": "LEAVECORE-UI-004",
    "T-044": "LEAVECORE-INT-004","T-045": "LEAVECORE-TEST-004","T-046": "LEAVECORE-E2E-001",
    "T-047": "COMPOFF-DB-001", "T-048": "COMPOFF-API-001","T-049": "COMPOFF-UI-001",
    "T-050": "COMPOFF-INT-001","T-051": "COMPOFF-TEST-001","T-052": "COMPOFF-E2E-001",
    "T-053": "LEAVECORE-API-005","T-054": "LEAVECORE-UI-005","T-055": "LEAVECORE-INT-005",
    "T-056": "LEAVECORE-TEST-005","T-057": "LEAVECORE-E2E-002",
    "T-058": "NOTIFICATIONS-DB-001","T-059": "NOTIFICATIONS-API-001",
    "T-060": "NOTIFICATIONS-UI-001","T-061": "NOTIFICATIONS-INT-001",
    "T-062": "NOTIFICATIONS-TEST-001","T-063": "NOTIFICATIONS-E2E-001",
    "T-064": "REPORTING-DB-001","T-065": "REPORTING-API-002","T-066": "REPORTING-UI-005",
    "T-067": "REPORTING-INT-002","T-068": "REPORTING-TEST-002","T-069": "REPORTING-E2E-002",
    "T-070": "REPORTING-API-001","T-071": "REPORTING-UI-001","T-072": "REPORTING-UI-002",
    "T-073": "REPORTING-UI-003","T-074": "REPORTING-UI-004","T-075": "REPORTING-INT-001",
    "T-076": "REPORTING-TEST-001","T-077": "REPORTING-E2E-001",
}

def update_task_status(pms_issues):
    ts_path = BASE_DIR / "task_status.md"
    content = ts_path.read_text()
    for task_id, issue_id in TASK_TO_ISSUE.items():
        if issue_id in pms_issues:
            n = pms_issues[issue_id]["number"]
            # Replace "— | " placeholder with "#N | "
            content = re.sub(
                rf"(\| {re.escape(task_id)} \| {re.escape(issue_id)} \| [^\|]+ \| [^\|]+ \|) — (\|)",
                rf"\1 #{n} \2",
                content
            )
    ts_path.write_text(content)
    print(f"── Updated task_status.md with GitHub issue numbers")

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------
def main():
    print(f"push_to_github.py — LMS V5 → github.com/{OWNER}/{REPO}")
    print("=" * 60)

    # Load issues.json
    issues_path = BASE_DIR / "ai-context" / "issues.json"
    data = json.loads(issues_path.read_text())
    epics  = data["epics"]
    issues = data["issues"]
    print(f"Loaded {len(epics)} epics, {len(issues)} issues")

    # Run steps
    create_labels()
    milestone_map = create_milestones(epics)
    pms_issues, failed = create_issues(issues, milestone_map)
    pms_map = write_pms_map(milestone_map, pms_issues, epics)
    update_task_status(pms_issues)

    # Summary
    print("\n" + "=" * 60)
    print(f"✅ Push to GitHub complete")
    print(f"   Milestones : {len(milestone_map)}")
    print(f"   Issues     : {len(pms_issues)}")
    print(f"   Failed     : {len(failed)}")
    if failed:
        print(f"   Failed IDs : {', '.join(failed)}")
    print(f"   Board      : https://github.com/{OWNER}/{REPO}/issues")
    print(f"   pms-map    : ai-context/pms-map.json")

if __name__ == "__main__":
    main()
