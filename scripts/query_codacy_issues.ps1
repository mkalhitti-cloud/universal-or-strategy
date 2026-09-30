# Query Codacy API for PR issues
param(
    [Parameter(Mandatory=$false)]
    [string]$Level = "Error",
    [Parameter(Mandatory=$false)]
    [int]$Limit = 100
)

$ErrorActionPreference = "Stop"

# Read API token from environment variable
if (-not $env:CODACY_API_TOKEN) {
    Write-Error "CODACY_API_TOKEN environment variable not set. Please set it in your .env file or session."
    exit 1
}
$org = "gh"
$owner = "malhitticrypto-debug"
$repo = "universal-or-strategy"

# Build headers
$headers = @{
    "api-token" = $env:CODACY_API_TOKEN
    "Content-Type" = "application/json"
}

# Build request body
$body = @{
    levels = @($Level)
} | ConvertTo-Json

# Query issues
$uri = "https://api.codacy.com/api/v3/analysis/organizations/$org/$owner/repositories/$repo/issues/search?limit=$Limit"

Write-Host "[Codacy API] Querying $Level issues (limit: $Limit)..." -ForegroundColor Cyan

try {
    $response = Invoke-RestMethod -Uri $uri -Method Post -Headers $headers -Body $body
    
    Write-Host "[Codacy API] Found $($response.data.Count) issues" -ForegroundColor Green
    
    # Save to JSON file
    $response.data | ConvertTo-Json -Depth 10 | Out-File -FilePath "codacy_warnings.json" -Encoding utf8
    Write-Host "[Codacy API] Saved to codacy_warnings.json" -ForegroundColor Green
    
    # Output summary table
    $response.data | Select-Object -First 20 | ForEach-Object {
        [PSCustomObject]@{
            File = $_.filePath
            Line = $_.lineNumber
            Category = $_.category
            Message = $_.message.Substring(0, [Math]::Min(60, $_.message.Length))
        }
    } | Format-Table -AutoSize
    
    Write-Host "[Codacy API] Showing first 20 of $($response.data.Count) issues" -ForegroundColor Cyan
    
    # Return raw data
    return $response
}
catch {
    Write-Host "[Codacy API] Error: $_" -ForegroundColor Red
    Write-Host "[Codacy API] Status: $($_.Exception.Response.StatusCode.value__)" -ForegroundColor Red
    throw
}

# Made with Bob
