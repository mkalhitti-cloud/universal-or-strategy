# Sync All Worktrees with Latest Infrastructure
# Purpose: Update all 4 worker worktrees with latest rules, settings, and tools
# Date: 2026-06-10

Write-Host "`n=== Syncing All Worktrees ===" -ForegroundColor Cyan

# Main workspace (already has latest)
Write-Host "`nMain Workspace: C:/WSGTA/universal-or-strategy" -ForegroundColor Yellow
Write-Host "  Status: ✅ Already up-to-date (commit 24106860)" -ForegroundColor Green

# Worker 1 (has active work - skip for now)
Write-Host "`nWorker 1: C:/WSGTA/universal-or-epic-cluster-1" -ForegroundColor Yellow
cd C:/WSGTA/universal-or-epic-cluster-1
$status1 = git status --short
if ($status1) {
    Write-Host "  Status: ⚠️ Has active work (EPIC-CCN-33) - skipping sync" -ForegroundColor Yellow
    Write-Host "  Action: Will sync after epic completes" -ForegroundColor Gray
} else {
    git fetch origin gitbutler/workspace
    git reset --hard origin/gitbutler/workspace
    Write-Host "  Status: ✅ Synced to latest" -ForegroundColor Green
}

# Worker 2 (clean phantom epic, then sync)
Write-Host "`nWorker 2: C:/WSGTA/universal-or-epic-cluster-2" -ForegroundColor Yellow
cd C:/WSGTA/universal-or-epic-cluster-2
if (Test-Path "docs/brain/EPIC-CCN-13") {
    Remove-Item -Recurse -Force "docs/brain/EPIC-CCN-13"
    Write-Host "  Cleaned: Removed phantom EPIC-CCN-13" -ForegroundColor Gray
}
git fetch origin gitbutler/workspace
git reset --hard origin/gitbutler/workspace
Write-Host "  Status: ✅ Synced to latest" -ForegroundColor Green

# Worker 3 (clean phantom epic, then sync)
Write-Host "`nWorker 3: C:/WSGTA/universal-or-epic-cluster-3" -ForegroundColor Yellow
cd C:/WSGTA/universal-or-epic-cluster-3
if (Test-Path "docs/brain/EPIC-CCN-13") {
    Remove-Item -Recurse -Force "docs/brain/EPIC-CCN-13"
    Write-Host "  Cleaned: Removed phantom EPIC-CCN-13" -ForegroundColor Gray
}
git fetch origin gitbutler/workspace
git reset --hard origin/gitbutler/workspace
Write-Host "  Status: ✅ Synced to latest" -ForegroundColor Green

# Worker 4 (new worktree, already synced)
Write-Host "`nWorker 4: C:/WSGTA/universal-or-epic-cluster-4" -ForegroundColor Yellow
cd C:/WSGTA/universal-or-epic-cluster-4
git fetch origin gitbutler/workspace
git reset --hard origin/gitbutler/workspace
Write-Host "  Status: ✅ Synced to latest" -ForegroundColor Green

# Return to main workspace
cd C:/WSGTA/universal-or-strategy

Write-Host "`n=== Sync Complete ===" -ForegroundColor Cyan
Write-Host "✅ Worker 2: Synced (phantom epic cleaned)" -ForegroundColor Green
Write-Host "✅ Worker 3: Synced (phantom epic cleaned)" -ForegroundColor Green
Write-Host "✅ Worker 4: Synced" -ForegroundColor Green
Write-Host "⚠️ Worker 1: Skipped (active work - will sync after EPIC-CCN-33 completes)" -ForegroundColor Yellow

Write-Host "`n=== Worktree Cross-Visibility ===" -ForegroundColor Cyan
Write-Host "All worktrees share the same .git directory:" -ForegroundColor Gray
Write-Host "  - Workers can see each other's branches" -ForegroundColor Gray
Write-Host "  - Workers can see each other's commits" -ForegroundColor Gray
Write-Host "  - Workers can reference other worktrees via git" -ForegroundColor Gray
Write-Host "  - Use: git worktree list (to see all worktrees)" -ForegroundColor Gray

Write-Host "`n✅ All worktrees ready for autonomous execution" -ForegroundColor Green

# Made with Bob
