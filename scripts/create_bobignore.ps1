# Create .bobignore for Bob IDE Context Control
# Delete stale .claudeignore (legacy from standalone Claude)

Write-Host "Creating .bobignore for Bob IDE..." -ForegroundColor Cyan

# Create .bobignore content
$content = @"
# Bob IDE Context Exclusions
# Reduces session start from 86k to ~20k tokens

# AI System Plumbing
.agent/
.agents/
mcp-servers/
node_modules/
.npm/

# Build Artifacts
bin/
obj/
*.bak*
*.tmp
*.log

# VS/System Noise
.vscode/
.history/
.git/

# Completed Wave Documentation
docs/brain/WAVE1*/
docs/brain/WAVE2*/
docs/brain/WAVE3*/
docs/brain/WAVE4*/
docs/brain/WAVE5*/
docs/brain/WAVE6*/

# Epic Folders (load on-demand)
docs/brain/EPIC-CCN-*/
docs/brain/EPIC-*/

# Templates (load on-demand)
building-blocks/

# Large Reference Docs
docs/andrewngtrascript.md
docs/bobshell_docs.md
docs/goose cli api.md
docs/Hermes Architecture.md
docs/droid api docs.md
docs/greptiledocs.md
docs/Gitbutlerdocs.md

# Historical Analysis
docs/brain-vm-backup/
temp_epic_*/
EPIC-CCN-*/

# Old Wave 2 Scripts
_p0_*.sh
_p1_*.sh
_p2_*.sh
_p3_*.sh
_p4_*.sh
_p5_*.sh
_p5v_*.sh
_p6_*.sh
_phase*.sh
complete_epic_*.sh

# Tool Directories
conductor/
routa-tools/
sandbox/
scaffolds/
Traycerrefactor/

# Test/Benchmark
benchmarks/
tests/

# VM Backups
src-vm-backup/

# Temporary
temp_*/
"@

Set-Content -Path ".bobignore" -Value $content -Encoding UTF8
Write-Host "✓ .bobignore created" -ForegroundColor Green

# Delete stale .claudeignore
if (Test-Path ".claudeignore") {
    Remove-Item ".claudeignore" -Force
    Write-Host "✓ Deleted stale .claudeignore" -ForegroundColor Green
} else {
    Write-Host "✓ No .claudeignore to delete" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Done! Restart Bob IDE session to see reduced context." -ForegroundColor Cyan

# Made with Bob
