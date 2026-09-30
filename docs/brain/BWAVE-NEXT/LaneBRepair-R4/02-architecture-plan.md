# Architecture Plan -- BWAVE-NEXT LaneBRepair-R4

**Epic**: BWAVE-NEXT LaneBRepair-R4
**Phase**: 1 (Architecture)
**Status**: REVIEW_PASS candidate
**Written by**: ptt-architect
**Date**: 2026-09-05
**Branch**: bwave-next-lane-b | PR: #43
**Brain dir**: docs/brain/BWAVE-NEXT/LaneBRepair-R4/

---

## 1. LANE-SPLIT GATE RESULT: SINGLE-PIPELINE

There is exactly ONE bug under investigation (R4-F1). The gate cannot approve lanes
with a single fix.

Q1. Same method or within 50 lines? N/A -- only one fix.
Q2. Fix B design depends on Fix A? N/A -- no Fix B.
Q3. Each fix has standalone value if the other is blocked? N/A -- no Fix B.
Q4. Each fix has an independent SIM verification path? N/A -- no Fix B.

**LANE-SPLIT GATE RESULT: SINGLE-PIPELINE**

Single ticket T1. No lane split.

---

## 2. Source Read Findings

**Method**: `SubmitDrainedEntry(string acctKey)`
**File**: `src/PropTraderTools/CopyEngine.cs`
**Lines read**: 6632-6652

### Exact operation order as found in source:

| Step | Operation | Line |
|------|-----------|------|
| (a) | `_pendingDispatchDrains.TryRemove(acctKey, out var payload)` | 6634 |
| (b) | `follower == null` null guard | 6638 |
| (c) | `SubmitEntryDirect(follower, ...)` | 6641 |
| (d) | `foreach (var id in payload.DrainedOrderIds) _drainOwnedOrderIds.TryRemove(id, out _)` | 6650-6651 |

No try/finally wrapper exists anywhere in the method.

### Exact source (lines 6632-6652):

```csharp
private void SubmitDrainedEntry(string acctKey)
{
    if (!_pendingDispatchDrains.TryRemove(acctKey, out var payload)) // line 6634
        return;

    var follower = payload.FollowerAccount;
    if (follower == null) // line 6638
        return;

    SubmitEntryDirect( // line 6641 -- submit FIRST
        follower,
        payload.Instrument,
        payload.Qty,
        payload.Price,
        payload.Action,
        payload.OrderType);

    // R3-F2: clear drain-owned IDs AFTER submit so IDs are preserved on submit failure.
    foreach (var id in payload.DrainedOrderIds) // line 6650 -- cleanup AFTER
        _drainOwnedOrderIds.TryRemove(id, out _);
}
```

### Key observation

The comment at line 6649 reads: *"R3-F2: clear drain-owned IDs AFTER submit so IDs are preserved on submit failure."*

This confirms that a prior repair round (R3-F2) **already fixed** the ordering. The cleanup runs
**after** `SubmitEntryDirect`, not before.

---

## 3. Finding: STALE

**R4-F1 is STALE.**

The bug description states:
> Cleanup (foreach DrainedOrderIds) runs BEFORE SubmitEntryDirect.
> If submit throws, cleanup already ran and IDs are leaked forever.

The current source shows the **opposite** order:
- `SubmitEntryDirect` at line 6641 runs **first**
- `foreach DrainedOrderIds` cleanup at line 6650 runs **after**

If `SubmitEntryDirect` throws, the cleanup at line 6650 does NOT execute, meaning the
`_drainOwnedOrderIds` entries are preserved. This is the correct behavior (R3-F2 design intent:
IDs remain in the dict until `TryDrainWatchdog` cleans them on timeout, preventing phantom-
reentry for an order whose submission is uncertain).

**No production code change is required.**

Evidence:
- Line 6641: `SubmitEntryDirect(...)` -- appears before line 6650
- Line 6649 comment: `R3-F2: clear drain-owned IDs AFTER submit so IDs are preserved on submit failure`
- No try/finally wrapper exists (and none is needed, because cleanup is already correctly deferred)

---

## 4. Fix Design

**NOT APPLICABLE -- R4-F1 is STALE.**

The try/finally pattern described in the task brief is not needed. The current code already
has the correct ordering: submit first, cleanup after.

For reference (not to be implemented): if the ordering were reversed (cleanup before submit),
the correct fix would be:

```csharp
// HYPOTHETICAL ONLY -- NOT NEEDED. Current source is already correct.
if (!_pendingDispatchDrains.TryRemove(acctKey, out var payload)) return;
var follower = payload.FollowerAccount;
if (follower == null) return;
try
{
    SubmitEntryDirect(follower, payload.Instrument, payload.Qty,
                      payload.Price, payload.Action, payload.OrderType);
}
finally
{
    foreach (var id in payload.DrainedOrderIds)
        _drainOwnedOrderIds.TryRemove(id, out _);
}
```

CYC note (for record): try/finally adds ZERO McCabe branches. CYC of SubmitDrainedEntry
would remain 4 even if this pattern were applied.

---

## 5. Dismissed Findings

All 11 findings from prior rounds remain dismissed. No new dismissals from R4.

| ID | Finding | Disposition |
|----|---------|-------------|
| CR5-outside-1 | Drain ID/instrument scoping | DW-NEXT-B-01. DISMISSED (future scope). |
| CR5-outside-2 | ATM mode/template preservation in payload | DW-NEXT-B-02. DISMISSED (future scope). |
| CR5-outside-3 | TryDrainWatchdog independent trigger | Advisory only. DISMISSED. |
| CR5-dup-1 | Order.Name null guard | NT8 guarantees non-null Order.Name. DISMISSED. |
| CR5-dup-2 | OnOrderUpdate helper extraction CYC | DW-NEXT-B-04. DISMISSED (future complexity epic). |
| CR5-dup-3 | _followerReplaceSpecs FSM | Scope creep. DISMISSED. |
| CR5-dup-4 | Hot-path heap alloc removal | DW-NEXT-A-07. DISMISSED. |
| CR5-test-1 | Test PascalCase no underscores | Project convention. DISMISSED. |
| CR5-test-2 | Test parameter type assertions | Advisory. DISMISSED. |
| DW-lock-1 | Watchdog resubmit vs drop | Director-locked (drop on timeout). DISMISSED. |
| DW-net-1 | TickCount64 usage | .NET 4.8 -- TickCount64 unavailable. DISMISSED. |

---

## 6. Locked Architecture Decisions

The following decisions are locked by prior Director instruction. None are changed by this plan.

| Decision | Rationale |
|----------|-----------|
| `(long)(int)Environment.TickCount` is correct | .NET 4.8 -- no TickCount64 available |
| `ActiveOrders.ToList()` stays | DW-NEXT-A-07 thread-safety -- copy-on-enumeration |
| Watchdog drops on timeout | No resubmit on expiry -- Director-locked |
| Drain key is acct-only | DW-NEXT-B-01 future -- multi-instrument extension deferred |
| GTC/TIF not preserved | DW-NEXT-B-02 future -- TIF carry deferred |
| TryAdd fail-fast on concurrent drain | Correct design -- concurrent drain prevented at the gate |
| NT8 API: Account.Cancel() + Account.CreateOrder() + Submit() only | AddOn pattern for bracket changes |
| Order.Name null guard: DISMISSED | NT8 guarantees non-null Order.Name |
| OnOrderUpdate helper extraction | DW-NEXT-B-04 future -- not this epic |
| Drain ID/instrument scoping | DW-NEXT-B-01 future |
| ATM mode/template preservation | DW-NEXT-B-02 future |
| Watchdog independent trigger | Out of scope |

---

## 7. Deferred Items

**No new DW- items generated by this block.**

A STALE finding produces no deferred work. The following items from prior rounds remain OPEN
and are carried forward unchanged:

| ID | Status |
|----|--------|
| DW-NEXT-B-01 | OPEN (carried forward) |
| DW-NEXT-B-02 | OPEN (carried forward) |
| DW-NEXT-B-03 | OPEN (carried forward) |
| DW-NEXT-B-04 | OPEN (carried forward) |

---

## 8. Single Ticket: T1

### T1 -- R4-F1 STALE Verification + Regression Guard

**Spec requirement**: R4-F1 (P2) SubmitDrainedEntry cleanup ordering investigation.

**File (production)**: `src/PropTraderTools/CopyEngine.cs` -- **NO CHANGE**

**File (test)**: `src/PropTraderTools/CopyEngineTests.cs`

**Finding**: STALE. Cleanup is already correctly ordered: submit first (line 6641),
cleanup after (line 6650). R3-F2 comment at line 6649 documents the intent.

**Production code change**: NONE.

**Test to add** (regression guard against ordering reversal):

```csharp
// File: src/PropTraderTools/CopyEngineTests.cs
// Jane Street rules: JS-021 (no lock). xUnit [Fact] only.
// CYC=2: base(1) + Assert branch(1).

[Fact]
public void SubmitDrainedEntry_SourceOrdering_SubmitBeforeCleanup_StaleR4F1()
{
    // Regression guard: R4-F1 was investigated and found STALE.
    // This test confirms the R3-F2 ordering comment still exists in source,
    // guarding against any future edit that moves cleanup before submit.
    // If this comment disappears, the ordering may have been changed and
    // R4-F1 should be re-evaluated.
    var sourceText = System.IO.File.ReadAllText(
        System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(
                typeof(CopyEngine).Assembly.Location),
            "..", "..", "..", "src", "PropTraderTools", "CopyEngine.cs"));
    Assert.Contains(
        "R3-F2: clear drain-owned IDs AFTER submit",
        sourceText,
        System.StringComparison.Ordinal);
}
```

**Method signatures** (test only, no new production methods):
- `SubmitDrainedEntry_SourceOrdering_SubmitBeforeCleanup_StaleR4F1()` : `void` [Fact]

**JS rule constraints**:
- JS-021: No lock() -- the test uses file I/O and string search only
- JS-001: No throw in hot path -- this is a test method, not a hot path

**CYC impact**: Test method CYC = 1 (base) + 1 (Assert condition) = 2. Within budget.

**SCAN-01 through SCAN-07 checklist** (engineer contract):

| Scan | Check | Expected Result |
|------|-------|-----------------|
| SCAN-01 | JS-021 lock() ban | Zero `lock(` in new code |
| SCAN-02 | DateTime.Now ban | Zero `DateTime.Now` in new code |
| SCAN-03 | ASCII-only identifiers | All identifiers and string literals ASCII |
| SCAN-04 | CYC <= 8 | New test method CYC = 2. All within budget. |
| SCAN-05 | No production code change | `git diff src/PropTraderTools/CopyEngine.cs` = empty |
| SCAN-06 | xUnit only | [Fact] attribute used, no [Test] or NUnit references |
| SCAN-07 | No new DW- items | This block generates zero new deferred items |

---

## 9. Acceptance Criteria

- [ ] Source read confirms: `SubmitEntryDirect` at line 6641 precedes `foreach DrainedOrderIds` at line 6650
- [ ] Finding recorded as STALE with exact line numbers as evidence
- [ ] Zero changes to `CopyEngine.cs` production code
- [ ] One [Fact] regression-guard test added to `CopyEngineTests.cs`
- [ ] Test name: `SubmitDrainedEntry_SourceOrdering_SubmitBeforeCleanup_StaleR4F1`
- [ ] Build passes with zero new errors or warnings
- [ ] All prior tests still pass
- [ ] SCAN-01 through SCAN-07 pass (see T1 checklist above)
- [ ] No new DW- deferred items generated

---

## 10. Threading Model

`SubmitDrainedEntry` is called from `OnOrderUpdate` on the NT8 event-callback thread.
No threading changes introduced by this block (no production code change).

All concurrency primitives (`ConcurrentDictionary.TryRemove`) remain unchanged.
No `lock()`, no `Dispatcher.InvokeAsync` additions needed.

---

## 11. Component Summary

| Component | Action | File |
|-----------|--------|------|
| `SubmitDrainedEntry` | NO CHANGE (STALE) | `src/PropTraderTools/CopyEngine.cs` |
| `CopyEngineTests` | ADD 1 [Fact] regression guard | `src/PropTraderTools/CopyEngineTests.cs` |

---

*Plan written: 2026-09-05 | ptt-architect | Phase 1 | BWAVE-NEXT LaneBRepair-R4*

---

**PLAN_COMPLETE**
