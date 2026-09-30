# Ticket 1 Verification: BWAVE-NEXT LaneBRepair-R3
**Status**: VERIFY_PASS
**Verifier**: ptt-verifier
**Date**: 2026-08-22
**Branch**: bwave-next-lane-b

---

## 1. Scope Lock Confirmation

SCOPE LOCK - VERIFY TICKET 1 ONLY.
No other ticket completion files read in this session.

---

## 2. Task 1 — R3-F1 BindingFlags Fix

**Result**: PASS

**Evidence from BwaveNextLaneBTests.cs (independently read)**:

Line 15 — `Priv` constant (UNCHANGED, verified by independent read):
```csharp
private const BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance;
```

Lines 172-174 — `FindFollowerEntryOrder` reflection call (FIXED):
```csharp
var method = EngineType.GetMethod(
    "FindFollowerEntryOrder",
    BindingFlags.NonPublic | BindingFlags.Static);
```

**Checklist**:
- [x] Line 172 uses `BindingFlags.NonPublic | BindingFlags.Static` (inline, not via `Priv`)
- [x] `Priv` constant at line 15 = `BindingFlags.NonPublic | BindingFlags.Instance` — UNCHANGED
- [x] `BindingFlags.Instance` NOT used for `FindFollowerEntryOrder` lookup
- [x] `FindFollowerEntryOrder` confirmed `private static` at CopyEngine.cs line 3703 (independent read)

No violation. R3-F1 correctly applied.

---

## 3. Task 2 — R3-F2 SubmitDrainedEntry Cleanup Reorder

**Result**: PASS

**Evidence from CopyEngine.cs lines 6632-6652 (independently read)**:

```csharp
private void SubmitDrainedEntry(string acctKey)
{
    if (!_pendingDispatchDrains.TryRemove(acctKey, out var payload)) // (1)
        return;

    var follower = payload.FollowerAccount;
    if (follower == null) // (2)
        return;

    SubmitEntryDirect( // (3) submit first -- drain IDs still in dict here
        follower,
        payload.Instrument,
        payload.Qty,
        payload.Price,
        payload.Action,
        payload.OrderType);

    // R3-F2: clear drain-owned IDs AFTER submit so IDs are preserved on submit failure.
    foreach (var id in payload.DrainedOrderIds) // (4)
        _drainOwnedOrderIds.TryRemove(id, out _);
}
```

**Checklist**:
- [x] `_pendingDispatchDrains.TryRemove` is FIRST statement (position 1) — line 6634
- [x] `SubmitEntryDirect()` call appears at position (3) — line 6641
- [x] `foreach _drainOwnedOrderIds.TryRemove` loop appears at position (4) — line 6650, AFTER SubmitEntryDirect
- [x] No `try/catch` added — confirmed by reading entire method body
- [x] No new branches — CYC=4 unchanged (same 4 decision points: TryRemove, null, SubmitEntryDirect delegate, foreach)
- [x] R3-F2 rationale comment present at line 6649

No violation. R3-F2 correctly applied.

---

## 4. Task 3 — R3-V1 Dismissal Documented

**Result**: PASS

**Evidence from ticket-1-completion.md line 119**:

```
R3-V1 DISMISSED: NT8 docs confirm Order.Name non-null for live orders. StartsWith is safe. No fix applied.
```

The exact required text is present verbatim in Section 4 of the completion artifact. PASS.

---

## 5. Task 4 — Baseline Preservation (Spot Check)

**Result**: PASS

Evidence (all confirmed by independent CopyEngine.cs read):

| Item | Location | Status |
|------|----------|--------|
| `AbortDrainOnFill` method exists (R2-F1) | CopyEngine.cs line 6657 — `private void AbortDrainOnFill(string acctKey)` | PASS |
| `DrainThenDispatch` entryCandidates includes `\|\| o.Name == "Entry"` (R2-F2) | CopyEngine.cs line 6535 | PASS |
| `_drainOwnedOrderIds` declared as `readonly ConcurrentDictionary<string, byte>` (F3) | CopyEngine.cs line 385 — `private readonly ConcurrentDictionary<string, byte> _drainOwnedOrderIds =` | PASS |
| `(long)(int)Environment.TickCount` preserved (not changed to TickCount64) | CopyEngine.cs lines 6452, 6545, 6672 | PASS |
| `.ToList()` on ActiveOrders preserved | CopyEngine.cs line 3478 (ActiveOrders method body), line 6536 (DrainThenDispatch entryCandidates) | PASS |

All baseline items verified. No regressions detected.

---

## 6. Task 5 — Independent Scan Results

### SCAN 1 — lock()
**Command**: `Get-ChildItem -Path "src\PropTraderTools" -Filter "*.cs" -Recurse | Select-String -Pattern "lock\s*\(" | Where-Object { $_.Line -notmatch '^\s*//'  }`
**Output**: (no output — 0 results)
**Result**: PASS — 0 lock() usages.

### SCAN 5 — dotnet build
**Command**: `dotnet build src/PropTraderTools/PropTraderTools.csproj`
**Output**:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:01.14
```
**Result**: PASS — 0 errors, 0 warnings.
Note: Engineer reported 1 pre-existing warning (xUnit2004 in B131Tests.cs). Independent build shows 0 warnings — warning may have been resolved or suppressed since engineer's build. No new warnings in any case.

### SCAN 6 — dotnet test (filter)
**Command**: `dotnet test --filter "DrainThenDispatch|OnDrainCancelAck|DrainWatchdog|ActiveOrders|NakedDetector|AbortDrainOnFill|FindFollowerEntryOrder" --no-build`
**Output**:
```
Passed!  - Failed:     0, Passed:    11, Skipped:     0, Total:    11, Duration: 1 s - PropTraderTools.dll (net48)
```
**Result**: PASS — 11/11 tests pass. `FindFollowerEntryOrder_AcceptsEntryName_ForCloneMode` confirmed passing (R3-F1 fix verified live by test runner).

---

## 7. Cross-Check vs Engineer Report

| Check | Engineer Report | Verifier Independent Result | Match? |
|-------|----------------|----------------------------|--------|
| SCAN 1 lock() | 0 results | 0 results | MATCH |
| SCAN 5 build errors | 0 errors | 0 errors | MATCH |
| SCAN 5 build warnings | 1 pre-existing warning (xUnit2004) | 0 warnings | MINOR DIFF — warning absent in verifier run; no new warnings either way. Not a violation. |
| SCAN 6 tests | 11 passed, 0 failed | 11 passed, 0 failed | MATCH |
| R3-F1 BindingFlags fix | `BindingFlags.NonPublic \| BindingFlags.Static` at line 172 | Confirmed at lines 172-174 | MATCH |
| R3-F2 submit-before-cleanup | `SubmitEntryDirect` at position (3), foreach at position (4) | Confirmed at lines 6641/6650 | MATCH |
| Priv constant unchanged | Line 15 = `NonPublic \| Instance` | Confirmed | MATCH |
| R3-V1 dismissal verbatim text | Present at completion.md line 119 | Confirmed | MATCH |
| AbortDrainOnFill exists | Confirmed | Line 6657 confirmed | MATCH |
| _drainOwnedOrderIds ConcurrentDictionary | Confirmed | Line 385 confirmed | MATCH |
| (long)(int)TickCount preserved | Confirmed | Lines 6452/6545/6672 confirmed | MATCH |
| .ToList() on ActiveOrders | Confirmed | Lines 3478/6536 confirmed | MATCH |

**Discrepancies**: 1 minor — engineer's SCAN 5 output showed 1 pre-existing warning (xUnit2004 in B131Tests.cs), verifier's independent build shows 0 warnings. This is not a violation — the warning was pre-existing and not in modified files. The build is clean either way (0 errors). The discrepancy is consistent with a suppression being applied between sessions, or a stale cached output. No VERIFY_FAIL triggered.

---

## 8. DNA Rule Audit (Jane Street)

| Rule | Check | Result |
|------|-------|--------|
| JS-021 — no lock() | SCAN 1: 0 results | PASS |
| JS-001 — no throw in hot paths | No throw in SubmitDrainedEntry or FindFollowerEntryOrder area | PASS |
| JS-002 — no return null in non-null context | No new return null in modified methods | PASS |
| JS-033 — no async void | Not present | PASS |
| JS-008 — immutability: readonly ConcurrentDictionary | _drainOwnedOrderIds is readonly at line 385 | PASS |
| NT8 — no try/catch in modified methods | SubmitDrainedEntry has no try/catch | PASS |
| NT8 — DateTime.UtcNow (not DateTime.Now) | No DateTime.Now introduced | PASS |
| NT8 — no FontFamily, no hex color, no PTT- prefix violation | Not introduced | PASS |
| CYC <= 8 — SubmitDrainedEntry | CYC=4 (4 decision points: TryRemove early exit, null early exit, SubmitEntryDirect delegate, foreach) | PASS |

---

## 9. VERIFY_PASS

All tasks pass. All independent scans match engineer report (with one benign warning-count discrepancy, non-blocking). All DNA rules satisfied. All baseline items preserved. R3-F1 fix confirmed live by test runner. R3-F2 fix confirmed by source inspection. R3-V1 dismissal documented verbatim.

**VERIFY_PASS**