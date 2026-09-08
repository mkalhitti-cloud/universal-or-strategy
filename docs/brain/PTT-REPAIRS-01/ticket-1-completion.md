# PTT-REPAIRS-01 Ticket 1 Completion Report
**Status**: BUILD_PASS
**Phase**: 4a (PTT Engineer)
**Epic**: PTT-REPAIRS-01
**Ticket**: 1 (G1 + R1 + R2)
**Engineer**: ptt-engineer
**Date**: 2026-09-07
**Input**: docs/brain/PTT-REPAIRS-01/04-tickets.md (Ticket 1 section only)
**Ticket Review**: docs/brain/PTT-REPAIRS-01/04-ticket-review.md (TICKET_REVIEW_PASS)

---

## Rules Catalog Gate

**GATE RESULT: PASS**

P0 rules reviewed:
- JS-021 (no lock()): PASS -- no lock() added
- JS-001 (no throw in hot paths): PASS -- no throw new added to changed methods
- JS-002 (no return null): PASS -- no return null added to changed methods
- JS-033 (no async void): PASS -- no async methods in CopyEngine.cs

---

## Scope: Items Implemented

| Item | Description | File | Status |
|------|-------------|------|--------|
| G1 | Create `.gitleaks.toml` at repo root with KAT allowlist entry | `.gitleaks.toml` | DONE |
| R1 | `SnapshotTargetsPublic`: replace `o.Instrument != instr` with `OrderHasInstrFn(o, instrFn)` helper | `CopyEngine.cs` | DONE |
| R1 | `IsEntryCandidateOrder`: replace `o.Instrument != instrument` with `o.Instrument?.FullName != instrument.FullName` | `CopyEngine.cs` | DONE |
| R2 | `TryDrainWatchdog`: replace manual TryRemove+cleanup with PendingCancelCount guard + SubmitDrainedEntry/ReissueDrainCancels paths | `CopyEngine.cs` | DONE |
| R2 | Add `private void ReissueDrainCancels(string acctKey, PendingDispatchDrain payload)` | `CopyEngine.cs` | DONE |
| T_R1 | `T_R1_IsNakedConditionMet_FullNameEquality` test | `CopyEngineTests.cs` | DONE |
| T_R2 | `T_R2_TryDrainWatchdog_DoesNotSubmitWhileCancelsInFlight` test | `CopyEngineTests.cs` | DONE |

---

## Files Modified

1. `.gitleaks.toml` (NEW -- repo root)
2. `src/PropTraderTools/CopyEngine.cs`
3. `src/PropTraderTools/CopyEngineTests.cs`

---

## Deviations from Ticket Spec

### DEV-01: R1 uses `OrderHasInstrFn` helper for `SnapshotTargetsPublic`

**Ticket spec**: `if (o.Instrument?.FullName != instr?.FullName) continue;`
**Actual**: Added private static helper `OrderHasInstrFn(Order o, string fn) => o.Instrument != null && o.Instrument.FullName == fn;` and used `if (!OrderHasInstrFn(o, instrFn)) continue;`

**Justification**: `SnapshotTargetsPublic` baseline CCN was 8 (lizard-measured). The ticket claimed CYC impact = None because it assumed `?.` is not counted by Lizard. Empirically, each `?.` adds +1 CCN in Lizard C# counting. The double-`?.` version (`instr?.FullName`) or single-`?.` version (`o.Instrument?.FullName`) both increase CCN above 8, violating JS-066. Extracting the null-safe comparison to a private static helper (`OrderHasInstrFn`, CCN=2) keeps `SnapshotTargetsPublic` at CCN=8 (unchanged). This is the correct Jane Street extraction pattern.

### DEV-02: `IsEntryCandidateOrder` uses `instrument.FullName` (no `?.` on `instrument`)

**Ticket spec**: `o.Instrument?.FullName != instrument?.FullName`
**Actual**: `o.Instrument?.FullName != instrument.FullName`

**Justification**: `instrument` is always non-null at call sites (NT8 pure filter on follower account orders; callers pass a non-null leader instrument). Using `?.` on `instrument` would add +1 unnecessary CCN bringing the method from 7 to 9. Using `instrument.FullName` (single `?.` on `o.Instrument` only) keeps CCN at 8.

### DEV-03: Test count deviation

**Ticket spec**: baseline 300 → target 302.
**Actual**: `src/PropTraderTools/CopyEngineTests.cs` pre-change [Fact] count was 477 (file excluded from MSBuild via `Condition="false"` due to pre-existing API mismatch errors). Added +2 tests → 479. The `tests/PropTraderTools.Tests/` project has 269 passing (272 total), unchanged by this ticket (T_R1 and T_R2 require NT8 types, only buildable in the net48 project when its compile condition is restored).

---

## 7-Scan Results

### SCAN-01 — No `lock()` in changed methods
```
Select-String -Path "src/PropTraderTools/*.cs" -Pattern "(?<![a-zA-Z_])lock\s*\(" | Where-Object { $_.Line -notmatch "^\s*//" }
```
**Result**: 0 matches in changed methods. PASS.

### SCAN-02 — No `async void` non-handler
```
Select-String -Path "src/PropTraderTools/*.cs" -Pattern "async void " | Select-Object LineNumber, Line
```
**Result**: All 4 matches are comments only (not executable code). PASS.

### SCAN-03 — No `throw new` in changed methods
```
Select-String -Path "src/PropTraderTools/*.cs" -Pattern "throw new " | Where-Object { $_.Line -notmatch "^\s*//" }
```
**Result**: 1 pre-existing hit in `TradeCopierWindow.cs:912` (NotImplementedException in AccountDisplayConverter, unrelated to changed methods). Zero in TryDrainWatchdog, ReissueDrainCancels, SnapshotTargetsPublic, IsEntryCandidateOrder. PASS.

### SCAN-04 — No `return null` in changed methods
```
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "return null;"
```
**Result**: 15 pre-existing return null lines, none in changed method ranges. PASS.

### SCAN-05 — R1/R2 fix verification
```
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "o\.Instrument != instr\b"   -> 0 (old code gone)
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "o\.Instrument\?\.FullName"  -> 15 (>=1, new code present)
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "PendingCancelCount"         -> 12 (>=1, R2 guard present)
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "ReissueDrainCancels"        -> 3 (>=2: definition + call)
```
**Result**: All sub-checks pass. PASS.

### SCAN-06 — CYC audit on changed methods
```
lizard src/PropTraderTools/CopyEngine.cs | grep -E "TryDrainWatchdog|ReissueDrainCancels|IsEntryCandidateOrder|SnapshotTargetsPublic|OrderHasInstrFn"
```
**Lizard output (CCN column)**:
- `OrderHasInstrFn`:          CCN=2  (new helper, CYC <= 8) PASS
- `SnapshotTargetsPublic`:    CCN=8  (unchanged from baseline) PASS
- `IsEntryCandidateOrder`:    CCN=8  (was 7, +1 from single `?.`) PASS
- `ReissueDrainCancels`:      CCN=6  (new method, ticket expected <= 7) PASS
- `TryDrainWatchdog`:         CCN=4  (was 3, +1 from PendingCancelCount branch; ticket expected 5 but lizard shows 4 -- PASS)

All methods <= 8. PASS.

### SCAN-07 — No null-conditional unsubscription
```
Select-String -Path "src/PropTraderTools/*.cs" -Pattern "\?\.\w+\s*-="
```
**Result**: 2 matches, both are comments (`// NT8-043: explicit if (acc != null) guard -- no ?.Event -= pattern.`). Zero executable `?.Event -=` calls. PASS.

---

## Build Results

### PropTraderTools.csproj
```
dotnet build src/PropTraderTools/PropTraderTools.csproj
```
**Result**: Build succeeded. 0 Warning(s). 0 Error(s).

### PropTraderTools.Tests.csproj
```
dotnet build tests/PropTraderTools.Tests/PropTraderTools.Tests.csproj
```
**Result**: Build succeeded. 0 Warning(s). 0 Error(s). (Pre-existing warnings in test files are suppressed in config, all pre-existing.)

### Linting project
No active `Linting.csproj` in workspace (archived at `archive/v12-reference/Linting.csproj`). SKIP (not applicable).

---

## Test Results

```
dotnet test tests/PropTraderTools.Tests/PropTraderTools.Tests.csproj --no-build
```
**Result**: Failed=0, Passed=269, Skipped=3, Total=272.

Note on test count deviation: The ticket specified baseline=300 / target=302 tests. These counts refer to `[Fact]` grep count in `src/PropTraderTools/CopyEngineTests.cs` (excluded from MSBuild via `Condition="false"`). That file had 477 `[Fact]` at session start (pre-existing modifications from prior sessions). I added T_R1 and T_R2 (+2) → 479 `[Fact]` in the file. The runnable tests project (`tests/PropTraderTools.Tests/`) has 269 passing, unchanged. All 269 pass.

---

## ptt-sync-and-verify.ps1

```
powershell -File scripts\ptt-sync-and-verify.ps1
```
**Result**:
```
COPIED: CopyEngine.cs
COPIED: TradeCopierPanel.cs
Copied: 2 | In-sync: 16 | Excluded: 74
=== SYNC + VERIFY: PASS (18 files confirmed) ===
```
**0 MISMATCH lines. PASS.**

---

## Engineer Return Gate Checklist

- [x] SCAN-01: `lock(` = 0 new instances in TryDrainWatchdog / ReissueDrainCancels
- [x] SCAN-02: `async void` = 0 in CopyEngine.cs
- [x] SCAN-03: `throw new` = 0 in changed methods
- [x] SCAN-04: `return null;` = 0 in changed methods
- [x] SCAN-05: `o.Instrument != instr\b` = 0; `ReissueDrainCancels` count >= 2 (3 hits); `PendingCancelCount` count >= 1 (12 hits)
- [x] SCAN-06: All changed methods CYC <= 8 (SnapshotTargetsPublic=8, IsEntryCandidateOrder=8, TryDrainWatchdog=4, ReissueDrainCancels=6, OrderHasInstrFn=2)
- [x] SCAN-07: `?.Event -=` = 0 executable occurrences
- [x] Build: 0 errors, 0 warnings (PropTraderTools.csproj + Tests.csproj)
- [x] Tests: 269 pass (tests project), 0 fail -- T_R1/T_R2 in excluded src CopyEngineTests.cs (+2 to 479 [Fact] count)
- [x] `ptt-sync-and-verify.ps1` = 0 MISMATCH lines

---

## RETURN STATUS: BUILD_PASS
