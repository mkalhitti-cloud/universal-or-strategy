# GitButler CLI Wrapper for Bob CLI
# Provides PowerShell functions for GitButler operations with error handling

param(
    [Parameter(Mandatory=$false)]
    [string]$Command,
    
    [Parameter(Mandatory=$false)]
    [string[]]$Args
)

# Check if GitButler CLI is installed
function Test-GitButlerCLI {
    try {
        $version = & but --version 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "[GitButler] CLI v$version detected" -ForegroundColor Green
            return $true
        }
    }
    catch {
        Write-Host "[GitButler] CLI not found in PATH" -ForegroundColor Yellow
        Write-Host "[GitButler] Install via: GitButler Desktop → Settings → General → Install CLI" -ForegroundColor Yellow
        return $false
    }
    return $false
}

# Create a new virtual branch
function New-GitButlerBranch {
    param(
        [Parameter(Mandatory=$true)]
        [string]$BranchName
    )
    
    if (-not (Test-GitButlerCLI)) {
        return $false
    }
    
    Write-Host "[GitButler] Creating virtual branch: $BranchName" -ForegroundColor Cyan
    
    try {
        $output = & but branch new $BranchName 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "[GitButler] ✓ Branch created: $BranchName" -ForegroundColor Green
            return $true
        }
        else {
            Write-Host "[GitButler] ✗ Failed to create branch: $output" -ForegroundColor Red
            return $false
        }
    }
    catch {
        Write-Host "[GitButler] ✗ Error: $_" -ForegroundColor Red
        return $false
    }
}

# Stage files to current virtual branch
function Add-GitButlerFiles {
    param(
        [Parameter(Mandatory=$true)]
        [string[]]$Files
    )
    
    if (-not (Test-GitButlerCLI)) {
        return $false
    }
    
    Write-Host "[GitButler] Staging files to current branch..." -ForegroundColor Cyan
    
    try {
        foreach ($file in $Files) {
            $output = & but stage $file 2>&1
            if ($LASTEXITCODE -ne 0) {
                Write-Host "[GitButler] ✗ Failed to stage $file: $output" -ForegroundColor Red
                return $false
            }
        }
        Write-Host "[GitButler] ✓ Files staged successfully" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "[GitButler] ✗ Error: $_" -ForegroundColor Red
        return $false
    }
}

# Commit to current virtual branch
function Invoke-GitButlerCommit {
    param(
        [Parameter(Mandatory=$true)]
        [string]$Message
    )
    
    if (-not (Test-GitButlerCLI)) {
        return $false
    }
    
    Write-Host "[GitButler] Committing to current branch..." -ForegroundColor Cyan
    Write-Host "[GitButler] Message: $Message" -ForegroundColor Gray
    
    try {
        $output = & but commit -m $Message 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "[GitButler] ✓ Commit successful" -ForegroundColor Green
            return $true
        }
        else {
            Write-Host "[GitButler] ✗ Failed to commit: $output" -ForegroundColor Red
            return $false
        }
    }
    catch {
        Write-Host "[GitButler] ✗ Error: $_" -ForegroundColor Red
        return $false
    }
}

# Push virtual branch (creates GitHub branch + PR)
function Invoke-GitButlerPush {
    param(
        [Parameter(Mandatory=$false)]
        [string]$BranchName
    )
    
    if (-not (Test-GitButlerCLI)) {
        return $false
    }
    
    Write-Host "[GitButler] Pushing virtual branch to GitHub..." -ForegroundColor Cyan
    
    try {
        if ($BranchName) {
            $output = & but push $BranchName 2>&1
        }
        else {
            $output = & but push 2>&1
        }
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "[GitButler] ✓ Branch pushed successfully" -ForegroundColor Green
            Write-Host $output
            return $true
        }
        else {
            Write-Host "[GitButler] ✗ Failed to push: $output" -ForegroundColor Red
            return $false
        }
    }
    catch {
        Write-Host "[GitButler] ✗ Error: $_" -ForegroundColor Red
        return $false
    }
}

# Get current workspace status
function Get-GitButlerStatus {
    if (-not (Test-GitButlerCLI)) {
        return $null
    }
    
    try {
        $output = & but status 2>&1
        if ($LASTEXITCODE -eq 0) {
            return $output
        }
        else {
            Write-Host "[GitButler] ✗ Failed to get status: $output" -ForegroundColor Red
            return $null
        }
    }
    catch {
        Write-Host "[GitButler] ✗ Error: $_" -ForegroundColor Red
        return $null
    }
}

# Main command dispatcher
if ($Command) {
    switch ($Command.ToLower()) {
        "new-branch" {
            if ($Args.Count -eq 0) {
                Write-Host "Usage: gitbutler_cli.ps1 new-branch <branch-name>" -ForegroundColor Yellow
                exit 1
            }
            $result = New-GitButlerBranch -BranchName $Args[0]
            exit $(if ($result) { 0 } else { 1 })
        }
        "stage" {
            if ($Args.Count -eq 0) {
                Write-Host "Usage: gitbutler_cli.ps1 stage <file1> [file2] ..." -ForegroundColor Yellow
                exit 1
            }
            $result = Add-GitButlerFiles -Files $Args
            exit $(if ($result) { 0 } else { 1 })
        }
        "commit" {
            if ($Args.Count -eq 0) {
                Write-Host "Usage: gitbutler_cli.ps1 commit <message>" -ForegroundColor Yellow
                exit 1
            }
            $result = Invoke-GitButlerCommit -Message ($Args -join ' ')
            exit $(if ($result) { 0 } else { 1 })
        }
        "push" {
            $branchName = if ($Args.Count -gt 0) { $Args[0] } else { $null }
            $result = Invoke-GitButlerPush -BranchName $branchName
            exit $(if ($result) { 0 } else { 1 })
        }
        "status" {
            $status = Get-GitButlerStatus
            if ($status) {
                Write-Host $status
                exit 0
            }
            else {
                exit 1
            }
        }
        "test" {
            $result = Test-GitButlerCLI
            exit $(if ($result) { 0 } else { 1 })
        }
        default {
            Write-Host "Unknown command: $Command" -ForegroundColor Red
            Write-Host "Available commands: new-branch, stage, commit, push, status, test" -ForegroundColor Yellow
            exit 1
        }
    }
}
else {
    # No command provided - show help
    Write-Host "GitButler CLI Wrapper for Bob CLI" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Usage: gitbutler_cli.ps1 <command> [args]" -ForegroundColor White
    Write-Host ""
    Write-Host "Commands:" -ForegroundColor Yellow
    Write-Host "  new-branch <name>    Create a new virtual branch" -ForegroundColor White
    Write-Host "  stage <files>        Stage files to current branch" -ForegroundColor White
    Write-Host "  commit <message>     Commit to current branch" -ForegroundColor White
    Write-Host "  push [branch]        Push branch to GitHub (creates PR)" -ForegroundColor White
    Write-Host "  status               Show workspace status" -ForegroundColor White
    Write-Host "  test                 Test if GitButler CLI is installed" -ForegroundColor White
    Write-Host ""
    Write-Host "Examples:" -ForegroundColor Yellow
    Write-Host "  .\gitbutler_cli.ps1 new-branch src/fix-compilation" -ForegroundColor Gray
    Write-Host "  .\gitbutler_cli.ps1 stage src/V12_002.cs" -ForegroundColor Gray
    Write-Host "  .\gitbutler_cli.ps1 commit 'fix: compilation errors'" -ForegroundColor Gray
    Write-Host "  .\gitbutler_cli.ps1 push" -ForegroundColor Gray
}

# Made with Bob
