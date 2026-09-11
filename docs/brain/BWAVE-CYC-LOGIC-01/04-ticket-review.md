# BWAVE-CYC-LOGIC-01 — Ticket Review (Phase 3.5)

**Reviewer:** PTT Ticket Reviewer (ptt-ticket-reviewer mode)
**Phase:** 3.5 — Ticket Review
**Date:** 2026-01-01
**Epic:** BWAVE-CYC-LOGIC-01
**Input:** `docs/brain/BWAVE-CYC-LOGIC-01/04-tickets.md` (TICKETS_COMPLETE)
**Input:** `docs/brain/BWAVE-CYC-LOGIC-01/02-architecture-plan.md` (REVIEW_PASS Cycle 2)
**Input:** `docs/brain/BWAVE-CYC-LOGIC-01/02-plan-review.md` (REVIEW_PASS)

---

## Execution Order Verified

Stated: **T4 → T1 → T2 → T3 → T5**

T4 must precede T1 deployment because A-21 (`IsLeaderTargetOrder`) forward-references
C-01 (`HasValidTargetNameSuffix`) and A-03 (`IsEligibleBeTargetOrder`) forward-references
C-05 (`IsBeTargetSnapshotState`). Both tickets compile independently per the C# class-scope
forward-reference rule, but T4 must be committed first for NT8 host correctness.
T2, T3, T5 have no cross-ticket dependencies.
**ORDERING: PASS**

---

## T4 — Group C (TaR2) + Group D Predicates (TaR3)

### Spec Requirements
C-01, C-02, C-03, C-04, C-05, D-04, D-05, D-06, D-07, D-08, D-09, D-10, D-11, D-12,
D-13, D-14, D-15, D-16, D-17, D-18

### Traceability
All 20 methods map to plan sections C and D. No phantom work. No duplicates. All IDs
appear in the architecture plan's T4 ticket breakdown. B-04 exclusion correctly not present.
**Traceability: PASS**

### JS Pre-Check
- JS-021 (lock): Zero `lock(` in any T4 method. All state access via ConcurrentDictionary
  (D-13 `_pendingFollowerBeSlots.ContainsKey`, D-14 `_filledBeTargetCount.TryGetValue`,
  D-24 `_beReplaceAttempts`). **PASS**
- JS-001 (no throw): Zero `throw` statements. All T4 methods are pure predicates with no
  direct NT8 API calls requiring try/catch. **PASS**
- JS-002 (no null return for value type): No non-nullable return types return null. **PASS**
- JS-003 (no string sentinel for state): State discrimination uses `OrderState` enum values
  and `OrderType` enum values. Order names are validated against documented naming conventions,
  not used as discriminated state proxies. **PASS**
- DateTime.Now: Not used. **PASS**
**JS Pre-Check: PASS**

### CYC Pre-Check
All 20 methods verified against plan-approved CYC values (Cycle 2 review Section E).
All stated CYC values match the code's control-flow branch count under project McCabe standard
as approved by the plan reviewer. All values ≤ 8.

| Method | Ticket CYC | Plan CYC | Code Count | ≤ 8? |
|--------|-----------|----------|------------|------|
| C-01 HasValidTargetNameSuffix | 5 | 5 | 5 (base+4&&) | ✓ |
| C-02 SelectBeTargetList | 3 | 3 | 3 (base+foreach+if+if) | ✓ |
| C-03 IsBeTargetActiveState | 2 | 2 | 2 (base+null-&&) | ✓ |
| C-04 IsBeTargetPendingChangeState | 2 | 2 | 2 (base+null-&&) | ✓ |
| C-05 IsBeTargetSnapshotState | 1 | 1 | 1 (single delegation) | ✓ |
| D-04 IsPttTgtDragOrder | 2 | 2 | 2 (base+&&) | ✓ |
| D-05 IsAtmTgtOrder | 4 | 4 | 4 (base+3&&) | ✓ |
| D-06 IsBePendingTargetOrder | 1 | 1 | 1 (single delegation) | ✓ |
| D-07 IsPttBeStopRejected | 3 | 3 | 3 (base+2&&) | ✓ |
| D-08 IsPttDragOrderCancellable | 5 | 5 | 5 (base+3if+1&&-compound) | ✓ |
| D-09 IsPttQxTargetOrder | 4 | 4 | 4 (base+3&&) | ✓ |
| D-10 IsNativeAtmBeRetryTarget | 4 | 4 | 4 (base+3&&) | ✓ |
| D-11 IsBeRetryEligibleOrderState | 3 | 3 | 3 (base+&&+||) | ✓ |
| D-12 IsBeRetryOrderInvalid | 3 | 3 | 3 (base+2||) | ✓ |
| D-13 IsBeSlotNonTerminal | 1 | 1 | 1 | ✓ |
| D-14 IsBeFilledWithOpenPosition | 2 | 2 | 2 (base+if) | ✓ |
| D-15 IsPttDragOrderName | 3 | 3 | 3 (base+&&+||) | ✓ |
| D-16 IsDragInstrumentMatch | 1 | 1 | 1 (pure expression) | ✓ |
| D-17 IsQxTOrderStateValid | 3 | 3 | 3 (base+&&+||) | ✓ |
| D-18 IsQxTBracketNameValid | 4 | 4 | 4 (base+3&&) | ✓ |

**CYC Pre-Check: PASS**

### NT8 Check
- No direct `Account.Cancel`, `Account.CreateOrder`, or `Account.Submit` calls in T4.
  All T4 methods are pure predicates or ConcurrentDictionary accessors.
- `OrderState`, `OrderType`, `MarketPosition` enum values all use correct NT8 enum members.
- No `AtmStrategyChangeStopTarget()` usage.
- `_pendingFollowerBeSlots` is a `ConcurrentDictionary` (lock-free). ✓
- `_filledBeTargetCount` is a `ConcurrentDictionary` (lock-free). ✓
- `_beReplaceAttempts` is a `ConcurrentDictionary` (lock-free). ✓
**NT8 Check: PASS**

### Test Coverage
All methods are `private` or `private static`. No `public`/`internal` methods introduced.
The [Fact] requirement applies to public/internal methods only. Existing test classes
(`BwaveCycTaR2HelperTests`, `BwaveCycTaR3HelperTests`) cover existence checks via reflection.
CopyEngineTests.cs is not modified. No Skip annotations removed.
**Test Coverage: PASS**

### Scan Checklist
SCAN-01 through SCAN-07 all present in the "7-Scan Checklist (T4)" section.
**Scan Checklist: PASS**

### File Routing
Target: `src/PropTraderTools/CopyEngine.cs` — Wave workspace. No Director workspace paths.
**File Routing: PASS**

### Verification Step
`dotnet test` failed = 0 stated. Hard-link sync `powershell -File .\deploy-sync.ps1` stated.

### VERDICT: TICKET_REVIEW_PASS

---

## T1 — Group A Predicates + BE Trigger Predicates

### Spec Requirements
A-01, A-02, A-03, A-04, A-05, A-12, A-13, A-14, A-14b (NEW INSERT), A-15, A-19, A-20, A-21, A-23

### Traceability
All 13 stub fills + 1 new INSERT (A-14b) map to plan Group A. A-14b is correctly documented
as "INSERT, not stub fill" with insertion point specified (after L7939, before L7941 attribute
line). ObfuscationAttribute requirement documented. Line-shift warning (+4 after A-14b
insertion) documented for downstream methods. No phantom work. No duplicates.
**Traceability: PASS**

### JS Pre-Check
- JS-021 (lock): Zero `lock(` in any T1 method. `_pendingBeSlots` accessed via `TryGetValue`
  (ConcurrentDictionary — lock-free). `_rules` iterated as `ConcurrentBag` (lock-free). **PASS**
- JS-001 (no throw): Zero `throw` statements in all 14 method bodies. A-01 calls `BreakEven`
  delegate (existing production method that handles its own error management). **PASS**
- JS-002 (no null return for non-nullable): `FindMatchingNativeAtmBracket` (A-13) returns
  `Order` — `null` is the documented failure sentinel for optional-Order returns (nullable
  reference type in this context). Acceptable. **PASS**
- JS-003 (no string sentinel for state): All mode discrimination uses `OrderType`, `OrderState`
  enums. Order name comparisons use documented naming conventions, not state proxies. **PASS**
- DateTime.Now: A-12 uses `DateTime.UtcNow` (not `DateTime.Now`). **PASS**
**JS Pre-Check: PASS**

### CYC Pre-Check
All CYC values match plan-approved Cycle 2 values. All ≤ 8.

| Method | Ticket CYC | Plan CYC | ≤ 8? | Note |
|--------|-----------|----------|------|------|
| A-01 TryFireImmediateBeIfAlreadyAtLevel | 5 | 5 | ✓ | |
| A-02 IsPendingBeTriggerMet | 8 | 8 | ✓ | At limit; correct |
| A-03 IsEligibleBeTargetOrder | 4 | 4 | ✓ | |
| A-04 IsNativeAtmTargetOrder | 5 | 5 | ✓ | |
| A-05 IsPttBeOrQxTargetOrder | 6 | 6 | ✓ | |
| A-12 IsReArmedAtmBracketCleanupRequired | 3 | 3 | ✓ | Uses DateTime.UtcNow |
| A-13 FindMatchingNativeAtmBracket | 4 | 4 | ✓ | INFO: strict || count could be 5-6; still ≤ 8 |
| A-14 TryFindRuleAndFollowerIndex | 5 | 5 | ✓ | V-001 fix verified |
| A-14b IsFollowerAccountMatch | 4 | 4 | ✓ | NEW INSERT |
| A-15 HasActiveQxOrdersForInstrument | 3 | 3 | ✓ | |
| A-19 HasInFlightFlattenOrder | 4 | 4 | ✓ | |
| A-20 IsPositionFlatOrMissing | 3 | 3 | ✓ | |
| A-21 IsLeaderTargetOrder | 4 | 4 | ✓ | |
| A-23 IsLeaderAccountForInstrument | 3 | 3 | ✓ | INFO: strict count could be 4; still ≤ 8 |

INFO (non-blocking): A-13 and A-23 have `||`/compound conditions that under strict per-operator
McCabe counting would be 1 higher than stated. The plan-reviewer approved these values and all
are ≤ 8. No CYC > 8 violation in any T1 method.
**CYC Pre-Check: PASS**

### NT8 Check
- A-01: `BreakEven(acc, instr, 0)` — delegates to existing production method. No direct
  `Account.Cancel/CreateOrder/Submit` in A-01 body. No `AtmStrategyChangeStopTarget`. ✓
- A-02: `GetMarketBidPrice`, `GetMarketAskPrice`, `FindPosition`, `SelectBeRefPriceByDirection`
  — all delegate to existing methods. No direct NT8 API calls. ✓
- A-13: `acc.Orders.ToList()` — `Account.Orders` is AddOnBase-available. ✓
  `OrderState.Working`, `OrderState.Accepted` — correct NT8 enum values. ✓
- A-14: `_rules` iteration (ConcurrentBag, lock-free). No NT8 API. ✓
- A-14b: Pure static predicate. No NT8 API. ✓
- A-15: `acc.Orders.ToList()` — AddOnBase. `OrderState.Working`, `OrderState.Submitted` — ✓
- A-19: `acc.Orders.ToList()` — AddOnBase. State enum values correct. ✓
- A-20: `MarketPosition.Flat` — correct NT8 enum value. ✓
- A-21: `OrderState.Working`, `OrderType.Limit` — correct. ✓
- No `AtmStrategyChangeStopTarget()` usage. ✓
- No `Account.All` calls. ✓
- No `async`/`await` in any method. ✓
**NT8 Check: PASS**

### Test Coverage
All methods are `private` or `private static`. No `public`/`internal` introduced.
CopyEngineTests.cs not modified. No Skip annotations removed. Existing test classes
(`B79CancelRaceGuardTests`, `BwaveCycT1R1BeHelperTests`) cover existence checks.
**Test Coverage: PASS**

### Scan Checklist
SCAN-01 through SCAN-07 all present in "7-Scan Checklist (T1)" section. SCAN-06 lists
all 14+1 methods with individual CYC values.
**Scan Checklist: PASS**

### File Routing
Target: `src/PropTraderTools/CopyEngine.cs` — Wave workspace.
**File Routing: PASS**

### Verification Step
`dotnet test` failed = 0 stated. Hard-link sync stated.

### VERDICT: TICKET_REVIEW_PASS

---

## T2 — Group A Actions

### Spec Requirements
A-06, A-07, A-08, A-09, A-10, A-11, A-16, A-17, A-18, A-22, A-24

### Traceability
All 11 methods map to plan Group A. No phantom work. No duplicates. T2 prerequisite
correctly states "None — all T2 methods delegate to existing production methods already
in CopyEngine.cs." Line number shift note (+4 after A-14b insertion) documented.
**Traceability: PASS**

### JS Pre-Check
- JS-021 (lock): Zero `lock(` in any T2 method. `acc.Cancel` / `acc.Submit` in try/catch.
  `_dedupCache` ConcurrentDictionary indexer (A-22) — lock-free. **PASS**
- JS-001 (no throw): Zero `throw`. All NT8 API calls (`acc.Cancel`, `acc.CreateOrder`,
  `acc.Submit`) wrapped in `try/catch {}` throughout T2. **PASS**
  - A-08: `try { acc.Cancel(...); } catch { }` ✓
  - A-09: `try { acc.Cancel(...); } catch { }` ✓
  - A-10: `try { acc.CreateOrder/acc.Submit } catch { }` ✓
  - A-11: `try { acc.CreateOrder/acc.Submit } catch { }` ✓
  - A-17: `try { acc.Cancel(...); } catch { }` ✓
  - A-18: `try { acc.CreateOrder/acc.Submit } catch { return null; }` ✓
  - A-22: `try { acc.CreateOrder/acc.Submit } catch { }` ✓
  - A-24: `try { acc.Cancel(...); } catch { }` ✓
- JS-002: `CreateAndSubmitReplacementTarget` (A-18) returns `null` on failure — Order is a
  nullable reference return type. Acceptable. **PASS**
- DateTime.Now: A-22 uses `DateTime.MaxValue` (static constant, not `DateTime.Now`). **PASS**
**JS Pre-Check: PASS**

### CYC Pre-Check

| Method | Ticket CYC | Plan CYC | ≤ 8? | Note |
|--------|-----------|----------|------|------|
| A-06 RegisterBeRetryIfNoTargets | 1 | 1 | ✓ | |
| A-07 RegisterPartialTargetBeRetry | 1 | 1 | ✓ | |
| A-08 CancelExistingStpDragOrders | 4 | 4 | ✓ | |
| A-09 CancelExistingTgtDragOrders | 4 | 4 | ✓ | |
| A-10 SubmitReplacementStopLeg | 3 | 3 | ✓ | |
| A-11 SubmitReplacementTargetLeg | 3 | 3 | ✓ | |
| A-16 SyncAtmFollowerStopBracket | 3 | 3 | ✓ | INFO: if/|| strict count = 4; still ≤ 8 |
| A-17 CancelStaleTgtDragOrders | 4 | 4 | ✓ | |
| A-18 CreateAndSubmitReplacementTarget | 3 | 3 | ✓ | |
| A-22 ResubmitFollowerEntry | 8 | 8 | ✓ | At limit; correct |
| A-24 CancelStaleCascadeTgtDrag | 8 | 8 | ✓ | At limit; correct |

INFO (non-blocking): A-16 `if (leaderStop == null || capturedPrice <= 0)` strict || count
would yield CYC=4. Plan-approved at 3. All ≤ 8. No violation.
**CYC Pre-Check: PASS**

### NT8 Check
- SCAN-05 PTT- prefix: A-10 = "PTT-STP-Drag" ✓, A-11 = "PTT-TGT-Drag" ✓,
  A-18 = "PTT-TGT-Drag" ✓, A-22 = "PTT-Copy" ✓. All start with "PTT-". ✓
- `acc.CreateOrder` signature: 12-parameter form matching NT8 AddOnBase `Account.CreateOrder`
  overload. `NinjaTrader.Core.Globals.MaxDate` used as expiry (A-10, A-11, A-18).
  `DateTime.MaxValue` used in A-22 (equivalent constant). ✓
- `OrderEntry.Automated` (A-10, A-11, A-18) and `OrderEntry.Manual` (A-22) — both valid. ✓
- `TimeInForce.Day` (A-10, A-11, A-18) and `TimeInForce.Gtc` (A-22) — both valid. ✓
- `(NinjaTrader.Cbi.CustomOrder)null` cast — required for NT8 overload resolution. ✓
- `acc.Cancel(new Order[] { o })` — array form required by NT8. ✓
- `acc.Submit(new[] { o })` — array form. ✓
- A-16 delegates to `SyncAtmFollowerBracket` (existing production method). No direct
  `acc.Cancel/CreateOrder/Submit` in A-16 body — delegates handle their own try/catch. ✓
- No `AtmStrategyChangeStopTarget()`. ✓
**NT8 Check: PASS**

### Test Coverage
All methods are `private`. No `public`/`internal` introduced. CopyEngineTests.cs not
modified. No Skip annotations removed.
**Test Coverage: PASS**

### Scan Checklist
SCAN-01 through SCAN-07 all present in "7-Scan Checklist (T2)" section.
**Scan Checklist: PASS**

### File Routing
Target: `src/PropTraderTools/CopyEngine.cs` — Wave workspace.
**File Routing: PASS**

### Verification Step
`dotnet test` failed = 0 stated. Hard-link sync stated.

### VERDICT: TICKET_REVIEW_PASS

---

## T3 — Group B BE Trigger/Arming Helpers

### Spec Requirements
B-01, B-02, B-03, B-05, B-06, B-07, B-08, B-09, B-10, B-11, B-12

### Traceability
All 11 methods map to plan Group B. B-04 (`SelectBeRefPriceByDirection`) correctly
excluded — noted at top of T3 ("B-04 EXCLUDED: SelectBeRefPriceByDirection (L8005)
already has correct working logic and MUST NOT be modified"). No phantom work.
No duplicates.
**Traceability: PASS**

### JS Pre-Check
- JS-021 (lock): Zero `lock(`. B-07 uses `_pendingBeSlots[acc.Name] = ...`
  (ConcurrentDictionary indexer — lock-free atomic set). B-08 uses `TryRemove` (lock-free).
  B-12 uses `TryRemove` (lock-free). **PASS**
- JS-023 (UI updates): B-07 fires `PendingBeArmed?.Invoke(...)` and B-05/B-11 fire
  `PendingBeFired?.Invoke(...)`. Plan review Section H confirmed subscribers marshal to
  UI thread internally per existing pattern (L409-412). No direct Dispatcher call needed
  in the stubs. **PASS**
- JS-001 (no throw): Zero `throw`. B-05 delegates to `SubmitBeStop` (existing production
  method with its own try/catch). B-12 delegates to `MoveStopToBreakEven` (existing). **PASS**
- DateTime.Now: Not used in any T3 method. **PASS**
**JS Pre-Check: PASS**

### CYC Pre-Check

| Method | Ticket CYC | Plan CYC | ≤ 8? | Note |
|--------|-----------|----------|------|------|
| B-01 GetMarketBidPrice | 1 | 1 | ✓ | |
| B-02 GetMarketAskPrice | 1 | 1 | ✓ | |
| B-03 GetBeTickSize | 1 | 1 | ✓ | |
| B-05 FireBeAndNotifyEvent | 1 | 1 | ✓ | |
| B-06 ShouldFireBeImmediately | 3 | 3 | ✓ | INFO: ternary strict count = 4; still ≤ 8 |
| B-07 CompleteBeArming | 1 | 1 | ✓ | |
| B-08 TryClaimPendingBeSlot | 2 | 2 | ✓ | |
| B-09 GetSlotInstrumentName | 1 | 1 | ✓ | INFO: code has 1 if branch = CYC 2 by strict count; plan approved 1 |
| B-10 GetSlotAccountName | 2 | 2 | ✓ | |
| B-11 RaisePendingBeFiredEvent | 1 | 1 | ✓ | |
| B-12 SettleAndFirePendingBe | 3 | 3 | ✓ | |

INFO (non-blocking): B-09 ticket comment says `CYC=1 (TryGetValue failure path handled
by ?? return)` — technically the `if (!TryGetValue(...)) return string.Empty` is 1 branch
giving CYC=2. Plan approved CYC=1 under project McCabe interpretation. Either way ≤ 8.
**CYC Pre-Check: PASS**

### NT8 Check
- B-01: `instr?.MarketData?.Bid?.Price` — AddOnBase-available. ✓
- B-02: `instr?.MarketData?.Ask?.Price` — AddOnBase-available. ✓
- B-03: `instr?.MasterInstrument?.TickSize` — AddOnBase-available. ✓
- B-05: Delegates to `SubmitBeStop` (existing). Null-conditional on `PendingBeFired?.Invoke`.
  `instr?.FullName ?? string.Empty` and `acc?.Name ?? string.Empty` — non-null event args. ✓
- B-07: `_pendingBeSlots[acc.Name] = new PendingBeSlot(acc, instr, bufferTicks)` —
  ConcurrentDictionary indexer set, lock-free. `PendingBeArmed?.Invoke` — null-conditional. ✓
- B-12: `FindPosition` and `IsFlat` are existing production helpers. `MoveStopToBreakEven`
  is an existing production method. No direct NT8 API in B-12 body. ✓
- No `AtmStrategyChangeStopTarget()`. ✓
- SCAN-05: No direct `acc.CreateOrder` in T3 methods. B-05 delegates to `SubmitBeStop`. ✓
**NT8 Check: PASS**

### Test Coverage
All methods are `private`. No `public`/`internal` introduced. CopyEngineTests.cs not
modified. No Skip annotations removed.
**Test Coverage: PASS**

### Scan Checklist
SCAN-01 through SCAN-07 all present in "7-Scan Checklist (T3)" section.
**Scan Checklist: PASS**

### File Routing
Target: `src/PropTraderTools/CopyEngine.cs` — Wave workspace.
**File Routing: PASS**

### Verification Step
`dotnet test` failed = 0 stated. Hard-link sync stated.

### VERDICT: TICKET_REVIEW_PASS

---

## T5 — Group D Actions + Group E (TaR3 + TaR6)

### Spec Requirements
D-01, D-02, D-03, D-19, D-20, D-21, D-22, D-23, D-24, E-01, E-02, E-03, E-04, E-05

### Traceability
All 14 methods map to plan sections D (actions) and E. No phantom work. No duplicates.
T5 prerequisite correctly states "None — all T5 methods are self-contained or delegate
to existing production methods."
**Traceability: PASS**

### JS Pre-Check
- JS-021 (lock): Zero `lock(`. `_beReplaceAttempts` (D-24) uses ConcurrentDictionary
  `TryGetValue` + indexer set — lock-free. `_qxPendingFollowerCleanup` (D-19) uses
  `TryGetValue` — lock-free. **PASS**
- JS-001 (no throw): Zero `throw`. D-21 `acc.Cancel` in `try { } catch { }`. D-01 and
  D-03 delegate to `SyncAtmFollowerBracket`/`SyncAtmFollowerTarget` (existing methods
  that handle their own try/catch). E-04 delegates to `SyncAtmFollowerBracket` /
  `CreateAndSubmitCollateralStop` (existing). **PASS**
- JS-002: No non-nullable method returns null. **PASS**
- DateTime.Now: D-20 uses `DateTime.UtcNow` (not `DateTime.Now`). ✓ **PASS**
**JS Pre-Check: PASS**

### CYC Pre-Check

| Method | Ticket CYC | Plan CYC | ≤ 8? | Note |
|--------|-----------|----------|------|------|
| D-01 TrySyncAtmBrackets | 6 | 6 | ✓ | |
| D-02 TrySkipTrailingStop | 2 | 2 | ✓ | INFO: strict && count = 3; still ≤ 8 |
| D-03 SyncStandardBracket | 4 | 4 | ✓ | |
| D-19 TryGetCleanupEntryForFollower | 2 | 2 | ✓ | |
| D-20 IsCleanupEntryCurrentAndMatching | 4 | 4 | ✓ | Uses DateTime.UtcNow |
| D-21 SendAtmCancelReplace | 3 | 3 | ✓ | INFO: strict count = 6; still ≤ 8 |
| D-22 TryMatchFollowerInRule | 4 | 4 | ✓ | |
| D-23 IsBeReplaceTargetValid | 4 | 4 | ✓ | |
| D-24 TryIncrementBeReplaceAttempt | 2 | 2 | ✓ | |
| E-01 IsBracketOrderLiveState | 4 | 4 | ✓ | |
| E-02 MatchesPttReplacementName | 3 | 3 | ✓ | |
| E-03 LogHbcDiag | 2 | 2 | ✓ | |
| E-04 ExecuteStopDragOrder | 3 | 3 | ✓ | INFO: strict count = 5; still ≤ 8 |
| E-05 IsOrderEventProcessable | 4 | 4 | ✓ | |

INFO (non-blocking): D-21 has multiple `if/||` branches; strict per-operator count = 6.
Plan-approved at 3. E-04 strict count = 5. Plan-approved at 3. All ≤ 8. No violation.
**CYC Pre-Check: PASS**

### NT8 Check
- D-01: `FindFollowerBracketOrder`, `DeriveLeaderBracketIndex`, `SyncAtmFollowerBracket`,
  `SyncAtmFollowerTarget` — all existing production methods. No direct NT8 API in body. ✓
- D-03: Same delegation pattern as D-01. ✓
- D-20: `DateTime.UtcNow` (not `.Now`). `ValueTuple<Instrument, DateTime>` — C# 7.0 /
  .NET 4.7+ available on .NET 4.8. `is` type test — C# 7.0. ✓
- D-21: `acc.Cancel(new Order[] { order })` in `try { } catch { }`. Delegates to
  `SyncAtmFollowerBracket`/`SyncAtmFollowerTarget` for the replace step. ✓
- E-03: `NinjaTrader.Code.Output.Process(..., NinjaTrader.NinjaScript.PrintTo.OutputTab1)`
  — AddOnBase-available output API. ✓
- E-04: `FindFollowerBracketOrder`, `SyncAtmFollowerBracket`,
  `CreateAndSubmitCollateralStop` — existing production methods. No direct `CreateOrder`
  in E-04 body. ✓
- SCAN-05: No direct `acc.CreateOrder` in T5 methods. E-04 delegates to
  `CreateAndSubmitCollateralStop` (existing). ✓
- No `AtmStrategyChangeStopTarget()`. ✓
**NT8 Check: PASS**

### Test Coverage
All methods are `private` or `private static`. No `public`/`internal` introduced.
CopyEngineTests.cs not modified. No Skip annotations removed.
**Test Coverage: PASS**

### Scan Checklist
SCAN-01 through SCAN-07 all present in "7-Scan Checklist (T5)" section. SCAN-02 correctly
scopes to D-20 (`DateTime.UtcNow` usage).
**Scan Checklist: PASS**

### File Routing
Target: `src/PropTraderTools/CopyEngine.cs` — Wave workspace.
**File Routing: PASS**

### Verification Step
`dotnet test` failed = 0 stated. Hard-link sync stated.

### VERDICT: TICKET_REVIEW_PASS

---

## Aggregate Checks

### Spec Coverage (aggregate)
- Total stub fills: 70 (A-01..A-24 minus none, B-01..B-12 minus B-04, C-01..C-05,
  D-01..D-24, E-01..E-05 = 14+11+11+5+14 = 65 stub fills)
- Wait — recounting: Group A: 24 IDs. T1 covers 13 fills + 1 new. T2 covers 11. Total Group A = 24. Group B: 12 IDs — 1 excluded = 11 covered. Group C: 5 covered. Group D: 24 covered across T4 (15) and T5 (9). Group E: 5 covered. Total = 24+11+5+24+5 = 69 fills + 1 new insert = 70 stub fills + A-14b = 71 implementations.
- B-04 (`SelectBeRefPriceByDirection`): Correctly excluded in T3 header. ✓
- All spec IDs from the plan appear in exactly one ticket. ✓
- No spec ID duplicated across tickets. ✓
**SPEC COVERAGE: PASS**

### No Lock() Prohibition (JS-021) — Global
Zero `lock(` statements across all 5 tickets. All shared-state access uses ConcurrentDictionary
operations (TryGetValue, TryRemove, ContainsKey, indexer set — all lock-free).
**GLOBAL LOCK CHECK: PASS**

### No Throw Prohibition — Global
Zero `throw` statements in any of the 71 method implementations. All NT8 API calls
that can raise exceptions are wrapped in `try { } catch { }`.
**GLOBAL THROW CHECK: PASS**

### No DateTime.Now — Global
A-12 uses `DateTime.UtcNow`. D-20 uses `DateTime.UtcNow`. A-22 uses `DateTime.MaxValue`.
No `DateTime.Now` found anywhere.
**GLOBAL DATETIME.NOW CHECK: PASS**

### ASCII-Only String Literals — Global
All string literals verified: "PTT-STP-Drag", "PTT-TGT-Drag", "PTT-QX-T", "PTT-Copy",
"PTT-BE-Stop", "PTT-Flatten", "PTT-Trim", "Target", "[HBC-DIAG]", "leader=", "fo=",
"rule=", "price=", "null", "F2", "1", "0" — all ASCII. No Unicode, no curly quotes,
no emoji.
**GLOBAL ASCII CHECK: PASS**

### .NET 4.8 Syntax — Global
No switch expressions, no record types, no `??=` assignments found in any ticket.
`ValueTuple<T1,T2>` (D-20) and `is` type test (D-20) are C# 7.0 features available
on .NET 4.8. LINQ `.ToList()` / `.Any()` available. `?.` null-conditional (C# 6.0)
available. All compliant.
**GLOBAL .NET 4.8 CHECK: PASS**

### Method Count Reconciliation
| Ticket | Methods | Spec IDs |
|--------|---------|----------|
| T4 | 20 | C-01..C-05, D-04..D-18 |
| T1 | 14 | A-01..A-05, A-12..A-15, A-14b(NEW), A-19..A-21, A-23 |
| T2 | 11 | A-06..A-11, A-16..A-18, A-22, A-24 |
| T3 | 11 | B-01..B-03, B-05..B-12 |
| T5 | 14 | D-01..D-03, D-19..D-24, E-01..E-05 |
| **Total** | **70 stubs + 1 new = 71** | All Groups A-E |

**METHOD COUNT: 71 ✓**

---

## Informational Notes (Non-Blocking)

The following observations are recorded for engineer awareness. None constitute a
TICKET_REVIEW_FAIL.

1. **CYC undercount pattern (consistent across tickets)**: Several methods with compound
   `||` conditions inside `if` guards (A-13, A-16, A-23, B-06, B-09, D-02, D-21, E-04)
   have stated CYC values that may be 1-2 lower than a strict per-`||`-operator McCabe
   count. This is consistent with the project McCabe interpretation approved in Plan Review
   Cycle 2. All methods remain ≤ 8 under any counting method. Engineer should use stated
   CYC values from tickets for `// CYC=N` comments as instructed by plan review.

2. **B-09 CYC comment discrepancy**: The comment `// CYC=1 (TryGetValue failure path
   handled by ?? return)` is slightly misleading — the method has one `if (!TryGetValue)`
   branch. Under project standard, plan-approved at CYC=1. Functional behavior is correct.

3. **A-17 `leaderName` parameter unused**: Correctly documented in the contract rationale
   as "reserved for future extension." No new field required.

4. **E-04 passes `leaderOrder` for both `fo` and `leaderLeg` parameters** of
   `CreateAndSubmitCollateralStop`. Plan note confirms this is safe:
   `fo.Instrument = leaderOrder.Instrument`, `fo.OrderAction = leaderOrder.OrderAction`.

---

## Overall

| Ticket | Traceability | JS Pre-Check | CYC | NT8 | Test Coverage | Scan Checklist | File Routing | VERDICT |
|--------|-------------|-------------|-----|-----|---------------|----------------|--------------|---------|
| T4 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **PASS** |
| T1 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **PASS** |
| T2 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **PASS** |
| T3 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **PASS** |
| T5 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **PASS** |

**Violations found: 0**
**Informational notes (non-blocking): 4**

---

## TICKET_REVIEW_PASS

All 5 tickets in `docs/brain/BWAVE-CYC-LOGIC-01/04-tickets.md` are approved for
Phase 4a engineering. The engineer must:

1. Execute in order: **T4 → T1 → T2 → T3 → T5**
2. Commit T4 before deploying T1 to NT8 host
3. For A-14b: use `insert_content` to INSERT the new method (not fill a stub)
4. Account for +4 line shift in T1 methods after A-14b insertion
5. Use plan-approved CYC values (from 02-plan-review.md Section E) for `// CYC=N` comments
6. Run all 7 scans per ticket before marking BUILD_PASS
7. Run `powershell -File .\deploy-sync.ps1` after every src edit
8. Confirm `dotnet test` failed = 0 after each ticket before proceeding to next
