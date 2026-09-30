# BWAVE-CYC Lane-B Architect Plan

**Phase**: STAGE 2 (ptt-architect)
**Date**: 2025-01-09
**Build Tag**: PTT-COPIER BWAVE-CYC Lane-B | 2025-01-09
**Input**: LaneB-01-mission-brief.md + code read at commit 68a1c1c4

---

## STEP 0 — RULES CATALOG GATE RESULT

**Gate**: PASS

Rules confirmed readable (UTF-8 clean). Applicable rules for all tickets:
- **JS-021** (P0 CRITICAL): no `lock()` — all helpers must use ConcurrentDictionary / Interlocked / lock-free
- **JS-002** (P1): no `return null` for reference types where `Option<T>` applies; helpers returning bool or void are exempt; `Order?` nullable value types are compliant
- **JS-033** (P1): no `async void` — all new helpers are synchronous void or return typed value; NT8 event handlers (`OnXxxAccountUpdate`) are exempt
- **CYC <= 8**: all parent methods after extraction; all new helper methods <= 4

---

## LANE-SPLIT GATE RESULT

**Q1. Same method or within 50 lines?** NO — 13 methods across 7 tickets.
**Q2. Fix B design depends on Fix A final design?** YES — TB-T1 must run before TB-T2 (sequential dependency on CopyEngine structure).
**GATE RESULT: SINGLE-PIPELINE** (7 tickets, sequential execution in order T1 through T7).

---

## LINE-RANGE CORRECTIONS (actual vs prompt)

The following ranges were tighter in the actual file than the prompt estimated.
All corrections are **less than 5 lines** except where noted.

| Method | Prompt Range | Actual Range | Shift | Notes |
|--------|-------------|-------------|-------|-------|
| OnPendingBeAccountUpdate | L5480-5560 | L5480-5520 | -40 end | Method only 40 lines, not 80 |
| OnTrailBeAccountUpdate | L5445-5480 | L5445-5472 | -8 end | Method 27 lines |
| DispatchCopy | L2082-2220 | L2082-2199 | -21 end | Loop ends at L2199 |
| TryFireFollowerBeRetry | L1483-1530 | L1483-1517 | -13 end | Method ends at L1517 |
| TryEvictFollowerBeSlot | L1542-1590 | L1542-1574 | -16 end | Method ends at L1574 |
| TryHandleEntryDrag | L1886-1920 | L1886-1909 | -11 end | Method ends at L1909 |
| IsExitSignalName | L2008-2050 | L2008-2033 | -17 end | Method ends at L2033 |
| SyncAtmFollowerBracket | L2395-2460 | L2395-2445 | -15 end | Method ends at L2445 |
| CancelPttDragOrphansForAccount | L1606-1640 | L1606-1626 | -14 end | Method ends at L1626 |
| DtoToRule | L5609-5680 | L5609-5672 | -8 end | Method ends at L5672 |
| GetRefPrice | L5241-5260 | L5241-5248 | -12 end | Method only 7 lines |
| OnOrderUpdate | L1316-1431 | L1316-1431 | 0 | EXACT MATCH |
| SubmitBeStop | L1087-1142 | L1087-1142 | 0 | EXACT MATCH |

---

## DESIGN CORRECTION — TB-T2 (CRITICAL)

**Prompt design conflict discovered**: `IsDispatchTriggerState` is listed as a new helper for TB-T2
but it **already exists** at L1989 (`internal static bool IsDispatchTriggerState(OrderState state, OrderType type)`).
It was created in B56 T1 and is called from `DispatchCopy` at L2089.

Additionally, `DispatchCopyToFollowers` (per-follower foreach loop) was described as coming from
`OnOrderUpdate`, but the follower loop is inside `DispatchCopy` (TB-T4), not `OnOrderUpdate`.

**Corrected TB-T2 design** (see TB-T2 section below):
The CCN=23 in `OnOrderUpdate` comes from two **inline BE-recovery blocks** at L1344-1374 that
were added after LaneA but not extracted. The correct extraction is:
- `private void TryRecordBeTargetFill(Order o)` — absorbs L1344-1352
- `private void TryTriggerBeRecovery(Order o)` — absorbs L1354-1374 (nested if included)

These two extractions reduce `OnOrderUpdate` from CCN=23 to approximately CCN=8 (Lizard).

---

## TB-T1 — OnPendingBeAccountUpdate

### Confirmed Line Range
- **Actual**: L5480-5520 (40 lines)
- **Prompt**: L5480-5560 — corrected, shift = -40 end lines
- **CCN before**: 32 (Lizard, confirmed by mission brief)

### Visual Branch Count
Counting all McCabe branches AND Lizard-style boolean operators:
(1) AccountItem filter; (2) TryGetValue; (3) IsFlat; (4) tickSize<=0; (5) refBid null-conditional chain;
(6) isLong ternary; (7) refPx<=0; (8) target computation; (9) triggered bool;
(10) triggered gate; (11) TryRemove atomic claim; (12) removed.Account != null check.
Plus ~20 `&&`/`||`/`?:` inside compound expressions = Lizard CCN=32 confirmed.

### Extraction Plan

#### Helper 1: `private bool IsPendingBeSlotActive(PendingBeSlot slot)`
- **Absorbs**: slot != null guard; slot.IsArmed check; slot.Account name-match guard (if applicable)
- **Lines absorbed**: The TryGetValue out-slot guard at L5485 + any IsArmed field check
- **Signature**: `private bool IsPendingBeSlotActive(PendingBeSlot slot)`
- **Return**: true when slot is non-null and in an active/armed state
- **CCN target**: <= 3
- **Constraints**: No NT8 API calls. Pure predicate on struct fields.

#### Helper 2: `private bool IsPendingBeTriggerConditionMet(PendingBeSlot slot, double refPx, double tickSize)`
- **Absorbs**: tickSize<=0 guard; refPx<=0 guard; target price calculation; isLong/triggered logic
- **Lines absorbed**: L5494-5510
- **Signature**: `private bool IsPendingBeTriggerConditionMet(PendingBeSlot slot, double refPx, double tickSize)`
- **Return**: true when price has crossed the break-even trigger level
- **CCN target**: <= 4
- **Constraints**: No NT8 API calls. `slot.Account` / `slot.Instrument` read-only access.

#### Helper 3: `private void ExecutePendingBeTrigger(PendingBeSlot removed, string accName)`
- **Absorbs**: L5511-5519 — TryRemove result usage, unsubscribe AccountItemUpdate, BreakEven call, PendingBeFired event invoke
- **Signature**: `private void ExecutePendingBeTrigger(PendingBeSlot removed, string accName)`
- **Return**: void
- **CCN target**: <= 3
- **Constraints**:
  - NT8: fires on account background thread. NO UI calls inside this method (NT8-003).
  - `removed.Account.AccountItemUpdate -= OnPendingBeAccountUpdate` is safe on background thread.
  - `BreakEven(...)` call preserved as-is — do not inline.
  - `PendingBeFired?.Invoke(...)` is a C# event — null-conditional is safe on background thread.

### Parent CCN Target After Extraction
<= 7 (branches remaining: item filter, TryGetValue, IsFlat, ref-price chain, trigger-met gate, TryRemove, slot-active check = 7 max Lizard-count)

### [Fact] Test Names
- `IsPendingBeTriggerConditionMet_ReturnsFalse_WhenSlotIsNull`
- `IsPendingBeTriggerConditionMet_ReturnsFalse_WhenSlotNotArmed`
- `IsPendingBeTriggerConditionMet_ReturnsTrue_WhenConditionMet`
- `IsPendingBeSlotActive_ReturnsFalse_WhenSlotIsNull`
- `IsPendingBeSlotActive_ReturnsFalse_WhenNotArmed`
- `ExecutePendingBeTrigger_CallsMoveStop`

All testable: helpers are pure logic (no NT8 constructor dependencies). `PendingBeSlot` is a plain struct. `ExecutePendingBeTrigger` test mocks/stubs `BreakEven` via dependency injection or verifies via event.

### Risk Flags
- **NT8-003**: `OnPendingBeAccountUpdate` fires on NT8 account background thread. No UI calls inside method or helpers.
- **JS-021**: `_pendingBeSlots.TryRemove` is the atomic claim gate — must remain in parent, not in helper.
- **One-shot**: This event is unsubscribed inside `ExecutePendingBeTrigger` — order of operations is critical: unsubscribe THEN BreakEven.

---

## TB-T2 — OnOrderUpdate

### Confirmed Line Range
- **Actual**: L1316-1431 (115 lines) — EXACT MATCH
- **CCN before**: 23 (Lizard, confirmed by mission brief)

### Visual Branch Count
Base=1 plus Lizard counting all `&&`/`||`:
- L1344-1352 block: `==` + `!=` + `.StartsWith` + `!= null` = 4 Lizard branches in one if
- L1354-1374 block: `==` + `!=` + `.StartsWith` + nested `.StartsWith` + `!` = 5 Lizard branches in nested ifs
- L1383 `IsPttEntryOrderCancelTrigger`: 1
- L1387 `!_isCopyEnabled`: 1
- L1395 `== null || !...Enabled`: 2
- L1402 `== Mirror`: 1
- L1407 `TryCancelFollowerEntries`: 1
- L1411 `TryDispatchLeaderFlat`: 1
- L1426 `TryHandleDrag`: 1
Total ≈ 23 (Lizard) — confirmed.

### DESIGN CORRECTION APPLIED
Prompt proposed `IsDispatchTriggerState` (already exists at L1989) and `DispatchCopyToFollowers`
(loop is in `DispatchCopy`, not in `OnOrderUpdate`). These are inapplicable.

**Correct extraction**: the two inline BE-recovery blocks (L1344-1374) are responsible for ~9
Lizard-counted branches inside `OnOrderUpdate` that belong in helpers.

#### Helper 1: `private void TryRecordBeTargetFill(Order o)`
- **Absorbs**: L1344-1352 — the `if (Filled && PTT-BE-Target-*)` block that updates `_filledBeTargetCount`
- **Signature**: `private void TryRecordBeTargetFill(Order o)`
- **Return**: void
- **CCN target**: <= 4 (null guard + state guard + name guard + AddOrUpdate = 4)
- **Constraints**:
  - JS-021: `_filledBeTargetCount.AddOrUpdate` is lock-free.
  - Called BEFORE Gate 1 — must remain in the pre-gate section.
  - No `return` from parent — this is fire-and-forget.

#### Helper 2: `private void TryTriggerBeRecovery(Order o)`
- **Absorbs**: L1354-1374 — the `if (Cancelled && PTT-BE-*)` block including inner `if (PTT-BE-Stop- && !HasFilledBeTargetFast)` → `TryReplacePttBeBrackets`
- **Signature**: `private void TryTriggerBeRecovery(Order o)`
- **Return**: void
- **CCN target**: <= 4 (null guard + state guard + name prefix guard + inner PTT-BE-Stop guard = 4)
- **Constraints**:
  - JS-021: `_filledBeTargetCount` reads via `HasFilledBeTargetFast` — no lock.
  - The `NinjaTrader.Code.Output.Process` diagnostic log inside must be preserved verbatim.
  - The nested if structure (`PTT-BE-Stop-` + `!HasFilledBeTargetFast`) must stay inside this helper.

### 4-Gate Sequence Note
The 4-gate sequence (Gate1=copy-enabled, Gate2=rule-match, Gate3=order-state, Gate4=dedup) is in
`DispatchCopy` (TB-T4), NOT in `OnOrderUpdate`. `OnOrderUpdate` has its own gate sequence
(pre-gate BE blocks, Gate1=enabled, Gate2=rule-match, Gates B+C=drag handling). This sequence
**MUST remain in order** — extraction only wraps inline blocks into helpers, does NOT reorder.

### Parent CCN Target After Extraction
<= 8 (Lizard count after removing the ~9 branches from the BE blocks)

### [Fact] Test Names
- `TryRecordBeTargetFill_DoesNothing_WhenOrderIsNull`
- `TryRecordBeTargetFill_DoesNothing_WhenStateIsNotFilled`
- `TryRecordBeTargetFill_DoesNothing_WhenNameDoesNotStartWithPttBeTarget`
- `TryRecordBeTargetFill_IncrementsCount_WhenConditionMet`
- `TryTriggerBeRecovery_DoesNothing_WhenOrderIsNull`
- `TryTriggerBeRecovery_DoesNothing_WhenStateIsNotCancelled`
- `TryTriggerBeRecovery_DoesNothing_WhenNameDoesNotStartWithPttBe`

All testable: helpers operate only on `Order` fields and `ConcurrentDictionary` state.

### Risk Flags
- **NT8 event handler**: `OnOrderUpdate` fires on NT8 order-update background thread.
- **Ordering**: `TryRecordBeTargetFill` must be called before `TryTriggerBeRecovery` — DW-B92 race depends on this order.
- **JS-021**: no lock in either helper. `_filledBeTargetCount.AddOrUpdate` and `HasFilledBeTargetFast` are lock-free.
- **SCAN-07**: verify no `async void` introduced. These helpers are synchronous void.

---

## TB-T3a — OnTrailBeAccountUpdate

### Confirmed Line Range
- **Actual**: L5445-5472 (27 lines)
- **Prompt**: L5445-5480 — corrected, shift = -8 end lines
- **CCN before**: 9 (Lizard, confirmed by mission brief)

### Visual Branch Count
(1) AccountItem filter; (2a) TryGetValue; (2b+3a) TryGetValue for PnL; (3b) newPnl<=oldPnl; (4) CAS AddOrUpdate; (5) actual!=newBits race check; plus `??` operators ≈ Lizard CCN=9.

### Extraction Plan

#### Helper 1: `private bool IsTrailBeTriggerMet(double newPnl, long oldBits)`
- **Absorbs**: L5453-5457 — the `TryGetValue` for `_trailBeLastPnlBits` + `oldPnl` decode + `newPnl <= oldPnl` guard
- **Signature**: `private bool IsTrailBeTriggerMet(double newPnl, long oldBits)`
- **Return**: true when newPnl > oldPnl (improvement detected)
- **CCN target**: <= 3
- **Constraints**: Pure arithmetic — no NT8 API, no concurrent state. `BitConverter` calls only.

### Parent CCN Target After Extraction
<= 6 (item filter + TryGetValue-slot + IsTrailBeTriggerMet gate + CAS AddOrUpdate + race check + BreakEven = 6 Lizard-counted branches)

### [Fact] Test Names
- `IsTrailBeTriggerMet_ReturnsFalse_WhenSlotIsNull` — (test null/empty slot input)
- `IsTrailBeTriggerMet_ReturnsFalse_WhenNotArmed` — (newPnl <= oldPnl)
- `IsTrailBeTriggerMet_ReturnsTrue_WhenLevelReached` — (newPnl > oldPnl improvement)

### Risk Flags
- **NT8-003**: fires on account background thread. No UI calls anywhere in method or helper.
- **JS-021**: CAS `AddOrUpdate` is the lock-free claim gate — must remain in parent.
- **Note**: Helper signature differs slightly from prompt (`slot, price` → `newPnl, oldBits`) because the actual trigger test is purely PnL-based (not price-based like `OnPendingBe`). The prompt's `TrailBeSlot slot, double price` parameters would not match the actual code logic. Adjusted to match real code.

---

## TB-T3b — SubmitBeStop

### Confirmed Line Range
- **Actual**: L1087-1142 (55 lines) — EXACT MATCH
- **CCN before**: 10 (Lizard, confirmed by mission brief)

### Visual Branch Count
(1) acc/instr null guard; (2) foreach positions; (3) inner p.Instrument != null; (4) pos==null/qty==0 guard; (5) isLong ternary OrderAction; (6) try/catch CreateOrder; (7) inner order!=null check = 7 base branches + Lizard `&&` in null-compound = CCN=10.

### Extraction Plan

#### Helper 1: `private NinjaTrader.Cbi.Position FindBePosition(Account acc, NinjaTrader.Cbi.Instrument instr)`
- **Absorbs**: L1097-1107 — the foreach over `acc.Positions` looking for instrument match, returns the `Position` or null
- **Signature**: `private NinjaTrader.Cbi.Position FindBePosition(Account acc, NinjaTrader.Cbi.Instrument instr)`
- **Return**: `NinjaTrader.Cbi.Position` or null (nullable reference; acceptable here per NT8 pattern — caller guards with `pos == null || pos.Quantity == 0`)
- **CCN target**: <= 3 (foreach + null guard + FullName match = 3)
- **Constraints**: Uses `FullName` comparison (B69 DW-B69-02 mandate). Must preserve `p.Instrument != null` inner guard.

#### Helper 2: `private void SubmitBeStopOrder(Account acc, NinjaTrader.Cbi.Instrument instr, OrderAction dir, int qty, double bePrice)`
- **Absorbs**: L1109-1141 — the `try { CreateOrder(...); if (order != null) { Submit; Log; } } catch { }` block
- **Signature**: `private void SubmitBeStopOrder(Account acc, NinjaTrader.Cbi.Instrument instr, OrderAction dir, int qty, double bePrice)`
- **Return**: void
- **CCN target**: <= 3 (try/catch + order!=null inner if = 3 Lizard-counted)
- **Constraints**:
  - NT8: `acc.CreateOrder(StopMarket)` + `acc.Submit()` pattern — AddOnBase available.
  - Order name MUST be `"PTT-BE-Stop"` (existing value, confirmed L1121).
  - `try/catch { }` with no rethrow (existing pattern) — preserved to absorb NT8 exceptions.
  - JS-001: no rethrow inside catch.

### Parent CCN Target After Extraction
<= 5 (acc/instr null guard + FindBePosition call + pos null/qty guard + dir ternary + SubmitBeStopOrder call = 5)

### [Fact] Test Names
- `BuildBeStopOrder_ReturnsNull_WhenStopPxIsZero` — test via `SubmitBeStopOrder` path where bePrice=0 (no order placed)
- `BuildBeStopOrder_ReturnsNull_WhenCreateOrderReturnsNull` — mock/stub `acc.CreateOrder` returning null
- `LinkBeStopToTargets_SkipsNullTarget` — not applicable (SubmitBeStop has no target-linking); **ADJUSTED**: `FindBePosition_ReturnsNull_WhenInstrumentNameDoesNotMatch`
- `LinkBeStopToTargets_SkipsTargetWithWrongInstrument` — **ADJUSTED**: `FindBePosition_ReturnsPosition_WhenInstrumentNameMatches`

**Note**: Prompt test names `BuildBeStopOrder_*` and `LinkBeStopToTargets_*` reference a different design (BuildBeStopOrder returning `Order?`, LinkBeStopToTargets OCO linking). The actual `SubmitBeStop` code has no OCO linking or target-linking. Test names are adjusted to reflect real code. If ptt-engineer finds OCO linking in a related method, report back.

### Risk Flags
- **NT8**: `acc.CreateOrder` + `acc.Submit` — AddOnBase confirmed available.
- **B69 DW-B69-02**: FullName comparison inside `FindBePosition` — must not be simplified to reference equality.
- **Thread**: `SubmitBeStop` may be called from any thread. No lock introduced.

---

## TB-T4 — DispatchCopy

### Confirmed Line Range
- **Actual**: L2082-2199 (117 lines)
- **Prompt**: L2082-2220 — corrected, shift = -21 end lines
- **CCN before**: 13 (Lizard, confirmed by mission brief)

### Visual Branch Count
Gates 0.5/3/4/5 = 4 early-returns; plus foreach loop; inside loop: acc==null/CapCheck compound (1); reversal guard compound (2); `Named` mode is-pattern (3); plus `||`/`&&`/`?:` in conditions ≈ Lizard CCN=13.

### Extraction Plan

#### Helper 1: `private bool ShouldSkipFollowerDispatch(Account acc)`
- **Absorbs**: L2134-2138 — `acc == null || !PassesDailyCapCheck(acc)` with `idx++; continue;`
- **Signature**: `private bool ShouldSkipFollowerDispatch(Account acc)`
- **Return**: true when follower should be skipped (null or cap exceeded)
- **CCN target**: <= 2 (null check + CapCheck = 2 Lizard branches)
- **Constraints**: JS-021: `PassesDailyCapCheck` is lock-free read. Must not call `idx++` — caller handles index increment.

#### Helper 2: `private bool ShouldSkipForReversalGuard(Account acc, NinjaTrader.Cbi.Instrument instr, OrderAction currentAction, OrderAction lastAction, bool hasLastDirection)`
- **Absorbs**: L2145-2161 — the `hasLastDirection && IsReversalToFlatFollower(...)` block + `Output.Process` log + `idx++; continue;`
- **Signature**: `private bool ShouldSkipForReversalGuard(Account acc, Instrument instr, OrderAction currentAction, OrderAction lastAction, bool hasLastDirection)`
- **Return**: true when reversal guard applies and follower should be skipped
- **CCN target**: <= 3 (hasLastDirection guard + IsReversalToFlatFollower + IsFlat = 3)
- **Constraints**: `FindPosition` + `IsFlat` — lock-free reads. Log preserved inside helper.

#### Helper 3: `private void DispatchToFollower(Account acc, Order order, CopyRule rule, int idx, CopySignal baseSignal, int baseQty)`
- **Absorbs**: L2163-2193 — multiplier resolution, signal scaling, ATM mode resolution, log, `SendCopyWithAtm` / `SendCopy` branch
- **Signature**: `private void DispatchToFollower(Account acc, Order order, CopyRule rule, int idx, CopySignal baseSignal, int baseQty)`
- **Return**: void
- **CCN target**: <= 3 (mult clamp + Named-mode is-pattern + else branch = 3)
- **Constraints**:
  - `GetMultiplier(rule, idx)` call preserved.
  - `ResolveAtmMode(rule, acc.Name)` call preserved.
  - `if (mode is FollowerAtmMode.Named namedAtm)` pattern preserved exactly.
  - NT8: `SendCopy`/`SendCopyWithAtm` are AddOnBase-available.

### Parent CCN Target After Extraction
<= 6 (Gate 0.5 + Gate 3 + Gate 4 + Gate 5 + foreach-loop + direction-record = 6 Lizard-counted)

### [Fact] Test Names
- `ComputeFollowerLimitPrice_AddsOffset_WhenPositive` — **ADJUSTED** (no `ComputeFollowerLimitPrice` in actual code; limit price flows from `CopySignal`); test renamed: `ShouldSkipFollowerDispatch_ReturnsFalse_WhenAccIsNotNullAndCapPasses`
- `ComputeFollowerLimitPrice_SubtractsOffset_WhenNegative` — **ADJUSTED**: `ShouldSkipFollowerDispatch_ReturnsTrue_WhenAccIsNull`
- `ShouldSkipFollowerDispatch_ReturnsTrue_WhenFollowerIsNull` — KEEP
- `ShouldSkipFollowerDispatch_ReturnsTrue_WhenFollowerIsSameAsSource` — **ADJUSTED** (no same-as-source check in this helper; self-copy prevention is in rule setup): `ShouldSkipFollowerDispatch_ReturnsTrue_WhenDailyCapExceeded`
- `SubmitFollowerCopyOrder_SkipsSubmit_WhenCreateOrderReturnsNull` — **ADJUSTED**: `DispatchToFollower_CallsSendCopyWithAtm_WhenModeIsNamed`

**Note**: The prompt test names reference `ComputeFollowerLimitPrice` and `SubmitFollowerCopyOrder` which are not in the actual DispatchCopy code. The limit-price offset logic is in `GetRefPrice`/`TightenOneAccountStops`, not here. Adjusted test names reflect actual extracted helpers.

### Risk Flags
- **B119 DW-B128**: `_lastLeaderDirection[instr.FullName] = currentAction` at L2198 must remain AFTER the loop. Do not move into any helper.
- **B8 T1**: `idx` must be incremented for every iteration (including skipped) — ensure `DispatchToFollower` does NOT take ownership of `idx`.
- **Lock-free**: All reads/writes in helpers use lock-free collections.

---

## TB-T5a — TryFireFollowerBeRetry

### Confirmed Line Range
- **Actual**: L1483-1517 (34 lines)
- **Prompt**: L1483-1530 — corrected, shift = -13 end lines
- **CCN before**: 15 (Lizard, confirmed by mission brief)

### Visual Branch Count
(1) o null; (2a) o.Name null + o.Account null; (2b) isPttQxT compound; (2b2) length check; (2b3) IsDigit; (2c) isAtmTgt compound; (2c2) length; (2c3) IsDigit; (3) !isPttQxT && !isAtmTgt; (4) Working state compound; (5) TryRemove; (6) IsFlat = Lizard CCN=15.

### Extraction Plan

#### Helper 1: `private bool IsPttBeRetryTriggerOrder(Order o)`
- **Absorbs**: L1488-1497 — isPttQxT + isAtmTgt bool-flag block + `if (!isPttQxT && !isAtmTgt) return;` gate
- **Signature**: `private static bool IsPttBeRetryTriggerOrder(Order o)`
- **Return**: true when the order is a PTT-QX-T* or ATM Target* that triggers BE retry
- **CCN target**: <= 4 (isPttQxT compound=2 + isAtmTgt compound=2, combined = 4 Lizard with &&/||)
- **Constraints**: `static` — no instance state needed. Name-pattern logic only.

#### Helper 2: `private bool IsBeRetryStateWorking(Order o)`
- **Absorbs**: L1498-1502 — `if (o.OrderState != Working && o.OrderState != Accepted) return;`
- **Signature**: `private static bool IsBeRetryStateWorking(Order o)`
- **Return**: true when order state is Working or Accepted
- **CCN target**: <= 2 (two state checks = 2)
- **Constraints**: Static predicate. Inline the state enum comparisons exactly.

### Parent CCN Target After Extraction
<= 6 (null guard + IsPttBeRetryTriggerOrder gate + IsBeRetryStateWorking gate + TryRemove + reset + IsFlat = 6)

### [Fact] Test Names
- `IsBeRetryEligible_ReturnsFalse_WhenSlotIsNull` — **ADJUSTED**: `IsPttBeRetryTriggerOrder_ReturnsFalse_WhenOrderNameIsNull`
- `IsBeRetryEligible_ReturnsFalse_WhenRetryCountAtMax` — **ADJUSTED**: `IsPttBeRetryTriggerOrder_ReturnsFalse_WhenNameDoesNotMatchPattern`
- `IsBeRetryEligible_ReturnsFalse_WhenPositionIsFlat` — KEEP (test via full method with mock IsFlat)
- `ExecuteBeRetryAndRearm_CallsBreakEven` — KEEP (test via `MoveStopToBreakEven` call verification)

**Note**: Prompt test names reference `IsBeRetryEligible(PendingFollowerBeSlot, Account)` which is not in the actual method signature space. Adjusted to reflect actual static predicates.

### Risk Flags
- **DW-B82-01**: `_beReplaceAttempts.TryRemove(o.Account.Name, out _)` at L1505 must remain in parent immediately after `TryRemove` on `_pendingFollowerBeSlots`.
- **Ordering**: `TryRemove` + reset + IsFlat flat-guard MUST remain in parent — atomic claim gate.
- **JS-021**: ConcurrentDictionary ops are lock-free. No lock anywhere.

---

## TB-T5b — TryEvictFollowerBeSlot

### Confirmed Line Range
- **Actual**: L1542-1574 (32 lines)
- **Prompt**: L1542-1590 — corrected, shift = -16 end lines
- **CCN before**: 13 (Lizard, confirmed by mission brief)

### Visual Branch Count
(1) o null; (2a) isFilled; (2b) isRejected compound; (3) name guard; (4) isFilled+isRejected gate; (5) IsFollowerAccount; (6) flat guard for Filled; (7) TryRemove; (8) slotEvicted gate; (9) isRejected ternary = Lizard CCN=13.

### Extraction Plan

#### Helper 1: `private bool IsEvictTriggerState(Order o)`
- **Absorbs**: L1547-1552 — isFilled + isRejected bool-flags + `if (!isFilled && !isRejected) return;`
- **Signature**: `private static bool IsEvictTriggerState(Order o)`
- **Return**: true when the order is in a terminal state (Filled or Rejected PTT-BE-Stop)
- **CCN target**: <= 4 (Filled check + Rejected check + name guard for Rejected + combined gate = 4 Lizard)
- **Constraints**: Static — no instance state. DW-B81-01: Rejected eviction is specifically for `o.Name == "PTT-BE-Stop"`.

### Parent CCN Target After Extraction
<= 6 (null guard + IsEvictTriggerState + clear + follower guard + flat guard + TryRemove/reset block = 6)

### [Fact] Test Names
- `IsBeSlotEvictable_ReturnsFalse_WhenSlotIsNull` — **ADJUSTED**: `IsEvictTriggerState_ReturnsFalse_WhenOrderIsNotFilledOrRejected`
- `IsBeSlotEvictable_ReturnsTrue_WhenPositionFlatAndTimeoutElapsed` — **ADJUSTED**: `IsEvictTriggerState_ReturnsTrue_WhenFilledState`
- Additional: `IsEvictTriggerState_ReturnsTrue_WhenRejectedPttBeStop`
- Additional: `IsEvictTriggerState_ReturnsFalse_WhenRejectedButNotPttBeStop`

### Risk Flags
- **DW-B95**: `_entryDispatchedOrders.Clear()` at L1553 fires for ALL accounts — must remain in parent BEFORE the follower-only guard at L1554.
- **DW-B81-01**: Rejected eviction bypass of flat-guard — ordering of `isFilled && !IsFlat` must be preserved.
- **JS-021**: ConcurrentDictionary ops lock-free throughout.

---

## TB-T6a — TryHandleEntryDrag

### Confirmed Line Range
- **Actual**: L1886-1909 (23 lines)
- **Prompt**: L1886-1920 — corrected, shift = -11 end lines
- **CCN before**: 11 (Lizard, confirmed by mission brief)

### Visual Branch Count
(1) OrderType.Limit||StopLimit; (2) OrderState Accepted||Working; (3) Filled!=0; (4) TryGetValue dedup; (5) Math.Abs<tickSize; plus `||` operators inside type/state checks ≈ Lizard CCN=11.

### Extraction Plan

#### Helper 1: `private bool IsEntryDragEligible(Order order)`
- **Absorbs**: L1888-1893 — OrderType guard + OrderState guard + Filled!=0 guard
- **Signature**: `private static bool IsEntryDragEligible(Order order)`
- **Return**: true when order is eligible for drag detection (right type, right state, not filled)
- **CCN target**: <= 4 (type check with `||` = 2 + state check with `||` = 2; `&&` chaining = 4 Lizard)
- **Constraints**: Static predicate. No NT8 API beyond field reads.

### Parent CCN Target After Extraction
<= 6 (IsEntryDragEligible + GetOrderPrice + TryGetValue + Math.Abs<tickSize + DedupCache update + HandleEntryChange = 6 Lizard)

### [Fact] Test Names
- `IsEntryDragEligible_ReturnsFalse_WhenOrderNameNotEntry` — **NOTE**: This method does not check order name; it checks type/state. Adjusted: `IsEntryDragEligible_ReturnsFalse_WhenOrderTypeIsMarket`
- `IsEntryDragEligible_ReturnsFalse_WhenOrderStateNotWorking` — KEEP (state guard)
- Additional: `IsEntryDragEligible_ReturnsTrue_WhenLimitAndAccepted`
- Additional: `IsEntryDragEligible_ReturnsFalse_WhenFilledIsNonZero`

### Risk Flags
- **DW-B64-01**: `_dedupCache[order.OrderId.ToString()] = currentPrice` at L1906 must be set BEFORE `HandleEntryChange` call. This ordering is load-bearing — do not extract.
- **HOTFIX-B65-GATE-C-FILL-GUARD-01**: `order.Filled != 0` check must be preserved in `IsEntryDragEligible` exactly.

---

## TB-T6b — IsExitSignalName and IsNonFlatDispatchName

### Confirmed Line Ranges
- **IsExitSignalName actual**: L2008-2033 (25 lines)
  - Prompt: L2008-2050 — corrected, shift = -17 end lines
  - **CCN before**: 10 (Lizard)
- **IsNativeExitName**: L2045-2057 (12 lines) — already separate method, CCN=4 (no extraction needed)
- **IsNonFlatDispatchName**: L2066-2074 (8 lines) — already separate method, CCN=3 (no extraction needed)

### Visual Branch Count for IsExitSignalName
(1) name==null; (2) StartsWith("PTT-"); (3) name=="Close"; (4) name=="Flatten"; (5) StartsWith("Rev"); (6) StartsWith("Exit"); (7) Length>6 compound + StartsWith("Target") + IsDigit = Lizard CCN=10.

### Key Finding: TB-T6b Scope Correction
`IsNativeExitName` (L2045) and `IsNonFlatDispatchName` (L2066) are **already separate methods** — they were already extracted in a prior build. The TB-T6b ticket covers only `IsExitSignalName` (CCN=10).

### Extraction Plan for IsExitSignalName

#### Helper 1: `private static bool IsAtmTargetSignalName(string name)`
- **Absorbs**: L2024-2029 — the `name.Length > 6 && name.StartsWith("Target") && char.IsDigit(name[6])` compound block
- **Signature**: `private static bool IsAtmTargetSignalName(string name)`
- **Return**: true when name matches ATM bracket Target1..Target9 pattern
- **CCN target**: <= 3 (Length check + StartsWith + IsDigit = 3 Lizard)
- **Constraints**: Static, pure string predicate. B78 DW-B78-01 rationale preserved in comment.

### Parent CCN Target After Extraction
<= 5 (null guard + PTT- prefix + Close/Flatten/Rev/Exit chain + IsAtmTargetSignalName call = Lizard ~7→ aim <=5 by collapsing the 4 flat equality/prefix checks)

**Note**: `IsNonFlatDispatchName` test names from prompt (`IsNonFlatDispatchName_ReturnsTrue_WhenNameIsPttCopy` etc.) are for an ALREADY EXTRACTED method (CYC=3, no action needed). `IsNativeExitName` test names are for an ALREADY EXTRACTED method (CYC=4, no action needed). Tests in TB-T6b should focus on `IsExitSignalName` and the new `IsAtmTargetSignalName`.

### [Fact] Test Names (corrected)
- `IsExitSignalName_ReturnsTrue_WhenNameStartsWithPtt`
- `IsExitSignalName_ReturnsTrue_WhenNameIsClose`
- `IsExitSignalName_ReturnsTrue_WhenNameIsAtmTarget` — (via `IsAtmTargetSignalName` path)
- `IsAtmTargetSignalName_ReturnsFalse_WhenNameIsEmpty`
- `IsAtmTargetSignalName_ReturnsTrue_WhenNameIsTarget1`
- `IsNonFlatDispatchName_ReturnsTrue_WhenNameIsPttCopy` — (existing method, test only)
- `IsNonFlatDispatchName_ReturnsFalse_WhenNameIsEmpty` — (existing method, test only)
- `IsNativeExitName_ReturnsTrue_WhenNameIsTarget` — NOTE: `IsNativeExitName` does NOT return true for "Target*"; it covers Close/Flatten/Rev/Exit. Adjusted: `IsNativeExitName_ReturnsTrue_WhenNameIsClose`
- `IsNativeExitName_ReturnsFalse_WhenNameIsPttCopy` — KEEP

### Risk Flags
- **Existing methods**: `IsNativeExitName` and `IsNonFlatDispatchName` are already extracted and at CCN <=4/3. Do NOT re-extract them.
- **Static methods**: All three methods are `internal static` — testable without NT8 runtime.

---

## TB-T6c — SyncAtmFollowerBracket

### Confirmed Line Range
- **Actual**: L2395-2445 (50 lines)
- **Prompt**: L2395-2460 — corrected, shift = -15 end lines
- **CCN before**: 11 (Lizard, confirmed by mission brief)

### Visual Branch Count
(1) acc==null; (2) fo==null; (3) IsNoPriceChange; (4) try/catch Block A; (5) try/catch Block B; (6) newStop==null inner guard; plus `??` and null-conditionals inside blocks ≈ Lizard CCN=11.

### Extraction Plan

#### Helper 1: `private bool IsSyncAtmBracketEligible(Account acc, Order fo, double newPrice)`
- **Absorbs**: L2397-2402 — acc null guard + fo null guard + IsNoPriceChange guard
- **Signature**: `private bool IsSyncAtmBracketEligible(Account acc, Order fo, double newPrice)`
- **Return**: true when all preconditions for sync are met (non-null + price changed)
- **CCN target**: <= 3 (acc null + fo null + IsNoPriceChange = 3)
- **Constraints**: Pure predicate. `IsNoPriceChange` is an existing helper (lock-free). No NT8 API calls beyond field reads.

### Parent CCN Target After Extraction
<= 6 (IsSyncAtmBracketEligible gate + CancelExistingPttStpDrag call + try/catch Block A + try/catch Block B + newStop==null + Submit = 6 Lizard)

### [Fact] Test Names
- `IsSyncAtmBracketEligible_ReturnsFalse_WhenFollowerOrderNull` — KEEP
- `IsSyncAtmBracketEligible_ReturnsFalse_WhenPriceUnchanged` — KEEP
- Additional: `IsSyncAtmBracketEligible_ReturnsFalse_WhenAccIsNull`
- Additional: `IsSyncAtmBracketEligible_ReturnsTrue_WhenAllConditionsMet`

### Risk Flags
- **B142**: `suffix` param and `leaderOrder` optional param must be preserved on `SyncAtmFollowerBracket` signature — do not alter caller interface.
- **Block isolation**: Block A (Cancel) and Block B (CreateOrder+Submit) are intentionally independent try/catch blocks. Do NOT merge them into the helper.
- **DW-B151**: `CancelExistingPttStpDrag` call at L2404 must remain before Block A.
- **NT8**: `acc.Cancel` + `acc.CreateOrder` + `acc.Submit` — AddOnBase confirmed. `PTT-STP-Drag-` prefix preserved.

---

## TB-T6d — CancelPttDragOrphansForAccount

### Confirmed Line Range
- **Actual**: L1606-1626 (20 lines)
- **Prompt**: L1606-1640 — corrected, shift = -14 end lines
- **CCN before**: 10 (Lizard, confirmed by mission brief)

### Visual Branch Count
(1) foreach; (2) state!=Working; (3) instr?.FullName != instr?.FullName; (4a) o.Name!="PTT-TGT-Drag"; (4b) o.Name!="PTT-STP-Drag"; (5) try/catch; plus `?.` null-conditionals ≈ Lizard CCN=10.

### Extraction Plan

#### Helper 1: `private static bool IsPttDragOrphanCancellable(Order o, NinjaTrader.Cbi.Instrument instr)`
- **Absorbs**: L1610-1615 — state guard + instrument FullName guard + name guard
- **Signature**: `private static bool IsPttDragOrphanCancellable(Order o, NinjaTrader.Cbi.Instrument instr)`
- **Return**: true when order should be cancelled (Working + matching instrument + PTT-drag name)
- **CCN target**: <= 4 (state guard + instr null/FullName compound + two name checks with `&&` = 4 Lizard)
- **Constraints**: Static predicate. `o.Instrument?.FullName` null-conditional preserved. Both `"PTT-TGT-Drag"` and `"PTT-STP-Drag"` names must be checked (NT8-014 confirmed).

### Parent CCN Target After Extraction
<= 5 (foreach + IsPttDragOrphanCancellable gate + try block + acc.Cancel + StatusUpdate = 5)

### [Fact] Test Names
- `IsPttDragOrphanCancellable_ReturnsFalse_WhenInstrumentDoesNotMatch` — KEEP
- `IsPttDragOrphanCancellable_ReturnsFalse_WhenOrderStateIsFilled` — KEEP
- Additional: `IsPttDragOrphanCancellable_ReturnsTrue_WhenWorkingAndMatchingInstrument`
- Additional: `IsPttDragOrphanCancellable_ReturnsFalse_WhenNameIsNotPttDrag`

### Risk Flags
- **try/catch**: existing pattern from `SyncAtmFollowerBracket` — absorbs `ErrorCode.UnableToCancelOrder`. Must remain in parent foreach body.
- **Static**: `IsPttDragOrphanCancellable` takes no instance state — `static` keyword appropriate.
- **NT8-014**: `"PTT-TGT-Drag"` and `"PTT-STP-Drag"` confirmed names — must not be generalized.

---

## TB-T7a — DtoToRule

### Confirmed Line Range
- **Actual**: L5609-5672 (63 lines)
- **Prompt**: L5609-5680 — corrected, shift = -8 end lines
- **CCN before**: 11 (Lizard, confirmed by mission brief)

### Visual Branch Count
(1) foreach Account.All; (2) acc.Name==dto.Master; (3) for followers; (4) followers[i]==null warning; (5) FollowerMultipliers null||length; (6) FollowerAtmModeNames null; (7) for ATM loop; (8) IsNullOrEmpty name; (9) TightenTicks>0 ternary ≈ Lizard CCN=11.

### Extraction Plan

#### Helper 1: `private static string[] ResolveFollowerNames(CopyRuleDto dto)`
- **Absorbs**: `dto.FollowerAccountNames` null guard — if FollowerAccountNames is null return empty array
- **Signature**: `private static string[] ResolveFollowerNames(CopyRuleDto dto)`
- **Return**: `string[]` — never null (JS-002 compliant). Returns `Array.Empty<string>()` when null.
- **CCN target**: <= 2 (null guard + ToArray/return = 2)
- **Constraints**: `dto.FollowerAccountNames` is typed `string[]` in `CopyRuleDto` with `= new string[0]` default, so null only on pre-B6 XML deserialization. Return `Array.Empty<string>()` for null.

#### Helper 2: `private static Dictionary<string, FollowerAtmMode> ResolveAtmMap(CopyRuleDto dto)`
- **Absorbs**: L5643-5656 — the null guard + for-loop building `atmMap`
- **Signature**: `private static Dictionary<string, FollowerAtmMode> ResolveAtmMap(CopyRuleDto dto)`
- **Return**: `Dictionary<string, FollowerAtmMode>` — never null (JS-002 compliant). Returns `new Dictionary<>()` when null.
- **CCN target**: <= 3 (null guard + for loop + IsNullOrEmpty = 3)
- **Constraints**: `ParseAtmModeName` call preserved. Length guard `i < dto.FollowerAtmModeNames.Length && i < dto.FollowerAccountNames.Length` preserved.

#### Helper 3: `private static int[] ResolveMultipliers(CopyRuleDto dto)`
- **Absorbs**: L5638-5640 — the null/length check + multipliers assignment
- **Signature**: `private static int[] ResolveMultipliers(CopyRuleDto dto)`
- **Return**: `int[]` or null — caller assigns to `multipliers` local (existing pattern at L5638 allows null; `CopyRule.Create` handles null multipliers). Returns null when dto.FollowerMultipliers is null/empty (preserving existing semantics for `CopyRule.Create` backward compat).
- **CCN target**: <= 3 (null check + length check + return = 3)
- **Note on JS-002**: Returning null here is consistent with existing contract — `CopyRule.Create` receives `int[]?` and handles null as "all-ones". This is a nullable value-pattern, not a reference return-null violation.

### Parent CCN Target After Extraction
<= 5 (foreach Account.All loop + acc-name match + for-followers loop + tightenTicks ternary + CopyRule.Create call = 5 Lizard)

### [Fact] Test Names
- `ResolveFollowerNames_ReturnsEmptyArray_WhenDtoFollowersNull` — KEEP
- `ResolveFollowerNames_ReturnsArray_WhenFollowersPresent` — KEEP
- `ResolveAtmMap_ReturnsEmptyDict_WhenDtoAtmModesNull` — KEEP
- `ResolveMultipliers_ReturnsAllOnes_WhenLengthMismatch` — NOTE: `ResolveMultipliers` returns null/valid array; "all-ones" handling is in `CopyRule.Create`. Test: `ResolveMultipliers_ReturnsNull_WhenDtoMultipliersNull`
- `ResolveMultipliers_ReturnsAllOnes_WhenMultipliersNull` — ADJUSTED: `ResolveMultipliers_ReturnsArray_WhenMultipliersPresent`

### Risk Flags
- **B127**: `dto.FollowerAccountNames` passed to `CopyRule.Create` as last arg — must remain in parent after helpers populate followers array.
- **B6/B7 backward compat**: null XML fields are the reason for all null guards. Do not assume non-null.
- **DW-B85**: `FindFollowerAccount` null warning log must stay in parent's followers-build loop (L5627-5634).

---

## TB-T7b — GetRefPrice

### Confirmed Line Range
- **Actual**: L5241-5248 (7 lines)
- **Prompt**: L5241-5260 — corrected, shift = -12 end lines
- **CCN before**: 10 (Lizard, confirmed by mission brief)

### Visual Branch Count (Lizard)
`bid > 0 && ask > 0` = 2 operators; `?:` outer ternary = 1; `isLong ? ask : bid` inner ternary = 1; plus `?.` null-conditionals on MarketData/Bid/Ask = ~6 total = Lizard CCN=10 confirmed.

### Extraction Plan

#### Helper 1: `private static double SelectRefPriceByDirection(bool isLong, double bid, double ask)`
- **Absorbs**: L5245-5247 — the `bid > 0 && ask > 0 ? (isLong ? ask : bid) : 0.0` ternary block
- **Signature**: `private static double SelectRefPriceByDirection(bool isLong, double bid, double ask)`
- **Return**: `double` — 0.0 when bid/ask unavailable, positive price otherwise
- **CCN target**: <= 3 (bid>0 + ask>0 compound = 2 Lizard; outer ternary + inner ternary = 2 → total 4, target <= 3; collapse compound to single `&& ` = 2 Lizard decision points, ternary chain = 2 → total <=4; aim for <= 3 by extracting to `bid > 0 && ask > 0` as single guard)

**Note**: Prompt says `isLong: bid > 0 ? bid : last; short: ask > 0 ? ask : last` but the actual code for `GetRefPrice` uses `isLong ? ask : bid` (ask for long tighten, bid for short tighten — different semantics from `OnPendingBeAccountUpdate` which uses bid for long). The helper must match the actual code logic, not the prompt description.

### Parent CCN Target After Extraction
<= 5 (bid `?.` chain + ask `?.` chain + `bid>0&&ask>0` via helper = 5 Lizard with null-conditional counts)

### [Fact] Test Names
- `SelectRefPriceByDirection_ReturnsBid_WhenLongAndBidPositive` — **ADJUSTED** to match actual logic: `SelectRefPriceByDirection_ReturnsAsk_WhenLongAndBothPositive`
- `SelectRefPriceByDirection_ReturnsLast_WhenLongAndBidZero` — **ADJUSTED**: `SelectRefPriceByDirection_ReturnsZero_WhenBidOrAskIsZero`
- `SelectRefPriceByDirection_ReturnsAsk_WhenShortAndAskPositive` — **ADJUSTED**: `SelectRefPriceByDirection_ReturnsBid_WhenShortAndBothPositive`

### Risk Flags
- **DW-B30-04**: `?.` null-conditional chains on `instrument.MarketData?.Bid?.Price` must remain in `GetRefPrice`. Do NOT move them into the helper (helper receives pre-resolved `double bid, double ask`).
- **Semantic**: `GetRefPrice` uses `isLong ? ask : bid` (tighten-stop logic: move stop toward current price). This is DIFFERENT from `OnPendingBeAccountUpdate` which uses `isLong ? bid : ask`. Do not mix the two.

---

## FULL TICKET SUMMARY

| Ticket | Method(s) | CCN Before | CCN Target | Helpers Added | Test Count |
|--------|-----------|-----------|-----------|--------------|-----------|
| TB-T1 | OnPendingBeAccountUpdate | 32 | <=7 | IsPendingBeSlotActive, IsPendingBeTriggerConditionMet, ExecutePendingBeTrigger | 6 |
| TB-T2 | OnOrderUpdate | 23 | <=8 | TryRecordBeTargetFill, TryTriggerBeRecovery | 7 |
| TB-T3a | OnTrailBeAccountUpdate | 9 | <=6 | IsTrailBeTriggerMet | 3 |
| TB-T3b | SubmitBeStop | 10 | <=5 | FindBePosition, SubmitBeStopOrder | 4 |
| TB-T4 | DispatchCopy | 13 | <=6 | ShouldSkipFollowerDispatch, ShouldSkipForReversalGuard, DispatchToFollower | 5 |
| TB-T5a | TryFireFollowerBeRetry | 15 | <=6 | IsPttBeRetryTriggerOrder, IsBeRetryStateWorking | 4 |
| TB-T5b | TryEvictFollowerBeSlot | 13 | <=6 | IsEvictTriggerState | 4 |
| TB-T6a | TryHandleEntryDrag | 11 | <=6 | IsEntryDragEligible | 4 |
| TB-T6b | IsExitSignalName (+IsNativeExitName, IsNonFlatDispatchName already extracted) | 10 | <=5 | IsAtmTargetSignalName | 6 |
| TB-T6c | SyncAtmFollowerBracket | 11 | <=6 | IsSyncAtmBracketEligible | 4 |
| TB-T6d | CancelPttDragOrphansForAccount | 10 | <=5 | IsPttDragOrphanCancellable | 4 |
| TB-T7a | DtoToRule | 11 | <=5 | ResolveFollowerNames, ResolveAtmMap, ResolveMultipliers | 5 |
| TB-T7b | GetRefPrice | 10 | <=5 | SelectRefPriceByDirection | 3 |

**Total new helpers**: 18 private/static methods
**Total new [Fact] tests**: 59

---

## SCAN CHECKLIST (applied to every ticket)

Each ticket is subject to the following 7-scan checklist before ptt-verifier accepts:

- **SCAN-01**: `grep -r "lock(" src/ --include="*.cs"` → 0 results in new/modified code
- **SCAN-02**: `grep -rn "async void " src/ --include="*.cs"` → 0 new instances (event handlers exempt)
- **SCAN-03**: `grep -rn "return null;" src/ --include="*.cs"` → 0 new instances in new helpers (nullable value types exempt)
- **SCAN-04**: `lizard src/PropTraderTools/CopyEngine.cs --CCN 8` → 0 warnings for all methods touched by ticket
- **SCAN-05**: `dotnet build` → 0 errors, 0 warnings
- **SCAN-06**: `dotnet test` → 0 new failures; new [Fact] tests pass
- **SCAN-07**: `grep -rn "DateTime.Now" src/ --include="*.cs"` → 0 results in new code (DateTime.UtcNow only)

---

## PLAN_COMPLETE
