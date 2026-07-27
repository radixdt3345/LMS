# AUTH-API-001 — Create GitHub PR
# Run from PowerShell in any directory. The branch is already pushed.

$PAT  = "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"
$repo = "radixdt3345/LMS"

$body = @{
    title = "[AUTH-API-001] Local login endpoint with JWT + lockout"
    body  = @"
Closes #3

## Changes
- ``Result<T>`` domain pattern — services return Result instead of throwing for expected failures
- ``JwtSettings`` bound from ``appsettings.json`` ``JwtSettings`` section
- ``ITokenService`` + ``TokenService`` (LMS.Infrastructure): JWT HS256 issuance; refresh tokens stored as SHA-256 hash only
- ``IAuthService`` + ``AuthService`` (LMS.Infrastructure): credential validation, failed-count tracking, 30-min lockout after 5 failures
- ``AuthController`` ``POST /api/v1/auth/login`` — tokens returned in response body only (never written to localStorage)
- JWT Bearer authentication fully configured in ``Program.cs``
- ``Microsoft.IdentityModel.Tokens`` + ``System.IdentityModel.Tokens.Jwt`` added to ``LMS.Infrastructure``
- ``Microsoft.Extensions.Options`` added to ``LMS.Application``
- ``Microsoft.EntityFrameworkCore.InMemory`` added to ``LMS.Tests``

## Architecture note
Services placed in ``LMS.Infrastructure.Services`` (not ``LMS.Application.Services``) to avoid circular dependency: ``LMS.Infrastructure`` already references ``LMS.Application``; reversing that reference would create a cycle. Interfaces remain in ``LMS.Application.Interfaces``.

## FR Coverage
- FR-3: local email/password login
- FR-4: JWT access token + refresh token issuance
- FR-7: account lockout after 5 failures

## UT Coverage
- UT-1: valid credentials -> success + tokens
- UT-2: wrong password -> 401
- UT-3: unknown email -> 401
- UT-4: 5 consecutive failures -> lockout applied
- UT-5: already-locked account -> 423 regardless of password
- UT-6: inactive user -> 401
- UT-7: successful login resets failed counter
"@
    head  = "feat/AUTH-API-001-local-login"
    base  = "main"
} | ConvertTo-Json -Depth 5

$headers = @{
    Authorization = "Bearer $PAT"
    "Content-Type" = "application/json"
    Accept = "application/vnd.github+json"
}

$response = Invoke-WebRequest -Uri "https://api.github.com/repos/$repo/pulls" `
    -Method POST -Headers $headers -Body $body -UseBasicParsing

$pr = $response.Content | ConvertFrom-Json
Write-Host ""
Write-Host "PR created successfully!" -ForegroundColor Green
Write-Host "URL : $($pr.html_url)"
Write-Host "Number: #$($pr.number)"
