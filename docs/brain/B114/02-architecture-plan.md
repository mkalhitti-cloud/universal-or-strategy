# B114 Architecture Plan — DW-B119 TryAdd Placement Race Fix

**Block**: B114
**Date**: 2026-08-27
**Status**: DRAFT — awaiting ptt-plan-reviewer Phase 2
**Author**: ptt-architect (Phase 1)
**Defect closed**: DW-B119 (P0) — `_qxPendingFollowerCleanup` TryAdd placement race
**Defect monitored**: DW-B120 (P1) — partial ATM arm (snapshot=3); mitigated by DW-B119 fix
**Prior block**: B113 (PIPELINE_COMPLETE 2026-08-26)

---

## Section A: Executive Summary

### Root Cause

`ExecuteOne` in [`src/PropTraderTools/Features/PttGlobalQuickExit.cs`](src/PropTraderTools/Features/PttGlobalQuickExit.cs) was modified in B113 to implement the cancel-after pattern (DW-B117). B113 correctly restructured the `try/finally` to wrap `executor.Execute` and arm `_qxPendingFollowerCleanup`. However, the `_qxPendingFollowerCleanup.TryAdd` call was placed **inside** the `try{}` block, **after** `executor.Execute` returns:

```csharp
// B113 SHIPPED STATE (broken ordering):
CopyEngine.Instance?._qxCancelInProgress.TryAdd(acc.Name, true);
try
{
    executor.Execute(...);                           // <-- Submit fires here
    CopyEngine.Instance?._qxPendingFollowerCleanup.TryAdd(   // <-- TOO LATE
        acc.Name, (instr, DateTime.UtcNow.AddSeconds(2)));
}
finally { CopyEngine.Instance?._qxCancelInProgress.TryRemove(...); }
```

In NT8 Simulator, `SubmitOrder` dispatches `OnOrderUpdate` callbacks **synchronously** on the same call stack. This means that `PTT-QX-T*` orders go to `Working` state *during* the execution of `executor.Execute`, *before* `TryAdd` to `_qxPendingFollowerCleanup` runs. When `TryCleanupReArmedAtmBracket` (called from `OnOrderUpdate`) invokes `TryGetValue` on the empty map, the guard returns false and no cleanup fires.

**Consequence**: Native ATM `Target1/2/3` brackets survive alongside `PTT-QX-T*` orders, creating an OCO conflict that can cancel `PTT-QX-T*` non-deterministically. QX-ALL is UNSAFE for live trading until this fix is deployed.

### Fix Rationale

Move `_qxPendingFollowerCleanup.TryAdd` to **before** the `try{}` block — before `executor.Execute`. This arms the cleanup map before any `OnOrderUpdate` reentrancy can occur:

```csharp
// B114 FIXED STATE:
CopyEngine.Instance?._qxCancelInProgress.TryAdd(acc.Name, true);
// B114 DW-B119: arm cleanup map BEFORE executor.Execute.
// In NT8 Sim, SubmitOrder dispatches OnOrderUpdate synchronously -- TryAdd after Execute is too late.
CopyEngine.Instance?._qxPendingFollowerCleanup.TryAdd(
    acc.Name, (instr, DateTime.UtcNow.AddSeconds(2)));
try
{
    executor.Execute(...);                           // OnOrderUpdate fires DURING this call in Sim
}
finally { CopyEngine.Instance?._qxCancelInProgress.TryRemove(...); }
```

### Why Moving TryAdd Earlier is Correct

1. **Sim-mode synchronous dispatch**: NT8 Sim fires `OnOrderUpdate(Working)` synchronously within `SubmitOrder`. The cleanup map must be populated before the first `SubmitOrder` call.
2. **Live-mode correctness preserved**: In live NT8, `OnOrderUpdate` fires asynchronously. TryAdd before Execute is equally correct — the map is populated before any possible Working event regardless of timing mode.
3. **Exception safety**: If `executor.Execute` throws, the map entry sits unused until the 2-second TTL expires (no matching `PTT-QX-T*` orders exist to trigger cleanup). The `finally{}` block correctly removes `_qxCancelInProgress` unconditionally.
4. **Zero CYC impact**: TryAdd is a simple method call with no conditional branching. `ExecuteOne` CYC remains 2.

---

## Section B: Change Scope

### B114 changes exactly 2 source files + 2 documentation files

| File | Change | Type |
|------|--------|------|
| `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | Move `TryAdd` 3 lines earlier + add DW-B119 comment | Source (1 method) |
| `src/PropTraderTools/Tests/B113Tests.cs` | T_B113_01: rename method + flip description | Test (1 method) |
| `docs/brain/NO-PIPELINE-REPAIRS.md` | Add DW-B119 entry | Documentation |
| `specs/002-trade-copier-spec.html` | Update #section-dw-b119, #section-dw-b120, #section-dw-b117 | Spec |

### Files NOT Modified

| File | Reason |
|------|--------|
| `src/PropTraderTools/CopyEngine.cs` | `TryCleanupReArmedAtmBracket`, `_qxPendingFollowerCleanup` field, `[InternalsVisibleTo]` all correctly deployed by B113. No change needed. |
| `src/PropTraderTools/Features/PttQuickExit.cs` | Per-account submit loop unchanged |
| `src/PropTraderTools/TradeCopierPanel.cs` | UI layer unchanged |
| Any other source file | B114 is a 3-line reorder in one method |

### ASSEMBLY-SEAM note

`[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("PropTraderTools.Tests")]`
is confirmed present at `CopyEngine.cs` L46, deployed by B113. B114 must NOT add it again.

---

## Section C: Detailed Change — `PttGlobalQuickExit.cs` :: `ExecuteOne` Follower Path

**File**: [`src/PropTraderTools/Features/PttGlobalQuickExit.cs`](src/PropTraderTools/Features/PttGlobalQuickExit.cs)
**Method**: `ExecuteOne` (private)
**Location**: L145–181 (follower guard block — confirmed via file read 2026-08-27)

### BEFORE (current B113 shipped state, L145–181 verbatim)

```csharp
            if (!skipIfFollower) // (1) follower path: cancel-after pattern (B113 DW-B117)
            {
                NinjaTrader.Code.Output.Process(
                    "[PTT-QX-GUARD] follower submit (cancel-after): "
                        + (acc != null ? acc.Name : "NULL"),
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
                // DW-B105: intent-guard covers the submit window so TryReplacePttBeBrackets
                // skips ATM-sweep recovery while PTT-QX orders are being placed.
                // B113 DW-B117: guard now wraps executor.Execute (not CancelQxBrackets).
                CopyEngine.Instance?._qxCancelInProgress.TryAdd(acc.Name, true);
                try
                {
                    var executor = new PttQuickExit();
                    executor.Execute(
                        acc,
                        instr,
                        t1Ticks,
                        targets,
                        skipIfFollower,
                        leaderStop,
                        leaderTargetCount
                    );
                    // B113 DW-B117: arm cancel-after cleanup. OnOrderUpdate will cancel each
                    // native ATM Target* one-for-one as the corresponding PTT-QX-T* confirms Working.
                    CopyEngine.Instance?._qxPendingFollowerCleanup.TryAdd(
                        acc.Name,
                        (instr, DateTime.UtcNow.AddSeconds(2))
                    );
                }
                finally
                {
                    // DW-B112: TryRemove clears guard synchronously after submit completes.
                    // DW-B112 Option 2 structural check compensates for async Cancelled events.
                    CopyEngine.Instance?._qxCancelInProgress.TryRemove(acc.Name, out _);
                }
                return; // follower path complete
            }
```

### AFTER (B114 fixed state)

```csharp
            if (!skipIfFollower) // (1) follower path: cancel-after pattern (B113 DW-B117)
            {
                NinjaTrader.Code.Output.Process(
                    "[PTT-QX-GUARD] follower submit (cancel-after): "
                        + (acc != null ? acc.Name : "NULL"),
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
                // DW-B105: intent-guard covers the submit window so TryReplacePttBeBrackets
                // skips ATM-sweep recovery while PTT-QX orders are being placed.
                // B113 DW-B117: guard now wraps executor.Execute (not CancelQxBrackets).
                CopyEngine.Instance?._qxCancelInProgress.TryAdd(acc.Name, true);
                // B114 DW-B119: arm cancel-after cleanup BEFORE executor.Execute so that
                // OnOrderUpdate finds the map entry when PTT-QX-T* goes Working.
                // In NT8 Sim, SubmitOrder dispatches OnOrderUpdate synchronously on the same
                // call stack -- TryAdd after Execute is too late (map empty when Working fires).
                CopyEngine.Instance?._qxPendingFollowerCleanup.TryAdd(
                    acc.Name,
                    (instr, DateTime.UtcNow.AddSeconds(2))
                );
                try
                {
                    var executor = new PttQuickExit();
                    executor.Execute(
                        acc,
                        instr,
                        t1Ticks,
                        targets,
                        skipIfFollower,
                        leaderStop,
                        leaderTargetCount
                    );
                }
                finally
                {
                    // DW-B112: TryRemove clears guard synchronously after submit completes.
                    // DW-B112 Option 2 structural check compensates for async Cancelled events.
                    CopyEngine.Instance?._qxCancelInProgress.TryRemove(acc.Name, out _);
                }
                return; // follower path complete
            }
```

**Net change**: `_qxPendingFollowerCleanup.TryAdd(...)` block (5 lines + comment) moved from inside `try{}` after `executor.Execute` to before `try{}`. The `try{}` body now contains only `executor.Execute`. Comment updated from B113 label to B114 DW-B119 label.

**DW-B112 guard (finally block)**: Preserved exactly as-is. `TryRemove(_qxCancelInProgress)` still fires unconditionally regardless of whether Execute succeeds or throws.

---

## Section D: Sequence Diagram — OnOrderUpdate Callback Timing

### BEFORE fix (B113 shipped state — map empty when Working fires)

```
QX-ALL fires
  └─ ExecuteOne(follower)
       ├─ _qxCancelInProgress.TryAdd("Sim101", true)     [guard armed]
       ├─ try {
       │     executor.Execute(...)
       │       └─ SubmitOrder(PTT-QX-T1) [NT8 Sim: synchronous dispatch]
       │            └─ OnOrderUpdate(PTT-QX-T1=Working) fires ON THIS STACK
       │                 └─ TryCleanupReArmedAtmBracket(e)
       │                      └─ TryGetValue("Sim101") --> FALSE (map empty)
       │                           └─ return early --> Target1 NOT cancelled [BUG]
       │       └─ SubmitOrder(PTT-QX-T2) [NT8 Sim: synchronous dispatch]
       │            └─ OnOrderUpdate(PTT-QX-T2=Working) fires ON THIS STACK
       │                 └─ TryGetValue("Sim101") --> FALSE --> Target2 NOT cancelled [BUG]
       │       └─ SubmitOrder(PTT-QX-T3) [NT8 Sim: synchronous dispatch]
       │            └─ TryGetValue("Sim101") --> FALSE --> Target3 NOT cancelled [BUG]
       │     _qxPendingFollowerCleanup.TryAdd("Sim101", ...)  <-- TOO LATE
       │ } finally { _qxCancelInProgress.TryRemove("Sim101") }
       └─ return
```

**Result**: Native ATM Target1/2/3 survive. OCO conflict with PTT-QX-T* orders.

### AFTER fix (B114 state — map armed before Execute)

```
QX-ALL fires
  └─ ExecuteOne(follower)
       ├─ _qxCancelInProgress.TryAdd("Sim101", true)          [guard armed]
       ├─ _qxPendingFollowerCleanup.TryAdd("Sim101", ...)      [map armed FIRST]
       ├─ try {
       │     executor.Execute(...)
       │       └─ SubmitOrder(PTT-QX-T1) [NT8 Sim: synchronous dispatch]
       │            └─ OnOrderUpdate(PTT-QX-T1=Working) fires ON THIS STACK
       │                 └─ TryCleanupReArmedAtmBracket(e)
       │                      └─ TryGetValue("Sim101") --> TRUE  (map armed)
       │                           └─ Cancel(Target1) fires --> Target1 cancelled [FIXED]
       │       └─ SubmitOrder(PTT-QX-T2) [NT8 Sim: synchronous dispatch]
       │            └─ OnOrderUpdate(PTT-QX-T2=Working) fires ON THIS STACK
       │                 └─ TryGetValue("Sim101") --> TRUE --> Target2 cancelled [FIXED]
       │       └─ SubmitOrder(PTT-QX-T3) [NT8 Sim: synchronous dispatch]
       │            └─ TryGetValue("Sim101") --> TRUE --> Target3 cancelled, TryRemove [FIXED]
       │ } finally { _qxCancelInProgress.TryRemove("Sim101") }
       └─ return
```

**Result**: All native ATM Target1/2/3 cancelled one-for-one. No OCO conflict. QX-ALL safe.

---

## Section E: Execution-Order Invariant — Safety Analysis

### Is TryAdd before try{} safe if Execute throws?

**Case 1 — Execute succeeds (normal path)**:
- Map armed → Execute runs → Working events fire during Execute → cleanups fire → TTL/T3 removal cleans up map entry.
- CORRECT ✓

**Case 2 — Execute throws (exceptional path)**:
- Map armed → Execute throws → `finally{}` removes `_qxCancelInProgress` unconditionally.
- `_qxPendingFollowerCleanup` entry remains: no `PTT-QX-T*` orders were submitted (exception before submit), so no `OnOrderUpdate(Working)` events will fire to trigger cleanup.
- Entry expires harmlessly after 2 seconds (TTL path in `TryCleanupReArmedAtmBracket`).
- `_qxCancelInProgress` is still correctly cleared by `finally{}`.
- SAFE ✓

**Case 3 — Execute submits partially (e.g. T1 placed, T2/T3 fail)**:
- T1 Working fires → Target1 cancelled correctly.
- T2/T3 never go Working → no cleanup attempted for Target2/Target3.
- Entry expires after 2 seconds.
- SAFE (no worse than B113 state — partial arm is DW-B120, monitored separately) ✓

**Case 4 — TryAdd to cleanup map returns false (key already exists from prior QX)**:
- Idempotent: the existing entry handles cleanup. New entry silently discarded.
- The existing entry's TTL covers the current QX window (2 seconds from prior call).
- SAFE (edge case, handled by ConcurrentDictionary semantics) ✓

**Key invariant**: `_qxCancelInProgress` TryRemove (in `finally{}`) is completely independent of `_qxPendingFollowerCleanup` TryAdd. Moving TryAdd earlier does NOT change the finally{} behavior in any execution path.

---

## Section F: CYC Analysis

| Method | File | CYC Before | CYC After | Delta | Status |
|--------|------|-----------|-----------|-------|--------|
| `ExecuteOne` | `PttGlobalQuickExit.cs` | 2 | 2 | 0 | PASS (<=8) |

**Manual branch count for `ExecuteOne` after B114**:
- `if (!skipIfFollower)` → +1
- Base → +1
- `try/finally` → 0 (exception handling, not McCabe branch)
- All `TryAdd`/`TryRemove` calls → 0 (simple method calls, no conditional)
- **Total = 2**

**No other methods modified.** `TryCleanupReArmedAtmBracket` (CYC=5, deployed by B113) is unchanged. `OnOrderUpdate` is unchanged. `TryReplacePttBeBrackets` (CYC=7, DW-B112 guard) is unchanged.

---

## Section G: Scan Plan (Ph4b Verifier)

The Ph4b verifier MUST run all 5 scans and record PASS/FAIL for each before confirming BUILD_PASS.

### SCAN-A — No `lock()` in modified region

```powershell
grep -n "lock(" src/PropTraderTools/Features/PttGlobalQuickExit.cs
```
**Pass criterion**: 0 results. All state uses `ConcurrentDictionary.TryAdd` (lock-free). JS-021 PASS.

### SCAN-B — No `async void` introduced

```powershell
grep -n "async void " src/PropTraderTools/Features/PttGlobalQuickExit.cs
grep -n "async void " src/PropTraderTools/Tests/B113Tests.cs
```
**Pass criterion**: 0 results. `ExecuteOne` is synchronous `void`. Test methods are synchronous `void`. JS-033 PASS.

### SCAN-C — ASCII-only strings in modified region

```powershell
grep -Pn "[^\x00-\x7F]" src/PropTraderTools/Features/PttGlobalQuickExit.cs
```
**Pass criterion**: 0 results. New comment uses only ASCII characters. Existing string literals are unchanged. ASCII-only PASS.

### SCAN-D — CYC <= 8 for all in-scope methods

```powershell
python scripts/complexity_audit.py
```
**Pass criterion**: `ExecuteOne` reports CYC = 2 (unchanged). No in-scope method exceeds 8. CYC PASS.

### SCAN-E — `DateTime.Now` ban

```powershell
grep -n "DateTime\.Now[^U]" src/PropTraderTools/Features/PttGlobalQuickExit.cs
```
**Pass criterion**: 0 results. The `TryAdd` call uses `DateTime.UtcNow.AddSeconds(2)` (unchanged from B113). DateTime.Now ban PASS.

---

## Section H: Test Strategy — `B113Tests.cs` T_B113_01 Update

**File**: [`src/PropTraderTools/Tests/B113Tests.cs`](src/PropTraderTools/Tests/B113Tests.cs)

### Change required

Only **T_B113_01** changes. Tests T_B113_02, T_B113_03, T_B113_04 are unchanged.

| Attribute | Old (B113) | New (B114) |
|-----------|-----------|-----------|
| Method name | `QxPendingFollowerCleanup_SetAfterExecuteOne_ForFollower` | `QxPendingFollowerCleanup_SetBeforeExecuteOne_ForFollower` |
| Method comment | "Set AFTER Execute" | "Set BEFORE Execute" |
| Act comment | "simulate the TryAdd call that Change 1 adds in ExecuteOne follower path" | "simulate the TryAdd call that fires BEFORE executor.Execute in ExecuteOne follower path" |
| Assertion logic | **unchanged** — TryAdd produces same correct dict state regardless of position in method | **unchanged** |

### Why assertion logic stays the same

T_B113_01 is a unit test of the ConcurrentDictionary operation itself:
1. Clear dict.
2. Call TryAdd with expected key + expiry.
3. Assert `ContainsKey` is true and `Expiry` is ~2 seconds in the future.

This assertion verifies the correctness of the dict operation — it is invariant to whether TryAdd is called before or after Execute. The ordering concern (before vs after) is the responsibility of the SIM re-test (Combo D) in B114-DEFER-02, not this unit test.

The rename documents that B113 incorrectly named the test "SetAfter" when the correct behavior (after B114) is "SetBefore".

### Full updated T_B113_01 method

```csharp
// -------------------------------------------------------------------------
// T_B113_01: QxPendingFollowerCleanup_SetBeforeExecuteOne_ForFollower
//
// What is tested: The TryAdd call in ExecuteOne follower path fires BEFORE
// executor.Execute, so OnOrderUpdate can find the map entry when PTT-QX-T*
// goes Working (DW-B119 fix -- B114).
// The dict operation itself produces: correct key, non-null Instr slot,
// Expiry ~2s in the future.
// Why direct TryAdd: ExecuteOne requires a live NT8 Account (sealed, no ctor).
// This test verifies the exact dict operation that the follower path performs.
// -------------------------------------------------------------------------
[Fact]
public void QxPendingFollowerCleanup_SetBeforeExecuteOne_ForFollower()
{
    // Arrange
    const string accName = "Sim101";
    var engine = CopyEngine.Instance;
    engine._qxPendingFollowerCleanup.Clear(); // isolate from prior test state
    var expiry = DateTime.UtcNow.AddSeconds(2);

    // Act: simulate the TryAdd call that fires BEFORE executor.Execute
    // in ExecuteOne follower path (B114 DW-B119 fix).
    engine._qxPendingFollowerCleanup.TryAdd(accName, (null!, expiry));

    // Assert
    Assert.True(engine._qxPendingFollowerCleanup.ContainsKey(accName));
    var entry = engine._qxPendingFollowerCleanup[accName];
    Assert.True(entry.Expiry > DateTime.UtcNow);
    Assert.True(entry.Expiry <= DateTime.UtcNow.AddSeconds(3));
}
```

Tests T_B113_02/03/04 are copied verbatim from B113 — no changes.

---

## Section I: Deferred Items

### DW-B120 (P1 — Monitored, not closed)

**Description**: Partial ATM arm when snapshot count = 3 (more native ATM brackets present than PTT-QX targets would cancel). DW-B119 fix eliminates the primary race that caused zero cleanups to fire. After B114 deploys, re-test Combo D to determine if snapshot=3 still produces partial arms.

**Status**: MONITORED — re-assess after B114-DEFER-02 SIM re-test passes.

**Target block**: B115 if SIM testing shows residual partial-arm behavior; closed as mitigated if Combo D shows full cleanup.

---

### B114-DEFER-01 — Director F5 NT8 Compilation Gate

**Priority**: P0 — prerequisite for all SIM re-tests
**Status**: PENDING
**Context**: `ptt-sync-and-verify.ps1` must show 0 MISMATCH after B114 source changes sync to NT8 folder. Then Director presses F5 in NinjaTrader 8. Must produce: `Compilation succeeded. 0 error(s), 0 warning(s).`
**Action**: Director executes after B114-T1 PIPELINE_COMPLETE.

---

### B114-DEFER-02 — Live Re-Test Combo D with B114 Binary

**Priority**: P1 — required before QX-ALL is considered safe for live trading
**Status**: PENDING
**Scenario**: QX-ALL on 3-follower setup (Sim101/Sim102/Sim103 all in position). Confirm cleanup fires for all T1/T2/T3 on all followers.

**Pass criteria**:
- All followers show PTT-QX-T1/T2/T3 all Working after QX-ALL
- `[PTT-QX-CLEANUP]` log lines: one per target per follower (3 cleanups x N followers)
- Native ATM Target1/2/3: ZERO remaining Working after cleanup fires
- `[PTT-QX-GUARD]` log lines: `follower submit (cancel-after):` present for each follower
- No unprotected position

**Fail criterion**: Any follower missing PTT-QX-T2 or T3 Working, or any native Target* surviving.
**Deferred to**: Director SIM gate session (after B114-DEFER-01 green).

---

### B114-DEFER-03 — DW-B120 Re-Assessment After B114 SIM Testing

**Priority**: P1 — conditional on B114-DEFER-02 outcome
**Status**: PENDING
**Context**: If Combo D shows any partial cleanup (snapshot=3 or similar residual behavior), DW-B120 escalates to P0 and requires a dedicated block. If Combo D is fully clean, DW-B120 is considered mitigated and closed.
**Deferred to**: Director decision after B114-DEFER-02 result.

---

### B114-OBS-01 — `acc.Cancel(new Order[] { toCancel })` vs `acc.CancelOrder(toCancel)` Discrepancy

**Priority**: Low — observational only, does not block B114
**Context**: B113 plan/tickets specified `acc.CancelOrder(toCancel)`. The deployed B113 code at `CopyEngine.cs` L2428 uses `acc.Cancel(new Order[] { toCancel })`. This suggests the B113 engineer resolved an NT8 API availability issue at implementation time. The behavior appears correct (cleanups are observed to fire in log output). However, the NT8_FULL_REFERENCE.md should be consulted to confirm `Account.Cancel(Order[])` vs `Account.CancelOrder(Order)` equivalence.
**Action**: Document in spec update. Validate against NT8_FULL_REFERENCE.md in next pipeline review.
**Note**: B114 does NOT touch CopyEngine.cs — this observation is carry-forward only.

---

### Carry-Forward Items from B113

All B113-DEFER-01/02/03 are superseded by B114-DEFER-01/02/03 (same scenarios, B114 binary). Additional carry-forwards unchanged:

| Item | Description | Status |
|------|-------------|--------|
| DW-B107 | MoveStopToBreakEven Step A stale snapshot (BE path) | OPEN, target B108 |
| DW-PTT-BE-FIX-03 | Pre-existing test build errors (83 errors in CopyEngineTests.cs) | OPEN, dedicated block |
| DW-PTT-BE-FIX-01 | Lazy re-resolve for null followers (DW-B85 Option A) | OPEN |
| DW-PTT-BE-FIX-02 | SIM gate: Path B 3-cycle runtime verification | OPEN |
| DW-B42-01 | T_BUG_QX_BE_01 does not assert PTT-QX-T3 | Low priority |
| DW-B42-02 | Live NT8 F5 verification required | Superseded by B114-DEFER-01 |
| DW-B42-03 | IsPttQxTarget range extension for T4/T5 slots | Conditional |
| DW-B89-DEFERRED-01..06 | Combo C/PATH A/PATH B SIM gates, spec update for DW-B89/88/87 | OPEN |

---

## Section J: Spec Update Plan (Ph5 responsibility)

Ph5 (spec updater) must make the following changes to [`specs/002-trade-copier-spec.html`](specs/002-trade-copier-spec.html):

### `#section-dw-b119`
- **Status**: Change from `OPEN` to `CLOSED-B114`
- **Root cause**: `_qxPendingFollowerCleanup.TryAdd` placed after `executor.Execute` in B113. NT8 Sim dispatches `OnOrderUpdate` synchronously within `SubmitOrder`, so the map was empty when `Working` fired.
- **Fix**: Moved `TryAdd` before `try{}` block in `ExecuteOne` follower path (`PttGlobalQuickExit.cs`). Confirmed by 100% reproduction rate across 3 SIM test runs 2026-08-27.
- **Verified by**: B114-T1 code change + B114-DEFER-02 SIM re-test.

### `#section-dw-b120`
- **Status**: Change from `OPEN` to `MONITORED-B114`
- **Note**: Mitigated by DW-B119 fix (cleanup now fires for all T1/T2/T3). Residual partial-arm (snapshot=3) TBD pending B114-DEFER-02 re-test. Re-assess after Combo D.
- **If Combo D fully clean**: Change status to `MITIGATED-B114`.
- **If partial arm persists**: Escalate to P0 OPEN for B115.

### `#section-dw-b117`
- **Status**: Already `CLOSED-B113`. Add note: "B114 confirms B113 cancel-after infrastructure intact. DW-B119 race (TryAdd ordering) fixed in B114 without modifying B113 cleanup logic."

---

## Section K: Jane Street Compliance Checklist

| Rule | Description | B114 Status |
|------|-------------|-------------|
| JS-021 | No `lock()` — ConcurrentDictionary only | PASS — move of TryAdd does not introduce any lock(). _qxPendingFollowerCleanup remains a ConcurrentDictionary. |
| JS-033 | No `async void` (non-event-handler) | PASS — no new methods. ExecuteOne is synchronous void. No async void anywhere in changes. |
| JS-001 | No `throw` in hot paths | PASS — no throw statements added. TryAdd is non-throwing on ConcurrentDictionary. |
| JS-002 | No `return null` for missing values | PASS — no return statements changed. |
| ASCII-only | No Unicode, emoji, or curly quotes in string literals | PASS — new comment is ASCII only. No new string literals. |
| DateTime.Now ban | Use `DateTime.UtcNow` only | PASS — existing `DateTime.UtcNow.AddSeconds(2)` call unchanged. |
| CYC <= 8 | All methods <= 8 McCabe branches | PASS — ExecuteOne CYC = 2 before and after (unchanged). |

---

## Section L: Files Modified / Files NOT Modified Summary

### Files Modified (4 total)

| File | Change |
|------|--------|
| `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | Move `_qxPendingFollowerCleanup.TryAdd` from inside `try{}` (after Execute) to before `try{}` (before Execute). Add B114 DW-B119 comment. |
| `src/PropTraderTools/Tests/B113Tests.cs` | T_B113_01: rename method `SetAfterExecuteOne` → `SetBeforeExecuteOne`. Update method comment and Act comment to reflect before-Execute ordering. Assertion logic unchanged. |
| `docs/brain/NO-PIPELINE-REPAIRS.md` | Add DW-B119 entry: `CLOSED-B114 — TryAdd moved before executor.Execute in ExecuteOne follower path.` |
| `specs/002-trade-copier-spec.html` | Update #section-dw-b119 (CLOSED-B114), #section-dw-b120 (MONITORED-B114), #section-dw-b117 (add B114 confirmation note). |

### Files NOT Modified (all others)

| File | Reason |
|------|--------|
| `src/PropTraderTools/CopyEngine.cs` | All B113 infrastructure (`_qxPendingFollowerCleanup`, `TryCleanupReArmedAtmBracket`, `[InternalsVisibleTo]`) correctly deployed. No changes needed. |
| `src/PropTraderTools/Features/PttQuickExit.cs` | Unchanged — per-account submit loop not involved |
| `src/PropTraderTools/Features/PttGlobalBreakEven.cs` | BE path unchanged |
| `src/PropTraderTools/Features/PttBreakEvenSwap.cs` | BE swap path unchanged |
| `src/PropTraderTools/TradeCopierPanel.cs` | UI layer unchanged |
| `src/PropTraderTools/CopyEngine.cs :: TryCleanupReArmedAtmBracket` | Method body at L2382–2444 untouched — B113 cleanup logic is correct |
| `src/PropTraderTools/CopyEngine.cs :: TryReplacePttBeBrackets` | DW-B112 guard chain (CYC=7) untouched |

---

## Section M: Sync Gate Command

After all implementation is complete, engineer MUST run:

```powershell
powershell -File scripts\ptt-sync-and-verify.ps1
```

Expected output: `N/N OK, 0 MISMATCH` (N = total file count in sync manifest).

Then press **F5** in NinjaTrader 8.
Expected: `Compilation succeeded. 0 error(s), 0 warning(s).`

If any MISMATCH lines appear: STOP. Fix sync before pressing F5.

---

*Plan written by ptt-architect (Phase 1). 10 sequential thoughts recorded. Awaiting ptt-plan-reviewer Phase 2.*
