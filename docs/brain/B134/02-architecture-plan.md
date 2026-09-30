# B134 Architecture Plan

**Epic**: B134 -- Two-Ticket: DW-B144 (Submitted-state gap) + DW-B145 (wrong bracket index)
**Status**: REVIEW_PENDING
**Phase**: 1 (Architecture)
**Author**: ptt-architect

---

## A. Epic Summary

B134 fixes two related defects in `FindFollowerBracketOrder` that caused bracket drag sync to fail silently during TP4 testing.

| DW ID | Description | Symptom | Root Cause |
|-------|-------------|---------|------------|
| DW-B144 | Submitted-state gap | fo=null; no sync at all | State filter excludes Submitted |
| DW-B145 | Wrong bracket returned | fo=Target1 when Target3 expected | Signal-only match finds wrong bracket when filter becomes less restrictive |

Both defects are in `CopyEngine.cs`. The fix is **two surgical changes** to a single method region. No new files in `src/`. One new test file.

---

## B. Source Investigation Results

### B.1 SyncFollowerBracket (L2179-2210) -- Call Site

```csharp
private void SyncFollowerBracket(
    Account acc, Order leaderOrder, bool isStop,
    double newPrice, double tickSize)
{
    var fo = FindFollowerBracketOrder(
        acc, leaderOrder.FromEntrySignal, isStop, leaderOrder.Name); // leaderName="Target3"
    TryLogSFBTrace(acc, leaderOrder, isStop, fo);
    if (fo == null)  // (1) returns early -- DW-B144 symptom: always null during TP4
        return;
    // ... cancel+resubmit via SyncAtmFollowerTarget / SyncAtmFollowerBracket
```

Key observations:
- Passes `leaderOrder.FromEntrySignal` (non-null for ATM orders) AND `leaderOrder.Name` ("Target3") to `FindFollowerBracketOrder`.
- When `fo == null`, sync silently returns -- this is the DW-B144 observable failure.

### B.2 FindFollowerBracketOrder (L2538-2566) -- Primary Fix Target

```csharp
// CYC=6. DW-B143: list-injection overload for xUnit testing.
private Order? FindFollowerBracketOrder(
    IEnumerable<Order> orders,
    string? fromEntrySignalName,
    bool isStop,
    string? leaderName = null)
{
    foreach (var order in orders)                                                    // (1) loop
    {
        if (!SignalOrNameMatches(order, fromEntrySignalName, leaderName))            // (1) guard
            continue;
        if (order.OrderState != OrderState.Working                                  // (2) state filter
            && order.OrderState != OrderState.Accepted)
            continue;
        if (isStop)                                                                  // (1) stop/tgt branch
        {
            if (order.OrderType == OrderType.StopMarket
                || order.OrderType == OrderType.StopLimit)                          // (1) type match
                return order;
        }
        else
        {
            if (order.OrderType == OrderType.Limit && !IsStopLeg(order))
                return order;
        }
    }
    return null;
}
```

**Current state filter**: `Working || Accepted` only.
**Comment at L2527**: "Submitted is intentionally excluded: NT8 cancel on Submitted is unreliable."
**This comment is the B143 design decision** -- it was overcautious (see NT8 Evidence, section B.5).

**Comment at L2535**: `CYC=6` -- counted as: base(1) + foreach(1) + SignalOrNameMatches guard(1) + state filter Working!=&&Accepted!=(2 conditions = 2 in comment's counting) + isStop(1) + OrderType match(1) = 7 branches. Comment-documented as 6.

### B.3 SignalOrNameMatches (L2511-2518) -- Selection Logic

```csharp
internal static bool SignalOrNameMatches(
    Order order, string? signalName, string? leaderName)
{
    if (signalName != null && order.FromEntrySignal == signalName)  // (1) primary: signal equality
        return true;
    if (leaderName == null)                                          // (2) no fallback
        return false;
    return order.Name == leaderName;                                 // (3) exact name fallback
}
```

**Critical behavior**: When `signalName` is non-null (all ATM bracket orders share the same `FromEntrySignal`), path (1) fires and returns `true` for ALL brackets (Target1, Target2, Target3, Stop1, Stop2, etc.). The `leaderName` exact-name-check at path (3) is **never reached** in the ATM-bracket context.

This means after Ticket 1 (adding Submitted to state filter), all three targets pass -- the first one encountered in iteration wins. If iteration returns Target1 before Target3, DW-B145 persists.

**Ticket 2 fix cannot be in SignalOrNameMatches** (changing it risks breaking existing callers that rely on signal-only match semantics). The fix must be in `FindFollowerBracketOrder` itself.

### B.4 SyncAtmFollowerTarget Phase C + DeriveLeaderBracketIndex (L2312-2423) -- Context Only

- `DeriveLeaderBracketIndex("Target3")` correctly extracts `3` via trailing-digit parse (L2388-2403).
- `SyncAtmFollowerTarget` is called by `SyncFollowerBracket` AFTER `FindFollowerBracketOrder` returns `fo`.
- Phase C: uses `DeriveLeaderBracketIndex(leaderOrder)` + `FindLeaderStopPrice` for stop replacement.
- **No changes to these methods in B134**. They receive the correctly selected `fo` from Ticket 2.

### B.5 NT8 Cancel-on-Submitted Safety Evidence

**From NT8_FULL_REFERENCE.md (Account.Cancel section, L2408-2452)**:
> Cancels specified Order object(s). No state restriction documented.

**OrderState taxonomy (NT8_FULL_REFERENCE.md L3357-3374)**:
- `OrderState.Submitted` = "Order is submitted to the broker" -- NOT a terminal state.
- Terminal states (per `IsTerminalState()`): Cancelled, Rejected, Filled, Unknown.
- Non-terminal (live) states include: Initialized, Submitted, Accepted, TriggerPending, Working, ChangePending, ChangeSubmitted.

**NT8_ADDON_KNOWLEDGE.md (L222)**:
> `acc.Cancel(Order[])   // Cancel working order`
> `order.OrderState      // Submitted / Working / Accepted / Filled / Cancelled`

The comment lists Submitted as a live state alongside Working and Accepted.

**NT8 Error code exists**: `ErrorCode.UnableToCancelOrder` -- cancel failures are reported via error callback, not exceptions. Combined with existing try/catch wrapping all `acc.Cancel()` calls in `SyncAtmFollowerTarget` and `SyncAtmFollowerBracket`, a failed cancel on Submitted is absorbed gracefully.

**NT8 Ruling**: `acc.Cancel()` on `OrderState.Submitted` is **safe with try/catch** (already present). The B143 comment ("unreliable") was an empirical observation from that session, not a documented NT8 restriction. Cancel may fail with `UnableToCancelOrder` error -- the try/catch absorbs it, and Block B (CreateOrder + Submit) still runs, creating the replacement order.

---

## C. DW-B144 Architecture Decision

### Decision: **Option A -- Add Submitted to state filter**

**Rationale**:
- TP4 evidence: all follower brackets are in `OrderState.Submitted` at drag time.
- NT8 docs: no restriction on `acc.Cancel()` for Submitted orders.
- Existing code: all `acc.Cancel()` calls are already wrapped in try/catch.
- Option B (ConcurrentQueue retry): adds a new field, new method, event-driven retry logic -- 5x more code for zero additional safety.
- Option A is the minimal, JS-compliant fix.

**Exact code change -- Ticket 1**:

**File**: `src/PropTraderTools/CopyEngine.cs`
**Line**: 2549 (the state-filter `continue` in `FindFollowerBracketOrder` list overload)

Before:
```csharp
if (order.OrderState != OrderState.Working && order.OrderState != OrderState.Accepted) // (2) branches
    continue;
```

After:
```csharp
// B134 DW-B144: add Submitted -- TP4 brackets are Submitted at drag time.
// NT8 Cancel on Submitted: safe with try/catch (no doc restriction; UnableToCancelOrder error absorbed).
if (order.OrderState != OrderState.Working
    && order.OrderState != OrderState.Accepted
    && order.OrderState != OrderState.Submitted) // (3) branches -- B134 DW-B144
    continue;
```

Update the comment on the overload signature (L2535):
```csharp
// CYC=7 (post-B134). DW-B143: state extended to Accepted. DW-B144: state extended to Submitted.
// foreach (1) + SignalOrNameMatches guard (1) + state filter (3) + isStop (1) + name guard (1) + type match (1) = 8. AT LIMIT; PASS.
```

---

## D. DW-B145 Architecture Decision

### Decision: **Real fix -- add exact name guard in FindFollowerBracketOrder**

**Root cause (confirmed)**:
- `SignalOrNameMatches` path (1): when `signalName` is non-null, ALL ATM brackets return `true` (they share `FromEntrySignal`).
- `leaderName` exact-match path (3) in `SignalOrNameMatches` is never reached when `signalName` fires.
- After DW-B144 fix (Submitted allowed): Target1, Target2, Target3 all pass. First in iteration wins.
- Iterator order is unspecified -- Target1 may precede Target3 -- returns wrong bracket.

**Not a NO-OP**: DW-B145 persists after Ticket 1 alone.

**Fix location**: `FindFollowerBracketOrder` (list overload), after the `SignalOrNameMatches` check. Adding `SignalOrNameMatches` change is ruled out (regression risk to existing callers).

**Exact code change -- Ticket 2**:

After line 2547 (the existing `SignalOrNameMatches` continue), insert:

```csharp
if (leaderName != null && order.Name != leaderName) // B134 DW-B145: require exact name when provided
    continue;
```

**Complete post-B134 state of FindFollowerBracketOrder (list overload)**:
```csharp
// CYC=8 (post-B134). AT LIMIT; PASS.
// foreach (1) + SignalOrNameMatches guard (1) + leaderName exact guard (1) + state filter (3) + isStop (1) + type match (1) = 8.
// DW-B143: Accepted added. DW-B144: Submitted added. DW-B145: leaderName exact guard added.
// JS-021: no lock. JS-001: no throw. JS-002: Order? null contract unchanged.
private Order? FindFollowerBracketOrder(
    IEnumerable<Order> orders,
    string? fromEntrySignalName,
    bool isStop,
    string? leaderName = null)
{
    foreach (var order in orders)
    {
        if (!SignalOrNameMatches(order, fromEntrySignalName, leaderName))
            continue;
        if (leaderName != null && order.Name != leaderName)  // B134 DW-B145
            continue;
        if (order.OrderState != OrderState.Working
            && order.OrderState != OrderState.Accepted
            && order.OrderState != OrderState.Submitted)    // B134 DW-B144
            continue;
        if (isStop)
        {
            if (order.OrderType == OrderType.StopMarket
                || order.OrderType == OrderType.StopLimit)
                return order;
        }
        else
        {
            if (order.OrderType == OrderType.Limit && !IsStopLeg(order))
                return order;
        }
    }
    return null;
}
```

**SignalOrNameMatches**: UNCHANGED. CYC stays at 3. No regression risk.

---

## E. CYC Analysis

| Method | Pre-B134 CYC | Post-T1 CYC | Post-T1+T2 CYC | Limit | Pass? |
|--------|-------------|-------------|----------------|-------|-------|
| `FindFollowerBracketOrder` (list overload, L2538) | 6 (per comment) | 7 | 8 | 8 | YES (AT LIMIT) |
| `SignalOrNameMatches` (L2511) | 3 | 3 (unchanged) | 3 (unchanged) | 8 | YES |
| `SyncFollowerBracket` (L2179) | unchanged | unchanged | unchanged | 8 | YES |
| `SyncAtmFollowerTarget` (L2312) | unchanged | unchanged | unchanged | 8 | YES |
| `DeriveLeaderBracketIndex` (L2388) | 3 | unchanged | unchanged | 8 | YES |

**CYC 8 on `FindFollowerBracketOrder` is AT the JS ceiling but valid.** No helper extraction required. If future DW items add more branches, plan a `MatchesStateFilter` extraction to reduce to CYC <= 6.

---

## F. Constraint Compliance

| Constraint | Status | Evidence |
|------------|--------|----------|
| JS-021: no `lock()` | PASS | Pure predicate changes; no state mutation; no lock() in new/modified code |
| JS-001: no throw in hot path | PASS | `FindFollowerBracketOrder` and `SignalOrNameMatches` contain zero Cancel calls; no throw risk |
| JS-002: `Order?` null contract | PASS | `return null` at L2565 unchanged; CYC addition does not affect null-return path |
| CYC <= 8 per method | PASS | Post-fix max = 8 (`FindFollowerBracketOrder`); all others <= 8 |
| ASCII-only | PASS | No new string literals; "Submitted" is ASCII; "B134 DW-B144/DW-B145" in comments are ASCII |
| `_diagnosticMode` stays `true` through B134 SIM | PASS | No changes to diagnostic mode fields or initialization |
| `acc.Cancel()` wrapped in try/catch | PASS | Existing try/catch in `SyncAtmFollowerTarget` Block A (L2340-2347) and Block A-Prime (L2328-2336) covers all cancel paths that `fo` from the updated `FindFollowerBracketOrder` will reach |
| `PropTraderTools.csproj` registration | REQUIRED | Add `<Compile Include="Tests\B134Tests.cs" />` after `Tests\B133Tests.cs` entry (L161) |

---

## G. Test Plan

**File**: `src/PropTraderTools/Tests/B134Tests.cs`
**Namespace**: `PropTraderTools`
**Class**: `B134FindFollowerBracketOrderTests`
**Using**: `Xunit`, `NinjaTrader.Cbi` (via `FakeOrder` / mock stubs per existing pattern)

All tests use `CopyEngine.SignalOrNameMatchesTestable` (L2570) and the `FindFollowerBracketOrder` list-injection overload directly via `InternalsVisibleTo("PropTraderTools.Tests")`.

### Ticket 1 Tests (DW-B144 -- Submitted state)

| [Fact] Name | What It Asserts |
|-------------|-----------------|
| `T1_SubmittedState_StopOrder_Found` | Stop bracket in `Submitted` state is now returned (was null pre-fix) |
| `T1_SubmittedState_TargetOrder_Found` | Target bracket in `Submitted` state is now returned |
| `T1_WorkingState_StillFound_Regression` | Stop bracket in `Working` state still returned (B143 regression guard) |
| `T1_AcceptedState_StillFound_Regression` | Target bracket in `Accepted` state still returned (B143 regression guard) |
| `T1_NullOrder_NotMatched_Guard` | Order with `OrderState.Initialized` (terminal-side) not returned (guard test) |

### Ticket 2 Tests (DW-B145 -- name-exact selection)

| [Fact] Name | What It Asserts |
|-------------|-----------------|
| `T2_Target3_ReturnsTarget3_NotTarget1` | When leaderName="Target3", Target1+Target2+Target3 all Submitted, returns Target3 specifically |
| `T2_Target1_ReturnsTarget1_WhenRequested` | When leaderName="Target1", returns Target1 (not Target3) -- index 1 correctness |
| `T2_NullLeaderName_ReturnsFirstMatch` | When leaderName=null, returns first bracket in order (signal-only match still works for callers with no leaderName) |

### Prior Regression Guard

| Suite | Count | Guard |
|-------|-------|-------|
| B133Tests.cs | 10 | All 10 must pass unchanged |
| B132Tests.cs | 6 | All 6 must pass unchanged |
| B131Tests.cs | 7 | All 7 must pass unchanged |
| B130Tests.cs | 8 | All 8 must pass unchanged |
| B129Tests.cs | 13 | All 13 must pass unchanged |

The B134 changes are **additive to the state filter** and **restrictive to iteration selection** -- no existing test scenarios pass Submitted orders or rely on signal-match returning a different-named order. Regression risk is LOW.

---

## H. Files Changed

| File | Change Type | Description |
|------|-------------|-------------|
| `src/PropTraderTools/CopyEngine.cs` | MODIFY | Two surgical edits in `FindFollowerBracketOrder` list overload (L2538-2566): (1) add `&& order.OrderState != OrderState.Submitted` to state filter; (2) insert `if (leaderName != null && order.Name != leaderName) continue;` after SignalOrNameMatches check. Update block comments. |
| `src/PropTraderTools/Tests/B134Tests.cs` | NEW | 8 xUnit [Fact] tests across `B134FindFollowerBracketOrderTests` class |
| `src/PropTraderTools/PropTraderTools.csproj` | MODIFY | Add `<Compile Include="Tests\B134Tests.cs" />` after the B133Tests.cs entry |

**Files NOT touched**: `SignalOrNameMatches`, `SyncFollowerBracket`, `SyncAtmFollowerTarget`, `SyncAtmFollowerBracket`, `DeriveLeaderBracketIndex`, `FindLeaderStopPrice`, any B129-B133 test file.

---

## I. Prior Test Regression Guard

The ptt-engineer MUST run the following verification before reporting Ticket completion:

```
B133Tests.cs  -- expect 10 PASS (0 FAIL)
B132Tests.cs  -- expect 6 PASS (0 FAIL)
B131Tests.cs  -- expect 7 PASS (0 FAIL)
B130Tests.cs  -- expect 8 PASS (0 FAIL)
B129Tests.cs  -- expect 13 PASS (0 FAIL)
```

No test in any prior file is allowed to transition from PASS to FAIL as a result of B134 changes. If any regression is observed, report immediately before committing.

---

## J. Summary of Architectural Decisions

1. **DW-B144**: Option A selected -- add `OrderState.Submitted` to state filter in `FindFollowerBracketOrder` list overload. NT8 evidence: `acc.Cancel()` has no documented state restriction; existing try/catch absorbs `UnableToCancelOrder` gracefully.

2. **DW-B145**: Real fix (not NO-OP) -- add `if (leaderName != null && order.Name != leaderName) continue;` guard in `FindFollowerBracketOrder` after the `SignalOrNameMatches` check. This enforces exact bracket identity when `SyncFollowerBracket` passes `leaderOrder.Name`. `SignalOrNameMatches` is not modified.

3. **CYC**: `FindFollowerBracketOrder` reaches CYC=8 after both tickets. AT the JS ceiling; valid. No extraction needed.

4. **Tests**: 8 new [Fact] tests across 2 logical groups. All prior block tests (B129-B133) must remain green.

5. **No new fields, no new methods, no lock()**: Pure in-method predicate changes only.

---

*Plan produced by ptt-architect, B134 Phase 1. Awaiting ptt-plan-reviewer.*
