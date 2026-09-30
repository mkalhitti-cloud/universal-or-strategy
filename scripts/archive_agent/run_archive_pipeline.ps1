# ===========================================================
# ARCHIVE PIPELINE LAUNCHER
# Media Architect (Tier 1) runs this to start the pipeline
# ===========================================================
# Usage:
#   .\scripts\archive_agent\run_archive_pipeline.ps1 -Step build
#   .\scripts\archive_agent\run_archive_pipeline.ps1 -Step assign
#   .\scripts\archive_agent\run_archive_pipeline.ps1 -Step status
#   .\scripts\archive_agent\run_archive_pipeline.ps1 -Step test -Session session_001
#   .\scripts\archive_agent\run_archive_pipeline.ps1 -Step full -Session session_001
# ===========================================================

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("build","assign","status","test","full","reset-failed")]
    [string]$Step,

    [string]$Session = "",
    [string]$Account = "account_02",
    [string]$WhisperModel = "base"
)

$ErrorActionPreference = "Stop"

function Write-Header($text) {
    Write-Host ""
    Write-Host ("=" * 55) -ForegroundColor Cyan
    Write-Host "  $text" -ForegroundColor Cyan
    Write-Host ("=" * 55) -ForegroundColor Cyan
    Write-Host ""
}

function Write-Step($num, $text) {
    Write-Host "  [$num] $text" -ForegroundColor Yellow
}

function Write-OK($text) {
    Write-Host "  ✅ $text" -ForegroundColor Green
}

function Write-Fail($text) {
    Write-Host "  ❌ $text" -ForegroundColor Red
}

# ── STEP: BUILD MANIFEST ─────────────────────────────────
if ($Step -eq "build") {
    Write-Header "STEP: BUILD ARCHIVE MANIFEST"
    Write-Step 1 "Parsing archive MD and building manifest..."
    python scripts/archive_agent/00_build_manifest.py
    if ($LASTEXITCODE -eq 0) {
        Write-OK "archive/archive_manifest.json created"
        Write-Host ""
        Write-Host "  Next: run -Step assign" -ForegroundColor Cyan
    } else {
        Write-Fail "Manifest build failed"
        exit 1
    }
}

# ── STEP: ASSIGN WORKERS ─────────────────────────────────
elseif ($Step -eq "assign") {
    Write-Header "STEP: ASSIGN TIER 2 ORCHESTRATORS"
    Write-Step 1 "Media Architect assigning batches..."
    python scripts/archive_agent/01_director.py --assign
    if ($LASTEXITCODE -eq 0) {
        Write-OK "worker_assignments/ files written"
        Write-Host ""
        Write-Host "  Next: distribute account_XX.md files to each Bob account" -ForegroundColor Cyan
        Write-Host "  Each Tier 2 account runs:" -ForegroundColor Cyan
        Write-Host "    python scripts/archive_agent/02_pipeline_orchestrator.py --account account_XX --assign-tier3" -ForegroundColor White
    } else {
        Write-Fail "Assignment failed"
        exit 1
    }
}

# ── STEP: STATUS ─────────────────────────────────────────
elseif ($Step -eq "status") {
    Write-Header "PIPELINE STATUS DASHBOARD"
    python scripts/archive_agent/01_director.py --status
}

# ── STEP: RESET FAILED ───────────────────────────────────
elseif ($Step -eq "reset-failed") {
    Write-Header "RESET FAILED SESSIONS"
    python scripts/archive_agent/01_director.py --reassign-failed
}

# ── STEP: TEST — Single session end-to-end ───────────────
elseif ($Step -eq "test") {
    if (-not $Session) {
        Write-Fail "Provide -Session (e.g. -Session session_001)"
        exit 1
    }

    Write-Header "TEST PIPELINE — $Session"
    Write-Host "  Running all 5 stages for single session..." -ForegroundColor Yellow
    Write-Host "  (No URL set — will show pending status on download)" -ForegroundColor Gray
    Write-Host ""

    Write-Step 1 "Transcribe (requires video at archive/raw/$Session.mp4)"
    python scripts/archive_agent/04_transcribe_worker.py --session $Session --model $WhisperModel

    Write-Step 2 "Analyze"
    python scripts/archive_agent/05_analyze_worker.py --session $Session

    Write-Step 3 "Extract"
    python scripts/archive_agent/06_extract_worker.py --session $Session

    Write-Step 4 "Metadata"
    python scripts/archive_agent/07_metadata_worker.py --session $Session

    Write-Header "TEST COMPLETE — CHECK RESULTS"
    Write-Host "  Clips:    archive/clips/" -ForegroundColor White
    Write-Host "  Metadata: archive/metadata/$Session`_metadata.json" -ForegroundColor White
    Write-Host "  Manifest: archive/archive_manifest.json" -ForegroundColor White
}

# ── STEP: FULL — All stages including download ───────────
elseif ($Step -eq "full") {
    if (-not $Session) {
        Write-Fail "Provide -Session (e.g. -Session session_001)"
        exit 1
    }

    Write-Header "FULL PIPELINE — $Session"

    Write-Step 1 "Download"
    python scripts/archive_agent/03_download_worker.py --session $Session
    if ($LASTEXITCODE -ne 0) { Write-Fail "Download failed"; exit 1 }

    Write-Step 2 "Transcribe (model: $WhisperModel)"
    python scripts/archive_agent/04_transcribe_worker.py --session $Session --model $WhisperModel
    if ($LASTEXITCODE -ne 0) { Write-Fail "Transcribe failed"; exit 1 }

    Write-Step 3 "Analyze"
    python scripts/archive_agent/05_analyze_worker.py --session $Session
    if ($LASTEXITCODE -ne 0) { Write-Fail "Analyze failed"; exit 1 }

    Write-Step 4 "Extract clips"
    python scripts/archive_agent/06_extract_worker.py --session $Session
    if ($LASTEXITCODE -ne 0) { Write-Fail "Extract failed"; exit 1 }

    Write-Step 5 "Generate metadata"
    python scripts/archive_agent/07_metadata_worker.py --session $Session
    if ($LASTEXITCODE -ne 0) { Write-Fail "Metadata failed"; exit 1 }

    Write-Header "PIPELINE COMPLETE — $Session"
    Write-OK "All 5 stages complete"
    Write-Host "  Clips:    archive/clips/" -ForegroundColor White
    Write-Host "  Metadata: archive/metadata/$Session`_metadata.json" -ForegroundColor White

    Write-Step "git" "Committing results..."
    git add archive/
    git commit -m "feat(archive): pipeline complete for $Session"
    git push
    Write-OK "Committed and pushed"
}
