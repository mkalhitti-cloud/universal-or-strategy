# DW-LB-FL-01 Architecture Plan

**Defect ID**: DW-LB-FL-01  
**Severity**: P1  
**Status**: REVIEW_PENDING  
**Author**: ptt-architect (Phase 1)  
**Date**: 2026-09-07  
**Output file**: `docs/brain/DW-LB-FL-01/02-architecture-plan.md`

---

## Rules Catalog Gate Result

**GATE RESULT: PASS**

P0 rules confirmed no-violation for this fix:

| Rule | Check | Result |
|------|-------|--------|
| JS-021 | No lock() in new/modified code | PASS — guard uses static method with .ToList() snapshot only |
| JS-001 | No throw in hot path | PASS — guard returns bool, no exceptions |
| JS-002 | No return null | PASS — all new methods return bool or void |
| JS-033 | No async void | PASS — synchronous methods only |
| JS-036/037 | No new heap alloc in hot path | PASS — .ToList() snapshot follows existing pattern in class |

---

## 1. LANE-SPLIT GATE RESULT

**LANE-SPLIT GATE RESULT: SINGLE-PIPELINE**

Q1. Are both code paths within 50 lines of each other in CopyEngine.cs?
> NOT APPLICABLE. Investigation confirmed only ONE root cause (Primary hypothesis). The Alternative hypothesis (TryDispatchLeaderFlat path) is ruled out — see Root Cause Analysis. There is one fix with two methods added, both on the same call chain. No lane split is warranted.

Q2. Does fix B depend on the final design of fix A?
> NOT APPLICABLE. Single fix path only.

Q3. Does each fix have standalone value if the other is blocked?
> NOT APPLICABLE. Single fix path only.

Q4. Does each fix have an independent SIM verification path?
> NOT APPLICABLE. Single fix path, verified by one SIM gate: Clone mode, BE ALL cycle, new entry — confirm no PTT-Flatten fires on followers during bracket arming.

**Decision**: Single pipeline. One ticket. One PR.

---

## 2. Root Cause Analysis

### Ruling Out the Alternative Hypothesis

`TryDispatchLeaderFlat` (L4693) is **NOT the root cause**.

Evidence:
- `IsNonFlatDispatchName` (L2379) returns `true` for ATM bracket names (`Stop1..Stop9`, `Target1..Target9`) via `IsAtmBracketName`. Guard (2.5/2.6) blocks early — no follower flatten dispatched. This is the DW-B94 protection.
- `IsNativeExitName("Close")` returns `true`. If leader is flat, guard (3.5) `IsNativeExitOnFlatLeader` (DW-LB-FL-02) blocks. If leader has position, guard (3) `hasOpenPosition(account, instrument)` blocks.
- Therefore `TryDispatchLeaderFlat` cannot produce a false PTT-Flatten during bracket arming. It is protected by two independent guards already in place.

### Confirmed Root Cause: TryNakedDetect Path

**Trigger event sequence** (confirmed from SIM logs and code trace):

```
BE ALL cycle fires:
  RelayBe(BeEventArgs e) [L678]
    └── foreach follower account (Sim102/103/104):
          CancelQxBrackets(acc, e.Instrument)  [L691]
            └── CommitQxCancelBatch → CommitStaleCancelBatch
                  └── acc.Cancel([Stop1, Stop2, Target1, Target2])
                      [cancels all four ATM brackets as one batch]
```

NT8 processes each cancel individually, firing `OnOrderUpdate` for each bracket in sequence:

```
OnOrderUpdate (Stop1:Cancelled)  -- Sim102 still has position. acc.Orders: Stop2/Target1/Target2 still Working.
  └── TryNakedDetect(e)
        └── IsFollowerAccount(Sim102) = true
            NakedPositionDetector(Sim102)
              └── HasNakedPosition: position exists, Stop2+Target1+Target2 Working -> false
              [NO DISPATCH -- correct]

OnOrderUpdate (Stop2:Cancelled)  -- similar: Target1/Target2 still Working -> false
OnOrderUpdate (Target1:Cancelled) -- similar: Target2 still Working -> false

OnOrderUpdate (Target2:Cancelled) [LAST cancel event]
  └── TryNakedDetect(e)
        └── IsFollowerAccount(Sim102) = true
            NakedPositionDetector(Sim102)
              └── HasNakedPosition:
                    foreach Positions: qty > 0 -> hasPosition = true
                    IsNakedConditionMet: foreach acc.Orders:
                      Stop1/Stop2/Target1/Target2 are ALL Cancelled now
                      -> hasStop = false, hasTarget = false
                      -> returns true (!!!)
              <- HasNakedPosition = true
              Debounce: last=0 (or >500ms ago) -> passes
              Dispatcher.InvokeAsync(() => FlattenOneAccount(Sim102, instr))
                         ^^^ CALLBACK QUEUED ON UI THREAD FIFO
```

Between the `Dispatcher.InvokeAsync` enqueue and the UI thread executing it:

```
New entry fills on leader.
DispatchCopy -> SendCopy -> PTT-Copy:Submitted -> PTT-Copy:Working -> PTT-Copy:Filled
  NT8 StartAtmStrategy arms new brackets:
    Stop1:Submitted -> Stop1:Accepted -> Stop1:Working
    Stop2:Submitted -> Stop2:Accepted -> Stop2:Working
    Target1:Submitted -> Target1:Accepted -> Target1:Working
    Target2:Submitted -> Target2:Accepted -> Target2:Working
```

UI thread processes queued callback:

```
FlattenOneAccount(Sim102, instr)  [UI thread]
  └── IsAccountFlattenable(Sim102, instr)
        HasInflightFlatten: no PTT-Flatten in Submitted/Accepted/Working -> false
                            [prior BE cycle's PTT-Flatten is ALREADY Filled]
        FindPosition: qty > 0 (new entry just filled) -> not null/zero
        -> returns TRUE  (BUG: should return false, bracket arm in progress)
      CancelAllAccountOrders(Sim102, instr)
        [cancels the newly-armed Stop1/Stop2/Target1/Target2]
      SubmitMarketFlattenOrder -> DoFlattenOrder
        acc.CreateOrder(..., "PTT-Flatten", ...)
        acc.Submit([order])
        [PTT-Flatten:Submitted -> Working -> Filled]
        [NEW ENTRY POSITION CLOSED -- ORPHANED BRACKETS REMAIN]
```

### Why It Fires After BE ALL and Not on the First Entry

On **first entry** (clean session start):
- No prior CancelQxBrackets bracket cancel storm occurs before entry fill.
- When entry fills, ATM brackets arm immediately from a clean state.
- `TryNakedDetect` fires on entry fill itself (OrderState.Filled), but `HasNakedPosition` = false because new brackets are already in Submitted/Accepted state (NT8 fires them synchronously during `StartAtmStrategy` before the cancel acks for any prior orders).
- Debounce may also prevent repeat.

After **BE ALL cycle**:
- CancelQxBrackets fires, cancelling 4 brackets per follower.
- The LAST cancel ack creates a window where `HasNakedPosition` = true (all brackets cancelled, position still live).
- `Dispatcher.InvokeAsync` callback is queued.
- New entry fills and new brackets arm **before** the UI thread processes the callback.
- By the time `IsAccountFlattenable` runs on the UI thread, the new brackets are Working — but `IsAccountFlattenable` has no check for this scenario.

### Exact Lines Where PTT-Flatten Is Wrongly Submitted

| Line | Code | Role |
|------|------|------|
| L7174 | `if (!HasNakedPosition(acct)) return;` | Returns false prematurely after last bracket cancel |
| L7190-7192 | `Dispatcher.InvokeAsync(() => FlattenOneAccount(acct, instr))` | Queues callback during cancel storm |
| L5185 | `if (!IsAccountFlattenable(acc, instrument)) return;` | Does NOT return false (ATM bracket guard missing) |
| L5267-5282 | `DoFlattenOrder(...)` | **PTT-Flatten wrongly submitted here** |

---

## 3. Fix Design

### Guard Condition

Add a check: at the moment `FlattenOneAccount` would be called from the `NakedPositionDetector` path, detect whether ATM bracket orders are in active state (Working / Submitted / Accepted / TriggerPending) on the target account/instrument. If yes, the account is in bracket-arm state — do not flatten.

### Strategy: Targeted Guard in NakedPositionDetector Dispatch Path

The guard is placed in the `Dispatcher.InvokeAsync` callback within `NakedPositionDetector`. This is the **only** call site that has the timing race. All other paths that call `FlattenOneAccount` (via `TryDispatchLeaderFlat` / `FlattenFollower`) are intentional leader-initiated flattens and must NOT be blocked.

**Pattern**: Extract the lambda body to a new instance method `FlattenIfNotArming(Account acct, Instrument instr)`. This keeps `NakedPositionDetector` CYC unchanged and makes the guard independently testable.

### Methods to Modify and Add

#### NEW METHOD: `HasArmingAtmBrackets`

```csharp
// DW-LB-FL-01: returns true if any ATM bracket order is in an active arming state.
// Active states: Working, Submitted, Accepted, TriggerPending.
// Reuses IsAtmBracketName (Stop1..Stop9 / Target1..Target9) for bracket identification.
// Guard purpose: when NakedPositionDetector queues a FlattenOneAccount callback via
// Dispatcher.InvokeAsync, bracket arming may complete before the UI thread runs the callback.
// If ATM brackets are active at callback time, the account has valid protection -- do not flatten.
// CYC=5: base(1)+foreach(1)+instr check(2)+state check(3)+IsAtmBracketName(4)=CYC<=5.
// JS-021: no lock. acc.Orders.ToList() snapshot prevents InvalidOperationException (same pattern
// as HasInflightFlatten L5222). JS-001: no throw. JS-002: returns bool. ASCII-only. static.
private static bool HasArmingAtmBrackets(Account acc, Instrument instr)
{
    foreach (var o in acc.Orders.ToList())
    {
        if (o.Instrument?.FullName != instr.FullName)
            continue;
        bool stateActive =
            o.OrderState == OrderState.Working
            || o.OrderState == OrderState.Submitted
            || o.OrderState == OrderState.Accepted
            || o.OrderState == OrderState.TriggerPending;
        if (!stateActive)
            continue;
        if (IsAtmBracketName(o.Name))
            return true;
    }
    return false;
}
```

**McCabe estimate**: 5 branches (base + foreach + instr skip + stateActive compound + IsAtmBracketName check). CYC=5. **PASS <= 8**.

#### NEW METHOD: `FlattenIfNotArming`

```csharp
// DW-LB-FL-01: dispatch helper used by NakedPositionDetector Dispatcher.InvokeAsync lambda.
// Guards the FlattenOneAccount call with a bracket-arming check.
// If ATM brackets are Working/Submitted/Accepted/TriggerPending on acc for instr, skip flatten.
// This prevents the false PTT-Flatten that fires after a BE ALL cancel storm when the
// Dispatcher.InvokeAsync callback runs after the new entry's brackets have started arming.
// Does NOT affect TryDispatchLeaderFlat -> FlattenFollower -> FlattenOneAccount path
// (intentional leader-initiated flatten -- DW-B65-01 bypass preserved).
// CYC=2: base(1)+HasArmingAtmBrackets branch(2). JS-021: no lock. JS-001: no throw.
// JS-002: void. ASCII-only. Instance method (calls FlattenOneAccount + StatusUpdate).
private void FlattenIfNotArming(Account acct, Instrument instr)
{
    if (HasArmingAtmBrackets(acct, instr))
    {
        StatusUpdate?.Invoke(acct.Name + ": flat-guard: bracket-arm skip");
        return;
    }
    FlattenOneAccount(acct, instr);
}
```

**McCabe estimate**: 2 branches (base + HasArmingAtmBrackets check). CYC=2. **PASS <= 8**.

#### MODIFIED METHOD: `NakedPositionDetector`

Change **one line** only — replace the direct `FlattenOneAccount` call with `FlattenIfNotArming`:

```csharp
// BEFORE (L7190-7192):
System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
    FlattenOneAccount(acct, instr)
);

// AFTER (DW-LB-FL-01):
System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
    FlattenIfNotArming(acct, instr)
);
```

**CYC impact on NakedPositionDetector**: Zero. Lambda body change does not add a branch to the parent method's McCabe count. Current CYC<=6 unchanged.

### Rationale: Why This Guard Prevents the False Positive

At the moment the `Dispatcher.InvokeAsync` callback runs:
- The new entry has filled (Dispatcher FIFO processes entries after the entry fill events complete).
- NT8 `StartAtmStrategy` has armed new brackets — they are in Working/Accepted/Submitted/TriggerPending state.
- `HasArmingAtmBrackets` returns `true`.
- `FlattenIfNotArming` returns early. PTT-Flatten is never submitted.

After the new brackets eventually fill (stop hit, target hit), those orders reach terminal state (Filled/Cancelled). If the position becomes legitimately naked at that point (e.g., partial fill with no remaining brackets), the NEXT event triggers `TryNakedDetect` again — debounce passes — `HasArmingAtmBrackets` returns false (all brackets terminal) — `FlattenOneAccount` proceeds correctly.

### Rationale: Why Legitimate Flatten Is Not Blocked

Scenario A — **Intentional leader flatten (DW-B65-01 path)**:
- Goes through `TryDispatchLeaderFlat` → `FlattenFollower` → `flattenOne(FlattenOneAccount)`.
- Does NOT go through `NakedPositionDetector` → `FlattenIfNotArming`.
- Guard `HasArmingAtmBrackets` is NOT evaluated. Intentional flatten proceeds. **DW-B65-01 preserved.**

Scenario B — **Legitimate naked position (orphaned — no brackets, position open)**:
- `TryNakedDetect` fires on a terminal order event.
- `HasNakedPosition` = true (position exists, no Working/Submitted Stop or Target in `IsNakedConditionMet`).
- `FlattenIfNotArming` callback runs on UI thread.
- `HasArmingAtmBrackets` = false (no Working/Submitted/Accepted/TriggerPending ATM brackets).
- `FlattenOneAccount` proceeds. **Correct.**

Scenario C — **Existing PTT-Flatten in-flight guard**:
- `IsAccountFlattenable` (L5202) checks `HasInflightFlatten` first.
- If a PTT-Flatten is already Submitted/Accepted/Working, `IsAccountFlattenable` returns false.
- This guard is UPSTREAM of `FlattenIfNotArming` (which only guards the NakedPositionDetector call to `FlattenOneAccount`).
- Both guards remain active and independent. **DW-LB-FL-02 preserved (unrelated path).**

### Confirm No Interference with Preserved Guards

| Guard | Location | Effect | Status |
|-------|----------|--------|--------|
| DW-B65-01 bypass (IsNativeExitName) | TryDispatchLeaderFlat L4712 | Leader native close propagates to followers | **PRESERVED** — different code path |
| DW-LB-FL-02 (IsNativeExitOnFlatLeader) | TryDispatchLeaderFlat L4710 | Native exit on already-flat leader does not flatten followers | **PRESERVED** — different code path |
| PTT-Flatten in-flight guard (HasInflightFlatten) | IsAccountFlattenable L5204 | Prevents double-submit of PTT-Flatten | **PRESERVED** — unchanged method |
| DW-B94 (IsNonFlatDispatchName) | TryDispatchLeaderFlat L4708 | ATM bracket cancel names blocked from follower flatten dispatch | **PRESERVED** — unchanged method |

---

## 4. Test Design

All tests are xUnit `[Fact]` unit tests. No SIM required. File: `tests/PropTraderTools.Tests/Core/CopyEngineTests.cs` (add to existing test class).

| # | Test Name | What It Asserts |
|---|-----------|-----------------|
| T1 | `HasArmingAtmBrackets_ReturnsFalse_WhenNoOrders` | Account with empty Orders returns false |
| T2 | `HasArmingAtmBrackets_ReturnsFalse_WhenAllBracketsAreCancelled` | Stop1+Target1 in Cancelled state returns false |
| T3 | `HasArmingAtmBrackets_ReturnsTrue_WhenStop1IsWorking` | Stop1 in Working state, correct instrument returns true |
| T4 | `HasArmingAtmBrackets_ReturnsTrue_WhenTarget2IsAccepted` | Target2 in Accepted state, correct instrument returns true |
| T5 | `HasArmingAtmBrackets_ReturnsTrue_WhenStop3IsTriggerPending` | Stop3 in TriggerPending state returns true |
| T6 | `HasArmingAtmBrackets_ReturnsFalse_WhenAtmBracketsForDifferentInstrument` | Stop1 Working but wrong instrument.FullName returns false |
| T7 | `HasArmingAtmBrackets_ReturnsFalse_WhenOnlyNonAtmOrdersAreWorking` | "PTT-Copy" in Working state (not an ATM bracket name) returns false |
| T8 | `FlattenIfNotArming_CallsFlattenOne_WhenNoArmingBrackets` | No active ATM brackets → flattenOne delegate is called |
| T9 | `FlattenIfNotArming_SkipsFlattenOne_WhenArmingBracketsPresent` | Active ATM brackets → flattenOne delegate is NOT called |
| T10 | `IsAccountFlattenable_ExistingInflightGuardStillWorks_Regression` | PTT-Flatten in Working state → IsAccountFlattenable returns false (unchanged) |

**Test seam note**: `FlattenIfNotArming` calls instance methods `HasArmingAtmBrackets` (which is static and directly testable) and `FlattenOneAccount` (instance). Tests T8/T9 can be covered via a testable overload of `FlattenIfNotArming` with injected delegates, OR by testing `HasArmingAtmBrackets` directly (T1-T7) and asserting `FlattenOneAccount` side effects (T8/T9 via mock account). Use the `InternalsVisibleTo("PropTraderTools.Tests")` declaration already present at CopyEngine.cs L46.

---

## 5. Spec Requirement Traceability

| Symptom / Requirement | Fix Element | Covers |
|-----------------------|-------------|--------|
| PTT-Flatten fires during bracket arming after BE ALL | `HasArmingAtmBrackets` + `FlattenIfNotArming` guard | Root cause: `NakedPositionDetector` Dispatcher callback runs after new brackets arm |
| PTT-Flatten fires on EVERY new Clone entry after BE ALL cycle | 500ms debounce resets per cycle; fix prevents the callback from proceeding | No second flatten possible — new brackets are Working during the entire arming window |
| Orphaned Working brackets (Stop1-3/Target1-3) after flatten | Not directly fixed — prevented by blocking the flatten entirely | When flatten is blocked, brackets remain and close position correctly |
| DW-B65-01 bypass must be preserved | Fix is in `NakedPositionDetector` dispatch path only | `TryDispatchLeaderFlat` path unaffected |
| DW-LB-FL-02 guard must be preserved | Guard is at `TryDispatchLeaderFlat` L4710, unrelated to this path | Unchanged |
| CYC <= 8 on all modified methods | `HasArmingAtmBrackets` CYC=5, `FlattenIfNotArming` CYC=2, `NakedPositionDetector` unchanged | All PASS |
| No lock() | `.ToList()` snapshot pattern, no new shared state | PASS |

---

## 6. Risks and Deferred Items

### DW-LB-FL-01-DEBOUNCE-OVERFLOW (Defer)

**File**: `CopyEngine.cs` L7178  
**Code**: `long now = (long)(int)Environment.TickCount;`  
**Issue**: `Environment.TickCount` is `int` (ms since boot, wraps at ~24.9 days to negative). The cast `(long)(int)` preserves the sign. When `TickCount` wraps to a large negative value, `now - last` may compute incorrectly, potentially allowing the debounce to fire more frequently than intended (or blocking valid triggers).  
**Risk level**: Low. The 500ms window is far shorter than the 24.9-day wrap period. The pathological case requires a wrap to occur within a 500ms window of a prior trigger.  
**Deferred**: Not in scope for DW-LB-FL-01 fix. Fix separately as `DW-LB-FL-01-DEBOUNCE-OVERFLOW`.

### DW-LB-FL-01-STATE-COVERAGE (Defer — SIM gate)

**Question**: Does `HasArmingAtmBrackets` need to include `OrderState.Initialized` in addition to Working/Submitted/Accepted/TriggerPending?  
**Analysis**: NT8 ATM bracket arming sequence: `CreateOrder` (Initialized) → `Submit` (Submitted) → NT8 acknowledges (Accepted) → exchange Working → TriggerPending (conditional triggers). `Initialized` is the state immediately after `CreateOrder` before `Submit` is called. In practice, NT8 calls `CreateOrder` + `Submit` within the same synchronous call (on the UI thread). By the time `Dispatcher.InvokeAsync` runs `FlattenIfNotArming`, brackets will be at minimum Submitted. However, if NT8's ATM uses a deferred submit pattern, `Initialized` could be present.  
**Risk level**: Negligible. Confirm during SIM gate validation. If SIM shows any remaining false PTT-Flatten, add `OrderState.Initialized` to `HasArmingAtmBrackets` state check.

### Out of Scope

- `DW-LB-SFB-01`: Explicitly excluded from this session per SCOPE LOCK directive.
- Multi-account ordering: the fix applies independently to each account — no cross-account state needed.

---

## Summary

**Fix**: Add `HasArmingAtmBrackets` (static, CYC=5) and `FlattenIfNotArming` (instance, CYC=2) to `CopyEngine.cs`. Modify `NakedPositionDetector` (1-line change: `FlattenOneAccount` → `FlattenIfNotArming` in Dispatcher lambda).

**Files modified**: `src/PropTraderTools/CopyEngine.cs` (1 file only).

**Tests added**: 10 `[Fact]` tests in existing `tests/PropTraderTools.Tests/Core/CopyEngineTests.cs`.

**Preserved invariants**: DW-B65-01, DW-LB-FL-02, existing PTT-Flatten in-flight guard (HasInflightFlatten), DW-B94.

**P0 violations introduced**: None.
