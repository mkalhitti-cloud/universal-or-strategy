# Architecture Plan -- BWAVE-NEXT LaneBRepair-R2 (Round 2)

**Epic**: BWAVE-NEXT LaneBRepair-R2
**Phase**: 1 (Architecture)
**Status**: REVIEW_PASS candidate
**Written by**: ptt-architect
**Date**: 2026-09-05
**Overwrite reason**: New bugs R2-F1/_drainOwnedOrderIds leak and R2-F2/Clone Entry filter gap.
  Prior plan (TryAdd guard + TryReplaceOnAtmCancel drain guard) was implemented and is already in source.
  This plan supersedes the prior plan entirely.
**Branch**: bwave-next-lane-b | PR: #43

---

## 1. LANE-SPLIT GATE RESULT: LANES-APPROVED

**Q1 -- Same method or within 50 lines?**
No. R2-F1 is in `OnOrderUpdate` (~line 1431-1434).
R2-F2 is in `DrainThenDispatch` (~line 6529-6534).
Different methods, approximately 5,100 lines apart.

**Q2 -- Fix B design depends on Fix A final design?**
No. R2-F1 adds `AbortDrainOnFill` (fill-abort cleanup path).
R2-F2 widens the `entryCandidates` filter (drain setup path).
Zero design dependency between them.

**Q3 -- Each fix has standalone value if the other is blocked?**
Yes. R2-F1 closes a permanent `_drainOwnedOrderIds` memory leak independently.
R2-F2 closes a silent no-drain path for Clone-mode orders independently.

**Q4 -- Each fix has an independent SIM verification path?**
Yes. R2-F1: SIM fill scenario where leader entry order fills while a drain is in progress.
R2-F2: SIM Clone-mode dispatch scenario where a follower holds an "Entry"-named working order.

**LANE-SPLIT GATE RESULT: LANES-APPROVED**

Two tickets: T1 (R2-F1) and T2 (R2-F2). No blocking dependency. May be committed together
or sequentially -- either order is correct.

---

## 2. Problem Statements

### R2-F1 (P1): _drainOwnedOrderIds leak on fill-abort path

**Location**: `CopyEngine.cs` line 1431-1434 (`OnOrderUpdate` Filled branch)

**Current code (line 1431-1434)**:
```csharp
else if (e.Order.OrderState == OrderState.Filled)
{
    // Drain-tracked entry filled -- abort replacement, position is open.
    _pendingDispatchDrains.TryRemove(e.Order.Account.Name, out _);
}
```

**Bug**: The `out _` discard means the removed `PendingDispatchDrain` payload is never processed.
`payload.DrainedOrderIds` contains the order IDs that were added to `_drainOwnedOrderIds` during
`DrainThenDispatch` (lines 6562-6566). Because those IDs are never removed from
`_drainOwnedOrderIds`, they remain permanently.

**Consequence**: `TryReplaceOnAtmCancel` calls `IsPttEntryOrderCancelTrigger`, which gates via
`_drainOwnedOrderIds.ContainsKey`. After the leak, the guard fires forever for those IDs, causing
future ATM-cancel replacement events on the same order IDs to be silently dropped.

**CYC constraint**: `OnOrderUpdate` current CYC = 8 (confirmed, comment at line 1424).
Inlining a TryRemove-then-foreach would add 1 branch (CYC = 9, exceeds budget).
Resolution: extract helper `AbortDrainOnFill(string acctKey)` -- a method call is a statement,
not a branch, so `OnOrderUpdate` CYC remains 8.

---

### R2-F2 (P1): entryCandidates misses Clone-mode "Entry" orders

**Location**: `CopyEngine.cs` lines 6529-6535 (`DrainThenDispatch` `entryCandidates` filter)

**Current code (lines 6529-6535)**:
```csharp
var entryCandidates = ActiveOrders(follower)
    .Where(o =>
        o.Instrument == instrument
        && (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted)
        && (o.OrderType == OrderType.Limit || o.OrderType == OrderType.StopLimit)
        && o.Name.StartsWith("PTT-Copy", StringComparison.Ordinal))
    .ToList();
```

**Bug**: `StartsWith("PTT-Copy")` excludes orders named exactly `"Entry"` (Clone mode).
When a Clone-mode follower has a live "Entry" working order, `entryCandidates.Any()` returns false,
`DrainThenDispatch` skips the drain path entirely, and `SubmitEntryDirect` runs immediately.
Result: two simultaneous entry orders on the account.

**Cross-reference**: `FindFollowerEntryOrder` (line 3717) uses
`order.Name == "PTT-Copy" || order.Name == "Entry"` (exact equality) for the same semantic filter.
The fix must match that pattern.

**CYC constraint**: The `||` is added inside the inline Where lambda predicate.
`DrainThenDispatch` body CYC = 3 (no new method-body branch added). CYC unchanged.

---

## 3. Proposed Fixes with Exact Code

### Fix T1 -- AbortDrainOnFill helper (R2-F1)

**New private method** (place in helper region near `OnOrderUpdate`, after line ~1436):
```csharp
// R2-F1: fill-abort cleanup. CYC=2: (1) TryRemove guard, (2) foreach.
// Called from OnOrderUpdate Filled branch -- no new branch there, just a statement.
private void AbortDrainOnFill(string acctKey)
{
    if (_pendingDispatchDrains.TryRemove(acctKey, out var payload))
        foreach (var id in payload.DrainedOrderIds)
            _drainOwnedOrderIds.TryRemove(id, out _);
}
```

**Modified OnOrderUpdate Filled branch** (replaces lines 1432-1435):
```csharp
else if (e.Order.OrderState == OrderState.Filled)
{
    // Drain-tracked entry filled -- abort replacement, position is open.
    // R2-F1: capture payload so DrainedOrderIds can be cleaned from _drainOwnedOrderIds.
    AbortDrainOnFill(e.Order.Account.Name);
}
```

**Net change**: 1 line modified in `OnOrderUpdate`; 1 new private helper method added.

---

### Fix T2 -- entryCandidates filter widened (R2-F2)

**Modified filter** (replaces line 6534):

Old (line 6534):
```csharp
        && o.Name.StartsWith("PTT-Copy", StringComparison.Ordinal))
```

New (lines 6534-6535, replace single line with two-line predicate):
```csharp
        && (o.Name.StartsWith("PTT-Copy", StringComparison.Ordinal)
            || o.Name == "Entry"))
```

**Complete updated block (lines 6529-6535 after fix)**:
```csharp
var entryCandidates = ActiveOrders(follower)
    .Where(o =>
        o.Instrument == instrument
        && (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted)
        && (o.OrderType == OrderType.Limit || o.OrderType == OrderType.StopLimit)
        && (o.Name.StartsWith("PTT-Copy", StringComparison.Ordinal)
            || o.Name == "Entry"))
    .ToList();
```

**Net change**: 1 line replaced by 2 lines in `DrainThenDispatch`. No new method. No new field.

---

## 4. CYC Analysis

| Method | Before | After | Budget | Pass? |
|--------|--------|-------|--------|-------|
| `OnOrderUpdate` | 8 | 8 | <=8 | YES -- `AbortDrainOnFill(...)` is a statement, not a branch |
| `AbortDrainOnFill` (new) | n/a | 2 | <=8 | YES -- (1) TryRemove if, (2) foreach |
| `DrainThenDispatch` | 3 | 3 | <=8 | YES -- `||` in lambda predicate, no method-body branch |

**Detailed branch count for `AbortDrainOnFill`**:
- Base: 1
- `if (_pendingDispatchDrains.TryRemove(...))`: +1
- `foreach (var id in ...)`: +1
- Total CYC = 3. (Documentation convention: CYC = 2 branches added = CYC 2 above baseline.)
Either measure: well within budget.

---

## 5. Interaction Analysis

Both fixes share `_drainOwnedOrderIds` and `_pendingDispatchDrains` but in strictly complementary roles:

| | R2-F1 (AbortDrainOnFill) | R2-F2 (entryCandidates filter) |
|-|--------------------------|-------------------------------|
| **Execution trigger** | Entry order fills during active drain | New leader entry arrives, drain setup |
| **Operation on `_drainOwnedOrderIds`** | TryRemove (cleanup) | TryAdd via existing loop lines 6562-6566 (setup) |
| **Operation on `_pendingDispatchDrains`** | TryRemove (cleanup) | TryAdd (setup, existing line 6560) |
| **Order of events** | F2 runs first (drain setup); F1 runs later (fill-abort teardown) | F2 is prerequisite for F1 (F1 cleans what F2 added) |

**No conflict**: F2 adds "Entry" order IDs to `_drainOwnedOrderIds` via the existing loop.
F1 removes those same IDs when the drain aborts on fill. Together they close the full lifecycle
for Clone-mode "Entry" orders.

**Applying only F1 (without F2)**: F1 correctly cleans up any IDs that did enter `_drainOwnedOrderIds`
(PTT-Copy mode only). Does not help Clone mode because "Entry" orders still skip the drain.

**Applying only F2 (without F1)**: Clone-mode "Entry" orders now enter the drain correctly.
But if a fill abort occurs, those IDs still leak in `_drainOwnedOrderIds`. The leak now affects
Clone mode as well.

**Applying both**: Full lifecycle closed for all modes. Recommended.

**No apply-order constraint**: F2 can be committed before or after F1.

---

## 6. 7-Scan Checklist Template (Engineer Contract)

The following 7 scans MUST pass on the modified `CopyEngine.cs` before the PR is merged.

| # | Scan | Command | Pass Condition | JS Rule |
|---|------|---------|----------------|---------|
| SCAN-01 | lock() ban | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` | 0 matches in new/modified lines | JS-021 |
| SCAN-02 | async void ban | `grep -n "async void " src/PropTraderTools/CopyEngine.cs` | 0 matches in new/modified lines | JS-033 |
| SCAN-03 | return null ban | `grep -n "return null;" src/PropTraderTools/CopyEngine.cs` | 0 matches in new/modified lines | JS-002 |
| SCAN-04 | ASCII only | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` | 0 matches in new/modified lines | ASCII-only |
| SCAN-05 | Banned NT8 APIs | `grep -n "Account\.Change\|AtmStrategyCreate\|AtmStrategyChangeStopTarget" src/PropTraderTools/CopyEngine.cs` | 0 matches in new/modified lines | NT8 AddOnBase |
| SCAN-06 | CYC audit | `python scripts/complexity_audit.py src/PropTraderTools/CopyEngine.cs` | `OnOrderUpdate`<=8, `AbortDrainOnFill`<=8, `DrainThenDispatch`<=8 | CYC <=8 |
| SCAN-07 | Build gate | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | 0 errors | Build |

**Post-build gate**:
```
powershell -File scripts\ptt-sync-and-verify.ps1   # 0 MISMATCH lines
F5 in NinjaTrader 8                                 # compile green
```

---

## 7. Acceptance Criteria

### T1 -- R2-F1 (_drainOwnedOrderIds leak)

- [ ] New private method `AbortDrainOnFill(string acctKey)` added to `CopyEngine.cs`
- [ ] Method body: `if (_pendingDispatchDrains.TryRemove(acctKey, out var payload))` + `foreach` cleanup
- [ ] `OnOrderUpdate` Filled branch (line ~1434): `out _` replaced with `AbortDrainOnFill(e.Order.Account.Name)`
- [ ] `OnOrderUpdate` comment updated to reflect R2-F1 fix
- [ ] SCAN-01 through SCAN-07 all PASS
- [ ] Lizard: `OnOrderUpdate` CYC = 8 (unchanged), `AbortDrainOnFill` CYC <= 3
- [ ] `ptt-sync-and-verify.ps1` 0 MISMATCH
- [ ] F5 compile green

### T2 -- R2-F2 (entryCandidates Clone mode gap)

- [ ] `DrainThenDispatch` `entryCandidates` filter (line ~6534): `StartsWith("PTT-Copy", StringComparison.Ordinal)` expanded to `(StartsWith(...) || o.Name == "Entry")`
- [ ] Exact equality `== "Entry"` used (not StartsWith) -- matches `FindFollowerEntryOrder` line 3717 pattern
- [ ] Comment in `DrainThenDispatch` updated to note Clone mode "Entry" inclusion
- [ ] SCAN-01 through SCAN-07 all PASS
- [ ] Lizard: `DrainThenDispatch` CYC = 3 (unchanged)
- [ ] `ptt-sync-and-verify.ps1` 0 MISMATCH
- [ ] F5 compile green

---

## 8. Out-of-Scope Items

| Item | Reason |
|------|--------|
| `TickCount64` migration | `.NET 4.8` does not support `TickCount64`; `(long)(int)TickCount` is the correct pattern |
| Remove `.ToList()` from `ActiveOrders` | DW-NEXT-A-07 thread-safety fix -- locked, do not remove |
| Watchdog resubmit on timeout | Spec decision: drop on timeout is intentional |
| Drain key acct+instrument (DW-NEXT-B-01) | P2 future backlog -- not this block |
| GTC/TIF preservation (DW-NEXT-B-02) | P2 future backlog -- not this block |
| Behavioral tests gap (DW-NEXT-B-03) | P2 future backlog -- not this block |
| `_followerReplaceSpecs` FSM changes | Out of scope for this repair |
| Magic constant naming (`2000L` timeout) | Style; not a correctness issue |
| Test PascalCase without underscores | Style; existing names valid |
| Any fix not explicitly R2-F1 or R2-F2 | No scope creep |

---

## Component List

**File**: `src/PropTraderTools/CopyEngine.cs` (only file modified)

| Method | Location | Change Type |
|--------|----------|-------------|
| `OnOrderUpdate` (Filled branch) | Line ~1431-1434 | 1 line modified (statement swap) |
| `AbortDrainOnFill` (new) | After line ~1436 | New private helper, ~5 lines |
| `DrainThenDispatch` | Line ~6534 | 1 line replaced by 2 lines (predicate expansion) |

**New fields**: none.
**New classes**: none.
**New files**: none.

---

## NinjaTrader 8 API Usage

No NT8 API calls are added or modified by either fix.

| API | Status |
|-----|--------|
| `Account.Change()` | NOT USED (banned for AddOnBase) |
| `AtmStrategyCreate()` | NOT USED (StrategyBase-only, banned) |
| `AtmStrategyChangeStopTarget()` | NOT USED (StrategyBase-only, banned) |
| `Account.Cancel()` + `Account.CreateOrder()` + `Submit()` | Existing pattern unchanged |
| `DateTime.Now` | NOT USED -- existing TickCount pattern unchanged |

---

## Threading Model

Both fixes operate on the NT8 order-update callback thread.

| Operation | Data Structure | Thread Safety |
|-----------|---------------|---------------|
| `TryRemove(acctKey, out var payload)` | `ConcurrentDictionary` | Atomic -- no lock |
| `TryRemove(id, out _)` in foreach | `ConcurrentDictionary` | Atomic -- no lock |
| `payload.DrainedOrderIds` foreach | `List<string>` locally owned post-TryRemove | Safe -- single owner after TryRemove |
| LINQ Where predicate `o.Name == "Entry"` | Pure expression | No state mutation |

No `lock()` anywhere. No `Dispatcher.InvokeAsync` needed (no UI updates). JS-021 PASS.

---

## Deferred Backlog Status

| ID | Description | Status |
|----|-------------|--------|
| DW-NEXT-B-01 | Drain key acct-only; extend to acct+instrument for multi-instrument trading | OPEN (P2 future) |
| DW-NEXT-B-02 | GTC/TIF not preserved in `SubmitEntryDirect` replacement | OPEN (P2 future) |
| DW-NEXT-B-03 | Behavioral test coverage gap for guard paths | OPEN (P2 future) |

No new deferred items opened by this plan.

---

*Plan written: ptt-architect | BWAVE-NEXT LaneBRepair-R2 Round 2 | Phase 1 Architecture*
