# WAVE1-LANE-A Architecture Plan
# CopyEngine.cs God-Method Extraction Epic

**File**: `src/PropTraderTools/CopyEngine.cs`
**Brain dir**: `docs/brain/WAVE1-LANE-A/`
**Date**: 2026-09-07
**Architect**: ptt-architect Phase 1

---

## 1. Rules Catalog Gate Result

**GATE RESULT: PASS**

P0 scan results (`src/PropTraderTools/CopyEngine.cs`):

| Check | Result | Notes |
|-------|--------|-------|
| `lock(` | PASS | 10 matches are all comment text (`// no lock()`). Zero actual lock blocks. |
| `async void ` | PASS | 1 match is a comment only. No actual `async void` methods. |
| `return null;` | EXISTING (pre-existing, not in target methods) | 15 matches exist in file but none are in the 10 target methods. New helpers must NOT add `return null`. |

**Pre-existing `return null` locations** (informational, NOT blocking this epic):
Lines 1259, 1968, 2925, 3030, 3038, 3846, 4045, 4323, 5778, 5800, 5813, 5819, 5903, 7172, 7187.
These are in non-target methods and are JS-002 violations to be tracked separately.

**JS rules confirmed for this plan:**
- JS-021 (no lock): PASS -- all extractions use ConcurrentDictionary/Interlocked
- JS-001 (no throw in hot path): All proposed helpers use void or bool returns with guard clauses
- JS-002 (no return null): All proposed helpers return void, bool, or value types -- never null
- JS-033 (no async void): No async helpers proposed
- SCAN-05 (PTT- prefix): All CreateOrder calls maintain "PTT-" prefixed order names
- ASCII-only: All proposed string literals are ASCII-only
- DateTime.UtcNow: Target methods use DateTime.MaxValue -- no DateTime.Now present

---

## 2. Lane-Split Gate Result

**GATE RESULT: SINGLE-PIPELINE**

Q1. **Are all 10 target methods in the same file within 50 lines of each other?**
    No. All are in `CopyEngine.cs` (class `TrimSignal`) but span lines 1269-6311 -- a ~5000-line spread.
    Each method is isolated in its own region of the file with no physical proximity requirement.

Q2. **Does fixing method A depend on the final design of fixing method B?**
    Yes -- three dependency chains exist:
    - A-01 (RegisterBeRetrySlotIfNeeded) is called by A-04 (MoveStopToBreakEven) twice. A-01 signature must NOT change.
    - A-01 (RegisterBeRetrySlotIfNeeded) calls A-02 (QueueBeRetryFallback). A-02 signature must NOT change.
    - A-07 (FlattenOneAccountLimit) and A-08 (TrimOneAccountLimit) share proposed `SubmitLimitExitOrder` helper -- it must be written once before both are updated.

Q3. **Does each method extraction have standalone value if another is blocked?**
    Yes. Each extraction is a self-contained change in a separate line range.

Q4. **Does each method have an independent verification path?**
    Yes. `lizard src/PropTraderTools/CopyEngine.cs -T cyclomatic_complexity=8` verifies each method CCN independently.

**Sequencing constraints for tickets:**
- T1 (A-01): independent, no blockers
- T2 (A-07 + A-08 shared helper): write shared helper first, then both callers
- T3 advisory (A-09 OnOrderUpdate): after T1 and T2 to avoid merge conflicts

---

## 3. CCN Baseline Table -- Lizard Ground Truth

**CRITICAL FINDING: CCN vs NLOC discrepancy**

The CCN values reported in the task brief (54, 52, 52, 49, 47, 45, 44, 43, 43, 42) are the NLOC
(non-comment lines of code) from lizard CSV column 1 -- NOT cyclomatic complexity (column 2).

Lizard command run: `lizard src/PropTraderTools/CopyEngine.cs -T cyclomatic_complexity=8`

ALL 10 target methods are currently CCN <= 8 (fully compliant with JS standard).

| ID   | Method                        | Reported "CCN" | Actual CCN (lizard col2) | NLOC | Lines     | CCN Status   |
|------|-------------------------------|---------------|--------------------------|------|-----------|--------------|
| A-01 | RegisterBeRetrySlotIfNeeded   | 54            | **8**                    | 54   | 6258-6311 | At limit     |
| A-02 | QueueBeRetryFallback          | 52            | **3**                    | 52   | 1901-1953 | Compliant    |
| A-03 | DispatchCopy                  | 52            | **7**                    | 52   | 2402-2476 | Compliant    |
| A-04 | MoveStopToBreakEven           | 49            | **7**                    | 49   | 6129-6232 | Compliant    |
| A-05 | SendCopy                      | 47            | **6**                    | 47   | 4821-4878 | Compliant    |
| A-06 | SendCopyWithAtm               | 45            | **6**                    | 45   | 4888-4932 | Compliant    |
| A-07 | FlattenOneAccountLimit        | 44            | **8**                    | 44   | 5592-5635 | At limit     |
| A-08 | TrimOneAccountLimit           | 43            | **8**                    | 43   | 5543-5585 | At limit     |
| A-09 | OnOrderUpdate                 | 43            | **8**                    | 43   | 1500-1597 | At limit     |
| A-10 | SubmitBeStopOrder             | 42            | **3**                    | 42   | 1269-1310 | Compliant    |

**Actual CCN violations in CopyEngine.cs (out of task scope -- flagged for separate epic):**

| Method               | Actual CCN | Lines     | Required Action                         |
|----------------------|-----------|-----------|------------------------------------------|
| IsExitSignalName     | **9**     | 2330-2353 | Extract IsKnownExactExitName or HashSet  |
| HasArmingAtmBrackets | **9**     | 5334-5352 | Extract IsArmingOrderState helper        |

---

## 4. Per-Method Extraction Analysis

### A-01 -- RegisterBeRetrySlotIfNeeded (CCN=8, NLOC=54, lines 6258-6311)

**Current CCN:** 8 (at JS limit -- priority extraction target)

**Branch analysis:**
1. `if (isRetry) return;` -- CCN+1
2. `if (targetsCount == 0)` -- CCN+1
3. `if (IsFlat(FindPosition(acc, instrument)))` -- CCN+1
4. `if (!IsFollowerAccount(acc))` -- CCN+1
5. `if (leaderCount <= 0 || targetsCount >= leaderCount || IsFlat(...))` -- CCN+3 (one if + two ||)
base = 1 -- Total: CCN=8

**Code duplication:** The `_pendingFollowerBeSlots[acc.Name] = new PendingFollowerBeSlot(...)` slot
assignment + `NinjaTrader.Code.Output.Process(...)` diagnostic log + `QueueBeRetryFallback(...)` call
appears TWICE (lines 6273-6284 and 6295-6310). Both paths perform identical operations with different delayMs.

**Extraction strategy:**

**Helper 1: `RegisterPendingBeSlot`**
- Absorbs: _pendingFollowerBeSlots assignment + Output.Process diagnostic log + QueueBeRetryFallback call
- Eliminates duplication between the two call paths
- Signature: `private void RegisterPendingBeSlot(Account acc, Instrument instrument, int bufferTicks, int delayMs = 500)`
- Return: `void`
- CCN: base(1) = **CCN=1** (no branches -- pure assignment + log + delegate call)

**Helper 2: `IsBeRetrySlotNeeded`**
- Absorbs: the compound guard `leaderCount <= 0 || targetsCount >= leaderCount || IsFlat(...)`
  expressed as a positive predicate: "should we arm the retry slot on the non-zero-targets path?"
- Signature: `private static bool IsBeRetrySlotNeeded(bool isFollower, int targetsCount, int leaderCount, bool isFlat)`
- Return: `bool` -- JS-002 compliant (no null)
- Logic: `return isFollower && leaderCount > 0 && targetsCount < leaderCount && !isFlat`
- CCN: base(1) + 3 `&&` boolean operators = **CCN=4**

**Post-extraction parent CCN:**
base(1) + isRetry(1) + targetsCount==0(1) + IsFlat flat guard(1) + IsFollowerAccount(1) + IsBeRetrySlotNeeded(1) = **CCN=6**

**Risk/coupling notes:**
- RegisterPendingBeSlot writes `_pendingFollowerBeSlots` BEFORE calling QueueBeRetryFallback. This
  ordering is mandatory: the timer tick TryRemoves the slot (which must exist when the tick fires).
- MoveStopToBreakEven (A-04) calls RegisterBeRetrySlotIfNeeded twice. External signature MUST NOT change.
- QueueBeRetryFallback (A-02) is the callee. A-02 signature MUST NOT change.

**Test [Fact] names:**
```
IsBeRetrySlotNeeded_ReturnsFalse_WhenNotFollowerAccount
IsBeRetrySlotNeeded_ReturnsFalse_WhenLeaderCountZero
IsBeRetrySlotNeeded_ReturnsFalse_WhenTargetsCountEqualsLeaderCount
IsBeRetrySlotNeeded_ReturnsFalse_WhenTargetsCountExceedsLeaderCount
IsBeRetrySlotNeeded_ReturnsFalse_WhenPositionIsFlat
IsBeRetrySlotNeeded_ReturnsTrue_WhenPartialFollowerWithOpenPosition
```

---

### A-02 -- QueueBeRetryFallback (CCN=3, NLOC=52, lines 1901-1953)

**Current CCN:** 3 -- well below JS limit.

**Analysis:** Single cohesive concern: marshal to UI thread, create DispatcherTimer,
attach Tick handler (TryRemove + flat check + MoveStopToBreakEven call), start timer.
NLOC=52 is inflated by the nested lambda and multi-line Output.Process strings.

**Extraction strategy: NO EXTRACTION WARRANTED**

CCN=3 is far below the limit. Splitting the lambda into a named method would require explicit
variable capture and adds structural complexity without compliance benefit.

**Risk/coupling:** Called by RegisterBeRetrySlotIfNeeded (via RegisterPendingBeSlot after extraction).
Signature MUST NOT change. Captured-by-value variables (`capturedAcc`, `capturedInstr`, `capturedBuf`)
prevent closure-over-loop-variable issues -- this pattern must be preserved in any future refactoring.

**Tests:** None required. Behavior covered by RegisterBeRetrySlotIfNeededTests.cs.

---

### A-03 -- DispatchCopy (CCN=7, NLOC=52, lines 2402-2476)

**Current CCN:** 7 -- below JS limit.

**Analysis:** Already a well-factored gate chain + foreach loop. All guard logic has been extracted
into named helpers: IsExitSignalName, IsDispatchTriggerState, IsDispatchableOrderType,
IsLiveEntryBlocked, ShouldSkipFollowerDispatch, ShouldSkipForReversalGuard.

**Extraction strategy: NO EXTRACTION WARRANTED**

**Dead code advisory:** None in this method.

**Risk/coupling:** `_lastLeaderDirection[instr.FullName] = currentAction` MUST remain after the loop.
Write-after-loop ordering is semantically required (all followers in one dispatch see same last direction).

**Tests:** None required.

---

### A-04 -- MoveStopToBreakEven (CCN=7, NLOC=49, lines 6129-6232)

**Current CCN:** 7 -- below JS limit.
**Total file lines:** 104 (6129-6232), but NLOC=49.
**Dead code:** ~55 lines of commented-out legacy DW-B88 code (lines 6198-6231).

**Extraction strategy: NO CCN EXTRACTION WARRANTED. CLEANUP ADVISORY ONLY.**

Primary advisory: Remove the 55 commented-out legacy lines. The DW-B88 rollback window has passed.
Removing them reduces NLOC from 104 to ~49 with zero CCN impact and no behavioral change.

A-04 calls A-01 twice (lines 6179-6196). After A-01 extraction, A-01 external signature is unchanged.
A-04 is not affected.

**Tests:** None required.

---

### A-05 -- SendCopy (CCN=6, NLOC=47, lines 4821-4878)

**Current CCN:** 6 -- below JS limit.

**Analysis:** Single concern: create and submit a copy order. Branches: mode Market check(1) +
Named ternary(1) + try(1) + order null check(1) + catch(1) + base(1) = CCN=6.

**Dead variable finding:**
Line 4839-4841: `string atmTemplate = mode is FollowerAtmMode.Named named ? named.TemplateName : null;`
`atmTemplate` is computed but NEVER referenced again in the method. This is an unused variable
and a latent JS-002 smell (can be null). Advisory: remove lines 4839-4841.

**Extraction strategy: NO CCN EXTRACTION WARRANTED**

Advisory: Remove dead `atmTemplate` local variable (no behavioral impact, eliminates JS-002 smell).

**Tests:** None required.

---

### A-06 -- SendCopyWithAtm (CCN=6, NLOC=45, lines 4888-4932)

**Current CCN:** 6 -- below JS limit.

**Analysis:** Single concern: ATM entry order via StartAtmStrategy. Branches: try(1) + order null(1) +
AtmObject null switch(1) + catch(1) + base(1) = CCN~5-6.

**Extraction strategy: NO EXTRACTION WARRANTED**

Clean, well-structured method. No duplication.

**Tests:** None required.

---

### A-07 -- FlattenOneAccountLimit (CCN=8, NLOC=44, lines 5592-5635)

**Current CCN:** 8 (at JS limit -- extraction target)

**Branch analysis:**
1. `if (pos == null || pos.Quantity == 0)` -- CCN+2 (one if + one ||)
2. `isLong ? OrderAction.Sell : OrderAction.BuyToCover` -- ternary CCN+1
3. `try` -- CCN+1
4. `catch` -- CCN+1
base = 1 -- Total: CCN~6-8

**Structural similarity to A-08:** FlattenOneAccountLimit and TrimOneAccountLimit are near-identical:
same 5 params, same CancelStaleExitOrders call, same FindPosition guard, same isLong ternary,
same ComputeLimitPx, same 12-arg CreateOrder block, same try/catch, same StatusUpdate pattern.

**Differences only:**
- Order name: "PTT-FlattenLimit" vs "PTT-TrimLimit"
- Quantity: `pos.Quantity` (full flatten) vs `(int)Math.Ceiling(pos.Quantity / 2.0)` (trim half)

**Extraction strategy:**

**Shared Helper: `SubmitLimitExitOrder`**
- Absorbs: 12-arg CreateOrder block + order null check + Submit + catch/error StatusUpdate
- Qty and orderName are params (the only structural differences between the two methods)
- StatusUpdate success log stays in each parent (different message format for flatten vs trim)
- Signature: `private void SubmitLimitExitOrder(Account acc, Instrument instrument, OrderAction action, int qty, double limitPx, string orderName)`
- NT8 requirement: arg12 = `(NinjaTrader.Cbi.CustomOrder)null` per NT8-007 -- MUST be preserved in helper
- Return: `void` -- JS-002 compliant
- CCN of helper: base(1) + order null check(1) + try(1) + catch(1) = **CCN=4**

**Post-extraction FlattenOneAccountLimit CCN:**
base(1) + pos null/qty || guard(2) + isLong ternary(1) = **CCN=4**

**Risk/coupling notes:**
- SubmitLimitExitOrder MUST preserve: `OrderEntry.Manual`, `TimeInForce.Gtc`, `DateTime.MaxValue`, `null` oco (arg11), `(NinjaTrader.Cbi.CustomOrder)null` (arg12)
- Write SubmitLimitExitOrder BEFORE modifying either parent method
- StatusUpdate success log stays in parent: FlattenOneAccountLimit logs "flatten-limit qty @ px", TrimOneAccountLimit logs "trim-limit qty @ px"

**Test [Fact] names:**
```
SubmitLimitExitOrder_UsesFullQty_WhenFlattenCalled
SubmitLimitExitOrder_UsesHalfQty_WhenTrimCalled
SubmitLimitExitOrder_UsesSellAction_WhenPositionIsLong
SubmitLimitExitOrder_UsesBuyToCoverAction_WhenPositionIsShort
```

---

### A-08 -- TrimOneAccountLimit (CCN=8, NLOC=43, lines 5543-5585)

**Current CCN:** 8 (at JS limit -- extraction target)

**See A-07 analysis.** TrimOneAccountLimit is the trim variant. Extraction via shared `SubmitLimitExitOrder` helper.

**Trim-specific note:** `int trimQty = (int)Math.Ceiling(pos.Quantity / 2.0)` stays in parent.
This calculation is what distinguishes trim from flatten and is passed as `qty` to SubmitLimitExitOrder.

**Post-extraction TrimOneAccountLimit CCN:**
base(1) + pos null/qty || guard(2) + isLong ternary(1) = **CCN=4**

**Test [Fact] names:** Covered by A-07 shared test set (same helper).

---

### A-09 -- OnOrderUpdate (CCN=8, NLOC=43, lines 1500-1597)

**Current CCN:** 8 (at JS limit -- advisory extraction)

**Branch analysis:** The method is already a well-factored dispatch table.
Actual branches in the method itself:
1. `if (!_isCopyEnabled) return;` -- CCN+1
2. `if (matchedRule == null) return;` -- CCN+1
3. `if (!matchedRule.Value.Enabled) return;` -- CCN+1
4. `if (TryCancelFollowerEntries(...)) return;` -- CCN+1
5. `if (TryDispatchLeaderFlat(...)) return;` -- CCN+1
6. `if (TryHandleDrag(...)) return;` -- CCN+1
base = 1 -- plus 1 for the multi-param TryDispatchLeaderFlat call structure = CCN=8

**Extraction strategy (ADVISORY -- reduces to CCN=5):**

**Optional Helper: `TryResolveEnabledRule`**
- Absorbs: _isCopyEnabled check + matchedRule null check + matchedRule.Enabled check
- Returns bool + out param (never null -- JS-002 compliant)
- Signature: `private bool TryResolveEnabledRule(Order order, out CopyRule rule)`
- Logic: three sequential guards; on fail: `rule = default; return false;` on success: `rule = matchedRule; return true;`
- Return: `bool` -- NEVER null -- JS-002 compliant; `out CopyRule rule` is a value type (struct) -- never null
- CCN of helper: base(1) + isCopyEnabled(1) + matchedRule null(1) + matchedRule.Enabled(1) = **CCN=4**

**Post-extraction OnOrderUpdate CCN:**
base(1) + TryResolveEnabledRule(1) + TryCancelFollowerEntries(1) + TryDispatchLeaderFlat(1) + TryHandleDrag(1) = **CCN=5**

**Priority: ADVISORY.** The method is functional and compliant at CCN=8. Extract only if the wave
requires CCN reduction beyond the threshold. Risk is low but the extraction adds indirection.

**Risk/coupling:** TryResolveEnabledRule MUST preserve gate ORDER: enabled check FIRST, null check SECOND,
rule.Enabled check THIRD. Order matters for correctness (cannot check .Value.Enabled before null check).

**Test [Fact] names:**
```
TryResolveEnabledRule_ReturnsFalse_WhenCopyDisabled
TryResolveEnabledRule_ReturnsFalse_WhenNoMatchingRule
TryResolveEnabledRule_ReturnsFalse_WhenRuleIsDisabled
TryResolveEnabledRule_ReturnsTrue_WhenAllGatesPass
```

---

### A-10 -- SubmitBeStopOrder (CCN=3, NLOC=42, lines 1269-1310)

**Current CCN:** 3 -- well below JS limit.

**Analysis:** CCN=3: base(1) + try(1) + order null check(1). The NLOC=42 is inflated by the
multi-line `NinjaTrader.Code.Output.Process(...)` call (lines 1296-1305, 10 lines of string concat).

**Extraction strategy: NO CCN EXTRACTION WARRANTED**

Advisory: Extract `LogBeStopSubmission(OrderAction dir, int qty, double bePrice, string accName)`
to reduce NLOC to ~30. Zero CCN impact.

**Tests:** None required.

---

## 5. Cross-Method Coupling Table

| Shared Element | Methods That Use It | Risk | Constraint |
|---------------|---------------------|------|------------|
| `_pendingFollowerBeSlots` write | A-01 RegisterBeRetrySlotIfNeeded | LOW | Slot MUST be written before timer starts; enforced in RegisterPendingBeSlot |
| `_pendingFollowerBeSlots` TryRemove | A-02 QueueBeRetryFallback timer tick | LOW | ConcurrentDictionary TryRemove is atomic; no race condition |
| `QueueBeRetryFallback` (callee) | A-01 calls A-02 | MEDIUM | After A-01 extraction, call moves inside RegisterPendingBeSlot. A-02 signature MUST NOT change. |
| `RegisterBeRetrySlotIfNeeded` (callee) | A-04 calls A-01 twice | LOW | A-01 external signature preserved. A-04 unaffected. |
| `SubmitLimitExitOrder` (new shared) | A-07 + A-08 | MEDIUM | Write helper before updating either parent. Both parents agree on signature. |
| `ComputeLimitPx` (existing) | A-07 + A-08 | NONE | Already extracted. No change needed. |
| `CancelStaleExitOrders` (existing) | A-07 + A-08 | NONE | Both call with different names. Stays in each parent. |
| `_lastLeaderDirection` write-after-loop | A-03 DispatchCopy | LOW | Write ordering semantically required. Do not extract this field access. |
| `StatusUpdate?.Invoke` | A-05, A-07, A-08, A-10 | NONE | Null-conditional event. Not shared state. No coupling risk. |
| `FindPosition + IsFlat` | A-01, A-04, A-02 timer | NONE | Read-only helpers. No shared mutation risk. |

**Mutation ordering constraint (critical):**
In RegisterBeRetrySlotIfNeeded -- after extraction to RegisterPendingBeSlot:
1. `_pendingFollowerBeSlots[acc.Name] = new PendingFollowerBeSlot(...)` -- FIRST
2. `NinjaTrader.Code.Output.Process(...)` log -- SECOND
3. `QueueBeRetryFallback(...)` -- THIRD (starts the timer that will TryRemove the slot)
This ordering MUST be preserved in RegisterPendingBeSlot.

---

## 6. Out-of-Scope CCN Violations -- Flagged for Separate Epic

These are the ACTUAL CCN > 8 violations in CopyEngine.cs (confirmed by lizard at -T 8).
They are NOT in the wave task list and MUST be tracked in a follow-up epic.

### IsExitSignalName (CCN=9, lines 2330-2353)

Branch count: 6 sequential `if-return` statements + `IsAtmTargetSignalName` call + base = CCN=9.
Extraction: Replace `name == "Close"` and `name == "Flatten"` exact-match checks with:
```
private static readonly HashSet<string> _exactExitNames =
    new(StringComparer.Ordinal) { "Close", "Flatten" };
```
Use `_exactExitNames.Contains(name)` -- reduces CCN by 1 (2 branches -> 1 lookup).
Post-extraction CCN: **7**. Helper: none needed (HashSet replaces branches).

### HasArmingAtmBrackets (CCN=9, lines 5334-5352)

Branch count: foreach(1) + instr guard(1) + stateActive 5-term OR(4) + continue(1) + IsAtmBracketName(1) + base(1) = CCN=9.
Extraction: Extract `IsArmingOrderState(OrderState s)` to absorb the 5-term OR.
- Signature: `private static bool IsArmingOrderState(OrderState s)`
- Logic: `return s == OrderState.Initialized || s == OrderState.Working || s == OrderState.Submitted || s == OrderState.Accepted || s == OrderState.TriggerPending;`
- Helper CCN: base(1) + 4 `||` = **CCN=5**
- Parent post-extraction CCN: base(1) + foreach(1) + instr guard(1) + IsArmingOrderState(1) + IsAtmBracketName(1) = **CCN=5**

**Tests for future epic:**
```
IsArmingOrderState_ReturnsTrue_WhenOrderWorking
IsArmingOrderState_ReturnsTrue_WhenOrderTriggerPending
IsArmingOrderState_ReturnsFalse_WhenOrderCancelled
IsArmingOrderState_ReturnsFalse_WhenOrderFilled
```

---

## 7. Proposed Helper Signatures

All helpers are `private` (never widened). All in class `TrimSignal` in `CopyEngine.cs`.

```csharp
// === T1: from RegisterBeRetrySlotIfNeeded ===
// Eliminates duplicated slot-registration + log + timer-start block.
private void RegisterPendingBeSlot(
    Account acc,
    Instrument instrument,
    int bufferTicks,
    int delayMs = 500)

// Positive predicate: should we arm the BE retry slot on the partial-targets path?
// Pure static -- no NT8 dependencies. Directly testable via inline mirror.
private static bool IsBeRetrySlotNeeded(
    bool isFollower,
    int targetsCount,
    int leaderCount,
    bool isFlat)

// === T2: shared by FlattenOneAccountLimit + TrimOneAccountLimit ===
// Absorbs the 12-arg CreateOrder + null check + Submit + error log pattern.
// NT8-007: arg12 = (NinjaTrader.Cbi.CustomOrder)null -- REQUIRED.
private void SubmitLimitExitOrder(
    Account acc,
    Instrument instrument,
    OrderAction action,
    int qty,
    double limitPx,
    string orderName)

// === T3 advisory: from OnOrderUpdate ===
// Consolidates the three gate checks into one call.
// out CopyRule rule -- value type struct, never null. JS-002 compliant.
private bool TryResolveEnabledRule(Order order, out CopyRule rule)

// === Out-of-scope CCN fix (future epic): ===
private static bool IsArmingOrderState(OrderState s)
```

---

## 8. Test [Fact] Specifications

**Test file:** `tests/PropTraderTools.Tests/Core/CopyEngineTests.cs`
**Test class:** `CopyEngineTests` (existing -- add to, do not replace)
**Pattern:** Inline predicate mirror (NT8 types not constructible outside NT8 runtime).
**Framework:** xUnit ONLY. NEVER NUnit or MSTest. ASCII-only. No lock. No async void.

### T1 -- IsBeRetrySlotNeeded (pure static bool -- directly mirrored inline)

| [Fact] Name | Input | Expected |
|-------------|-------|----------|
| `IsBeRetrySlotNeeded_ReturnsFalse_WhenNotFollowerAccount` | `(false, 1, 3, false)` | false |
| `IsBeRetrySlotNeeded_ReturnsFalse_WhenLeaderCountZero` | `(true, 1, 0, false)` | false |
| `IsBeRetrySlotNeeded_ReturnsFalse_WhenTargetsCountEqualsLeaderCount` | `(true, 3, 3, false)` | false |
| `IsBeRetrySlotNeeded_ReturnsFalse_WhenTargetsCountExceedsLeaderCount` | `(true, 4, 3, false)` | false |
| `IsBeRetrySlotNeeded_ReturnsFalse_WhenPositionIsFlat` | `(true, 1, 3, true)` | false |
| `IsBeRetrySlotNeeded_ReturnsTrue_WhenPartialFollowerWithOpenPosition` | `(true, 1, 3, false)` | true |

### T1 -- RegisterPendingBeSlot (logic-mirror inline -- NT8 types not constructible outside runtime)

`RegisterPendingBeSlot` is a `void` method that writes `_pendingFollowerBeSlots` and then calls
`QueueBeRetryFallback` (which starts a `DispatcherTimer`). Because `Account`, `Instrument`, and
`DispatcherTimer` cannot be constructed outside the NT8 runtime, the NT8 runtime behavior is not
tested. Instead, the two tests below mirror the **logic expressed in the method body** inline --
verifying the key assignments and the default-parameter contract without invoking NT8 types.

| [Fact] Name | What It Verifies |
|-------------|-----------------|
| `RegisterPendingBeSlot_SlotWrittenWithCorrectKeys_WhenBothDelayVariants` | Inline: `ConcurrentDictionary[acc.Name] = new PendingFollowerBeSlot(acc, instr, bufferTicks)` -- key matches `acc.Name`, value holds correct `acc` + `instr` + `bufferTicks` fields |
| `RegisterPendingBeSlot_DefaultDelayMs_Is500` | Inline: default parameter `delayMs = 500` confirmed by reading the method signature -- guard that the default is never silently changed to another value |

### T2 -- SubmitLimitExitOrder computation (inline mirror of action/qty logic)

| [Fact] Name | What It Verifies |
|-------------|-----------------|
| `SubmitLimitExitOrder_UsesSellAction_WhenPositionIsLong` | `isLong ? OrderAction.Sell : OrderAction.BuyToCover` == Sell when isLong |
| `SubmitLimitExitOrder_UsesBuyToCoverAction_WhenPositionIsShort` | same expression returns BuyToCover when !isLong |
| `FlattenOneAccountLimit_UsesFullPositionQty_WhenCalled` | `qty == pos.Quantity` (not half) |
| `TrimOneAccountLimit_UsesHalfPositionQty_WhenCalled` | `qty == (int)Math.Ceiling(pos.Quantity / 2.0)` |

### T3 (advisory) -- TryResolveEnabledRule

| [Fact] Name | Precondition | Expected |
|-------------|-------------|----------|
| `TryResolveEnabledRule_ReturnsFalse_WhenCopyDisabled` | `_isCopyEnabled = false` | false |
| `TryResolveEnabledRule_ReturnsFalse_WhenNoMatchingRule` | no rule for order's account | false |
| `TryResolveEnabledRule_ReturnsFalse_WhenRuleIsDisabled` | rule exists but `.Enabled = false` | false |
| `TryResolveEnabledRule_ReturnsTrue_WhenAllGatesPass` | enabled + matched + rule.Enabled | true |

---

## 9. Summary Table: Method -> Helpers -> CCN Delta

| ID   | Method                      | Current CCN | Helpers Proposed                         | Priority  | Post-Extraction CCN | CCN Delta |
|------|-----------------------------|------------|------------------------------------------|-----------|---------------------|-----------|
| A-01 | RegisterBeRetrySlotIfNeeded | 8          | IsBeRetrySlotNeeded + RegisterPendingBeSlot | HIGH   | 6                   | -2        |
| A-02 | QueueBeRetryFallback        | 3          | None                                     | SKIP      | 3                   | 0         |
| A-03 | DispatchCopy                | 7          | None                                     | SKIP      | 7                   | 0         |
| A-04 | MoveStopToBreakEven         | 7          | None (advisory: remove commented block)  | CLEANUP   | 7                   | 0         |
| A-05 | SendCopy                    | 6          | None (advisory: remove dead variable)    | CLEANUP   | 6                   | 0         |
| A-06 | SendCopyWithAtm             | 6          | None                                     | SKIP      | 6                   | 0         |
| A-07 | FlattenOneAccountLimit      | 8          | SubmitLimitExitOrder (shared with A-08)  | HIGH      | 4                   | -4        |
| A-08 | TrimOneAccountLimit         | 8          | SubmitLimitExitOrder (shared with A-07)  | HIGH      | 4                   | -4        |
| A-09 | OnOrderUpdate               | 8          | TryResolveEnabledRule                    | ADVISORY  | 5                   | -3        |
| A-10 | SubmitBeStopOrder           | 3          | None (advisory: log extraction)          | CLEANUP   | 3                   | 0         |

**Out-of-scope CCN violations (separate epic REQUIRED):**

| Method               | Current CCN | Proposed Fix                  | Post-Fix CCN |
|----------------------|-----------|-------------------------------|-------------|
| IsExitSignalName     | 9          | HashSet for exact-match names | 7           |
| HasArmingAtmBrackets | 9          | Extract IsArmingOrderState    | 5           |

---

## 10. Threading Model

| Concern | Status | Notes |
|---------|--------|-------|
| lock() in any proposed helper | NONE | ConcurrentDictionary indexer is lock-free. JS-021 compliant. |
| Dispatcher.InvokeAsync | Unchanged | QueueBeRetryFallback retains UI-thread marshal. Not modified. |
| NT8 event thread | Safe | OnOrderUpdate runs on NT8 thread pool. All helpers are synchronous void/bool. |
| _pendingFollowerBeSlots write ordering | Safe | RegisterPendingBeSlot: write slot THEN start timer. Ordering enforced in helper body. |
| async void | None proposed | All helpers are synchronous. |

---

## 11. NT8 API Key Facts

- `Account.CreateOrder()` (12-arg) requires explicit `Account.Submit(new[] { order })` after -- confirmed in all target methods
- `AtmStrategyCreate()` is StrategyBase-only -- NOT used in any target method (correct)
- `AtmStrategyChangeStopTarget()` is StrategyBase-only -- NOT used in any target method (correct)
- `Account.Cancel() + Account.CreateOrder() + Submit()` = correct AddOn bracket-change pattern
- Arg12 of CreateOrder MUST be `(NinjaTrader.Cbi.CustomOrder)null` per NT8-007 -- MUST be preserved in SubmitLimitExitOrder
- `NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy` used only in SendCopyWithAtm (A-06) -- not extracted

---

## 12. Ticket Execution Order

**T1 (HIGH priority):** A-01 RegisterBeRetrySlotIfNeeded
- Write `IsBeRetrySlotNeeded` (private static bool) first
- Write `RegisterPendingBeSlot` (private void) second
- Refactor RegisterBeRetrySlotIfNeeded body to call both
- Verify: lizard reports RegisterBeRetrySlotIfNeeded CCN <= 8 (will be 6)
- Tests: 6 [Fact] in CopyEngineTests.cs for IsBeRetrySlotNeeded + 2 [Fact] for RegisterPendingBeSlot (logic-mirror)

**T2 (HIGH priority):** A-07 + A-08 FlattenOneAccountLimit + TrimOneAccountLimit
- Write `SubmitLimitExitOrder` (private void) first
- Refactor FlattenOneAccountLimit to call it
- Refactor TrimOneAccountLimit to call it
- Verify: lizard reports both methods CCN <= 8 (will be 4 each); helper CCN=4
- Tests: 4 [Fact] in CopyEngineTests.cs for shared extraction logic

**T3 (ADVISORY):** A-09 OnOrderUpdate
- Write `TryResolveEnabledRule` (private bool, out param)
- Refactor OnOrderUpdate to call it for the three gate checks
- Verify: OnOrderUpdate CCN drops from 8 to 5
- Tests: 4 [Fact] for TryResolveEnabledRule

**Cleanup (non-blocking advisory):**
- A-04 MoveStopToBreakEven: remove 55-line commented legacy block (DW-B88 LEGACY)
- A-05 SendCopy: remove 3-line dead `atmTemplate` variable
- A-10 SubmitBeStopOrder: optionally extract LogBeStopSubmission for NLOC reduction only

**Follow-up epic (must be filed separately):**
- IsExitSignalName CCN=9: HashSet lookup refactor
- HasArmingAtmBrackets CCN=9: IsArmingOrderState extraction

---

**PLAN_COMPLETE**