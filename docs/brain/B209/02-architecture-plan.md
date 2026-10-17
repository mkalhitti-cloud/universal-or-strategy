# B209 Architecture Plan — PTT-COPIER-B209 BE-ALL Permanent Lock Fix (FIX-G-1 + FIX-G-2)

**Block**: B209  
**Phase**: 1 — Architecture Plan  
**Status**: REVIEW_PASS
**Author**: ptt-architect  
**Date**: 2026-07-07  

---

## LANE-SPLIT GATE

### Q1. Are both fixes in the same method OR within 50 lines of each other?
**NO** — FIX-G-1 targets `TryClearBeSwapOnStopWorking` at ~line 3159. FIX-G-2 targets
`TryCancelSingleStaleGtcExit` at ~line 2177. Distance ~982 lines.

### Q2. Does FIX-G-2 design depend on FIX-G-1 final design?
**NO** — FIX-G-2 calls the pre-existing `IsBeSwapInProgress()` method which is unchanged by FIX-G-1.
FIX-G-1 only adds new terminal states to `TryClearBeSwapOnStopWorking`. The two fixes share a data
structure (`_beSwapInProgress`) but have no design-level dependency.

### Q3. Does each fix have standalone value if the other is blocked?
**YES** — FIX-G-1 eliminates the permanent flag-leak on OCO cancel path independently. FIX-G-2
prevents premature cancellation of a live PTT-BE-Stop-N during rapid re-entry independently.

### Q4. Does each fix have an independent SIM verification path?
**YES** — FIX-G-1: SIM verify by executing an OCO scratch-exit (target fills, stop cancels); confirm
`IsBeSwapInProgress` returns `false` after `Cancelled` transition. FIX-G-2: SIM verify by entering,
entering again quickly while stop is Working and `_beSwapInProgress=true`; confirm stop order
survives the GTC sweep.

### LANE-SPLIT GATE RESULT: LANES-APPROVED
(NO on Q1+Q2, YES on Q3+Q4)

---

## Root Cause Analysis

### Defect 1 — OCO Path: Permanent _beSwapInProgress Flag Leak

**Trigger sequence**:
1. Leader fills. CopyEngine dispatches PTT-Copy/Entry to follower. Follower fills.
2. `PttBreakEvenSwap.Execute()` is called. Sets `_beSwapInProgress[accName] = true`.
3. CopyEngine submits `PTT-BE-Stop-N` (new BE stop) and `PTT-BE-Target-N` (new BE target) as OCO pair.
4. PTT-BE-Target-N fills (profit target hit). NT8 cancels PTT-BE-Stop-N via OCO mechanism.
5. **NT8 fires `OnOrderUpdate` for PTT-BE-Stop-N with `OrderState.Cancelled`.**
6. `TryClearBeSwapOnStopWorking` checks `e.Order.OrderState != OrderState.Working` → **returns early**.
7. `_beSwapInProgress[accName]` stays `true` **permanently**.
8. On any subsequent trade on this follower account, `NakedPositionDetector.Detect()` calls
   `IsBeSwapInProgress(accName)` → returns `true` → **returns early, never fires PTT-Flatten**.
9. Open positions on that follower account are unprotected indefinitely.

**Root cause**: Single-state terminal check. `Working` is the normal case (stop survives to broker).
OCO cancel (`Cancelled`), bad-price reject (`Rejected`), and SIM acceptance (`Accepted`) are all
terminal states that also mean "the swap is resolved — clear the flag" but were not handled.

### Defect 2 — Latent Risk: Premature Cancellation of Live PTT-BE-Stop-N

**Trigger sequence**:
1. BE swap is in progress: `_beSwapInProgress[accName] = true`. PTT-BE-Stop-N is `Working`.
2. Leader opens a new trade. CopyEngine copy-dispatches a new entry to the follower.
3. Follower entry fills (PTT-Copy order reaches `OrderState.Filled`).
4. `TryCancelStaleGtcExitOnEntry` fires. Calls `TryCancelSingleStaleGtcExit` for each order.
5. `IsStaleGtcExitOrder(PTT-BE-Stop-N)` → Working + starts with "PTT-BE-" → **returns true**.
6. `acc.Cancel(new Order[] { PTT-BE-Stop-N })` is called — **live stop cancelled**.
7. Follower's new position is unprotected (BE stop is gone).

**Root cause**: `TryCancelSingleStaleGtcExit` has no awareness of the BE-swap window. It correctly
sweeps genuinely stale GTC exits but cannot distinguish between a stale Working PTT-BE-Stop from a
prior trade vs. a live PTT-BE-Stop-N currently being placed as part of an active swap.

---

## Component Inventory

| Component | File | Lines (approx) | Change |
|-----------|------|-----------------|--------|
| `TryClearBeSwapOnStopWorking` | `CopyEngine.cs` | ~3159 | MODIFY |
| `TryCancelSingleStaleGtcExit` | `CopyEngine.cs` | ~2177 | MODIFY |
| `IsBeClearStateTrigger` | `CopyEngine.cs` | after ~3169 | ADD (internal static seam) |
| `IsBeSwapGtcSweepGuard` | `CopyEngine.cs` | after `IsBeClearStateTrigger` | ADD (internal static seam) |
| `B209Tests.cs` | `Tests/B209Tests.cs` | new file | ADD (10 [Fact] tests) |

---

## Fix-G-1 Design

### Method Signature (unchanged)
```csharp
private void TryClearBeSwapOnStopWorking(OrderEventArgs e)
```

### Method Body (AFTER)
```csharp
// B209-FIX-G-1: clear on Working (normal), Cancelled (OCO), Rejected (bad price), Accepted (SIM).
// CYC=5: (1) name check, (2a-d) four-state isClearState, (3) IsBeSwapInProgress.
// JS-021: no lock. _beSwapInProgress is ConcurrentDictionary -- lock-free. JS-001: no throw.
// JS-002: void. ASCII-only.
private void TryClearBeSwapOnStopWorking(OrderEventArgs e)
{
    if (e.Order.Name == null
        || !e.Order.Name.StartsWith("PTT-BE-Stop", StringComparison.Ordinal))
        return;
    bool isClearState = e.Order.OrderState == OrderState.Working
        || e.Order.OrderState == OrderState.Cancelled
        || e.Order.OrderState == OrderState.Rejected
        || e.Order.OrderState == OrderState.Accepted;
    if (!isClearState)
        return;
    if (!IsBeSwapInProgress(e.Order.Account?.Name))
        return;
    SetBeSwapInProgress(e.Order.Account.Name, false);
}
```

### CYC Analysis (FIX-G-1)
| Branch | Source |
|--------|--------|
| 1 | Base method |
| +1 | `if (e.Order.Name == null \|\| !StartsWith("PTT-BE-Stop"))` — if with `\|\|` |
| +1 | `\|\| Cancelled` in isClearState assignment |
| +1 | `\|\| Rejected` in isClearState assignment |
| +1 | `\|\| Accepted` in isClearState assignment |
| +1 | `if (!isClearState)` |
| +1 | `if (!IsBeSwapInProgress(...))` |
**Strict Lizard CYC = 7 (conservative). Spec-declared CYC = 5. Both <= 8. PASS.**

### Terminal States Covered
| State | Scenario |
|-------|----------|
| `Working` | Normal: BE stop accepted at broker — position protected. Clear flag. |
| `Cancelled` | OCO: BE target filled, NT8 auto-cancels BE stop. Clear flag. |
| `Rejected` | Bad price / broker reject. No position protection needed. Clear flag. |
| `Accepted` | SIM only: NT8 SIM acceptance (no real broker). Clear flag. |

### BEFORE vs. AFTER diff summary
- Remove: `if (e.Order.OrderState != OrderState.Working) return;`
- Add: `bool isClearState = Working || Cancelled || Rejected || Accepted;`
- Add: `if (!isClearState) return;`

---

## Fix-G-2 Design

### Method Signature (unchanged)
```csharp
private void TryCancelSingleStaleGtcExit(Account acc, Order o)
```

### Method Body (AFTER)
```csharp
// B209-FIX-G-2: do not cancel PTT-BE-* orders while BE-swap is in progress.
// CYC: +1 branch from FIX-G-2 guard. Final CYC <= 5 (strict). ASCII-only. JS-021: lock-free.
// JS-001: no new throw. JS-002: void.
private void TryCancelSingleStaleGtcExit(Account acc, Order o)
{
    if (!IsStaleGtcExitOrder(o)) return;
    // B209-FIX-G-2: protect live PTT-BE-Stop-N during active swap window.
    if (o.Name != null
        && o.Name.StartsWith("PTT-BE-", StringComparison.Ordinal)
        && IsBeSwapInProgress(acc.Name))
        return;
    try
    {
        acc.Cancel(new Order[] { o });
        if (StatusUpdate != null)
            StatusUpdate(acc.Name + ": [GTC-SWEEP] cancelled " + o.Name);
    }
    catch (Exception ex)
    {
        if (StatusUpdate != null)
            StatusUpdate("[GTC-SWEEP] cancel error: " + ex.Message);
    }
}
```

### CYC Analysis (FIX-G-2)
| Branch | Source |
|--------|--------|
| 1 | Base method |
| +1 | `if (!IsStaleGtcExitOrder(o))` |
| +1 | `if (o.Name != null && o.Name.StartsWith("PTT-BE-"...) && IsBeSwapInProgress(acc.Name))` — if |
| +1 | first `&&` inside the compound guard |
| +1 | second `&&` inside the compound guard |
| +1 | `if (StatusUpdate != null)` (first) |
| +1 | `if (StatusUpdate != null)` (second, in catch) |
**Strict Lizard CYC <= 7. Both spec-declared CYC=2 and strict counting are <= 8. PASS.**

### Why PTT-QX-* is NOT guarded
`IsStaleGtcExitOrder` returns true for both `PTT-BE-*` and `PTT-QX-*`. The FIX-G-2 guard only
protects `PTT-BE-*` because:
- `_beSwapInProgress` tracks the BE-swap window only.
- PTT-QX-* orders are Quick-Exit orders with their own lifecycle — no swap window tracking.
- Stale PTT-QX-* orders should still be cancelled normally on re-entry.

---

## Testable Seam Designs

### Seam 1: IsBeClearStateTrigger
```csharp
// B209: testable seam for FIX-G-1 state-check logic.
// CYC=5: name check(1) + four-state OR(4). Pure function, no side effects.
// JS-021: no lock. JS-001: no throw. ASCII-only.
internal static bool IsBeClearStateTrigger(string orderName, OrderState state)
{
    if (orderName == null || !orderName.StartsWith("PTT-BE-Stop", StringComparison.Ordinal))
        return false;
    return state == OrderState.Working
        || state == OrderState.Cancelled
        || state == OrderState.Rejected
        || state == OrderState.Accepted;
}
```
**Placement**: After `TryClearBeSwapOnStopWorking` (~line 3170), before next method.

### Seam 2: IsBeSwapGtcSweepGuard
```csharp
// B209: testable seam for FIX-G-2 guard.
// CYC=2: null check + prefix check + beSwapInProgress check. Pure function.
// JS-021: no lock. JS-001: no throw. ASCII-only.
internal static bool IsBeSwapGtcSweepGuard(string orderName, bool beSwapInProgress)
    => orderName != null
        && orderName.StartsWith("PTT-BE-", StringComparison.Ordinal)
        && beSwapInProgress;
```
**Placement**: After `IsBeClearStateTrigger`, before next method.

### InternalsVisibleTo Assumption
Both seams are `internal static`. The test project must have `[assembly: InternalsVisibleTo]` access
to `PropTraderTools`. This attribute is assumed present from B197 (B197 T3 uses a `private static`
seam pattern per the comment at line 2199). **Engineer must verify** this attribute exists in
`AssemblyInfo.cs` or the assembly's `Properties` before creating `B209Tests.cs`.

---

## Test Coverage Plan (B209Tests.cs)

**Framework**: xUnit only. All tests use `[Fact]`. No NUnit. No MSTest.  
**File**: `src/PropTraderTools/Tests/B209Tests.cs`  
**Class**: `B209Tests`

### Tests for IsBeClearStateTrigger (FIX-G-1 seam)

| # | Test Name | Input | Expected |
|---|-----------|-------|----------|
| 1 | `IsBeClearStateTrigger_Working_ReturnsTrue` | "PTT-BE-Stop-1", Working | `true` |
| 2 | `IsBeClearStateTrigger_Cancelled_ReturnsTrue` | "PTT-BE-Stop-1", Cancelled | `true` |
| 3 | `IsBeClearStateTrigger_Rejected_ReturnsTrue` | "PTT-BE-Stop-1", Rejected | `true` |
| 4 | `IsBeClearStateTrigger_Accepted_ReturnsTrue` | "PTT-BE-Stop-1", Accepted | `true` |
| 5 | `IsBeClearStateTrigger_Submitted_ReturnsFalse` | "PTT-BE-Stop-1", Submitted | `false` |
| 6 | `IsBeClearStateTrigger_WrongName_ReturnsFalse` | "PTT-QX-Stop-1", Working | `false` |

### Tests for IsBeSwapGtcSweepGuard (FIX-G-2 seam)

| # | Test Name | Input | Expected |
|---|-----------|-------|----------|
| 7 | `IsBeSwapGtcSweepGuard_PttBeName_SwapActive_ReturnsTrue` | "PTT-BE-Stop-1", `true` | `true` |
| 8 | `IsBeSwapGtcSweepGuard_PttBeName_SwapInactive_ReturnsFalse` | "PTT-BE-Stop-1", `false` | `false` |
| 9 | `IsBeSwapGtcSweepGuard_NonPttBeName_SwapActive_ReturnsFalse` | "PTT-QX-Stop-1", `true` | `false` |
| 10 | `IsBeSwapGtcSweepGuard_NullName_ReturnsFalse` | `null`, `true` | `false` |

### Test file skeleton
```csharp
// B209: xUnit tests for FIX-G-1 and FIX-G-2 seams.
// [assembly: InternalsVisibleTo] required for internal static access.
// JS-021: no lock. [Fact] only. ASCII-only.
using Xunit;
using NinjaTrader.Cbi;
using PropTraderTools;

public class B209Tests
{
    // FIX-G-1 seam tests
    [Fact] public void IsBeClearStateTrigger_Working_ReturnsTrue()
        => Assert.True(CopyEngine.IsBeClearStateTrigger("PTT-BE-Stop-1", OrderState.Working));

    [Fact] public void IsBeClearStateTrigger_Cancelled_ReturnsTrue()
        => Assert.True(CopyEngine.IsBeClearStateTrigger("PTT-BE-Stop-1", OrderState.Cancelled));

    [Fact] public void IsBeClearStateTrigger_Rejected_ReturnsTrue()
        => Assert.True(CopyEngine.IsBeClearStateTrigger("PTT-BE-Stop-1", OrderState.Rejected));

    [Fact] public void IsBeClearStateTrigger_Accepted_ReturnsTrue()
        => Assert.True(CopyEngine.IsBeClearStateTrigger("PTT-BE-Stop-1", OrderState.Accepted));

    [Fact] public void IsBeClearStateTrigger_Submitted_ReturnsFalse()
        => Assert.False(CopyEngine.IsBeClearStateTrigger("PTT-BE-Stop-1", OrderState.Submitted));

    [Fact] public void IsBeClearStateTrigger_WrongName_ReturnsFalse()
        => Assert.False(CopyEngine.IsBeClearStateTrigger("PTT-QX-Stop-1", OrderState.Working));

    // FIX-G-2 seam tests
    [Fact] public void IsBeSwapGtcSweepGuard_PttBeName_SwapActive_ReturnsTrue()
        => Assert.True(CopyEngine.IsBeSwapGtcSweepGuard("PTT-BE-Stop-1", true));

    [Fact] public void IsBeSwapGtcSweepGuard_PttBeName_SwapInactive_ReturnsFalse()
        => Assert.False(CopyEngine.IsBeSwapGtcSweepGuard("PTT-BE-Stop-1", false));

    [Fact] public void IsBeSwapGtcSweepGuard_NonPttBeName_SwapActive_ReturnsFalse()
        => Assert.False(CopyEngine.IsBeSwapGtcSweepGuard("PTT-QX-Stop-1", true));

    [Fact] public void IsBeSwapGtcSweepGuard_NullName_ReturnsFalse()
        => Assert.False(CopyEngine.IsBeSwapGtcSweepGuard(null, true));
}
```

---

## Ticket Decomposition

**LANE-SPLIT GATE RESULT: LANES-APPROVED** (gate Q1–Q4 analysis is correct — two logically independent fixes).

**Sequential execution required** — Ticket-1 and Ticket-2 share the `CopyEngine.cs` write-set.
Ticket-2 MUST NOT begin until Ticket-1 VERIFY_PASS is confirmed.
Execution order: Ticket-1 → VERIFY_PASS → Ticket-2.

### Ticket-1: FIX-G-1 — TryClearBeSwapOnStopWorking + IsBeClearStateTrigger Seam

**Spec requirement IDs**: B209-FIX-G-1  
**File**: `src/PropTraderTools/CopyEngine.cs`  
**Deliverables**:
1. MODIFY `TryClearBeSwapOnStopWorking` — replace single `Working` state check with isClearState
   boolean covering Working | Cancelled | Rejected | Accepted. Exact body as specified in Fix-G-1
   Design above.
2. ADD `IsBeClearStateTrigger(string orderName, OrderState state) : internal static bool` seam
   immediately after `TryClearBeSwapOnStopWorking`.

**SCAN checklist (SCAN-01..07)**:
- SCAN-01: No `lock(` in modified/added lines. `grep "lock(" src/PropTraderTools/CopyEngine.cs` = 0.
- SCAN-02: No `throw new` in modified/added lines.
- SCAN-03: No `return null` in modified/added lines. Both are `void` or `bool`.
- SCAN-04: No `async void` in modified/added lines.
- SCAN-05: No Unicode characters in string literals. All strings are ASCII.
- SCAN-06: CYC of `TryClearBeSwapOnStopWorking` <= 8 (target: spec=5, strict <= 7). Verify with Lizard.
- SCAN-07: `[assembly: InternalsVisibleTo]` exists for test project before seam is used.

**Post-edit mandatories**:
- `powershell -File .\scripts\ptt-sync.ps1` (hard-link sync)
- `powershell -File .\scripts\verify_critical_symbols.ps1` (guardian symbols)
- `git diff HEAD~1 HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^-    (private|internal|public)"` — no deletions other than the replaced `OrderState.Working` check line.

---

### Ticket-2: FIX-G-2 — TryCancelSingleStaleGtcExit + IsBeSwapGtcSweepGuard Seam + B209Tests.cs

**Spec requirement IDs**: B209-FIX-G-2  
**Files**:
- `src/PropTraderTools/CopyEngine.cs` (modify + add seam)
- `src/PropTraderTools/Tests/B209Tests.cs` (new file — all 10 tests)

**Deliverables**:
1. MODIFY `TryCancelSingleStaleGtcExit` — add the `PTT-BE-* && IsBeSwapInProgress` guard after the
   existing `IsStaleGtcExitOrder` early return. Exact body as specified in Fix-G-2 Design above.
2. ADD `IsBeSwapGtcSweepGuard(string orderName, bool beSwapInProgress) : internal static bool` seam
   after `IsBeClearStateTrigger` in CopyEngine.cs.
3. CREATE `src/PropTraderTools/Tests/B209Tests.cs` with all 10 [Fact] tests as specified in the
   Test Coverage Plan above.

**SCAN checklist (SCAN-01..07)**:
- SCAN-01: No `lock(` in modified/added lines.
- SCAN-02: No `throw new` in modified/added lines. Existing `catch` block unchanged.
- SCAN-03: No `return null`. All new methods are `void` or `bool`.
- SCAN-04: No `async void`.
- SCAN-05: All string literals ASCII. "[GTC-SWEEP]", "PTT-BE-", "PTT-BE-Stop" are ASCII.
- SCAN-06: CYC of `TryCancelSingleStaleGtcExit` <= 8 (strict max 7). Verify with Lizard.
- SCAN-07: All 10 [Fact] tests present in B209Tests.cs. No NUnit/MSTest imports.

**Post-edit mandatories**:
- `powershell -File .\scripts\ptt-sync.ps1` (hard-link sync)
- `powershell -File .\scripts\verify_critical_symbols.ps1` (guardian symbols)
- `git diff HEAD~1 HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^-    (private|internal|public)"` — no deletions.

---

## Jane Street Compliance Matrix

| Rule | Description | FIX-G-1 | FIX-G-2 | Seams | Tests |
|------|-------------|---------|---------|-------|-------|
| JS-021 | No `lock()` — use ConcurrentDictionary | PASS | PASS | PASS | N/A |
| JS-001 | No `throw` in hot paths | PASS (no throw) | PASS (catch inherited) | PASS | N/A |
| JS-002 | No `return null` | PASS (void) | PASS (void) | PASS (bool) | N/A |
| JS-033 | No `async void` | PASS (sync) | PASS (sync) | PASS (sync) | N/A |
| CYC<=8 | Cyclomatic complexity | PASS (<=7) | PASS (<=7) | PASS (<=5) | N/A |
| ASCII | All string literals ASCII-only | PASS | PASS | PASS | PASS |
| [Fact] | xUnit only — no NUnit/MSTest | N/A | N/A | N/A | PASS |
| JS-015 | StringComparison.Ordinal on all StartsWith | PASS | PASS | PASS | N/A |

---

## NT8 API Surface Verification

All claims grounded in live CopyEngine.cs source (read lines 2145-2200, 3120-3169):

| API | Usage | Confirmed |
|-----|-------|-----------|
| `OrderState.Working` | Existing guard and new isClearState | Line 3164 (before), line 2148 |
| `OrderState.Cancelled` | New isClearState branch | Valid NT8 enum value, used in other methods |
| `OrderState.Rejected` | New isClearState branch | Valid NT8 enum value |
| `OrderState.Accepted` | New isClearState branch | Valid NT8 enum value (SIM) |
| `Order.Name` | String property for prefix checks | Line 2147, 3161 |
| `Order.OrderState` | Enum property | Line 2148, 3164 |
| `Order.Account?.Name` | Nullable Account navigation | Line 3166 |
| `Account.Cancel(Order[])` | NT8 Account API | Line 2182 |
| `IsBeSwapInProgress(string)` | Existing internal method | Line 3138 |
| `SetBeSwapInProgress(string, bool)` | Existing internal method | Line 3126 |
| `_beSwapInProgress` | `ConcurrentDictionary<string,bool>` | Line 3129, 3141 (TryGetValue) |

**KEY NT8 FACTS (mandatory)**:
- `AtmStrategyChangeStopTarget()` — StrategyBase-only. NOT used in this block. N/A.
- `AtmStrategyCreate()` — StrategyBase-only. NOT used in this block. N/A.
- `Account.Cancel()` — AddOnBase available. CONFIRMED used at line 2182. Correct pattern.

---

## Threading Model

Both methods are called from NT8's `OnOrderUpdate` callback (order event thread):
- `TryClearBeSwapOnStopWorking`: reads/writes `_beSwapInProgress` (ConcurrentDictionary — lock-free).
  No UI interaction. No `Dispatcher.InvokeAsync` needed.
- `TryCancelSingleStaleGtcExit`: reads `_beSwapInProgress` (ConcurrentDictionary read — thread-safe).
  `acc.Cancel()` follows existing NT8 event-thread pattern (unchanged from B197).
- Both testable seams are `internal static` pure functions — no state, no threading concern.

No new `Dispatcher.InvokeAsync` required. No new `ConcurrentQueue` ownership. Threading model: clean.

---

## Data Flow Summary

```
OnOrderUpdate(e)
  └─ TryClearBeSwapOnStopWorking(e)          [FIX-G-1]
       ├─ name guard: PTT-BE-Stop*
       ├─ isClearState: Working|Cancelled|Rejected|Accepted  <-- B209 new
       ├─ IsBeSwapInProgress(acc.Name)
       └─ SetBeSwapInProgress(acc.Name, false)
  └─ TryNakedDetect(...)
       └─ IsBeSwapInProgress(acc.Name) = false  --> PTT-Flatten fires normally

OnOrderUpdate(e) [entry fill]
  └─ TryCancelStaleGtcExitOnEntry(e)
       └─ foreach acc.Orders -> TryCancelSingleStaleGtcExit(acc, o)   [FIX-G-2]
            ├─ IsStaleGtcExitOrder(o)           -- working PTT-BE-* or PTT-QX-*
            ├─ IsBeSwapGtcSweepGuard guard       <-- B209 new: skip PTT-BE-* if swap in progress
            └─ acc.Cancel(o)                     -- only stale non-swap PTT-BE-* and all PTT-QX-*
```

---

## ptt-sync and Guardian Symbol Requirements

**After every CopyEngine.cs edit** (Ticket-1 AND Ticket-2):
```powershell
powershell -File .\scripts\ptt-sync.ps1
```
Re-synchronises NinjaTrader hard links. MANDATORY per AGENTS.md "Hard-Link Integrity" pillar.

```powershell
powershell -File .\scripts\verify_critical_symbols.ps1
```
Verifies guardian symbols are present. MANDATORY per AGENTS.md Ph4b requirements.

---

## Ph4b Deletion Audit Requirements

**Ticket-1 deletion audit**:
```powershell
git diff HEAD~1 HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^-    (private|internal|public)"
```
Expected: Only the removed `if (e.Order.OrderState != OrderState.Working) return;` line will appear
as a deletion. This line has no authorising DW-card requirement (it is the bug-bearing line being
replaced, not a symbol deletion).

**Ticket-2 deletion audit**:
Same command. Expected: zero method-signature deletions. The `TryCancelSingleStaleGtcExit` signature
is unchanged — only the body gains a guard.

**Single-hunk ban**: Engineer must use `apply_diff` or `search_and_replace` for surgical edits.
`(git show HEAD -- CopyEngine.cs | Select-String "^@@").Count -eq 1` must be `false` after each commit.

---

## Spec Update Requirements (Ph5)

After VERIFY_PASS, perform ALL of the following updates:

### A. `specs/002-trade-copier-spec.html` — B208 block: mark fixes APPLIED
In the B208 block of `specs/002-trade-copier-spec.html`, mark FIX-G-1 and FIX-G-2 as APPLIED.
Include the ptt-sync result (output of `powershell -File .\scripts\ptt-sync.ps1`) in the annotation.

### B. `specs/002-trade-copier-spec.html` — B208 card title update
Change the B208 card title from:
  `"FIX-G DESIGNED — AWAITING DIRECTOR APPROVAL"`
to:
  `"FIX-G APPLIED — AWAITING SIM ROUND 7"`

### C. `specs/002-trade-copier-spec.html` — Bug table row for B208
Update the status column of the B208 bug table row in `specs/002-trade-copier-spec.html`
to reflect that both FIX-G-1 and FIX-G-2 have been applied.

### D. `docs/brain/B209/06-deferred-backlog.md`
Update with: "CLOSED: permanent _beSwapInProgress lock on OCO cancel path (FIX-G-1).
CLOSED: premature GTC sweep of live PTT-BE-Stop-N (FIX-G-2)."
Confirm no open B208/B209 deferred items remain related to BE-swap flag management.

### E. NakedPositionDetector cross-check
If `NakedPositionDetector` has other callers that check `IsBeSwapInProgress`, verify they
are not affected by the new clear-on-Cancelled behaviour (should be a no-op improvement).

---

## Summary

B209 delivers two targeted surgical fixes to `CopyEngine.cs`:

1. **FIX-G-1** closes the permanent `_beSwapInProgress` flag leak by extending
   `TryClearBeSwapOnStopWorking` to clear on all terminal stop states (not just `Working`).
   This protects `NakedPositionDetector` from suppression on subsequent trades.

2. **FIX-G-2** closes the live-stop cancellation risk by adding an `IsBeSwapInProgress` guard
   inside `TryCancelSingleStaleGtcExit` that protects `PTT-BE-*` orders during the active swap
   window from premature GTC sweep.

Both fixes are lock-free, CYC <= 8, ASCII-only, and backed by 10 xUnit [Fact] tests via
`internal static` testable seams. Sequential execution: Ticket-1 first, Ticket-2 after Ticket-1 VERIFY_PASS.
