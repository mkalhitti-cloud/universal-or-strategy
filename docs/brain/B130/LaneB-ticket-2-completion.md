# B130-LaneB Ticket-2 Completion Report
# DW-B136 Gap B: Order-ID Scoped Cancel for Simultaneous Entries

**Ticket**: B130-LaneB-T2
**Engineer**: ptt-engineer
**Ticket Review**: TICKET_REVIEW_PASS (Cycle 2 confirmed in LaneB-04-ticket-review.md)
**Date**: 2026-09-01
**Verdict**: BUILD_PASS

---

## Summary of Changes

### Files Modified

| File | Change |
|------|--------|
| `src/PropTraderTools/CopyEngine.cs` | Added 1 field + 2 new methods + 3 method modifications |
| `src/PropTraderTools/Tests/B130Tests.cs` | Appended 3 new [Fact] tests (APPEND ONLY) |

### CopyEngine.cs Changes

#### STEP 1: New Field `_followerCopyMap` (~L191)
- **Type**: `internal readonly ConcurrentDictionary<string, ConcurrentBag<Order>>`
- **Inserted**: After `_entryDispatchedOrders` declaration
- **Purpose**: Leader order ID -> follower Order objects dispatched for that leader order
- **JS-021**: ConcurrentDictionary + ConcurrentBag, no lock()
- **Visibility**: `internal readonly` (required for B130Tests.cs test-seam access)

#### STEP 2: New Method `RecordFollowerCopy` (CYC=1) (~L1673)
- **Signature**: `internal void RecordFollowerCopy(string leaderOrderId, Order followerOrder)`
- **Body**: `GetOrAdd` + `ConcurrentBag.Add` -- lock-free, no branches
- **Location**: Inserted after `TryCancelFollowerEntries`, before `TryHandleBracketDrag`

#### STEP 3: New Method `CancelScopedFollowerEntries` (CYC=5) (~L1685)
- **Signature**: `internal void CancelScopedFollowerEntries(string leaderOrderId)`
- **Body**: TryGetValue miss guard -> foreach bag -> OrderState guard -> try/cancel -> catch/log -> TryRemove
- **NT8 API**: `fo.Account.Cancel(new Order[] { fo })` -- AddOn-safe (NT8_ADDON_KNOWLEDGE.md L222)
- **Eviction**: `TryRemove` called AFTER loop -- sole eviction point on cancel path
- **Location**: Inserted after `RecordFollowerCopy`

#### STEP 4: Modified `TryCancelFollowerEntries` (CYC 6->4) (~L1638)
- **Change**: Removed `foreach (var acc in rule.FollowerAccounts)` loop and `CancelOneAccount` call
- **Replacement**: Single call `CancelScopedFollowerEntries(order.OrderId.ToString())`
- **Preserved**: `rule` parameter (call-site stability -- 1 call site at L1361)
- **Preserved**: Single-entry best-practice comment in method body
- **Updated**: Method header comment to CYC=4 (was 6) + DW-B136 Gap B note

#### STEP 5: Modified `SendCopy` (~L2982)
- **Change**: Expanded `if (order != null)` single-statement into braced block
- **Added**: `RecordFollowerCopy(signal.OrderId, order);` after `follower.Submit(new[] { order })`
- **CYC**: Unchanged at 5 (no new branch)

#### STEP 6: Modified `SendCopyWithAtm` (~L3029)
- **Added**: `RecordFollowerCopy(signal.OrderId, order);` after StartAtmStrategy call, before StatusUpdate
- **CYC**: Unchanged at 4 (no new branch)

#### STEP 7: `EvictDedup` -- NOT MODIFIED
- **Verified**: `grep internal void EvictDedup` at L3665 -- body unchanged
- **Body**: Only `_dedupCache.TryRemove` and `_entryDispatchedOrders.Clear`
- **Zero**: `_followerCopyMap` references in EvictDedup body

### B130Tests.cs Changes (APPEND ONLY)

3 new [Fact] tests appended before closing brace. LaneA B130_DW137_* tests untouched.

| Test | Purpose |
|------|---------|
| `B130_DW136_CancelLeaderOrder1DoesNotCancelFollowerCopiesOfOrder2` | Map isolation: cancel id-1 evicts id-1 bag; id-2 bag survives |
| `B130_DW136_SingleEntryPathUnchanged` | Single-entry eviction clean; double-call no-throw |
| `B130_DW136_CancelLeaderOrder2DoesNotEvictLeaderOrder1Bag` | V-01 regression guard: EvictDedup does NOT touch _followerCopyMap |

---

## 7-Scan Results

### SCAN-01: No lock() in new/modified code
**Command**: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "lock\("`
**Result**: 4 matches -- ALL in comments (e.g. "ConcurrentDictionary -- lock-free. No lock() anywhere.")
**Verdict**: PASS -- zero actual `lock()` statements in any new or modified method

### SCAN-02: CYC <= 8 for all new/modified methods
**Command**: Manual McCabe count per method
**Result**:
- `RecordFollowerCopy`: CYC=1 (no branches) ✅
- `CancelScopedFollowerEntries`: CYC=5 (TryGetValue(1)+foreach(2)+OrderState(3)+try(4)+catch(5)) ✅
- `TryCancelFollowerEntries`: CYC=4 (Cancelled(1)+IsAtmBracketName(2)+PTT-prefix compound OR(3)) ✅
- `SendCopy`: CYC=5 (Market mode(1)+Named ternary(2)+try(3)+order null(4)+catch(5)) ✅
- `SendCopyWithAtm`: CYC=4 (try(1)+order null(2)+AtmObject(3)+catch(4)) ✅
- `EvictDedup`: CYC=2 (unchanged) ✅
**Verdict**: PASS -- all <= 8

### SCAN-03: No new async void
**Command**: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "async void "`
**Result**: (no output)
**Verdict**: PASS -- zero results

### SCAN-04: No return null in new methods; catch only logs
**Command**: Inspect `CancelScopedFollowerEntries` catch block
**Result**: catch body = `StatusUpdate?.Invoke("PTT-ScopedCancel error: " + ex.Message)` only; no rethrow, no return null
**Verdict**: PASS

### SCAN-05: ASCII-only in new lines
**Command**: Byte scan of CopyEngine.cs for bytes > 127
**Result**: `SCAN-05 PASS: Zero non-ASCII bytes`
**Verdict**: PASS -- zero non-ASCII bytes in entire file

### SCAN-06: NT8 API correctness (manual review)
**Items verified**:
- `fo.Account.Cancel(new Order[] { fo })` -- AddOn-safe, matches existing `CancelOneAccount` pattern; NT8_ADDON_KNOWLEDGE.md L222 confirmed
- `signal.OrderId` -- `CopySignal.OrderId` field at L499, set at L513
- `order.OrderId.ToString()` key format -- matches existing pattern at L1894, L1684
- No StrategyBase-only API (`AtmStrategyCreate`, `AtmStrategyChangeStopTarget`) used
- No `async`/`await` in new methods
- No `DateTime.Now`
- No hardcoded hex colors
**Verdict**: PASS

### SCAN-07: B130_DW136_* tests compile and pass
**Command**: `dotnet test src/PropTraderTools/ --filter "B130_DW136"`
**Result**:
```
Passed!  - Failed: 0, Passed: 3, Skipped: 0, Total: 3, Duration: 646 ms
```
**Full B130 suite** (including LaneA DW137 tests):
```
Passed!  - Failed: 0, Passed: 5, Skipped: 0, Total: 5, Duration: 2 s
```
**Verdict**: PASS -- 3 new B130_DW136 tests pass; all 5 B130 tests pass; LaneA tests untouched

---

## EvictDedup Invariant Verification

```
internal void EvictDedup(string orderId, OrderState state)   <- L3665
{
    if (
        state != OrderState.Filled
        && state != OrderState.Cancelled
        && state != OrderState.Rejected
    )
        return;

    _dedupCache.TryRemove(orderId, out _);
    if (state == OrderState.Cancelled)
        _entryDispatchedOrders.Clear();
    // DW-B91-A-v2: ...
    // DW-B101: ...
}
```
**Confirmed**: Only `_dedupCache.TryRemove` and `_entryDispatchedOrders.Clear`. Zero `_followerCopyMap` references. ✅

---

## Execution Order Verification

EvictDedup fires at L1277 in OnOrderUpdate. TryCancelFollowerEntries fires at L1361 (84 lines later).
The `_followerCopyMap` entry for the cancelled leader order is preserved through EvictDedup and
consumed by `CancelScopedFollowerEntries` (called from TryCancelFollowerEntries at L1361).
V-01 defect (EvictDedup removing map entry before CancelScopedFollowerEntries can use it) is
prevented by design: EvictDedup does NOT touch `_followerCopyMap`.

---

## Acceptance Criteria Status

| Criterion | Status |
|-----------|--------|
| Leader order #1 cancelled -> only follower copies of #1 cancelled | PASS (Test 1 asserts id-2 bag survives) |
| Leader order #2 copies NOT cancelled when order #1 is cancelled | PASS (Test 1 Assert.True ContainsKey("leader-id-2")) |
| Single-entry path unchanged (no regression) | PASS (Test 2) |
| All 7 scans pass to zero | PASS (all 7 above) |
| EvictDedup body unchanged | PASS (verified L3665-3680, no _followerCopyMap) |
| dotnet build passes with zero errors | PASS (dotnet test includes build; 0 errors) |
| B130_DW137_* tests unchanged | PASS (5/5 B130 tests pass) |
| F5 in NinjaTrader 8 | Pending Director SIM gate |

---

## Verdict: BUILD_PASS
