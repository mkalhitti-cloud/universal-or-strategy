# Tickets -- BWAVE-NEXT LaneBRepair-R2 (Round 2)

**Epic**: BWAVE-NEXT LaneBRepair-R2
**Phase**: 3 (Ticket Generation)
**Written by**: ptt-architect
**Date**: 2026-09-05
**Input plan**: `docs/brain/BWAVE-NEXT/LaneBRepair-R2/02-architecture-plan.md` (REVIEW_PASS)
**Input review**: `docs/brain/BWAVE-NEXT/LaneBRepair-R2/02-plan-review.md` (REVIEW_PASS)
**Branch**: bwave-next-lane-b | PR: #43

---

## Ticket Count: 1

Both R2-F1 and R2-F2 target `src/PropTraderTools/CopyEngine.cs`.
Sequential implementation in one ptt-engineer session is correct per protocol.
Two separate tickets would require separate `.cs` files -- not the case here.

---

# T1 -- R2-F1 + R2-F2: AbortDrainOnFill helper + Clone Entry order filter

> **SCOPE LOCK -- TICKET T1 ONLY. Do NOT implement any other ticket.**

**Spec requirement IDs**: R2-F1, R2-F2
**Reviewer approval**: REVIEW_PASS (ptt-plan-reviewer, 2026-09-05)

---

## Files

| File | Access |
|------|--------|
| `src/PropTraderTools/CopyEngine.cs` | WRITE |
| `src/PropTraderTools/Tests/BwaveNextLaneBTests.cs` | WRITE (append only -- do not rewrite existing tests) |
| `docs/brain/BWAVE-NEXT/LaneBRepair-R2/ticket-1-completion.md` | WRITE (create new) |
| All other files | READ ONLY |

---

## Method Signatures

### New method (R2-F1)

```csharp
// R2-F1: fill-abort cleanup. CYC=3: (1) base, (2) TryRemove guard, (3) foreach.
// Called from OnOrderUpdate Filled branch -- method call is a statement, not a branch.
// OnOrderUpdate CYC remains 8. JS-021: no lock(). ConcurrentDictionary TryRemove is atomic.
// Cleans _drainOwnedOrderIds for fill-aborted drain payloads.
private void AbortDrainOnFill(string acctKey)
```

**Return type**: `void`
**Parameters**: `string acctKey` -- value of `e.Order.Account.Name` from the Filled branch
**Body contract**:
```csharp
if (_pendingDispatchDrains.TryRemove(acctKey, out var payload))
    foreach (var id in payload.DrainedOrderIds)
        _drainOwnedOrderIds.TryRemove(id, out _);
```

### Modified call site (R2-F1)

**Location**: `OnOrderUpdate` Filled branch (~line 1431-1435 on bwave-next-lane-b)

Current:
```csharp
else if (e.Order.OrderState == OrderState.Filled)
{
    // Drain-tracked entry filled -- abort replacement, position is open.
    _pendingDispatchDrains.TryRemove(e.Order.Account.Name, out _);
}
```

Replace with:
```csharp
else if (e.Order.OrderState == OrderState.Filled)
{
    // Drain-tracked entry filled -- abort replacement, position is open.
    AbortDrainOnFill(e.Order.Account.Name); // R2-F1: clean _drainOwnedOrderIds on fill-abort
}
```

### Modified predicate (R2-F2)

**Location**: `DrainThenDispatch` `entryCandidates` Where predicate, last line of predicate (~line 6529-6535 on bwave-next-lane-b)

Current last line of predicate:
```csharp
        && o.Name.StartsWith("PTT-Copy", StringComparison.Ordinal))
```

Replace with (two lines):
```csharp
        && (o.Name.StartsWith("PTT-Copy", StringComparison.Ordinal)
            || o.Name == "Entry")) // R2-F2: include Clone mode Entry orders (FindFollowerEntryOrder line 3717)
```

Complete updated block after fix:
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

---

## CYC Contract

| Method | Before | After | Budget | Pass? |
|--------|--------|-------|--------|-------|
| `OnOrderUpdate` | 8 | 8 | <=8 | YES -- `AbortDrainOnFill(...)` is a statement, not a branch |
| `AbortDrainOnFill` (new) | n/a | 3 | <=8 | YES -- base(1) + TryRemove if(+1) + foreach(+1) = 3 |
| `DrainThenDispatch` | 3 | 3 | <=8 | YES -- `||` in lambda predicate, no method-body branch added |

---

## JS Rule Constraints Per Method

| Method / change | Applicable rules | Requirement |
|----------------|-----------------|-------------|
| `AbortDrainOnFill` | JS-021 | No `lock()` -- ConcurrentDictionary.TryRemove is atomic |
| `AbortDrainOnFill` | JS-033 | Synchronous `private void`, NOT `async void` |
| `AbortDrainOnFill` | JS-002 | Returns void -- no `return null` possible |
| `AbortDrainOnFill` | ASCII-only | No Unicode, emoji, or curly quotes in identifiers or strings |
| `AbortDrainOnFill` | CYC<=8 | CYC=3, within budget |
| `OnOrderUpdate` Filled branch | JS-021 | No `lock()` -- call site is pure statement swap |
| `OnOrderUpdate` Filled branch | CYC<=8 | CYC=8 unchanged |
| `DrainThenDispatch` predicate | JS-021 | No `lock()` -- LINQ predicate, no state mutation |
| `DrainThenDispatch` predicate | CYC<=8 | CYC=3 unchanged -- lambda `||` does not count toward method body |
| All new/modified lines | NT8 AddOnBase | No `Account.Change()`, `AtmStrategyCreate()`, `AtmStrategyChangeStopTarget()` |
| All new/modified lines | ASCII-only | No `DateTime.Now` -- existing TickCount pattern unchanged |

---

## xUnit Tests

**File**: `src/PropTraderTools/Tests/BwaveNextLaneBTests.cs`
**Action**: APPEND to existing file -- do NOT rewrite or remove any existing test.
**Framework**: xUnit ONLY. NEVER NUnit, NEVER MSTest.

### Test A -- R2-F1 behavioral (preferred) / structural (fallback)

**Test name**: `AbortDrainOnFill_RemovesDrainedOrderIds_FromConcurrentDict`

**What it asserts**:
Given a `PendingDispatchDrain` payload with one or more `DrainedOrderIds` previously added to
`_drainOwnedOrderIds`, when `AbortDrainOnFill` is called with the matching account key, the
order IDs are removed from `_drainOwnedOrderIds`.

**Preferred (behavioral)**: Use reflection or internal test seam to access `_pendingDispatchDrains`
and `_drainOwnedOrderIds`, seed them, call `AbortDrainOnFill`, assert both dicts are empty.

**Fallback (structural)**: If NT8 test-seam limits prevent behavioral setup, assert that the method
`AbortDrainOnFill` exists on `CopyEngine` with the signature `private void AbortDrainOnFill(string)`.
Use `typeof(CopyEngine).GetMethod("AbortDrainOnFill", BindingFlags.NonPublic | BindingFlags.Instance)`
and `Assert.NotNull(methodInfo)`.

```csharp
[Fact]
public void AbortDrainOnFill_RemovesDrainedOrderIds_FromConcurrentDict()
{
    // ... (behavioral or structural per seam availability)
}
```

### Test B -- R2-F2 structural / behavioral

**Test name**: `DrainThenDispatch_EntryPredicate_IncludesCloneModeEntry`

**What it asserts**:
Confirms the `entryCandidates` filter logic accepts an order named exactly `"Entry"`.
Use the same entry-order name pattern as `FindFollowerEntryOrder` line 3717.

**Preferred (behavioral)**: Build a mock order list with one order named `"Entry"` in Working
state and Limit type; invoke the predicate logic (or a testable helper that wraps it) and assert
the "Entry"-named order is in the result set.

**Fallback (structural)**: Assert that `FindFollowerEntryOrder` (line 3717) accepts `"Entry"` by
verifying the method body contains `o.Name == "Entry"` via source inspection or by exercising the
method with a mock `"Entry"`-named order and asserting non-null return.

```csharp
[Fact]
public void DrainThenDispatch_EntryPredicate_IncludesCloneModeEntry()
{
    // ... (behavioral or structural per seam availability)
}
```

---

## 7-Scan Checklist

**The engineer MUST run all 7 scans and report results verbatim in `ticket-1-completion.md`.**

| # | Scan | Command | Pass Condition | JS Rule |
|---|------|---------|----------------|---------|
| SCAN-01 | `lock()` ban | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` | 0 actual code statements (comments OK) | JS-021 |
| SCAN-02 | `async void` ban | `grep -n "async void " src/PropTraderTools/CopyEngine.cs` | 0 actual declarations (comments OK) | JS-033 |
| SCAN-03 | `return null` ban | `grep -n "return null;" src/PropTraderTools/CopyEngine.cs` | 0 hits in `AbortDrainOnFill` and `DrainThenDispatch` | JS-002 |
| SCAN-04 | ASCII only | `Get-Content src/PropTraderTools/CopyEngine.cs \| Where-Object {$_ -match '[^\x00-\x7F]'} \| Measure-Object` | `Count=0` | ASCII-only |
| SCAN-05 | Banned NT8 APIs | `grep -n "Account\.Change\|AtmStrategyCreate\|AtmStrategyChangeStopTarget" src/PropTraderTools/CopyEngine.cs` | 0 code hits | NT8 AddOnBase |
| SCAN-06 | CYC audit | `lizard src/PropTraderTools/CopyEngine.cs` or `python scripts/complexity_audit.py src/PropTraderTools/CopyEngine.cs` | `OnOrderUpdate` CCN<=8, `AbortDrainOnFill` CCN<=8, `DrainThenDispatch` CCN<=8 | CYC<=8 |
| SCAN-07 | Build gate | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | 0 errors, 0 relevant warnings | Build |

---

## Post-Build Gates

```powershell
# 1. Sync and verify (expect 18/18 same as R1)
powershell -File scripts\ptt-sync-and-verify.ps1
# Requirement: all files OK -- 0 MISMATCH lines

# 2. NinjaTrader 8 compile (manual gate -- attest in completion report)
# Open NinjaTrader 8, press F5, confirm green compile
```

---

## Baseline Preservation

The following MUST NOT be changed under any circumstances:

| Location | What to preserve |
|----------|-----------------|
| ~line 6544 | `(long)(int)Environment.TickCount` -- DO NOT change to `TickCount64` (`.NET 4.8` does not support it) |
| ~line 6535 | `ActiveOrders(follower).Where(...).ToList()` -- DO NOT remove `.ToList()` (DW-NEXT-A-07 thread-safety lock) |
| All F1-F5 and F7/F8/F9 baseline fixes from the spec | DO NOT REVERT any prior round fixes |
| ~line 385 | `_drainOwnedOrderIds` field declaration -- DO NOT CHANGE (only add `TryRemove` calls in `AbortDrainOnFill`) |
| Lines 867-868 | `TryReplaceOnAtmCancel` guard -- DO NOT CHANGE |

---

## Acceptance Criteria

Copy these verbatim into `ticket-1-completion.md` and check each off:

```
[ ] R2-F1: AbortDrainOnFill helper added to CopyEngine.cs
[ ] R2-F1: Filled branch calls AbortDrainOnFill(e.Order.Account.Name)
[ ] R2-F1: Helper iterates DrainedOrderIds and removes each from _drainOwnedOrderIds
[ ] R2-F1: OnOrderUpdate CYC = 8 post-fix (verified via lizard)
[ ] R2-F1: AbortDrainOnFill CYC <= 8
[ ] R2-F2: entryCandidates Where predicate includes || o.Name == "Entry"
[ ] R2-F2: DrainThenDispatch CYC = 3 (unchanged)
[ ] SCAN-01: 0 lock() in new code
[ ] SCAN-02: 0 async void in new code
[ ] SCAN-03: 0 return null in AbortDrainOnFill / DrainThenDispatch
[ ] SCAN-04: 0 non-ASCII chars
[ ] SCAN-05: 0 NT8 banned API calls in new code
[ ] SCAN-06: All CYC <= 8
[ ] SCAN-07: dotnet build 0 errors
[ ] ptt-sync-and-verify.ps1 all files OK
[ ] F5 in NinjaTrader 8 green (attested)
[ ] (long)(int)Environment.TickCount preserved (no TickCount64)
[ ] ActiveOrders .ToList() preserved
```

---

## Completion Report Template

Create `docs/brain/BWAVE-NEXT/LaneBRepair-R2/ticket-1-completion.md` with the following structure:

```markdown
# Ticket T1 Completion -- BWAVE-NEXT LaneBRepair-R2

**Implemented by**: ptt-engineer
**Date**: {DATE}
**Branch**: bwave-next-lane-b

## Changes Made

### R2-F1
- [ ] AbortDrainOnFill method added at line {LINE}
- [ ] OnOrderUpdate Filled branch updated at line {LINE}

### R2-F2
- [ ] DrainThenDispatch entryCandidates predicate updated at line {LINE}

## 7-Scan Results

| Scan | Command output | Result |
|------|---------------|--------|
| SCAN-01 | {PASTE OUTPUT} | PASS/FAIL |
| SCAN-02 | {PASTE OUTPUT} | PASS/FAIL |
| SCAN-03 | {PASTE OUTPUT} | PASS/FAIL |
| SCAN-04 | {PASTE OUTPUT} | PASS/FAIL |
| SCAN-05 | {PASTE OUTPUT} | PASS/FAIL |
| SCAN-06 | {PASTE OUTPUT} | PASS/FAIL |
| SCAN-07 | {PASTE OUTPUT} | PASS/FAIL |

## Post-Build Results

- ptt-sync-and-verify.ps1: {PASTE SUMMARY LINE}
- F5 NinjaTrader 8: [ ] GREEN COMPILE (attested by engineer)

## Acceptance Criteria

[ ] R2-F1: AbortDrainOnFill helper added to CopyEngine.cs
[ ] R2-F1: Filled branch calls AbortDrainOnFill(e.Order.Account.Name)
[ ] R2-F1: Helper iterates DrainedOrderIds and removes each from _drainOwnedOrderIds
[ ] R2-F1: OnOrderUpdate CYC = 8 post-fix (verified via lizard)
[ ] R2-F1: AbortDrainOnFill CYC <= 8
[ ] R2-F2: entryCandidates Where predicate includes || o.Name == "Entry"
[ ] R2-F2: DrainThenDispatch CYC = 3 (unchanged)
[ ] SCAN-01: 0 lock() in new code
[ ] SCAN-02: 0 async void in new code
[ ] SCAN-03: 0 return null in AbortDrainOnFill / DrainThenDispatch
[ ] SCAN-04: 0 non-ASCII chars
[ ] SCAN-05: 0 NT8 banned API calls in new code
[ ] SCAN-06: All CYC <= 8
[ ] SCAN-07: dotnet build 0 errors
[ ] ptt-sync-and-verify.ps1 all files OK
[ ] F5 in NinjaTrader 8 green (attested)
[ ] (long)(int)Environment.TickCount preserved (no TickCount64)
[ ] ActiveOrders .ToList() preserved
```

---

*Tickets written: ptt-architect | BWAVE-NEXT LaneBRepair-R2 Round 2 | Phase 3*
