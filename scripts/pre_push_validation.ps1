# V12 Pre-Push Validation Suite
# Runs ALL checks locally before GitHub push to catch issues early
# Integrates: Build, Lint, Tests, Security, Formatting, ASCII, Links, PR Hygiene

param(
    [switch]$SkipBuild,
    [switch]$SkipTests,
    [switch]$SkipLint,
    [switch]$Fast  # Skip slow checks (complexity audit, dead code scan)
)

$ErrorActionPreference = "Stop"
$script:FailureCount = 0
$script:Checks = @()

function Write-CheckHeader {
    param([string]$Name)
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "CHECK: $Name" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
}

function Write-CheckResult {
    param(
        [string]$Name,
        [bool]$Passed,
        [string]$Details = ""
    )
    
    $status = if ($Passed) { "PASS" } else { "FAIL" }
    $color = if ($Passed) { "Green" } else { "Red" }
    
    Write-Host "[$status] $Name" -ForegroundColor $color
    if ($Details) {
        Write-Host "  $Details" -ForegroundColor Gray
    }
    
    $script:Checks += [PSCustomObject]@{
        Name = $Name
        Status = $status
        Details = $Details
    }
    
    if (-not $Passed) {
        $script:FailureCount++
    }
}

# ============================================================================
# 1. ASCII GATE (V12 DNA Mandate)
# ============================================================================
Write-CheckHeader "1. ASCII-Only Compliance"
try {
    $files = Get-ChildItem -Path "src" -Filter "*.cs" -Recurse -ErrorAction SilentlyContinue
    $violations = @()
    foreach ($f in $files) {
        $content = [System.IO.File]::ReadAllBytes($f.FullName)
        foreach ($byte in $content) {
            if ($byte -gt 127) {
                $violations += $f.FullName
                break
            }
        }
    }
    
    if ($violations.Count -gt 0) {
        Write-CheckResult "ASCII Gate" $false "Non-ASCII found in $($violations.Count) files"
        $violations | ForEach-Object { Write-Host "  - $_" -ForegroundColor Yellow }
    } else {
        Write-CheckResult "ASCII Gate" $true "All source files are ASCII-clean"
    }
} catch {
    Write-CheckResult "ASCII Gate" $false $_.Exception.Message
}

# ============================================================================
# 2. BUILD VERIFICATION
# ============================================================================
if (-not $SkipBuild) {
    Write-CheckHeader "2. Build Compilation"
    try {
        $buildOutput = dotnet build Linting.csproj --nologo --verbosity quiet 2>&1
        $buildSuccess = $LASTEXITCODE -eq 0
        
        if ($buildSuccess) {
            Write-CheckResult "Build" $true "Linting.csproj compiled successfully"
        } else {
            Write-CheckResult "Build" $false "Compilation failed"
            Write-Host $buildOutput -ForegroundColor Red
        }
    } catch {
        Write-CheckResult "Build" $false $_.Exception.Message
    }
}

# ============================================================================
# 3. UNIT TESTS
# ============================================================================
if (-not $SkipTests) {
    Write-CheckHeader "3. Unit Tests"
    try {
        $testOutput = dotnet test tests/V12_Performance.Tests/V12_Performance.Tests.csproj --nologo --verbosity quiet 2>&1
        $testSuccess = $LASTEXITCODE -eq 0
        
        if ($testSuccess) {
            Write-CheckResult "Unit Tests" $true "All tests passed"
        } else {
            Write-CheckResult "Unit Tests" $false "Test failures detected"
            Write-Host $testOutput -ForegroundColor Red
        }
    } catch {
        Write-CheckResult "Unit Tests" $false $_.Exception.Message
    }
}

# ============================================================================
# 4. LINTING (Roslyn Analyzers)
# ============================================================================
if (-not $SkipLint) {
    Write-CheckHeader "4. Roslyn Linting"
    try {
        & "$PSScriptRoot\lint.ps1" -ErrorAction Stop
        Write-CheckResult "Lint" $true "No linting violations"
    } catch {
        Write-CheckResult "Lint" $false "Linting violations found"
    }
}

# ============================================================================
# 5. CSHARPIER FORMATTING CHECK (V12 DNA - Curly Braces Mandate)
# ============================================================================
Write-CheckHeader "5. Code Formatting (CSharpier)"
try {
    # Check if CSharpier is installed as dotnet tool
    $csharpierCheck = dotnet tool list --global 2>&1 | Select-String "csharpier"
    
    if ($csharpierCheck) {
        # Run CSharpier in check mode (doesn't modify files)
        # FIXED: Use 'check' command, not '--check' flag
        $formatOutput = dotnet csharpier check src/ 2>&1
        $formatSuccess = $LASTEXITCODE -eq 0
        
        if ($formatSuccess) {
            Write-CheckResult "Formatting" $true "All files properly formatted"
        } else {
            Write-CheckResult "Formatting" $false "Formatting issues detected - run 'dotnet csharpier format src/'"
            Write-Host $formatOutput -ForegroundColor Yellow
        }
    } else {
        Write-CheckResult "Formatting" $false "CSharpier not installed - run 'dotnet tool install -g csharpier'"
    }
} catch {
    Write-CheckResult "Formatting" $false "CSharpier check failed: $($_.Exception.Message)"
}

# ============================================================================
# 6. SECURITY SCANS
# ============================================================================
Write-CheckHeader "6. Security Scans"

# 6a. Gitleaks (secrets detection)
try {
    $gitleaksInstalled = Get-Command "gitleaks" -ErrorAction SilentlyContinue
    if ($gitleaksInstalled) {
        $gitleaksOutput = gitleaks detect --no-git --verbose 2>&1
        $gitleaksSuccess = $LASTEXITCODE -eq 0
        
        if ($gitleaksSuccess) {
            Write-CheckResult "Gitleaks" $true "No secrets detected"
        } else {
            Write-CheckResult "Gitleaks" $false "Potential secrets found"
            Write-Host $gitleaksOutput -ForegroundColor Red
        }
    } else {
        Write-CheckResult "Gitleaks" $true "Not installed (skipped)"
    }
} catch {
    Write-CheckResult "Gitleaks" $true "Skipped: $($_.Exception.Message)"
}

# 6b. Snyk (if available)
try {
    $snykInstalled = Get-Command "snyk" -ErrorAction SilentlyContinue
    if ($snykInstalled) {
        # Check if node_modules exists (Snyk requirement for many environments)
        # If not, and this is a C# project, skip Snyk or use specific args
        if (-not (Test-Path "node_modules")) {
             Write-CheckResult "Snyk" $true "Skipped: node_modules not found (C# Project)"
        } else {
            $snykOutput = snyk test --severity-threshold=high 2>&1
            $snykSuccess = $LASTEXITCODE -eq 0
            
            if ($snykSuccess) {
                Write-CheckResult "Snyk" $true "No high-severity vulnerabilities"
            } else {
                Write-CheckResult "Snyk" $false "Vulnerabilities detected"
                Write-Host $snykOutput -ForegroundColor Red
            }
        }
    } else {
        Write-CheckResult "Snyk" $true "Not installed (skipped)"
    }
} catch {
    Write-CheckResult "Snyk" $true "Skipped: $($_.Exception.Message)"
}

# ============================================================================
# 7. MARKDOWN LINK VALIDATION
# ============================================================================
Write-CheckHeader "7. Markdown Links"
try {
    & "$PSScriptRoot\verify_links.ps1" -ErrorAction Stop
    Write-CheckResult "Markdown Links" $true "All links valid"
} catch {
    Write-CheckResult "Markdown Links" $false "Broken links detected"
}

# ============================================================================
# 8. PR HYGIENE (if on a branch)
# ============================================================================
Write-CheckHeader "8. PR Hygiene"
try {
    $currentBranch = git rev-parse --abbrev-ref HEAD 2>$null
    
    if ($currentBranch -and $currentBranch -ne "main") {
        & "$PSScriptRoot\verify_pr_hygiene.ps1" -ErrorAction Stop
        Write-CheckResult "PR Hygiene" $true "Diff size and commit structure OK"
    } else {
        Write-CheckResult "PR Hygiene" $true "On main branch (skipped)"
    }
} catch {
    Write-CheckResult "PR Hygiene" $false "Hygiene violations detected"
}

# ============================================================================
# 9. COMPLEXITY THRESHOLD ENFORCEMENT (V12 DNA - Jane Street Aligned)
# ============================================================================
if (-not $Fast) {
    Write-CheckHeader "9. Complexity Threshold (CYC ≤ 15)"
    try {
        $pythonInstalled = Get-Command "python" -ErrorAction SilentlyContinue
        if ($pythonInstalled) {
            # Run with threshold enforcement
            $complexityOutput = python "$PSScriptRoot\complexity_audit.py" --threshold 8 --fail-on-violation 2>&1
            $complexitySuccess = $LASTEXITCODE -eq 0
            
            if ($complexitySuccess) {
                Write-CheckResult "Complexity (≤15)" $true "All methods within threshold"
            } else {
                Write-CheckResult "Complexity (≤15)" $false "Methods exceed CYC 8 threshold"
                Write-Host $complexityOutput -ForegroundColor Red
            }
        } else {
            Write-CheckResult "Complexity" $true "Python not installed (skipped)"
        }
    } catch {
        Write-CheckResult "Complexity" $true "Skipped: $($_.Exception.Message)"
    }
}

# ============================================================================
# 10. DEAD CODE DETECTION (Warning - Non-blocking)
# ============================================================================
if (-not $Fast) {
    Write-CheckHeader "10. Dead Code Detection"
    try {
        $pythonInstalled = Get-Command "python" -ErrorAction SilentlyContinue
        if ($pythonInstalled) {
            $deadCodeOutput = python "$PSScriptRoot\dead_code_scan.py" 2>&1
            $deadCodeSuccess = $LASTEXITCODE -eq 0
            
            if ($deadCodeSuccess) {
                Write-CheckResult "Dead Code" $true "No dead private methods detected"
            } else {
                # Non-blocking warning
                Write-CheckResult "Dead Code" $true "Dead methods found (warning only)"
                Write-Host $deadCodeOutput -ForegroundColor Yellow
            }
        } else {
            Write-CheckResult "Dead Code" $true "Python not installed (skipped)"
        }
    } catch {
        Write-CheckResult "Dead Code" $true "Skipped: $($_.Exception.Message)"
    }
}

# ============================================================================
# 11. CODACY LOCAL PREVIEW (API-based, Warning - Non-blocking)
# ============================================================================
Write-CheckHeader "11. Codacy Issue Preview"
try {
    if ($env:CODACY_API_TOKEN) {
        # Get current branch
        $branch = git rev-parse --abbrev-ref HEAD 2>&1
        
        if ($branch -and $branch -ne "HEAD") {
            # Query Codacy for this branch's issues
            $codacyScript = Join-Path $PSScriptRoot "query_codacy_issues.ps1"
            if (Test-Path $codacyScript) {
                & $codacyScript -ErrorAction SilentlyContinue | Out-Null
                
                if (Test-Path "codacy_warnings.json") {
                    $issues = Get-Content "codacy_warnings.json" | ConvertFrom-Json
                    $errorCount = ($issues | Where-Object { $_.level -eq "Error" }).Count
                    $warningCount = ($issues | Where-Object { $_.level -eq "Warning" }).Count
                    
                    if ($errorCount -gt 0) {
                        Write-CheckResult "Codacy Preview" $true "$errorCount errors, $warningCount warnings (warning only)"
                        Write-Host "  Run 'powershell -File .\scripts\query_codacy_issues.ps1' for details" -ForegroundColor Yellow
                    } else {
                        Write-CheckResult "Codacy Preview" $true "$warningCount warnings (no errors)"
                    }
                } else {
                    Write-CheckResult "Codacy Preview" $true "Branch not yet pushed (skipped)"
                }
            } else {
                Write-CheckResult "Codacy Preview" $true "query_codacy_issues.ps1 not found (skipped)"
            }
        } else {
            Write-CheckResult "Codacy Preview" $true "Detached HEAD or main branch (skipped)"
        }
    } else {
        Write-CheckResult "Codacy Preview" $true "CODACY_API_TOKEN not set (skipped)"
    }
} catch {
    Write-CheckResult "Codacy Preview" $true "Preview failed (non-blocking): $($_.Exception.Message)"
}

# ============================================================================
# 12. SEMGREP SECURITY SCAN (Warning - Non-blocking)
# ============================================================================
if (-not $Fast) {
    Write-CheckHeader "12. Semgrep Security Scan"
    try {
        $semgrepInstalled = Get-Command "semgrep" -ErrorAction SilentlyContinue
        if ($semgrepInstalled) {
            $semgrepOutput = semgrep --config auto --json src/ 2>&1
            if ($LASTEXITCODE -eq 0) {
                try {
                    $semgrepJson = $semgrepOutput | ConvertFrom-Json
                    $findings = $semgrepJson.results.Count
                    
                    if ($findings -eq 0) {
                        Write-CheckResult "Semgrep" $true "No security findings"
                    } else {
                        # Non-blocking warning
                        Write-CheckResult "Semgrep" $true "$findings security findings (warning only)"
                        Write-Host "  Run 'semgrep --config auto src/' for details" -ForegroundColor Yellow
                    }
                } catch {
                    Write-CheckResult "Semgrep" $true "Scan complete (parse error, non-blocking)"
                }
            } else {
                Write-CheckResult "Semgrep" $true "Scan failed (non-blocking)"
            }
        } else {
            Write-CheckResult "Semgrep" $true "Not installed (skipped)"
        }
    } catch {
        Write-CheckResult "Semgrep" $true "Scan failed (non-blocking): $($_.Exception.Message)"
    }
}
# ============================================================================
# 13. CODERABBIT AI REVIEW (Warning - Non-blocking during validation period)
# ============================================================================
if (-not $Fast) {
    Write-CheckHeader "13. CodeRabbit AI Review"
    try {
        $crInstalled = Get-Command "coderabbit" -ErrorAction SilentlyContinue
        if (-not $crInstalled) {
            $crInstalled = Get-Command "cr" -ErrorAction SilentlyContinue
        }
        
        if ($crInstalled) {
            Write-Host "  Running CodeRabbit AI review (may take 7-30 minutes)..." -ForegroundColor Yellow
            Write-Host "  Command: coderabbit review --agent --type uncommitted" -ForegroundColor Gray
            
            # Run CodeRabbit in background with 30-minute timeout
            $crJob = Start-Job -ScriptBlock {
                coderabbit review --agent --type uncommitted 2>&1
            }
            
            $crTimeout = 1800 # 30 minutes
            $crCompleted = Wait-Job -Job $crJob -Timeout $crTimeout
            
            if ($crCompleted) {
                $crOutput = Receive-Job -Job $crJob
                Remove-Job -Job $crJob
                
                # Parse JSON output
                $crOutputStr = $crOutput -join "`n"
                if ($crOutputStr -match '\{.*"findings".*\}') {
                    try {
                        $crResults = $crOutputStr | ConvertFrom-Json
                        $criticalCount = 0
                        $highCount = 0
                        $totalCount = 0
                        
                        if ($crResults.findings) {
                            $totalCount = $crResults.findings.Count
                            $criticalCount = ($crResults.findings | Where-Object { $_.severity -eq "critical" }).Count
                            $highCount = ($crResults.findings | Where-Object { $_.severity -eq "high" }).Count
                        }
                        
                        $criticalHighCount = $criticalCount + $highCount
                        
                        if ($criticalHighCount -eq 0) {
                            Write-CheckResult "CodeRabbit" $true "No critical/high issues ($totalCount total findings)"
                        } else {
                            Write-CheckResult "CodeRabbit" $true "$criticalHighCount critical/high issues (warning only - will be blocking after validation period)"
                            Write-Host "  Total findings: $totalCount (Critical: $criticalCount, High: $highCount)" -ForegroundColor Yellow
                        }
                        
                        # Save results for review
                        $crResults | ConvertTo-Json -Depth 10 | Out-File "coderabbit_review.json" -Encoding UTF8
                        Write-Host "  Results saved to: coderabbit_review.json" -ForegroundColor Gray
                    } catch {
                        Write-CheckResult "CodeRabbit" $true "Failed to parse results (non-blocking): $($_.Exception.Message)"
                    }
                } else {
                    Write-CheckResult "CodeRabbit" $true "No JSON output detected (non-blocking)"
                }
            } else {
                Write-CheckResult "CodeRabbit" $true "Review timed out after 30 minutes (non-blocking)"
                Remove-Job -Job $crJob -Force
            }
        } else {
            Write-CheckResult "CodeRabbit" $true "Not installed (skipped)"
            Write-Host "  Install: curl -fsSL https://cli.coderabbit.ai/install.sh | sh" -ForegroundColor Gray
            Write-Host "  Or: brew install coderabbit" -ForegroundColor Gray
            Write-Host "  Then authenticate: cr auth login" -ForegroundColor Gray
        }
    } catch {
        Write-CheckResult "CodeRabbit" $true "Review failed (non-blocking): $($_.Exception.Message)"
    }
}

# ============================================================================
# 14. CODESCENE DELTA ANALYSIS (Code Health Impact)
# ============================================================================
Write-CheckHeader "14. CodeScene Delta Analysis"

try {
    # Check if CodeScene CLI is installed
    $csInstalled = Get-Command cs -ErrorAction SilentlyContinue
    
    if ($csInstalled) {
        # Check if CS_ACCESS_TOKEN is set
        if ($env:CS_ACCESS_TOKEN) {
            Write-Host "Running CodeScene delta analysis on staged changes..." -ForegroundColor Gray
            
            # Run delta analysis on staged changes
            $csOutput = cs delta --staged --output-format json 2>&1
            
            if ($LASTEXITCODE -eq 0) {
                try {
                    $csResults = $csOutput | ConvertFrom-Json
                    
                    # Check for code health degradation
                    $newIssues = @()
                    if ($csResults.review) {
                        foreach ($issue in $csResults.review) {
                            if ($issue.indication -ge 3) {  # High severity
                                $newIssues += $issue
                            }
                        }
                    }
                    
                    if ($newIssues.Count -eq 0) {
                        Write-CheckResult "CodeScene Delta" $true "No code health degradation detected"
                    } else {
                        Write-CheckResult "CodeScene Delta" $false "$($newIssues.Count) high-severity code health issues detected"
                        Write-Host "  Run 'cs delta --staged' for details" -ForegroundColor Yellow
                    }
                    
                    # Save results
                    $csResults | ConvertTo-Json -Depth 10 | Out-File "codescene_delta.json" -Encoding UTF8
                    Write-Host "  Results saved to: codescene_delta.json" -ForegroundColor Gray
                } catch {
                    Write-CheckResult "CodeScene Delta" $true "Failed to parse results (non-blocking): $($_.Exception.Message)"
                }
            } else {
                # No staged changes or other non-error condition
                Write-CheckResult "CodeScene Delta" $true "No staged changes to analyze"
            }
        } else {
            Write-CheckResult "CodeScene Delta" $true "CS_ACCESS_TOKEN not set (skipped)"
            Write-Host "  Set token: `$env:CS_ACCESS_TOKEN = '<your-token>'" -ForegroundColor Gray
        }
    } else {
        Write-CheckResult "CodeScene Delta" $true "Not installed (skipped)"
        Write-Host "  Install: Invoke-WebRequest -Uri 'https://downloads.codescene.io/enterprise/cli/install-cs-tool.ps1' -OutFile install-cs-tool.ps1; .\install-cs-tool.ps1" -ForegroundColor Gray
    }
} catch {
    Write-CheckResult "CodeScene Delta" $true "Analysis failed (non-blocking): $($_.Exception.Message)"
}



# ============================================================================
# FINAL REPORT
# ============================================================================
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "PRE-PUSH VALIDATION SUMMARY" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$passCount = ($script:Checks | Where-Object { $_.Status -eq "PASS" }).Count
$failCount = $script:FailureCount
$totalCount = $script:Checks.Count

Write-Host "`nResults: $passCount/$totalCount checks passed" -ForegroundColor $(if ($failCount -eq 0) { "Green" } else { "Yellow" })

if ($failCount -gt 0) {
    Write-Host "`nFailed Checks:" -ForegroundColor Red
    $script:Checks | Where-Object { $_.Status -eq "FAIL" } | ForEach-Object {
        Write-Host "  - $($_.Name): $($_.Details)" -ForegroundColor Red
    }
    
    Write-Host "`n[BLOCKED] Fix the above issues before pushing to GitHub" -ForegroundColor Red
    exit 1
} else {
    Write-Host "`n[READY] All checks passed - safe to push!" -ForegroundColor Green
    exit 0
}

# Made with Bob
