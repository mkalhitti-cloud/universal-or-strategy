# Load .env file and run Codacy query
param(
    [Parameter(Mandatory=$false)]
    [string]$Level = "Warning",
    [Parameter(Mandatory=$false)]
    [string]$Category = "",
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = "codacy_warnings.json"
)

# Load .env file
if (Test-Path ".env") {
    Get-Content ".env" | ForEach-Object {
        if ($_ -match '^CODACY_API_TOKEN=(.+)$') {
            $env:CODACY_API_TOKEN = $matches[1].Trim()
            Write-Host "[ENV] Loaded CODACY_API_TOKEN from .env" -ForegroundColor Green
        }
    }
}

# Build parameters for the main script
$params = @{
    Level = $Level
    OutputPath = $OutputPath
}

if ($Category) {
    $params.Category = $Category
}

# Run the query
& ".\scripts\query_codacy_issues.ps1" @params

# Made with Bob
