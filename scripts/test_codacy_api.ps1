# Test Codacy API connectivity and response
$ErrorActionPreference = "Stop"

# Load token from .env
if (Test-Path ".env") {
    Get-Content ".env" | ForEach-Object {
        if ($_ -match '^CODACY_API_TOKEN=(.+)$') {
            $env:CODACY_API_TOKEN = $matches[1].Trim()
            Write-Host "[TEST] Loaded CODACY_API_TOKEN" -ForegroundColor Green
        }
    }
}

if (-not $env:CODACY_API_TOKEN) {
    Write-Error "CODACY_API_TOKEN not found"
    exit 1
}

$org = "gh"
$owner = "malhitticrypto-debug"
$repo = "universal-or-strategy"

$headers = @{
    "api-token" = $env:CODACY_API_TOKEN
    "Content-Type" = "application/json"
}

# Test 1: Query with no filters
Write-Host "`n[TEST 1] Querying with no level filter..." -ForegroundColor Cyan
$uri1 = "https://api.codacy.com/api/v3/analysis/organizations/$org/$owner/repositories/$repo/issues/search?limit=10"
$body1 = @{} | ConvertTo-Json

try {
    $response1 = Invoke-RestMethod -Uri $uri1 -Method Post -Headers $headers -Body $body1
    Write-Host "[TEST 1] Success! Found $($response1.data.Count) issues" -ForegroundColor Green
    if ($response1.data.Count -gt 0) {
        Write-Host "[TEST 1] Sample issue:" -ForegroundColor Yellow
        $response1.data[0] | ConvertTo-Json -Depth 3
    }
} catch {
    Write-Host "[TEST 1] Error: $_" -ForegroundColor Red
    Write-Host "[TEST 1] Status: $($_.Exception.Response.StatusCode.value__)" -ForegroundColor Red
}

# Test 2: Try different severity levels
foreach ($level in @("Error", "Warning", "Info")) {
    Write-Host "`n[TEST 2] Querying level: $level..." -ForegroundColor Cyan
    $body2 = @{ levels = @($level) } | ConvertTo-Json
    
    try {
        $response2 = Invoke-RestMethod -Uri $uri1 -Method Post -Headers $headers -Body $body2
        Write-Host "[TEST 2] Level $level : Found $($response2.data.Count) issues" -ForegroundColor Green
    } catch {
        Write-Host "[TEST 2] Level $level : Error $_" -ForegroundColor Red
    }
}

# Test 3: Check repository info
Write-Host "`n[TEST 3] Checking repository info..." -ForegroundColor Cyan
$uri3 = "https://api.codacy.com/api/v3/analysis/organizations/$org/$owner/repositories/$repo"

try {
    $response3 = Invoke-RestMethod -Uri $uri3 -Method Get -Headers $headers
    Write-Host "[TEST 3] Repository found!" -ForegroundColor Green
    Write-Host "[TEST 3] Last analysis: $($response3.data.lastAnalysis)" -ForegroundColor Yellow
    Write-Host "[TEST 3] Grade: $($response3.data.grade)" -ForegroundColor Yellow
} catch {
    Write-Host "[TEST 3] Error: $_" -ForegroundColor Red
}

Write-Host "`n[TEST] Complete" -ForegroundColor Cyan

# Made with Bob
