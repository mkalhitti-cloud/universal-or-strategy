# PTT-REPAIRS-04-POST-BUG-E Deferred Backlog

**Date:** 2026-09-06
**Epic:** PTT-REPAIRS-04-POST-BUG-E
**Pipeline verdict:** FINAL_PASS (see 05-final-review.md)

---

## Overview

Four items are deferred from this pipeline. All were identified during plan, ticket, or verification phases.
None of these items is blocking FINAL_PASS for PTT-REPAIRS-04-POST-BUG-E.

---

## DEFERRED-1: Option A Test Runner Wiring

**ID:** DW-REPAIRS-04-BUG-E-01
**Priority:** Medium (P1)
**Target Block:** B6 / future session

**Description:**
PropTraderTools.Tests.csproj project setup and execution of the unit test
`EvictDedup_CancelledEntry_ClearsLastLeaderDirection` [Fact].

**Test spec:**

| Element | Value |
|---------|-------|
| Name | `EvictDedup_CancelledEntry_ClearsLastLeaderDirection` |
| Framework | xUnit (or equivalent) |

**Arrange:**
```csharp
SetLeaderDirection_ForTest("MGC DEC26", OrderAction.Buy)
IsLiveEntryBlocked_ForTest("MGC DEC26|Buy", "ord-1", 0.0)
```

**Act:**
```csharp
EvictDedup("ord-1", OrderState.Cancelled)
```

**Assert:**
```csharp
HasLeaderDirection_ForTest("MGC DEC26") == false
```

**Blocking condition:**
CopyEngineTests.cs is not currently executable. PropTraderTools.Tests.csproj is not wired.
The test seam helpers (`SetLeaderDirection_ForTest`, `HasLeaderDirection_ForTest`,
`IsLiveEntryBlocked_ForTest`) are defined in CopyEngine.cs (DW-B135 / B143 test accessor
region) and require `InternalsVisibleTo("PropTraderTools.Tests")` to be accessible.

This test must NOT be added to CopyEngineTests.cs until the Option A InternalsVisibleTo
session is complete and the test project is buildable.

**Status:** OPEN

---

## DEFERRED-2: TOCTOU Window in Value-Guarded TryRemove

**ID:** DW-REPAIRS-04-BUG-E-02
**Priority:** Low (P2)
**Target Block:** future

**Description:**
Carried forward from PTT-REPAIRS-03-POST (DW-REPAIRS-03-POST-01).

The value-guarded TryRemove pattern in EvictDedup (Filled branch, lines 5892-5898) has a
theoretical TOCTOU window between the `TryGetValue` read (line 5895) and the `TryRemove`
(line 5897):

```csharp
if (_liveEntryInstruments.TryGetValue(filledInstrKey, out storedId)
    && storedId == orderId)
    _liveEntryInstruments.TryRemove(filledInstrKey, out _);
```

Between the TryGetValue and TryRemove, another thread could theoretically update
`_liveEntryInstruments[filledInstrKey]` to a different value. The TryRemove would then
remove the updated entry, not the original `storedId` entry.

**Assessment:**
Acceptable under the NT8 single-threaded OnOrderUpdate model. Only one thread calls
EvictDedup. The TOCTOU window cannot open in production because NinjaTrader dispatches
OnOrderUpdate on a single thread per strategy instance.

A safe alternative is the overloaded `TryRemove(key, out V value)` pattern with post-removal
value check, but this requires .NET 6 and is unnecessary under the current threading model.

**Status:** OPEN (low risk; acceptable under current threading model)

---

## DEFERRED-3: BUG-F

**ID:** DW-REPAIRS-04-BUG-E-03
**Priority:** P1 (Active -- parallel pipeline)
**Target Block:** Active

**Description:**
BUG-F is covered by the parallel pipeline PTT-REPAIRS-04-POST-BUG-F. This entry is a
reference cross-link only. No action required in PTT-REPAIRS-04-POST-BUG-E.

See: `docs/brain/PTT-REPAIRS-04-POST-BUG-F/`

**Status:** OPEN (tracked in parallel pipeline)

---

## DEFERRED-4: EvictDedup CYC Refactoring

**ID:** DW-REPAIRS-04-BUG-E-04
**Priority:** High (P1)
**Target Block:** B6 / future session

**Description:**
`EvictDedup` post-fix CYC = 13 (D = 12), exceeding the JS-013 limit of CYC <= 8.
This is a pre-existing violation: CYC = 12 (D = 11) before BUG-E. BUG-E added +1 (the
`if (pipeIdx > 0)` guard at line 5881).

**Extraction plan:**

| New Method | Responsibility | Estimated CYC |
|-----------|---------------|---------------|
| `EvictCancelledEntry(string orderId)` | Cancelled branch: `_entryDispatchedOrders.TryRemove` + `_entryInstrKeyByOrderId.TryRemove` block + live-entry guard + BUG-E direction clear | ~5 |
| `EvictFilledEntry(string orderId)` | Filled branch: `_entryInstrKeyByOrderId.TryRemove` block + value-guarded live-entry remove | ~4 |
| `EvictDedup` (residual) | Terminal-state guard + `_dedupCache.TryRemove` + dispatch to `EvictCancelledEntry` / `EvictFilledEntry` | ~6 |

All three methods would remain under CYC 8, satisfying JS-013.

**Blocking condition:**
PTT-REPAIRS-04-POST-BUG-E is SOURCE VERIFICATION ONLY. New .cs edits are out of scope for
this pipeline. The violation was pre-existing before BUG-E and is carried forward.

**JS rule cited:** JS-013 (CYC <= 8 per method)

**Status:** OPEN (JS-013 violation; extraction required in a future session)

---

## Summary Table

| ID | Description | Priority | Target | Status |
|----|-------------|----------|--------|--------|
| DW-REPAIRS-04-BUG-E-01 | Option A test runner wiring + EvictDedup_CancelledEntry [Fact] | P1 | B6 | OPEN |
| DW-REPAIRS-04-BUG-E-02 | TOCTOU window in value-guarded TryRemove (carried from REPAIRS-03-POST) | P2 | future | OPEN |
| DW-REPAIRS-04-BUG-E-03 | BUG-F (reference -- parallel pipeline PTT-REPAIRS-04-POST-BUG-F) | P1 | Active | OPEN |
| DW-REPAIRS-04-BUG-E-04 | EvictDedup CYC=13 refactoring (JS-013 violation; pre-existing + BUG-E +1) | P1 | B6 | OPEN |
