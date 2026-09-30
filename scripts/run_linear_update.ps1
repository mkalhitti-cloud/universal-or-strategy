# Run Linear Update with proper environment setup
# This script ensures LINEAR_API_KEY is set before running the Python script

# Check if LINEAR_API_KEY is set in environment
if (-not $env:LINEAR_API_KEY) {
    Write-Host "ERROR: LINEAR_API_KEY environment variable not set" -ForegroundColor Red
    Write-Host "Please set it in your .env file or session:" -ForegroundColor Yellow
    Write-Host '  $env:LINEAR_API_KEY = "your_api_key_here"' -ForegroundColor Yellow
    exit 1
}

Write-Host "Running Linear status update..." -ForegroundColor Cyan

# Run the Python script
python scripts/linear_update_status.py

# Made with Bob