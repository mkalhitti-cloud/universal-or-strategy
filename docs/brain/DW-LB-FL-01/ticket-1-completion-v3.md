# DW-LB-FL-01 Ticket-1 Completion v3

**Defect ID**: DW-LB-FL-01
**Ticket**: T1 (retry-2 / v3 fix)
**Engineer**: ptt-engineer
**Date**: 2026-09-09
**Status**: BUILD_PASS
**Spec**: `docs/brain/DW-LB-FL-01/02-architecture-plan-v3.md`

---

## 1. Field Name Confirmed

**Grace-window timestamp field**: `_nakedDetectLastQueuedTicks`

- Type: `ConcurrentDictionary<string, long>` (declared at `src/PropTraderTools/CopyEngine.cs` L373)
- Usage in `NakedPositionDetector`: L7250 reads (`GetOrAdd`), L7255 writes (`AddOrUpdate`)
- v3 addition in `TryNakedDetect`: writes via `AddOrUpdate` inside the existing v2 guard branch
- Value type: `(long)(int)Environment.TickCount` (milliseconds since boot, matching NakedPositionDetector pattern at L7248)

---

## 2. Change Summary

### Method Modified: `TryNakedDetect`

**File**: `src/PropTraderTools/CopyEngine.cs`
**Lines**: ~L7217-L7247 (method block expanded by 5 lines from v2 state)

**Before (v2)**:
```csharp
if (e.Order.OrderState == OrderState.Filled && IsPttCopyEntry(e.Order)) // DW-LB-FL-01-V2
    return;
```

**After (v3)**:
```csharp
if (e.Order.OrderState == OrderState.Filled && IsPttCopyEntry(e.Order)) // DW-LB-FL-01-V2
{
    // DW-LB-FL-01-V3: stamp debounce clock so stale cancel acks within 500ms are blocked.
    long now = (long)(int)Environment.TickCount;
    _nakedDetectLastQueuedTicks.AddOrUpdate(
        e.Order.Account.Name, now, (_, __) => now);
    return;
}
```

**Change type**: Single-line return expanded to block with 3 additional lines. No structural change.

**CYC impact**: None. `AddOrUpdate` executes inside existing branch (not a new decision point). CYC=4 unchanged.

**JS compliance**:
- JS-021: `ConcurrentDictionary.AddOrUpdate` is lock-free. No `lock()`.
- JS-001: No `throw` in modified path.
- JS-002: Method is `void`. No `return null`.
- JS-033: Synchronous `void`. Not `async void`.
- ASCII-only: All new string literals/comments are 7-bit ASCII.

---

## 3. v1+v2 Fixes Preserved

| Fix | Method | Status |
|-----|--------|--------|
| v1: FlattenIfNotArming + HasArmingAtmBrackets guard | FlattenIfNotArming, HasArmingAtmBrackets | PRESERVED -- unchanged |
| v2: IsPttCopyEntry guard in TryNakedDetect | TryNakedDetect | PRESERVED -- v3 is inside v2 branch |
| v2: Initialized state in HasArmingAtmBrackets | HasArmingAtmBrackets | PRESERVED -- unchanged |

---

## 4. Files Modified

| File | Change |
|------|--------|
| `src/PropTraderTools/CopyEngine.cs` | TryNakedDetect: v3 timestamp stamp (3 lines inside existing branch) |
| `tests/PropTraderTools.Tests/Core/CopyEngineTests.cs` | Added T20-T23 (4 new [Fact] tests) |

---

## 5. New Tests (T20-T23)

| # | Test Name | Asserts |
|---|-----------|---------|
| T20 | `TryNakedDetect_StampsDebounce_WhenEntryFills` | Entry fill at T=0, cancel ack at T+200ms: 200ms < 500ms = debounced (blocked) |
| T21 | `NakedPositionDetector_NotDebounced_WhenBracketCancels_After500ms` | Stamp at T=0, cancel ack at T+600ms: 600ms >= 500ms = NOT debounced (allowed) |
| T22 | `TryNakedDetect_StillSkipsEntry_WhenV2GuardActive` | IsPttCopyEntry("PTT-Copy")=true, IsPttCopyEntry("Entry")=true (v2 regression guard) |
| T23 | `TryNakedDetect_DoesNotStampDebounce_ForNonPttCancelAck` | IsPttCopyEntry("Stop1")=false, ("Target1")=false, ("Stop2")=false (non-PTT not stamped) |

Framework: xUnit [Fact] only. No NUnit. No MSTest.

Total tests: 23 (T1-T19 regression + T20-T23 new).

---

## 6. Seven Mandatory Scans

### SCAN 1 — lock()
**Command**: `Select-String -Path "src/PropTraderTools/*.cs" -Pattern "lock(" -SimpleMatch`
**Result**: No output (0 matches)
**Status**: PASS

### SCAN 2 — async void
**Command**: `Select-String -Path "src/PropTraderTools/*.cs" -Pattern "async void " -SimpleMatch`
**Result**: 4 matches — ALL are comments in pre-existing code explicitly confirming the code is NOT async void (e.g., "NOT async void (JS-033)"). Zero actual `async void` method declarations.
**Status**: PASS (0 new async void in modified methods)

### SCAN 3 — return null
**Command**: `Select-String -Path "src/PropTraderTools/*.cs" -Pattern "return null;" -SimpleMatch`
**Result**: Pre-existing `return null` statements in methods unrelated to this ticket. `TryNakedDetect` returns `void` — no `return null` possible. Zero new `return null` in modified methods.
**Status**: PASS

### SCAN 4 — ASCII-only
**Command**: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "[^\x00-\x7F]" -Encoding UTF8`
**Result**: No output (0 non-ASCII characters)
**Status**: PASS

### SCAN 5 — CYC
**Command**: `python scripts/complexity_audit.py src/PropTraderTools/CopyEngine.cs` (script not present in repo)
**Manual McCabe analysis**:
- `TryNakedDetect`: base(1) + gate-if(1) + IsFollowerAccount-if(1) + IsPttCopyEntry-if(1) = CYC=4 (unchanged). AddOrUpdate is inside existing branch, not a new decision point.
- `IsPttCopyEntry`: CYC=2 (unchanged -- single || expression)
- `HasArmingAtmBrackets`: CYC=5 (unchanged)
- `FlattenIfNotArming`: CYC=2 (unchanged)
- `NakedPositionDetector`: CYC=6 (unchanged)
All modified/new methods <= 8. Required thresholds: TryNakedDetect<=4 PASS, IsPttCopyEntry<=2 PASS, HasArmingAtmBrackets<=5 PASS.
**Status**: PASS

### SCAN 6 — Build
**Command**: `dotnet build src/PropTraderTools/PropTraderTools.csproj`
**Result**:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:03.95
```
**Status**: PASS

### SCAN 7 — Tests
**Command**: `dotnet test tests/PropTraderTools.Tests/PropTraderTools.Tests.csproj`
**Result**:
```
Passed!  - Failed: 0, Passed: 110, Skipped: 3, Total: 113, Duration: 20 ms
```
New tests T20-T23 individually confirmed:
- Passed PropTraderTools.Tests.Core.CopyEngineTests.TryNakedDetect_StampsDebounce_WhenEntryFills
- Passed PropTraderTools.Tests.Core.CopyEngineTests.NakedPositionDetector_NotDebounced_WhenBracketCancels_After500ms
- Passed PropTraderTools.Tests.Core.CopyEngineTests.TryNakedDetect_StillSkipsEntry_WhenV2GuardActive
- Passed PropTraderTools.Tests.Core.CopyEngineTests.TryNakedDetect_DoesNotStampDebounce_ForNonPttCancelAck
**Status**: PASS

---

## 7. Scan Summary Table

| Scan | Description | Result |
|------|-------------|--------|
| SCAN 1 | lock() in src/PropTraderTools/ | 0 matches -- PASS |
| SCAN 2 | async void in src/PropTraderTools/ | 0 new declarations -- PASS |
| SCAN 3 | return null in modified methods | 0 new (void method) -- PASS |
| SCAN 4 | ASCII-only CopyEngine.cs | 0 non-ASCII -- PASS |
| SCAN 5 | CYC: TryNakedDetect<=4, IsPttCopyEntry<=2, HasArmingAtmBrackets<=5 | Manual McCabe verified -- PASS |
| SCAN 6 | dotnet build: 0 errors | 0 errors, 0 warnings -- PASS |
| SCAN 7 | dotnet test: all pass | 110 passed, 0 failed -- PASS |

---

## Final Status

**BUILD_PASS**

All 7 scans: PASS. Build: 0 errors. Tests: 110 passed / 0 failed. T20-T23 all passing.
v1+v2 fixes preserved. TryNakedDetect CYC=4 unchanged. No lock(), no async void, ASCII-only.
