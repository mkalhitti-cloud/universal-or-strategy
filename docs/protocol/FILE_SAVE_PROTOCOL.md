# File Save Protocol (V12.24)

**Effective**: 2026-06-01  
**Status**: MANDATORY for all agents and workflows

## Problem Statement

**Root Cause**: Agents editing files via Bob IDE temporary workspace paths instead of `src/` directly, leaving files unsaved in VS Code, causing commits to contain OLD code instead of fixes.

**Evidence**: PR #13 compilation errors - fixes were applied to `C:/Users/Mohammed Khalid/AppData/Local/Programs/IBM Bob/` but never saved, so `src/` files remained broken.

## Mandatory Rules

### Rule 1: Edit src/ Directly
**BANNED**: Editing files in Bob IDE temp paths (`C:/Users/.../IBM Bob/`)  
**REQUIRED**: All edits MUST target `src/` files directly

```powershell
# ❌ WRONG
apply_diff("C:/Users/Mohammed Khalid/AppData/Local/Programs/IBM Bob/V12_002.cs")

# ✅ CORRECT
apply_diff("src/V12_002.cs")
```

### Rule 2: Enable Auto-Save
**REQUIRED**: VS Code auto-save MUST be enabled

**Manual Setup**:
1. Open VS Code
2. `File → Auto Save` (or `Ctrl+,` → search "auto save")
3. Set to "afterDelay" (1000ms)

**Verification**:
```json
// .vscode/settings.json
{
  "files.autoSave": "afterDelay",
  "files.autoSaveDelay": 1000
}
```

### Rule 3: Pre-Commit Unsaved File Check
**REQUIRED**: Before EVERY commit, check for unsaved files

**Visual Check**: Look for white dots on VS Code tabs (indicates unsaved)

**Automated Check** (add to `pre_push_validation.ps1`):
```powershell
# Check for unsaved files in VS Code workspace
$unsavedFiles = Get-ChildItem -Path "src/" -Recurse -File | Where-Object {
    $_.LastWriteTime -gt (Get-Date).AddMinutes(-5)
}
if ($unsavedFiles.Count -gt 0) {
    Write-Host "⚠️ WARNING: Recently modified files detected. Ensure all files are saved in VS Code." -ForegroundColor Yellow
    $unsavedFiles | ForEach-Object { Write-Host "  - $($_.FullName)" }
}
```

### Rule 4: Post-Edit Verification
**REQUIRED**: After applying fixes, verify changes are on disk

```powershell
# After apply_diff or write_to_file
git diff src/V12_002.cs  # Should show your changes
```

## Workflow Integration

### epic-run Command
Add to **Step C** (after v12-engineer execution):

```markdown
**Step C.1 -- File Save Verification (NEW)**

**Switch to: Advanced mode**

Hand off:
```
TASK: Verify File Save Integrity
PROTOCOL:
  1. Run: git status
  2. Check for modified files in src/
  3. If NO modified files: HALT - edits were not saved
  4. Run: git diff src/ | Select-String "^[\+\-]" | Measure-Object
  5. If diff line count < 10: HALT - incomplete save
  6. Emit: [SAVE-VERIFIED] X files modified, Y lines changed
```

**Gate**: If [SAVE-VERIFIED] not emitted, HALT and report to Director.
```

### pr-loop Command
Add to **Step 2** (after v12-engineer fixes):

```markdown
**Step 2.5 -- File Save Verification (NEW)**

Before running `pre_push_validation.ps1`:
1. Check VS Code for unsaved files (white dots on tabs)
2. Run: `git status` - verify modified files exist
3. Run: `git diff --stat` - verify line counts match expectations
4. If ANY file shows 0 changes: HALT - file was not saved
```

### epic-tdd Command
Add to **Step 2** (after test implementation):

```markdown
**Step 2.5 -- Test File Save Verification (NEW)**

Before running tests:
1. Verify test file exists: `Test-Path tests/V12_Performance.Tests/...`
2. Run: `git diff tests/` - verify test code is on disk
3. If diff is empty: HALT - test file was not saved
```

## Prevention Checklist

Before EVERY commit, agents MUST verify:
- [ ] All edits targeted `src/` paths (not Bob IDE temp paths)
- [ ] VS Code auto-save is enabled
- [ ] No white dots on VS Code tabs (unsaved files)
- [ ] `git status` shows modified files in `src/`
- [ ] `git diff src/` shows expected changes
- [ ] Line count in diff matches edit scope

## Enforcement

**Violation Protocol**:
1. If unsaved files detected: HALT commit
2. Save all files manually (Ctrl+S in VS Code)
3. Re-run `git status` to verify
4. Proceed with commit only after verification

**Audit Trail**:
- Log all file save violations to `docs/brain/file_save_violations.log`
- Include: timestamp, agent, file path, violation type

## Related Protocols
- `docs/protocol/BRANCH_STRATEGY.md` - Three-tier branch model
- `docs/protocol/PR_LOOP_V2.md` - Perfection loop workflow
- `docs/protocol/INSTITUTIONAL_WORKFLOW_DNA.md` - Core workflow principles

---

**Last Updated**: 2026-06-01  
**Next Review**: 2026-06-08