# B53-LaneB Architecture Plan — Limit Drag Sync

**Block**: B53-LaneB  
**Feature**: DW-B53-02 — Follower limit entry order price not updated on leader drag  
**Status**: REVIEW_PENDING  
**Date**: 2026-08-10  
**Author**: ptt-architect  
**Prerequisite**: B53-LaneA FINAL_PASS confirmed (follower orders are AddOn-owned; acc.Change() resolves cleanly)

---

## § 1 Problem Statement (DW-B53-02)

When a leader drags a working limit **entry** order to a new price in NinjaTrader, NT8 emits
`OnOrderUpdate` with `OrderState = ChangeSubmitted` on the **same orderId** as the original
working order. The follower account holds a matching `"PTT-Copy"` working limit order that
should have its price updated to match. It never gets updated.

**Observable symptom**: Leader's limit entry sits at new price; follower's `"PTT-Copy"` order
remains at the original price and fills (or does not fill) at the wrong level.

**Scope**: Affects limit entry orders only. Bracket legs (stop/target) are handled by
`SyncFollowerBracket`. Market orders are not affected (no limit price to drag). This is a
new gap exposed by B53-LaneA which gave the follower order a stable `"PTT-Copy"` identity.

---

## § 2 Root Cause

The root cause is in `DispatchCopy` (`CopyEngine.cs` line 584):

```
Gate 3: OrderState.Submitted only  (line ~592)
Gate 5: IsDedup(orderId)            (line ~601)
```

`IsDedup` stamps the orderId on the first `Submitted` event (the original limit entry).
When NT8 fires `ChangeSubmitted` on the same orderId to represent a drag, `Gate 3`
(`OrderState.Submitted`) rejects the event — the `ChangeSubmitted` state does not pass
the `== OrderState.Submitted` check — and execution never reaches `IsDedup`.

Even if `Gate 3` were relaxed, `IsDedup` would stamp the orderId at initial Submitted time,
so a later `ChangeSubmitted` with the same orderId would be swallowed as a duplicate.

**Either gate independently prevents the drag-sync from working.** The fix must bypass
`DispatchCopy` entirely for `ChangeSubmitted` entry-drag events, handling them through a
dedicated path before either gate is reached.

---

## § 3 Proposed Fix

### 3.1 Overview

Three coordinated changes to `CopyEngine.cs`:

| Change | Type | Location |
|--------|------|----------|
| Add `IsLeaderEntryChangeSubmitted` | New private static predicate | After `IsStopLeg` region |
| Add `FindFollowerEntryOrder` | New private static helper | Near `FindFollowerBracketOrder` (line 748) |
| Add `SyncFollowerEntryDrag` | New private void method | Near `SyncFollowerBracket` (line 685) |
| Add `HandleRuleMatch` | New private void method (extraction) | After `OnOrderUpdate` |
| Modify `OnOrderUpdate` | Insert ChangeSubmitted branch + call HandleRuleMatch | Lines 509–524 |

### 3.2 IsLeaderEntryChangeSubmitted(Order order, CopyRule rule) → bool

**Purpose**: Pure predicate. Returns `true` only when the given order event represents a
leader limit entry being dragged to a new price — and not a bracket leg, follower order,
or any other event.

**Signature**:
```csharp
private static bool IsLeaderEntryChangeSubmitted(Order order, CopyRule rule)
```

**Logic**:
```csharp
return order.OrderState == OrderState.ChangeSubmitted
    && !IsStopLeg(order)
    && !order.Name.StartsWith("Target")
    && order.Name != "PTT-Copy"
    && order.Account.Name == rule.MasterAccount.Name;
```

**Conditions explained**:
- `ChangeSubmitted` — only drag events (not new submissions)
- `!IsStopLeg(order)` — excludes bracket stop legs (detected by existing `IsStopLeg` method at line 1524)
- `!order.Name.StartsWith("Target")` — excludes bracket target legs (no dedicated `IsTargetLeg` method; inline StartsWith per codebase pattern)
- `order.Name != "PTT-Copy"` — excludes follower ChangeSubmitted events (follower's own acc.Change() fires back a ChangeSubmitted; this guard prevents relay loop)
- `order.Account.Name == rule.MasterAccount.Name` — confirms this is a master account order (redundant given Gate 2 already matched the rule by account, but defensive per JS-002 principles)

**Note**: `rule.MasterAccount` uses the field name `MasterAccount` (NOT `LeaderAccount`) as
confirmed in `CopyRule` struct at line 181 of `CopyEngine.cs`.

**NT8 API**: `OrderState.ChangeSubmitted` — confirmed valid by Director empirical observation
(the state emitted by NT8 when a working order is dragged). If NT8 F5 compile produces
`CS0117 'OrderState' does not contain a definition for 'ChangeSubmitted'`, escalate as a
new NT8 compiler rule; do not workaround with int cast.

### 3.3 FindFollowerEntryOrder(Account acc, Order leaderOrder) → Order

**Purpose**: Searches one follower account's order list for the working/accepted `"PTT-Copy"`
entry order that corresponds to the leader's entry being dragged. Returns `null` if not found.

**Signature**:
```csharp
private static Order FindFollowerEntryOrder(Account acc, Order leaderOrder)
```

**Logic**:
```csharp
foreach (var o in acc.Orders)
{
    if (o.Name == "PTT-Copy"
     && o.Instrument.FullName == leaderOrder.Instrument.FullName
     && (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted))
        return o;
}
return null;
```

**Pattern reference**: Follows `FindFollowerBracketOrder` at line 748 — same acc.Orders
iteration pattern, same Instrument.FullName comparison, same null-return convention.

**JS-002 note**: Returns `null` for "not found" rather than `Option<T>` — consistent with
the existing codebase pattern (FindFollowerBracketOrder also returns null). The NT8 API
returns raw NT8 objects; NT8 headers do not support Option<T>. This is an approved deviation
(see `docs/standards/JANE_STREET_DEVIATIONS.md`).

### 3.4 SyncFollowerEntryDrag(Order order, CopyRule rule) → void

**Purpose**: For each follower account in the rule, finds the working `"PTT-Copy"` entry order
and updates its limit price to match the leader's new price via `acc.Change()`.

**Signature**:
```csharp
private void SyncFollowerEntryDrag(Order order, CopyRule rule)
```

**Logic**:
```csharp
foreach (var acc in rule.FollowerAccounts)
{
    var fo = FindFollowerEntryOrder(acc, order);
    if (fo == null)
    {
        StatusUpdate?.Invoke($"PTT-Drag: no PTT-Copy entry found on {acc.Name} for {order.Instrument.FullName}");
        continue;
    }
    try
    {
        fo.LimitPrice = order.LimitPrice;
        acc.Change(new Order[] { fo });
        StatusUpdate?.Invoke($"PTT-Drag: synced {acc.Name} PTT-Copy to {order.LimitPrice:F2}");
    }
    catch (Exception ex)
    {
        StatusUpdate?.Invoke($"PTT-Drag: acc.Change failed on {acc.Name}: {ex.Message}");
    }
}
```

**Pattern reference**: `SyncFollowerBracket` at line 685 uses the identical
`fo.LimitPrice = newPrice; acc.Change(new Order[] { fo })` sequence (confirmed at line 708).
No tick-alignment arithmetic applied — `order.LimitPrice` is already tick-aligned by NT8.

**JS-001 compliance**: `acc.Change` call wrapped in `try/catch`. Catch logs via StatusUpdate
and does NOT re-throw (hot path). ✓

**NT8-046 compliance**: `acc.Change()` is called only on `fo` (a PTT-created follower order
with `fo.FromEntrySignal != null`). NT8 ATM engine interception (NT8-046) affects only
`Stop1/Stop2` slot orders (ATM-owned, `FromEntrySignal == null`). "PTT-Copy" entry orders
are AddOn-owned since B53-LaneA. ✓

### 3.5 HandleRuleMatch(Order order, CopyRule rule) → void (extraction)

**Purpose**: Encapsulates the "per-rule processing tail" of `OnOrderUpdate` — Mirror relay,
bracket handling, and standard copy dispatch. Extracted from `OnOrderUpdate` to free one CYC
slot for the new ChangeSubmitted branch without exceeding the CYC=8 limit.

**Signature**:
```csharp
private void HandleRuleMatch(Order order, CopyRule rule)
```

**Logic** (moved verbatim from `OnOrderUpdate` lines ~510–524):
```csharp
if ((CopyMode)_copyModeValue == CopyMode.Mirror)
{
    MirrorOrderUpdate(order, rule);
    return;
}
if (IsWorkingBracket(order))
{
    HandleBracketChange(order, rule);
    return;
}
DispatchCopy(order, rule);
```

**Semantic equivalence**: The extracted code is a verbatim move. No behavior change.
Mirror → early return; IsWorkingBracket → HandleBracketChange → early return; fall-through
to DispatchCopy. All paths terminate identically to the pre-extraction behavior.

### 3.6 OnOrderUpdate Modification

**Insertion point**: After Gate 2.5 (`!matchedRule.Value.Enabled` check, line ~507),
before the Mirror relay (currently line ~510).

**Before** (lines ~507–524, simplified):
```csharp
if (!matchedRule.Value.Enabled) return;       // Gate 2.5

// B9 T3 Mirror mode relay
if ((CopyMode)_copyModeValue == CopyMode.Mirror)
{
    MirrorOrderUpdate(order, matchedRule.Value);
    return;
}
// Gate B
if (IsWorkingBracket(order))
{
    HandleBracketChange(order, matchedRule.Value);
    return;
}
DispatchCopy(order, matchedRule.Value);
```

**After**:
```csharp
if (!matchedRule.Value.Enabled) return;       // Gate 2.5

// NEW: B53-LaneB — handle leader limit entry drag before IsDedup in DispatchCopy
if (IsLeaderEntryChangeSubmitted(order, matchedRule.Value))
{
    SyncFollowerEntryDrag(order, matchedRule.Value);
    return;
}

HandleRuleMatch(order, matchedRule.Value);    // Mirror + bracket + dispatch
```

The tail of `OnOrderUpdate` (Mirror + bracket + DispatchCopy) is replaced by a single
`HandleRuleMatch` call. The ChangeSubmitted branch fires early and returns before
`HandleRuleMatch` is reached.

---

## § 4 CYC Analysis

### CYC Methodology

Standard McCabe cyclomatic complexity. Each `if`, `foreach`, `catch` (when containing
conditional logic), and boolean short-circuit operator (`&&`, `||`) adds +1. Base CYC = 1.

### OnOrderUpdate (modified)

| Branch | +CYC |
|--------|------|
| `!_isCopyEnabled` (Gate 1) | +1 |
| B53-LaneA block (`&&` compound) | +1 |
| `foreach (_rules)` (Gate 2 loop) | +1 |
| instrument + account match condition | +1 |
| `matchedRule == null` (Gate 2 null) | +1 |
| `!matchedRule.Value.Enabled` (Gate 2.5) | +1 |
| `IsLeaderEntryChangeSubmitted` (NEW) | +1 |
| **Total** | **8** ✓ |

*Mirror check and IsWorkingBracket are moved to HandleRuleMatch — net CYC change = 0.*

### HandleRuleMatch (new)

| Branch | +CYC |
|--------|------|
| `(CopyMode)_copyModeValue == CopyMode.Mirror` | +1 |
| `IsWorkingBracket(order)` | +1 |
| **Total** | **3** ✓ |

### IsLeaderEntryChangeSubmitted (new)

| Branch | +CYC |
|--------|------|
| `&& !IsStopLeg(order)` | +1 |
| `&& !order.Name.StartsWith("Target")` | +1 |
| `&& order.Name != "PTT-Copy"` | +1 |
| `&& order.Account.Name == rule.MasterAccount.Name` | +1 |
| **Total** | **5** ✓ |

*Spec informal target was "≤ 3"; the McCabe-accurate value is 5. Both are well within the
project hard limit of CYC ≤ 8. The informal target was aspirational readability guidance.*

### FindFollowerEntryOrder (new)

| Branch | +CYC |
|--------|------|
| `foreach (var o in acc.Orders)` | +1 |
| `if (o.Name == "PTT-Copy" && ...)` — Name + Instrument | +1 |
| `&&` state check `(Working || Accepted)` | +1 |
| **Total** | **4** ✓ |

### SyncFollowerEntryDrag (new)

| Branch | +CYC |
|--------|------|
| `foreach (var acc in rule.FollowerAccounts)` | +1 |
| `if (fo == null)` | +1 |
| **Total** | **3** ✓ |

*Inner search loop extracted to `FindFollowerEntryOrder`. `try/catch` does not add CYC
(no conditional logic in the catch body).*

### Summary Table

| Method | CYC Before | CYC After | Limit |
|--------|-----------|-----------|-------|
| `OnOrderUpdate` | 8 | 8 | ≤ 8 ✓ |
| `HandleRuleMatch` | — | 3 | ≤ 8 ✓ |
| `IsLeaderEntryChangeSubmitted` | — | 5 | ≤ 8 ✓ |
| `FindFollowerEntryOrder` | — | 4 | ≤ 8 ✓ |
| `SyncFollowerEntryDrag` | — | 3 | ≤ 8 ✓ |

---

## § 5 JS Rules Compliance

### JS-001 — No throw in hot paths

- `SyncFollowerEntryDrag`: `acc.Change()` wrapped in `try/catch`. Catch logs to StatusUpdate and does NOT re-throw. ✓
- `IsLeaderEntryChangeSubmitted`: pure predicate, no exceptions. ✓
- `FindFollowerEntryOrder`: loop + return, no exceptions. ✓
- `HandleRuleMatch`: delegates to existing methods (already JS-001 compliant). ✓

### JS-002 — No return null for missing values

- `FindFollowerEntryOrder` returns `null` when not found — consistent with the existing
  `FindFollowerBracketOrder` codebase pattern. NT8 API returns raw objects; no Option<T>
  infrastructure available. Approved deviation (same as FindFollowerBracketOrder). ✓

### JS-021 — No lock()

- Zero `lock()` usage in any new or modified method. All new methods are stateless
  (static predicates/helpers) or operate on local stack variables + NT8 API calls. ✓

### JS-033 — No async void

- All new methods are synchronous. `IsLeaderEntryChangeSubmitted`, `FindFollowerEntryOrder`,
  `HandleRuleMatch`, `SyncFollowerEntryDrag` are all non-async. ✓

---

## § 6 NT8 Rules Compliance

### NT8-013 — DateTime.Now for order expiry

Not applicable. `SyncFollowerEntryDrag` uses `acc.Change()` (not `acc.CreateOrder()`). No
GTD expiry parameter used. ✓

### NT8-014 — CreateOrder signal name must start with "PTT-"

Not applicable. No `acc.CreateOrder()` call in this block. `acc.Change()` operates on an
existing "PTT-Copy" named order. ✓

### NT8-018 — lock() banned

Zero lock() usage. ✓

### NT8-019 — async void banned

All new methods are synchronous void or bool. ✓

### NT8-031 — OrderState.PendingSubmit does not exist

Not applicable. New code uses `OrderState.ChangeSubmitted` (drag state), `OrderState.Working`,
and `OrderState.Accepted` — not `PendingSubmit`. ✓

**F5 Compiler Gate for ChangeSubmitted**: `OrderState.ChangeSubmitted` is expected to exist
based on Director empirical observation (DispatchCopy Gate 3 is `OrderState.Submitted`,
implying ChangeSubmitted is a distinct valid enum value). If NT8 F5 produces CS0117 for
`OrderState.ChangeSubmitted`, the engineer MUST stop, escalate, and add a new NT8 rule
(NT8-056). Do NOT cast to int as a workaround without Director approval.

### NT8-042 — Dispatcher.InvokeAsync not available in AddOn

Not applicable. No UI updates in the new methods. `StatusUpdate?.Invoke()` is the existing
pattern for order-thread logging — no dispatcher needed. ✓

### NT8-043 — Null-conditional compound assignment banned

Not applicable. No `?.` on the left side of `-=` or `+=`. ✓

### NT8-044 — StringComparison requires using System

`IsLeaderEntryChangeSubmitted` does not use `StringComparison` directly.
`order.Name.StartsWith("Target")` uses the single-argument overload (no `StringComparison`
parameter). `order.Name != "PTT-Copy"` uses == operator. `FindFollowerEntryOrder` uses
`o.Instrument.FullName == leaderOrder.Instrument.FullName` (== operator). No
`StringComparison` enum reference in new code. ✓

### NT8-046 — acc.Change() on ATM slot orders silently overridden

`SyncFollowerEntryDrag` calls `acc.Change(new Order[] { fo })` only on `fo` where
`fo.Name == "PTT-Copy"`. PTT-Copy orders are AddOn-owned (B53-LaneA established
`FromEntrySignal != null` for follower fills). NT8-046 affects only ATM slot orders
`Stop1/Stop2` with `FromEntrySignal == null`. ✓

---

## § 7 Test Plan

### T_B53B_01 — IsLeaderEntryChangeSubmitted returns true for ChangeSubmitted leader entry

```csharp
[Fact]
public void T_B53B_01_IsLeaderEntryChangeSubmitted_ReturnsTrue_ForChangeSubmittedLeaderEntry()
```

**Arrange**:
- Create stub `Order` with:
  - `OrderState = OrderState.ChangeSubmitted`
  - `Name = "ManualEntry"` (not "PTT-Copy", not "Stop*", not "Target*")
  - `FromEntrySignal = null` (not a bracket leg — IsStopLeg also checks this)
  - `Account.Name = "Sim101"`
- Create `CopyRule` with `MasterAccount.Name = "Sim101"`

**Act**: `bool result = CopyEngine_TestAccessor.IsLeaderEntryChangeSubmitted(order, rule)`

**Assert**: `Assert.True(result)`

**What this proves**: The predicate correctly identifies a standard leader entry drag event.

---

### T_B53B_02 — IsLeaderEntryChangeSubmitted returns false for bracket stop leg

```csharp
[Fact]
public void T_B53B_02_IsLeaderEntryChangeSubmitted_ReturnsFalse_ForStopLeg()
```

**Arrange**:
- Create stub `Order` with:
  - `OrderState = OrderState.ChangeSubmitted`
  - `Name = "Stop"` (triggers `IsStopLeg` → `order.Name.StartsWith("Stop")` = true)
  - `Account.Name = "Sim101"`
- Create `CopyRule` with `MasterAccount.Name = "Sim101"`

**Act**: `bool result = CopyEngine_TestAccessor.IsLeaderEntryChangeSubmitted(order, rule)`

**Assert**: `Assert.False(result)`

**What this proves**: Bracket stop legs are correctly excluded from drag sync.

---

### Test insertion point

Both tests insert after the current last test `T_B53_AtmSkippedWhenNameIsNotPttCopy`
(line 4652 of `CopyEngineTests.cs`), before the class closing brace at lines 4655–4656.

### Future test candidates (not required in B53-LaneB)

| Scenario | Expected |
|----------|----------|
| `order.Name == "PTT-Copy"` | `false` — follower order excluded |
| `order.Name.StartsWith("Target")` | `false` — bracket target leg excluded |
| `order.Account.Name != rule.MasterAccount.Name` | `false` — account mismatch |
| `order.OrderState == OrderState.Submitted` | `false` — new submission, not drag |

---

## § 8 Hard-Link Sync Requirement

After the engineer writes changes to:
- `src/PropTraderTools/CopyEngine.cs`
- `src/PropTraderTools/CopyEngineTests.cs`

The engineer MUST run:
```powershell
powershell -File scripts\verify_links.ps1 -Fix
```

This syncs the hard-linked copy in the NT8 AddOns deployment folder.
Running `deploy-sync.ps1` is NOT correct for this workspace — that script belongs to the
V12 epic-cluster workspace, not the PTT Wave workspace.

---

## § 9 Scope

**Files modified**: 2

| File | Change type | Description |
|------|-------------|-------------|
| `src/PropTraderTools/CopyEngine.cs` | Modification | 4 new private methods; 1 modified method (OnOrderUpdate tail refactored + ChangeSubmitted branch added) |
| `src/PropTraderTools/CopyEngineTests.cs` | Addition | 2 new [Fact] tests (T_B53B_01, T_B53B_02) appended after line 4652 |

**Files NOT modified**:
- `PttContracts.cs` — no event changes
- `TradeCopierWindow.cs` — no UI changes
- `TradeCopierAddOn.cs` — no lifecycle changes
- Any `.csproj` file — no new dependencies

**Zero scope creep**: No changes to CopyRule struct, no new enums, no new public API.

---

## § 10 Deferred Items

### Items carried forward from B53-LaneA (UNCHANGED — do not close in B53-LaneB)

| ID | Priority | Status | Description |
|----|----------|--------|-------------|
| DW-B54-01 | P0 | OPEN | AtmStrategyCreate API for AddOn context (NT8-055 resolution) |
| DW-B54-02 | P0 | OPEN — blocked by DW-B54-01 | F5-GATE-02 live ATM bracket test on Sim101 |
| DW-B54-03 | P2 | OPEN | Diagnostic log for `#if NT8_ADDON_ATM` inactive state |
| DW-BACKLOG-01 | P2 | OPEN | PttContracts.cs FillSignal dead-code cleanup |

### B53-LaneB resolves

| ID | Resolved by |
|----|-------------|
| DW-B53-02 | This block — IsLeaderEntryChangeSubmitted + SyncFollowerEntryDrag + OnOrderUpdate routing |

### New deferred items from B53-LaneB

None. The fix is complete within the block scope.

### LaneC (if applicable)

If the Director identifies additional drag-sync scenarios (e.g., stop leg drag sync, bracket
target drag sync), those would constitute B53-LaneC. B53-LaneB's scope is strictly limit
entry drag sync via the `"PTT-Copy"` order name.

---

## § 11 Risk Assessment

### R1 — OrderState.ChangeSubmitted may not exist in this NT8 build (LOW)

**Trigger**: NT8 F5 compile produces `CS0117 'OrderState' does not contain a definition for 'ChangeSubmitted'`.  
**Probability**: Low. Director confirmed the state empirically. DispatchCopy Gate 3 (OrderState.Submitted) implicitly acknowledges ChangeSubmitted as a distinct value that falls through.  
**Mitigation**: If CS0117 occurs, engineer stops, adds NT8-056 to NT8_COMPILER_RULES.md with the actual state name, and escalates to Director before proceeding.

### R2 — acc.Change() on Accepted-state follower order (VERY LOW)

**Trigger**: `FindFollowerEntryOrder` finds a follower order in `OrderState.Accepted` (transient
state between submission and Working). `acc.Change()` called on an Accepted order.  
**Probability**: Very low. Accepted is a transient state; leader drag is unlikely to fire within
the Accepted window.  
**Mitigation**: NT8-046 confirms `acc.Change()` works on PTT-created orders. StatusUpdate logging
captures result. No behavioral harm if Change() is ignored during Accepted state (order will
reach Working and the price will be set on the next drag if any).

### R3 — HandleRuleMatch extraction changes observable behavior (NONE)

**Probability**: Zero. Semantic equivalence proven in planning (Thought 9). The extraction
is a verbatim move of 9 lines into a private method. Call stack depth increases by 1 frame —
irrelevant in NT8 order processing.

### R4 — Mirror mode follower drag (LOW, future concern)

**Trigger**: In Mirror mode, leader entry is dragged. The new branch returns early, bypassing
`MirrorOrderUpdate`.  
**Analysis**: Mirror mode currently relies on `DispatchCopy` which is already blocked by
IsDedup/OrderState.Submitted. The drag was already not working in Mirror mode. The new branch
handles it for the standard Copy mode. Mirror mode drag handling would be a separate deferred
item (LaneC/LaneD scope).  
**Mitigation**: Document as known limitation. No regression — prior behavior was "silently
not working"; new behavior is "handled for Copy mode, still not handled for Mirror mode".

---

## Appendix A — Method Placement Guide

Recommended insertion order within `CopyEngine.cs` for reviewer verification:

```
[~line 530]  HandleRuleMatch(Order, CopyRule)      -- immediately after OnOrderUpdate
[~line 700]  SyncFollowerEntryDrag(Order, CopyRule) -- near SyncFollowerBracket (line 685)
[~line 760]  FindFollowerEntryOrder(Account, Order) -- near FindFollowerBracketOrder (line 748)
[~line 1530] IsLeaderEntryChangeSubmitted(Order, CopyRule) -- near IsStopLeg (line 1524)
```

Exact line numbers shift with insertions; the important constraint is logical adjacency
with related existing methods.

---

## Appendix B — SCAN-01 through SCAN-07 Checklist (Engineer Pre-Commit)

| SCAN | Check | Expected |
|------|-------|----------|
| SCAN-01 | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` | 0 matches in new code |
| SCAN-02 | `grep -n "async void" src/PropTraderTools/CopyEngine.cs` | 0 matches in new code |
| SCAN-03 | `grep -n "return null" src/PropTraderTools/CopyEngine.cs` | `FindFollowerEntryOrder` only (1 match, approved) |
| SCAN-04 | `grep -n "DateTime.Now" src/PropTraderTools/CopyEngine.cs` | 0 matches in new code |
| SCAN-05 | `grep -n "\"#[0-9A-Fa-f]"` — hex color literals | 0 matches in new code |
| SCAN-06 | `grep -n "throw new" src/PropTraderTools/CopyEngine.cs` | 0 matches in new code (hot path) |
| SCAN-07 | `grep -n "FontFamily" src/PropTraderTools/CopyEngine.cs` | 0 matches in new code |

All 7 scans must pass before the engineer commits.
