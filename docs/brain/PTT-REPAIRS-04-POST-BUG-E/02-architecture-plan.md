# PTT-REPAIRS-04-POST-BUG-E Architecture Plan

**Epic:** PTT-REPAIRS-04-POST-BUG-E
**Phase:** 1 -- Architecture (corrected per Director scoping decision, escalation 2026-09-06)
**Author:** ptt-architect
**Status:** PLAN_COMPLETE
**Date:** 2026-09-06

> BUG-E fix is CORRECT and IN SCOPE. EvictDedup CYC refactoring is OUT OF SCOPE for this pipeline.
> Ph4a is SOURCE VERIFICATION ONLY -- no new .cs edits.

---

## LANE-SPLIT GATE RESULT: SINGLE-PIPELINE

The BUG-E fix is a single 4-line addition inside `EvictDedup`'s Cancelled branch
(`src/PropTraderTools/CopyEngine.cs` lines 5878-5882). No other method is modified.
`DispatchCopy` line 2555 is intentionally left unchanged (see Section 2).
All affected logic resides in one method in one file. No lane split required.

---

## Section 1 -- BUG-E Root Cause Narrative

### Invariant violated

`_lastLeaderDirection` (`CopyEngine.cs` line 369, `ConcurrentDictionary<string, OrderAction>`)
is keyed by instrument `FullName` and holds the most-recently-dispatched order direction for
that instrument. The reversal-entry guard (`ShouldSkipForReversalGuard`, line 2604) reads this
value to decide whether to skip a follower copy when the user is flat and reversing direction.

The invariant that breaks BUG-E: when a dispatched entry is **cancelled without a fill**, the
direction recorded in `_lastLeaderDirection` is never cleared. The `TryClearLeaderDirectionOnFlat`
path fires only when a real position goes flat. A cancelled entry that was never filled creates
no position, so `TryClearLeaderDirectionOnFlat` never fires.

### Step-by-step failure sequence (from `docs/brain/PTT-REPAIRS-04/direct-edits.md`, Step 3)

| Step | Event | State of `_lastLeaderDirection["MGC DEC26"]` |
|------|-------|----------------------------------------------|
| 1 | User places Buy entry. `DispatchCopy` line 2555 writes unconditionally. | = `Buy` |
| 2 | Buy entry reaches Cancelled. `EvictDedup(orderId, Cancelled)` fires. | = `Buy` (NOT cleared -- BUG) |
| 3 | User is flat (no fill). User places Sell entry. | = `Buy` (stale) |
| 4 | `ShouldSkipForReversalGuard`: `hasLastDirection=true`, `cur=Sell`, `last=Buy`, `followerIsFlat=true` -- `IsReversalToFlatFollower(Sell,Buy,true)` = `(Sell!=Buy) && true` = `true` -- ALL followers skipped, `dispatched=0`. `DispatchCopy` line 2555 still writes `_lastLeaderDirection["MGC DEC26"] = Sell`. | = `Sell` |
| 5 | User places Sell again (second attempt). `last=Sell`, `cur=Sell` -- `IsReversalToFlatFollower(Sell,Sell,true)` = `false` -- NOT a reversal -- dispatches correctly. | = `Sell` |

**User experience:** "must click twice -- copies only on the third attempt."

### Why `EvictDedup` Cancelled branch did not clear the direction (pre-fix)

Pre-fix, the Cancelled branch (lines 5866-5877) cleared only:
- `_entryDispatchedOrders[orderId]`
- `_liveEntryInstruments[cancelledInstrKey]` (value-guarded)
- `_entryInstrKeyByOrderId[orderId]`

`_lastLeaderDirection[instrName]` was never touched.

---

## Section 2 -- Why the Unconditional Write at Line 2555 Is Intentional

```csharp
// src/PropTraderTools/CopyEngine.cs line 2555
_lastLeaderDirection[instr.FullName] = currentAction;
```

This write occurs **after** the follower loop and **unconditionally** -- even when `dispatched == 0`.
This is intentional for the following reason:

When `dispatched == 0` due to the reversal guard (Step 4 above), the leader has still expressed
a direction intent. Recording `currentAction` at this point is what allows Step 5 to work:
the second Sell attempt sees `last=Sell, cur=Sell` (same direction) and is not reversal-blocked.

If the write were conditional on `dispatched > 0`, Step 5 would still have `last=Buy` and would
still be reversal-blocked, requiring a third attempt. The unconditional write is load-bearing
for the self-healing retry path.

**No change to line 2555 is required or planned.**

---

## Section 3 -- Fix Description

### Fix applied (PTT-REPAIRS-04 session, 2026-09-06)

File: `src/PropTraderTools/CopyEngine.cs`
Method: `EvictDedup`
Location: inside the `if (_entryInstrKeyByOrderId.TryRemove(...))` block, Cancelled branch

### Before (lines 5872-5877, pre-fix)

```csharp
if (_entryInstrKeyByOrderId.TryRemove(orderId, out var cancelledInstrKey))
{
    string storedId;
    if (_liveEntryInstruments.TryGetValue(cancelledInstrKey, out storedId)
        && storedId == orderId)
        _liveEntryInstruments.TryRemove(cancelledInstrKey, out _);
}
```

### After (lines 5872-5883, post-fix -- exact quoted lines 5878-5882 added)

```csharp
if (_entryInstrKeyByOrderId.TryRemove(orderId, out var cancelledInstrKey))
{
    string storedId;
    if (_liveEntryInstruments.TryGetValue(cancelledInstrKey, out storedId)
        && storedId == orderId)
        _liveEntryInstruments.TryRemove(cancelledInstrKey, out _);
    // PTT-REPAIRS-04 BUG-E: entry cancelled without fill -- direction record is stale.
    // Clear _lastLeaderDirection so next entry in any direction is not reversal-blocked.
    var pipeIdx = cancelledInstrKey.IndexOf('|');
    if (pipeIdx > 0)
        _lastLeaderDirection.TryRemove(cancelledInstrKey.Substring(0, pipeIdx), out _);
}
```

### Rationale

`cancelledInstrKey` has format `"MGC DEC26|Buy"` (instrument FullName + pipe + action).
`IndexOf('|')` extracts the instrument name prefix at index 9 for "MGC DEC26|Buy".
`TryRemove` on `_lastLeaderDirection` clears the stale direction entry. `TryRemove` is safe
even if the key is absent (returns false, no exception). `pipeIdx > 0` guards against malformed
keys (no pipe found returns -1; pipe at position 0 would produce empty prefix). All operations
are lock-free ConcurrentDictionary operations. JS-021 PASS.

---

## Section 4 -- CYC Accounting (Lizard McCabe, Corrected)

### Methodology

**Lizard McCabe** (full implementation): CYC = 1 + D where D is the count of all decision tokens.
Decision tokens counted: `if`, `else if`, `for`, `foreach`, `while`, `do`, `case`, `catch`,
`&&`, `||`, `?` (ternary). Each occurrence adds 1 to D. Applied consistently to every line.

### Director Scoping Decision (binding)

The pre-existing CYC of `EvictDedup` BEFORE the BUG-E fix was **CYC=12** (D=11 decision tokens).
This already violated JS-013 (limit CYC<=8) before any BUG-E work began.

BUG-E adds ONE new decision token: the `if (pipeIdx > 0)` guard at line 5881.
Post-fix CYC = 1 + 12 = **13** (D=12 decision tokens).

BUG-E contribution: +1 CYC point (from CYC=12 to CYC=13) on an already-violating method.
BUG-E did NOT introduce the JS-013 violation; it added one point to a pre-existing violation.

### Full branch table -- EvictDedup post-fix (lines 5851-5901)

| Line | Token | Expression | D (running) |
|------|-------|------------|-------------|
| 5853 | `if` | `state != OrderState.Filled ...` | D=1 |
| 5854 | `&&` | `&& state != OrderState.Cancelled` | D=2 |
| 5855 | `&&` | `&& state != OrderState.Rejected` | D=3 |
| 5862 | `if` | `state == OrderState.Cancelled` | D=4 |
| 5872 | `if` | `_entryInstrKeyByOrderId.TryRemove(orderId, out var cancelledInstrKey)` | D=5 |
| 5875 | `if` | `_liveEntryInstruments.TryGetValue(cancelledInstrKey, out storedId)` | D=6 |
| 5876 | `&&` | `&& storedId == orderId` | D=7 |
| 5881 | `if` | `pipeIdx > 0` (BUG-E addition) | D=8 |
| 5886 | `if` | `state == OrderState.Filled` | D=9 |
| 5892 | `if` | `_entryInstrKeyByOrderId.TryRemove(orderId, out var filledInstrKey)` | D=10 |
| 5895 | `if` | `_liveEntryInstruments.TryGetValue(filledInstrKey, out storedId)` | D=11 |
| 5896 | `&&` | `&& storedId == orderId` | D=12 |

**Total decision tokens D = 12**

### CYC summary

| State | D | CYC | Violates JS-013? |
|-------|---|-----|-----------------|
| Pre-fix (no line 5881) | 11 | 12 | YES -- pre-existing |
| Post-fix (with line 5881) | 12 | 13 | YES -- BUG-E adds +1 to pre-existing |

**BUG-E contribution: +1 (from CYC=12 to CYC=13).**
**Classification: PRE-EXISTING VIOLATION. BUG-E did NOT introduce JS-013 non-compliance.**

### Deferred resolution

Extraction to satisfy JS-013 is DEFERRED to DEFERRED-4 (see Section 6).
The BUG-E fix lines 5878-5882 are functionally correct and must be preserved during
any future refactoring.

> BUG-E fix is CORRECT and IN SCOPE.
> EvictDedup CYC refactoring is OUT OF SCOPE for this pipeline.

---

## Section 5 -- Jane Street Compliance Table

| Rule | Method | Check | Status |
|------|--------|-------|--------|
| JS-021 no lock() | `EvictDedup` | `TryRemove`, `TryGetValue`, `IndexOf`, `Substring` -- no lock keyword in lines 5878-5882 or anywhere in method | **PASS** |
| JS-001 no throw in dispatch | `EvictDedup` | `TryRemove` returns false on absent key (no throw). `IndexOf` returns -1 on no match (no throw). `Substring(0, pipeIdx)` where `pipeIdx > 0` is always valid. No allocation failures on ConcurrentDictionary removes. | **PASS** |
| JS-013 CYC <= 8 | `EvictDedup` | CYC=13 post-fix (Lizard McCabe). Pre-existing violation: CYC=12 before BUG-E. BUG-E adds +1. Extraction planned in DEFERRED-4. | **DEFERRED** (pre-existing, CYC=13, extraction planned in DEFERRED-4) |
| JS-042 ASCII-only | `EvictDedup` | All new identifiers and string literals in lines 5878-5882 are 7-bit ASCII. `--` in comments = two hyphens (U+002D). No Unicode, no curly quotes, no emoji. | **PASS** |
| JS-025 ConcurrentDictionary | `EvictDedup` | `_lastLeaderDirection.TryRemove(...)` is the correct lock-free atomic remove. | **PASS** |
| JS-002 no null return | `EvictDedup` | Method returns void; no null return site. | **PASS** |

**Overall JS compliance: PLAN_COMPLETE.**
JS-013 is DEFERRED (pre-existing violation, not blocking this formalization pipeline).
All other rules PASS.

> BUG-E fix is CORRECT and IN SCOPE. EvictDedup CYC refactoring is OUT OF SCOPE for this pipeline.

---

## Section 6 -- Deferred Items

### DEFERRED-1: Option A test runner wiring

**Test:** `EvictDedup_CancelledEntry_ClearsLastLeaderDirection`

```
Arrange:
    SetLeaderDirection_ForTest("MGC DEC26", OrderAction.Buy)
    IsLiveEntryBlocked_ForTest("MGC DEC26|Buy", "ord-1", 0.0)
Act:
    EvictDedup("ord-1", OrderState.Cancelled)
Assert:
    HasLeaderDirection_ForTest("MGC DEC26") == false
```

Status: test helper methods (`SetLeaderDirection_ForTest`, `IsLiveEntryBlocked_ForTest`,
`HasLeaderDirection_ForTest`) require InternalsVisibleTo wiring. Deferred to Phase B.

---

### DEFERRED-2: TOCTOU window in value-guarded TryRemove

Carried from PTT-REPAIRS-03-POST. The value-guarded TryRemove pattern
(`TryGetValue` + check + `TryRemove`) has a theoretical TOCTOU window where
a concurrent write could interleave between the check and the remove.

**Assessment:** Acceptable under the NT8 single-threaded `OnOrderUpdate` model.
NT8 guarantees serial delivery of order state transitions for a given orderId.
No concurrent write to the same orderId's entry is possible within the NT8 threading model.

---

### DEFERRED-3: BUG-F

BUG-F (per-instrument clone ATM storage -- scalar `_cloneAtmObject` / `_cloneAtmCache`
replaced with per-instrument ConcurrentDictionaries) is covered in the parallel pipeline
`docs/brain/PTT-REPAIRS-04-POST-BUG-F/`. Full diagnosis and fix in
`docs/brain/PTT-REPAIRS-04/direct-edits.md` Steps 5-7 (BUG-F section). No action here.

---

### DEFERRED-4: EvictDedup CYC refactoring

**Reason:** EvictDedup CYC=13 (post-fix) exceeds JS-013 limit of 8. This is a pre-existing
violation (CYC=12 before BUG-E). Requires extraction of two private helper methods.

**Proposed extraction (Option A):**

```
private void EvictCancelledEntry(string orderId)
    Cancelled branch body extracted from EvictDedup.
    Decision tokens: if(TryRemove instrKey)[+1] + if(TryGetValue)[+1] + &&(==orderId)[+1]
                   + if(pipeIdx>0)[+1]
    D=4, CYC=5. PASS (<=8).

private void EvictFilledEntry(string orderId)
    Filled branch body extracted from EvictDedup.
    Decision tokens: if(TryRemove instrKey)[+1] + if(TryGetValue)[+1] + &&(==orderId)[+1]
    D=3, CYC=4. PASS (<=8).

internal void EvictDedup(string orderId, OrderState state)  [residual after extraction]
    Decision tokens: if(A&&B&&C)[+1+1+1] + if(Cancelled)[+1] + if(Filled)[+1]
    D=5, CYC=6. PASS (<=8).
```

All three resulting methods comply with JS-013. BUG-E fix lines 5878-5882 must be
preserved inside `EvictCancelledEntry` during the refactoring.

**Scope:** This extraction requires new .cs edits and is OUT OF SCOPE for this pipeline.
Requires a separate session and pipeline.

---

## Section 7 -- Post-Fix Evidence

### Functional fix confirmed

From `docs/brain/PTT-REPAIRS-04/direct-edits.md`, Step 6 (BUG-F section, first paragraph):

> "BUG-E confirmed fixed: Zero `[PTT-COPY-GUARD] skip reversal entry` lines in post-fix log."

The symptom (user must click twice, copies only on the third attempt) is resolved.

### Log evidence -- first-attempt dispatch confirmed

`docs/output.md` lines 14-16: First attempt at Sell MGC DEC26 dispatched immediately to
Sim102/103/104 at Accepted state. Confirms initial dispatch on first entry attempt.

```
[PTT-COPY] dispatch: Sell x2 MGC DEC26 -> Sim102 mult=1 mode=Named name=Entry
[PTT-COPY] dispatch: Sell x2 MGC DEC26 -> Sim103 mult=1 mode=Named name=Entry
[PTT-COPY] dispatch: Sell x2 MGC DEC26 -> Sim104 mult=1 mode=Named name=Entry
```

`docs/output.md` lines 43-46: Second dispatch cycle (after cancel/resubmit) also dispatches
on first attempt at Accepted state. No wasted attempt.

`docs/output.md` lines 164-166: Third dispatch cycle confirms consistent fix behavior.

### Zero reversal-guard skips confirmed

```
Select-String -Path docs/output.md -Pattern "PTT-COPY-GUARD" | Measure-Object -Line
```

Result: 0 lines. Zero `[PTT-COPY-GUARD] skip reversal entry` entries in the 211-line
post-fix log. The reversal guard never fired in the entire test session, confirming that
`_lastLeaderDirection` is cleared correctly by EvictDedup on each cancellation.

### 7-scan results (as applied in PTT-REPAIRS-04 session)

| # | Check | Result |
|---|-------|--------|
| 1 | lock() scan | PASS: 0 matches in lines 5878-5882 and full EvictDedup body |
| 2 | non-ASCII | PASS: 0 non-ASCII characters in lines 5878-5882 |
| 3 | CYC spot-check | NOTE: Inline comment at line 5849 states CYC=7 (if-block-only counting, pre-Lizard methodology). Under Lizard McCabe the correct post-fix value is CYC=13. Comment correction is part of DEFERRED-4. |
| 4 | [Fact] | N/A -- no test added in Phase A (BUG-E test deferred to DEFERRED-1) |
| 5 | Build (CopyEngine errors) | PASS: 0 CopyEngine errors |
| 6 | Hard-link sync | PASS: 7/7 files linked |
| 7 | Hard-link count | PASS: 2 (repo + NT8 dir) |

---

## PLAN STATUS: PLAN_COMPLETE

The BUG-E fix (lines 5878-5882) is functionally correct, confirmed working, and fully
formalized in this plan.

JS-013 (CYC) is classified as a **pre-existing violation** that existed before BUG-E
(CYC=12 pre-fix) and was incremented by +1 by the BUG-E guard (CYC=13 post-fix).
The violation is DEFERRED to DEFERRED-4 (EvictDedup extraction). It does NOT block
this formalization pipeline.

All other Jane Street rules (JS-021, JS-001, JS-042, JS-025, JS-002) PASS.

> BUG-E fix is CORRECT and IN SCOPE.
> EvictDedup CYC refactoring is OUT OF SCOPE for this pipeline.
> Ph4a is SOURCE VERIFICATION ONLY -- no new .cs edits.

**Return: PLAN_COMPLETE**
