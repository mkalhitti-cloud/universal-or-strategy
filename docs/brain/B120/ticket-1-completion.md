# B120-T1 Completion Report

**Ticket**: B120-T1  
**Title**: DW-B129 -- Leader Fallback Flatten After B118 PTT-BE Cancel  
**Status**: BUILD_PASS  
**Engineer**: ptt-engineer (Phase 4a)  
**Date**: 2026-08-28  

---

## Files Modified

- `src/PropTraderTools/Features/PttGlobalQuickExit.cs`

## Files Created

- `src/PropTraderTools/Tests/B120Tests.cs`

---

## Implementation Summary

### STEP 1: ExecuteFollowers() extraction

Extracted the follower dispatch block (former lines 92-167 of `Execute()`) into a new
`private void ExecuteFollowers(Account, Position, List<(double,int)>, (int,int), double)` method.
`CopyEngine.Instance` captured internally (same pattern as `ResolveQuickTicks()`).
`Execute()` now calls `ExecuteFollowers(acc, pos, targets, ticks, leaderStop)` in place of the inline block.

### STEP 2: NeedsLeaderFallbackFlatten() helper

Added `internal static bool NeedsLeaderFallbackFlatten(int beCancelCount, int snapshotCount, int posQty)`:
- Returns `true` only when ALL three conditions hold: `beCancelCount > 0 && snapshotCount == 0 && posQty > 0`
- CYC=2, internal static, no lock, no throw, ASCII-only, bool return (no null)

### STEP 3: Flatten guard inserted

Guard inserted in `Execute()` AFTER `SnapshotTargetOrders` (after the DW-B115-DIAG block), BEFORE `ExecuteOne`:
```
if (NeedsLeaderFallbackFlatten(_beCancelCount, targets.Count, pos.Quantity))
{
    NinjaTrader.Code.Output.Process("[PTT-QX-FLATTEN] leader fallback flatten: ...", ...);
    acc.Flatten(pos.Instrument);
    continue;
}
```
When true: log, `acc.Flatten(pos.Instrument)`, `continue` -- `ExecuteOne` skipped.
When false: falls through to `ExecuteOne` unchanged (normal QX path).

### STEP 4: XML summary comment updated

`Execute()` summary updated with B120 DW-B129 annotation and corrected CYC=7 breakdown.

### STEP 5: B120Tests.cs created

3 xUnit [Fact] tests for `NeedsLeaderFallbackFlatten`:
- `Test_NeedsLeaderFallbackFlatten_True_WhenBECancelledAndSnapshotEmpty` -- true path
- `Test_NeedsLeaderFallbackFlatten_False_WhenBECancelCountZero` -- false: no BE cancel
- `Test_NeedsLeaderFallbackFlatten_False_WhenSnapshotHasTargets` -- false: targets present

---

## 7-Scan Results

| Scan | Rule | Command/Check | Result |
|------|------|---------------|--------|
| SCAN-01 | JS-021 no lock() | `Select-String -Pattern "lock\("` | **PASS** -- 0 results |
| SCAN-02 | JS-033 no async void | `Select-String -Pattern "async void"` | **PASS** -- 0 code results (1 comment-only match in header rule list) |
| SCAN-03 | JS-066 CYC <= 8 | Manual count per method | **PASS** -- Execute()=7, ExecuteFollowers()=7, NeedsLeaderFallbackFlatten=2 |
| SCAN-04 | JS-001 no throw | `Select-String -Pattern "throw new"` | **PASS** -- 0 results |
| SCAN-05 | JS-002 no null return | `Select-String -Pattern "internal static bool NeedsLeaderFallbackFlatten"` | **PASS** -- bool return, not null-capable |
| SCAN-06 | ASCII-only | `Select-String -Pattern "[^\x00-\x7F]"` | **PASS** -- 0 non-ASCII matches |
| SCAN-07 | NT8 API | `Select-String -Pattern "acc\.Flatten"` | **PASS** -- `acc.Flatten(pos.Instrument)` at line 103; no Submit() needed |

---

## CYC Counts

| Method | CYC | Decision Points | Limit | Result |
|--------|-----|-----------------|-------|--------|
| `Execute()` | 7 | acc loop(1), follower guard(2), pos loop(3), null/flat(4), DIAG for-loop(5), NeedsLeaderFallbackFlatten guard(6) | 8 | PASS |
| `ExecuteFollowers()` | 7 | rule null(1), follower foreach(2), follower null(3), DIAG pos-foreach(4), DIAG _p guard(5), DIAG for-loop(6), delegate via ExecuteOne(7) | 8 | PASS |
| `NeedsLeaderFallbackFlatten` | 2 | single && chain (3 predicates = 1 compound decision, 1 implicit short-circuit) | 8 | PASS |

---

## Methods Added

- `private void ExecuteFollowers(Account, Position, List<(double Price, int Qty)>, (int t1, int t2), double)` -- CYC=7
- `internal static bool NeedsLeaderFallbackFlatten(int, int, int)` -- CYC=2

---

## MD5 Sync Verification

```
=== PTT SYNC: src/PropTraderTools -> NT8 AddOns ===
  COPIED:  Features\PttGlobalQuickExit.cs
  Copied: 1  |  In-sync: 15  |  Excluded: 46

=== PTT VERIFY: MD5 check every synced file ===
  OK  Features\PttGlobalQuickExit.cs
  (15 other files: OK)

=== SYNC + VERIFY: PASS (16 files confirmed) ===
```

**0 MISMATCH lines** confirmed.

---

## Acceptance Criteria

| Criterion | Result |
|-----------|--------|
| A. `NeedsLeaderFallbackFlatten` present, CYC=2, `internal static` | PASS |
| B. `acc.Flatten(pos.Instrument)` on fallback path with `[PTT-QX-FLATTEN]` log | PASS |
| C. `continue` immediately after `acc.Flatten` -- `ExecuteOne` skipped on fallback | PASS |
| D. `ExecuteFollowers()` extracted as `private void`; `Execute()` calls it in place of inline block | PASS |
| E. `Execute()` CYC <= 8 after fix (CYC=7) | PASS |
| F. Follower path unchanged -- `_fBeCancelCount` separate local var inside `ExecuteFollowers()` | PASS |
| G. Normal QX path unchanged -- `beCancelCount=0` returns false, falls through to `ExecuteOne` | PASS |
| H. No `lock()` in `PttGlobalQuickExit.cs` (JS-021) | PASS |
| I. No `async void` in `PttGlobalQuickExit.cs` (JS-033) | PASS |
| J. `B120Tests.cs` present, xUnit, 3 `[Fact]` tests | PASS |
| K. `ptt-sync-and-verify.ps1` exits 0 MISMATCH (16 files confirmed) | PASS |

---

## Invariants Preserved

| Invariant | Location | Status |
|-----------|----------|--------|
| `CancelPttBeOrders(acc, pos.Instrument)` on leader path | `Execute()` ~L49 | UNCHANGED |
| `WaitForPttBeCancelled(acc, ...)` on leader path | `Execute()` ~L50 | UNCHANGED |
| `NeedsLeaderFallbackFlatten` check + `acc.Flatten` + `continue` | `Execute()` -- new | ADDED |
| `ExecuteOne(acc, ...)` on normal leader path | `Execute()` ~L106 | UNCHANGED |
| `ExecuteFollowers(...)` call replacing inline follower block | `Execute()` after ExecuteOne | ADDED (extraction) |
| Follower `CancelPttBeOrders` + `WaitForPttBeCancelled` | `ExecuteFollowers()` | MOVED (not changed) |
| `ResolveFollowerTargets` method | unchanged | UNCHANGED |
| `SnapshotTargetOrders` dedup by LimitPrice | unchanged | UNCHANGED |

---

**BUILD_PASS**
