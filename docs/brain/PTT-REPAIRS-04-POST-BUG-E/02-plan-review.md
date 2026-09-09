# PTT-REPAIRS-04-POST-BUG-E Plan Review

**Epic:** PTT-REPAIRS-04-POST-BUG-E
**Phase:** 2 -- Plan Review
**Reviewer:** ptt-plan-reviewer
**Date:** 2026-09-06
**Plan reviewed:** docs/brain/PTT-REPAIRS-04-POST-BUG-E/02-architecture-plan.md

---

## VERDICT: REVIEW_PASS

All 10 checklist items confirmed. JS-013 DEFERRED classification is correct per Director scoping
decision. No blocking violations found.

---

## Checklist Results

### Item 1 -- _lastLeaderDirection field type

**PASS**

Source: `src/PropTraderTools/CopyEngine.cs` lines 369-370:
```csharp
private readonly ConcurrentDictionary<string, OrderAction> _lastLeaderDirection =
    new ConcurrentDictionary<string, OrderAction>();
```
Type is exactly `ConcurrentDictionary<string, OrderAction>`. Plan claim (Section 1, line 27)
`ConcurrentDictionary<string, OrderAction>` at line 369 is **correct**.

---

### Item 2 -- DispatchCopy unconditional write at line 2555

**PASS**

Source: `src/PropTraderTools/CopyEngine.cs` lines 2550-2555:
```csharp
if (dispatched > 0)
    SetLiveEntryDispatched(instrKey, orderId);

// B119: DW-B128 -- record direction dispatched for this instrument.
// Write happens AFTER the loop so all followers in this dispatch see the same lastAction.
_lastLeaderDirection[instr.FullName] = currentAction;
```
Line 2555 `_lastLeaderDirection[instr.FullName] = currentAction;` is **outside** and **after**
the `if (dispatched > 0)` block at line 2550. There is no `dispatched > 0` guard wrapping
line 2555. The write is unconditional. Plan claim (Section 2) is **correct**.

---

### Item 3 -- ShouldSkipForReversalGuard logic

**PASS**

Source: `src/PropTraderTools/CopyEngine.cs` lines 2612-2628:
```csharp
if (!hasLastDirection)
    return false;
bool followerIsFlat = IsFlat(FindPosition(acc, instr))
                      && !HasWorkingEntries(acc, instr);
if (!IsReversalToFlatFollower(currentAction, lastAction, followerIsFlat))
    return false;
```
Guard checks `hasLastDirection` first (line 2612), then calls `IsReversalToFlatFollower`
(line 2616). Consistent with plan Section 1 narrative: `hasLastDirection=true` AND
`IsReversalToFlatFollower(cur, last, followerIsFlat)=true` both required to skip.
Plan claim is **correct**.

---

### Item 4 -- IsReversalToFlatFollower return condition

**PASS**

Source: `src/PropTraderTools/CopyEngine.cs` lines 6008-6015:
```csharp
internal static bool IsReversalToFlatFollower(
    OrderAction currentAction,
    OrderAction lastAction,
    bool followerIsFlat
)
{
    return currentAction != lastAction && followerIsFlat;
}
```
Returns `currentAction != lastAction && followerIsFlat`. Plan Section 1 Step 4 states:
`IsReversalToFlatFollower(Sell,Buy,true) = (Sell!=Buy) && true = true`. Condition is
`cur != last AND isFlat`. **Correct**.

---

### Item 5 -- EvictDedup fix lines present (lines 5878-5882)

**PASS**

Source: `src/PropTraderTools/CopyEngine.cs`:

| Line | Content |
|------|---------|
| 5878 | `// PTT-REPAIRS-04 BUG-E: entry cancelled without fill -- direction record is stale.` |
| 5879 | `// Clear _lastLeaderDirection so next entry in any direction is not reversal-blocked.` |
| 5880 | `var pipeIdx = cancelledInstrKey.IndexOf('|');` |
| 5881 | `if (pipeIdx > 0)` |
| 5882 | `_lastLeaderDirection.TryRemove(cancelledInstrKey.Substring(0, pipeIdx), out _);` |

All three code lines (5880, 5881, 5882) are present. `IndexOf('|')` at 5880, `pipeIdx > 0`
guard at 5881, `TryRemove(...Substring(0, pipeIdx)...)` at 5882. **Correct**.

---

### Item 6 -- "PTT-REPAIRS-04 BUG-E" comment present

**PASS**

Source line 5878:
```csharp
// PTT-REPAIRS-04 BUG-E: entry cancelled without fill -- direction record is stale.
```
Comment is present at line **5878**, immediately above the fix block. **Correct**.

---

### Item 7 -- CYC pre-existing violation count

**PASS**

Independent CYC count performed against actual source lines 5851-5901:

| Line | Token | Expression | D (running) |
|------|-------|-----------|------------|
| 5853 | `if`  | `state != OrderState.Filled` | 1 |
| 5854 | `&&`  | `&& state != OrderState.Cancelled` | 2 |
| 5855 | `&&`  | `&& state != OrderState.Rejected` | 3 |
| 5862 | `if`  | `state == OrderState.Cancelled` | 4 |
| 5872 | `if`  | `_entryInstrKeyByOrderId.TryRemove(orderId, out var cancelledInstrKey)` | 5 |
| 5875 | `if`  | `_liveEntryInstruments.TryGetValue(cancelledInstrKey, out storedId)` | 6 |
| 5876 | `&&`  | `&& storedId == orderId` | 7 |
| 5881 | `if`  | `pipeIdx > 0` (BUG-E addition) | 8 |
| 5886 | `if`  | `state == OrderState.Filled` | 9 |
| 5892 | `if`  | `_entryInstrKeyByOrderId.TryRemove(orderId, out var filledInstrKey)` | 10 |
| 5895 | `if`  | `_liveEntryInstruments.TryGetValue(filledInstrKey, out storedId)` | 11 |
| 5896 | `&&`  | `&& storedId == orderId` | 12 |

**Reviewer's count: D=12, CYC=13 (post-fix). Pre-fix (exclude line 5881): D=11, CYC=12.**

Plan Section 4 claims: pre-fix CYC=12 (D=11), post-fix CYC=13 (D=12). **Matches exactly.**

Classification: JS-013 DEFERRED (pre-existing violation). Plan correctly documents this as
a pre-existing violation (CYC=12 before BUG-E) incremented +1 by BUG-E, with DEFERRED-4
extraction planned. **Classification is correct. JS-013 DEFERRED is ACCEPTABLE per Director
scoping decision.**

---

### Item 8 -- No lock() in EvictDedup (lines 5845-5895)

**PASS**

Full source of `EvictDedup` (lines 5851-5901) contains no `lock(` statement. All state
mutations use `ConcurrentDictionary.TryRemove` (lock-free). JS-021 **PASS**.

---

### Item 9 -- ASCII-only in new lines 5878-5882

**PASS**

Lines 5878-5882 inspected:
- Line 5878: `// PTT-REPAIRS-04 BUG-E: entry cancelled without fill -- direction record is stale.`
  `--` = two U+002D hyphens. No curly quotes, no Unicode, no emoji.
- Line 5879: Plain ASCII comment.
- Line 5880: `var pipeIdx = cancelledInstrKey.IndexOf('|');` -- all 7-bit ASCII.
- Line 5881: `if (pipeIdx > 0)` -- all 7-bit ASCII.
- Line 5882: `_lastLeaderDirection.TryRemove(cancelledInstrKey.Substring(0, pipeIdx), out _);`
  -- all 7-bit ASCII.

JS-042 **PASS**.

---

### Item 10 -- Lane-split gate result present

**PASS**

Plan line 14:
```
## LANE-SPLIT GATE RESULT: SINGLE-PIPELINE
```
Gate result is present. **Correct**.

---

## Summary Table

| # | Checklist Item | Source Lines | Result |
|---|---------------|-------------|--------|
| 1 | `_lastLeaderDirection` is `ConcurrentDictionary<string, OrderAction>` | 369-370 | PASS |
| 2 | Line 2555 unconditional write (no `dispatched>0` guard) | 2550-2555 | PASS |
| 3 | `ShouldSkipForReversalGuard` checks `hasLastDirection` + `IsReversalToFlatFollower` | 2612-2616 | PASS |
| 4 | `IsReversalToFlatFollower` returns `cur != last && isFlat` | 6013-6015 | PASS |
| 5 | Fix lines 5878-5882: `IndexOf('|')` + `pipeIdx>0` + `TryRemove` present | 5878-5882 | PASS |
| 6 | "PTT-REPAIRS-04 BUG-E" comment at line 5878 | 5878 | PASS |
| 7 | CYC pre-fix=12 (D=11), post-fix=13 (D=12); JS-013 DEFERRED (pre-existing) | 5851-5901 | PASS |
| 8 | No `lock()` in EvictDedup | 5851-5901 | PASS |
| 9 | Lines 5878-5882 ASCII-only | 5878-5882 | PASS |
| 10 | `LANE-SPLIT GATE RESULT: SINGLE-PIPELINE` present in plan | plan line 14 | PASS |

---

## JS Rule Verification

| Rule ID | Check | Status |
|---------|-------|--------|
| JS-021 | No `lock()` in EvictDedup fix lines or full method body | PASS |
| JS-001 | No `throw` in EvictDedup (TryRemove returns false on absent key, no throw) | PASS |
| JS-013 | CYC=13 post-fix; pre-existing violation (CYC=12 pre-BUG-E); DEFERRED-4 planned | DEFERRED (acceptable) |
| JS-042 | New lines 5878-5882 are 7-bit ASCII; `--` = two U+002D hyphens | PASS |
| JS-025 | `_lastLeaderDirection.TryRemove(...)` is lock-free ConcurrentDictionary operation | PASS |
| JS-002 | `EvictDedup` returns void; no null return site | PASS |

---

## FINAL VERDICT

**REVIEW_PASS**

All 10 checklist items confirmed against actual source. The plan's factual claims are accurate.
JS-013 DEFERRED classification is correct: the violation is pre-existing (CYC=12 before BUG-E),
the BUG-E fix adds +1 (CYC=13), DEFERRED-4 extraction is documented, and the Director scoping
decision is binding. BUG-E fix is CORRECT and IN SCOPE. Phase 3 (ticket generation) is unlocked.
