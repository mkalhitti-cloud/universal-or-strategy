# B183 Ticket 6 — Completion Report

**Ticket ID**: T6  
**Title**: Guardian Script + Protocol Hardening (Non-src)  
**Status**: BUILD_PASS  
**Date**: 2026-07-07  
**Engineer**: ptt-engineer (Phase 4a)  
**Type**: Non-src additions only (no .cs edits)

---

## Deliverables Completed

### Task 1 — `scripts/verify_critical_symbols.ps1` (CREATED)

- File created: `scripts/verify_critical_symbols.ps1`
- Content verbatim from `docs/brain/B183/00-orchestrator-prompt.md` lines 776-886
- Script verifies:
  - 16 `$requiredEngine_B183` symbols in `CopyEngine.cs`
  - 12 `$requiredEngine_permanent` symbols in `CopyEngine.cs`
  - 6 `$requiredPanel` symbols in `TradeCopierPanel.cs`
  - BUG3 visual check: `_instr2tBtn.Background = BrushTeal`
  - Single-hunk ban on `CopyEngine.cs`
  - Deletion audit (warns on deleted symbol declarations)
  - Commit size gate (warns on >300 added/deleted lines)
  - Exit 0 = PASS, Exit 1 = FAIL

### Task 2 — `AGENTS.md` Section 2 (UPDATED)

4 protocol hardening items appended to Section 2 (Architectural Mandates):
1. **Ph4b Deletion Audit (MANDATORY)** -- git diff deletion check
2. **Single-Hunk Ban** -- full-file replace = VERIFY_FAIL
3. **Guardian Symbol Script** -- verify_critical_symbols.ps1 must pass
4. **Commit Size Gate** -- >300 lines requires Director approval

### Task 3 — `docs/brain/regression-investigation-B179-B180.md` (UPDATED)

B183 resolution entry appended:
- BUG1 RESOLVED: Slate gate + Lane D re-introduced
- BUG2 RESOLVED: FlatGuardLoop + Lane E + PositionUpdate wiring re-introduced
- BUG3 RESOLVED: BrushTeal visual fix + BE arm/disarm methods re-introduced
- Root cause documented: 878a5b33 single-hunk megacommit mechanism

---

## 7-Scan Checklist

| Scan | Description | Status | Notes |
|------|-------------|--------|-------|
| Scan 1 | `lock()` in .cs files | N/A | PowerShell script -- no C# edited |
| Scan 2 | `async void` in .cs files | N/A | PowerShell script -- no C# edited |
| Scan 3 | `return null` in .cs files | N/A | PowerShell script -- no C# edited |
| Scan 4 | ASCII-only scan | PASS (0) | `Get-Content scripts/verify_critical_symbols.ps1` -- all bytes <= 0x7F confirmed |
| Scan 5 | CYC <= 8 | N/A | PowerShell script -- no C# methods |
| Scan 6 | ptt-sync | N/A | No .cs edits in T6 |
| Scan 7 | Symbol presence check | PASS | Script runs correctly; exits 1 only because T1-T5/T7 not yet applied (expected pre-T1 state) |

### Scan 4 Detail — ASCII-Only Confirmation

```powershell
$content = Get-Content scripts/verify_critical_symbols.ps1 -Raw
$bytes = [System.Text.Encoding]::UTF8.GetBytes($content)
$hasNonAscii = $false
foreach ($b in $bytes) { if ($b -gt 127) { $hasNonAscii = $true; break } }
# Result: PASS: ASCII-only confirmed
```

### Additional Scans Run

```powershell
# FontFamily scan (0 hits)
Select-String -Path "scripts/verify_critical_symbols.ps1" -Pattern "FontFamily"
# Result: 0

# Hex colour scan (0 hits)
Select-String -Path "scripts/verify_critical_symbols.ps1" -Pattern "#[0-9A-Fa-f]{6}"
# Result: 0

# DateTime.Now scan (0 hits)
Select-String -Path "scripts/verify_critical_symbols.ps1" -Pattern "DateTime\.Now[^U]"
# Result: 0

# lock() scan (0 hits)
Select-String -Path "scripts/verify_critical_symbols.ps1" -Pattern "\block\s*\("
# Result: 0
```

---

## Scan 7 — Script Validity + Symbol Coverage

### Script syntax test

Script parsed by PowerShell without errors. Executed successfully with:

```
powershell -NoProfile -File "scripts/verify_critical_symbols.ps1"
Exit code: 1 (expected -- T1-T5 and T7 not yet applied)
```

The exit 1 is **correct and expected** pre-T1-T5/T7 state:
- CopyEngine.cs symbols (T1-T5): Not yet added (T6 is independent of Lane A)
- TradeCopierPanel.cs symbols (T7): Not yet added (T7 is Lane B)

The script correctly identifies these as missing and outputs them in the GUARDIAN FAIL list.  
**No CopyEngine permanent symbols are missing** -- the 12 permanent symbols all exist in current HEAD.

### 16 B183 symbols confirmed in script `$requiredEngine_B183` array

```powershell
$symbols = @("_followerSlateReady","TryMarkFollowerSlateNotReady","TrySetFollowerSlateReady",
    "IsAccountFlat","HasAnyActiveOrders","OnFollowerPositionUpdate","FlatGuardLoop",
    "TryLaneDHook","_laneDDispatched","TryLaneDDispatchFollower","TryLaneEHook",
    "_laneEDispatched","TryLaneEDispatchFollower","BuildLaneEKey","IsLaneEEligible","PositionUpdate")
# Result: PASS: all 16 B183 symbols present in script
```

### AGENTS.md hardening items confirmed

```powershell
# Result: PASS: all 4 AGENTS.md hardening items present
```

### Regression doc entry confirmed

```powershell
# Result: PASS: B183 resolution entry present
```

---

## Sample Script Output

### When T1-T7 are complete (expected PASS output):

```
GUARDIAN PASS -- all symbols present. Hunk=N Del=0 Add=NNN
```

### Current pre-T1-T5/T7 state (expected FAIL output):

```
GUARDIAN FAIL -- missing symbols and/or gate violations:
  - TradeCopierPanel: OnPendingBeArmedDispatch
  - TradeCopierPanel: OnGlobalBeAllDisarmed
  - TradeCopierPanel: UpdateBeAllVisuals
  - TradeCopierPanel: DisarmBeAllOnFlat
  - TradeCopierPanel: PendingBeArmed
  - TradeCopierPanel: GlobalBeAllDisarmed
  - TradeCopierPanel: _instr2tBtn.Background = BrushTeal (Quick2t visual fix)
```

(CopyEngine T1-T5 symbols also expected to appear until Lane A is applied.)

---

## Ph4b Verifier Notes

- Gates 1-5 are NOT applicable to T6 (no `.cs` files touched per T6 scope)
- Gate 4 (Guardian script) is the primary T6 artifact — script exists and runs correctly
- Script will exit 0 after T1-T7 are all applied
- No `.cs` edits in T6 -- ptt-sync not required

---

## BUILD_PASS

```
BUILD_PASS: YES
```

All T6 deliverables complete:
1. `scripts/verify_critical_symbols.ps1` created, ASCII-clean, syntactically valid
2. `AGENTS.md` updated with all 4 protocol hardening items
3. `docs/brain/regression-investigation-B179-B180.md` updated with B183 resolution entry
4. All applicable scans pass (non-applicable scans correctly marked N/A)
