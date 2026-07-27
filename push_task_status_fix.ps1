# Push task_status.md fix — populates GitHub Issue column (#N) for all 77 tasks
# Run from LMS_V5 root

$ErrorActionPreference = "Stop"

$pat = Get-Content ".env.local.txt" | Where-Object { $_ -match "^GITHUB_PAT=" } | ForEach-Object { $_.Split("=",2)[1].Trim() }
if (-not $pat) { Write-Error "PAT not found in .env.local.txt"; exit 1 }

$env:GIT_AUTHOR_NAME  = "Harshil Madhu"
$env:GIT_AUTHOR_EMAIL = "harshil.madhu@radixweb.com"
$env:GIT_COMMITTER_NAME  = $env:GIT_AUTHOR_NAME
$env:GIT_COMMITTER_EMAIL = $env:GIT_AUTHOR_EMAIL

# Stage only the file that changed
git add task_status.md

# Commit
git commit -m "chore: populate GitHub Issue numbers in task_status.md for all 77 tasks"

# Push
$repoUrl = "https://${pat}@github.com/radixdt3345/LMS.git"
git push $repoUrl main

Write-Host "`n[OK] task_status.md pushed to GitHub" -ForegroundColor Green
