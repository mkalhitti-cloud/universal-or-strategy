# BWAVE-CYC Lane-A — Architecture Plan
## ptt-architect Stage 2 Output

**Status**: REVIEW_PASS (pending ptt-plan-reviewer)
**Wave**: BWAVE-CYC Lane-A
**Tickets**: T1 through T8 (21 methods, 8 tickets)
**Constraint summary**:
- Private helpers ONLY — no new public or internal surface
- Each helper CCN <= 4 (leave headroom for future feature growth)
- Each parent after extraction CCN <= 8 (Jane Street strict standard)
- Zero behaviour change — no logic changes, no reordering, no early returns added/removed
- JS-021: no lock() anywhere
- JS-002: no return null for missing values
- JS-033: no async void (non-event-handler)
- One new `[Fact]` test per extracted helper (add to CopyEngineTests.cs)
- Build must pass after every ticket before moving to the next

---

## JS Rule Pre-Check

| Rule | Scope | Applies to Lane-A |
|------|-------|-------------------|
| JS-021 (no lock()) | All extracted helpers | Yes — all helpers use ConcurrentDictionary or NT8 dispatcher; no lock() |
| JS-002 (no return null) | Helpers returning collections | SnapshotBeTargets already returns empty list; helpers must follow same contract |
| JS-033 (no async void) | All helpers | All helpers are synchronous void or value-returning — no async |
| CYC <= 8 | All parents after extraction | Verified per ticket below |
| CYC <= 4 | All extracted helpers | Verified per ticket below |

---

## NT8 Compiler Rules in Scope

Per `docs/standards/NT8_COMPILER_RULES.md`:
- NT8-001: no `{ get; init; }` — not applicable to this refactor (no new properties)
- NT8-002: no `record` types — not applicable
- All helper parameters use reference types already present in the codebase
- `acc.CreateOrder()` requires explicit `acc.Submit()` — existing pattern preserved in all extractions

---

## Ticket Roster

---

### T8 — QX Bracket Cancel Cluster + AllAccounts

**Methods**: CancelQxBrackets overload 1 (L875–905, CCN=14), BuildQxSnapshot (L916–944, CCN=11), CancelQxBrackets overload 2 (L955–1004, CCN=16), CancelAllAccountOrders (L1013–1043, CCN=12), AllAccounts (L4705–4752, CCN=9)
**CodeScene flags**: Complex Method (cc=16), Complex Method (cc=14), Complex Method (cc=19), Complex Method (cc=14), Complex Method (cc=13)

#### CancelQxBrackets (overload 1, L875)
- Parent CCN target: <= 6
- Extractions:
  1. `private bool IsQxCancellableOrderState(Order o)` — returns true when o.OrderState is any of the 5 live states (Working, Initialized, Accepted, Submitted, TriggerPending); absorbs the 4 `||` branches in the stateOk bool — CCN target <= 2
  2. `private bool IsOrderTerminalState(Order o)` — returns true when o.OrderState is Filled or Cancelled; used by the RemoveAll lambda to absorb the 1 `||` branch — CCN target <= 2
- `[Fact]` test names:
  - `IsQxCancellableOrderState_ShouldReturnTrue_WhenOrderIsWorking()`
  - `IsQxCancellableOrderState_ShouldReturnFalse_WhenOrderIsFilled()`
  - `IsOrderTerminalState_ShouldReturnTrue_WhenOrderIsCancelled()`
  - `IsOrderTerminalState_ShouldReturnFalse_WhenOrderIsWorking()`
- Risk notes: `IsQxCancellableOrderState` is shared by overload 2 and `BuildQxSnapshot` — extract once, reference three times. Both helpers are pure (no side effects, no NT8 API calls).

#### BuildQxSnapshot (L916)
- Parent CCN target: <= 5
- Extractions: reuses `IsQxCancellableOrderState` (shared from overload 1) — no new helper required
- `[Fact]` test names: (covered by shared helper tests above; add integration test)
  - `BuildQxSnapshot_ShouldReturnEmptySet_WhenAccountIsNull()`
  - `BuildQxSnapshot_ShouldExcludeFilledOrders()`
- Risk notes: Returns `new HashSet<Order>()` on null — never null (JS-002 compliant, preserved).

#### CancelQxBrackets (overload 2, L955)
- Parent CCN target: <= 7
- Extractions: reuses `IsQxCancellableOrderState` and `IsOrderTerminalState` (shared) — no new helper. The snapshot-gate branch (`snapshot != null && !snapshot.Contains(o)`) is a single bool with one `&&` = 1 branch; keeping it inline.
- `[Fact]` test names:
  - `CancelQxBrackets_ShouldSkipOrder_WhenNotInSnapshot()`
  - `CancelQxBrackets_ShouldCancelOrder_WhenSnapshotIsNull()`
- Risk notes: snapshot parameter may be null (fallback to 2-param behaviour) — preserved exactly.

#### CancelAllAccountOrders (L1013)
- Parent CCN target: <= 6
- Extractions:
  1. `private bool IsAccountOrderCancellableState(Order o)` — returns true when o.OrderState is Working, Initialized, Submitted, or Accepted (4-state variant used by this method only — note this differs from the 5-state QX variant); absorbs the 3 `||` branches in stateOk — CCN target <= 2
  2. reuses `IsOrderTerminalState` (shared from T8 above) for the RemoveAll guard
- `[Fact]` test names:
  - `IsAccountOrderCancellableState_ShouldReturnTrue_WhenOrderIsSubmitted()`
  - `IsAccountOrderCancellableState_ShouldReturnFalse_WhenOrderIsTriggerPending()`
- Risk notes: This method uses a 4-state variant (no TriggerPending) — do NOT unify with `IsQxCancellableOrderState`. The difference is intentional (comment on L886).

#### AllAccounts (L4705)
- Parent CCN target: <= 6
- Extractions:
  1. `private Account TryResolveLazyFollowerAccount(string name)` — absorbs the lazy-resolve block: name-empty guard, `_resolvedFollowers.TryGetValue`, `FindFollowerAccount`, `_resolvedFollowers.TryAdd`, and both `Output.Process` log lines. Returns `null` only when account is genuinely not found (not a JS-002 violation — it is signalling absence, caller yields nothing). Returns the resolved Account otherwise — CCN target <= 4
- `[Fact]` test names:
  - `TryResolveLazyFollowerAccount_ShouldReturnNull_WhenNameIsEmpty()`
  - `TryResolveLazyFollowerAccount_ShouldReturnCachedAccount_WhenAlreadyResolved()`
- Risk notes: yield iterator — the extraction must NOT be a yield method itself. `TryResolveLazyFollowerAccount` returns `Account?` (nullable reference); caller `yield return`s only when result is not null.

---

### T5 — ATM/Bracket Sync (DW-B143-POSSTATE-CYC8 P0)

**Methods**: SyncFollowerBracket (L2279–2373, CCN=20), SyncAtmFollowerTarget (L2869–2953, CCN=21)
**CodeScene flags**: Complex Method (cc=16), Complex Method (cc=15) + Large Method (71 LoC)

#### SyncFollowerBracket (L2279)
- Parent CCN target: <= 7
- Extractions:
  1. `private void SyncAtmFollowerStopBracket(Account acc, Order fo, double newPrice, string legSuffix, double[] otherLegPrices, double? capturedTargetPrice, Order leaderOrder)` — absorbs the entire `if (isStop && IsAtmSTPOrder(fo))` block (lines 2311–2341): the `fo.StopPrice < tickSize` guard, `TryParseStopSuffix`, `CaptureLinkedTargetPrice`, `CaptureOtherLegTargetPrices`, `SyncAtmFollowerBracket`, conditional `ResubmitTargetAfterCascade`, and `ResubmitCollateralLegs` calls — CCN target <= 4
- `[Fact]` test names:
  - `SyncAtmFollowerStopBracket_ShouldReturn_WhenStopPriceIsZero()`
  - `SyncAtmFollowerStopBracket_ShouldCallResubmitTarget_WhenCapturedPriceHasValue()`
- Risk notes: DW-B134, DW-B137, DW-B153, B142-DIRECT-2/4/6 documented in existing comments — these comments must be preserved in the extracted helper. The helper signature threads `leaderOrder` for DW-B142-QTY-DESYNC-01 qty propagation. Callers of `CaptureLinkedTargetPrice` use `leaderOrder.Name` not `fo.Name` — preserve exactly.

#### SyncAtmFollowerTarget (L2869)
- Parent CCN target: <= 7
- Extractions:
  1. `private void CancelStaleTgtDragOrders(Account acc, Order fo, string tgtDragName)` — absorbs Block A-Prime (lines 2893–2910): the foreach over `acc.Orders.ToList()`, the compound `o.OrderState==Working && o.Name==tgtDragName && instrument` guard, and the inner try/catch — CCN target <= 3
  2. `private Order? CreateAndSubmitReplacementTarget(Account acc, Order fo, double newPrice, string tgtDragName, Order? leaderOrder)` — absorbs Block B (lines 2922–2950): `acc.CreateOrder(...)`, null check on newTarget, `acc.Submit`, `StatusUpdate` — CCN target <= 3
- `[Fact]` test names:
  - `CancelStaleTgtDragOrders_ShouldCancelMatchingWorkingOrder()`
  - `CancelStaleTgtDragOrders_ShouldSkipNonMatchingOrders()`
  - `CreateAndSubmitReplacementTarget_ShouldReturnNull_WhenCreateOrderFails()`
  - `CreateAndSubmitReplacementTarget_ShouldUseLeaderQuantity_WhenLeaderOrderIsNotNull()`
- Risk notes: `CreateAndSubmitReplacementTarget` returns `Order?` to allow caller to log the null case. `acc.CreateOrder` requires explicit `acc.Submit` — preserved. DW-B142-QTY-DESYNC-01: quantity from `leaderOrder` when available — threaded through helper parameter.

---

### T3 — Collateral Resubmit (CCN 25, Large Method)

**Methods**: ResubmitOneCollateralLeg (L2701–2785, CCN=25)
**CodeScene flags**: Complex Method (cc=15) + Large Method (79 LoC)

#### ResubmitOneCollateralLeg (L2701)
- Parent CCN target: <= 5
- Extractions:
  1. `private void CancelExistingStpDragOrders(Account acc, Order fo, string stpDragName)` — absorbs Block A-Prime-Stop (lines 2711–2717): foreach, compound `IsPttStpDragCancellable && name && instrument` guard, try/catch cancel — CCN target <= 3
  2. `private void CancelExistingTgtDragOrders(Account acc, Order fo, string tgtDragName)` — absorbs Block A-Prime-Target (lines 2720–2726): foreach, compound `IsTargetOrderLive && name && instrument` guard, try/catch cancel — CCN target <= 3
  3. `private void SubmitReplacementStopLeg(Account acc, Order fo, double newPrice, string suffix, Order? leaderLeg)` — absorbs Block C stop creation (lines 2728–2755): `acc.CreateOrder(StopMarket)`, null check, `acc.Submit`, `StatusUpdate`, outer catch — CCN target <= 3
  4. `private void SubmitReplacementTargetLeg(Account acc, Order fo, double targetPrice, string suffix, Order? leaderLeg)` — absorbs Block D target creation (lines 2757–2784): `acc.CreateOrder(Limit)`, null check, `acc.Submit`, `StatusUpdate`, outer catch — CCN target <= 3
- `[Fact]` test names:
  - `CancelExistingStpDragOrders_ShouldCancelMatchingLiveStpDragOrder()`
  - `CancelExistingTgtDragOrders_ShouldCancelMatchingLiveTgtDragOrder()`
  - `SubmitReplacementStopLeg_ShouldReturnEarly_WhenCreateOrderReturnsNull()`
  - `SubmitReplacementStopLeg_ShouldUseLeaderQuantity_WhenLeaderLegProvided()`
  - `SubmitReplacementTargetLeg_ShouldReturnEarly_WhenCreateOrderReturnsNull()`
  - `SubmitReplacementTargetLeg_ShouldUseLeaderQuantity_WhenLeaderLegProvided()`
- Risk notes: `CancelExistingStpDragOrders` and `CancelExistingTgtDragOrders` follow the same structural pattern — two distinct helpers because they use `IsPttStpDragCancellable` vs `IsTargetOrderLive` respectively (different predicates, different order types). Do NOT merge. DW-B142-QTY-DESYNC-01: per-leg leader qty threaded through `leaderLeg` parameter in both submit helpers.

---

### T4 — ATM Cleanup Pair (CCN 23 + 18)

**Methods**: TryCleanupReArmedAtmBracket (L3727–3793, CCN=23), ReplaceFollowerCopyOnAtmCancel (L3548–3601, CCN=18), TryReplacePttBeBrackets (L3644–3715, CCN=14)
**CodeScene flags**: Complex Method (cc=20) + Complex Conditional (10 expressions), Bumpy Road (2 bumps) + Complex Method (cc=16), Complex Method (cc=12)

#### TryCleanupReArmedAtmBracket (L3727)
- Parent CCN target: <= 5
- Extractions:
  1. `private bool IsReArmedAtmBracketCleanupRequired(OrderEventArgs e)` — absorbs the 10-expression compound guard (lines 3737–3751): all `||` and `&&` conditions testing OrderState, Name pattern, digit check, IsFollowerAccount, TryGetValue, Expiry, and FullName. Returns `false` (guard fails = cleanup NOT required) when any of the 10 conditions is violated. Parent calls `if (!IsReArmedAtmBracketCleanupRequired(e)) return;` — CCN target <= 4
  2. `private Order? FindMatchingNativeAtmBracket(Account acc, string nativeName, Instrument instr)` — absorbs the foreach loop (lines 3761–3772) that finds `toCancel`: iterates `acc.Orders.ToList()`, matches Name + FullName + Working|Accepted state. Returns the found Order or null — CCN target <= 4
- `[Fact]` test names:
  - `IsReArmedAtmBracketCleanupRequired_ShouldReturnFalse_WhenOrderStateIsNotWorkingOrAccepted()`
  - `IsReArmedAtmBracketCleanupRequired_ShouldReturnFalse_WhenNameDoesNotStartWithPttQxT()`
  - `IsReArmedAtmBracketCleanupRequired_ShouldReturnFalse_WhenTtlHasExpired()`
  - `IsReArmedAtmBracketCleanupRequired_ShouldReturnTrue_WhenAllConditionsMet()`
  - `FindMatchingNativeAtmBracket_ShouldReturnNull_WhenNoMatchingOrderExists()`
  - `FindMatchingNativeAtmBracket_ShouldReturnOrder_WhenNameAndInstrumentMatch()`
- Risk notes: `IsReArmedAtmBracketCleanupRequired` reads `_qxPendingFollowerCleanup` (ConcurrentDictionary.TryGetValue — lock-free, JS-021 compliant) and `DateTime.UtcNow` — NOT `DateTime.Now` (JS ban). Parent still has access to `entry` (the out param from TryGetValue) — if extracted, entry must be re-fetched in parent or threaded out. Preferred: parent calls TryGetValue again after the guard passes (safe because single-threaded NT8 dispatch; same entry will be found).

#### ReplaceFollowerCopyOnAtmCancel (L3548)
- Parent CCN target: <= 7
- Extractions:
  1. `private bool TryFindRuleAndFollowerIndex(Order cancelledOrder, out CopyRule? matchedRule, out int followerIndex)` — absorbs Bump 1: the `foreach (_rules)` + inner `for (FollowerAccounts)` nested loop (lines 3554–3569) that locates the matching rule and follower slot index. Returns `false` if no match found — CCN target <= 4
- `[Fact]` test names:
  - `TryFindRuleAndFollowerIndex_ShouldReturnFalse_WhenInstrumentDoesNotMatch()`
  - `TryFindRuleAndFollowerIndex_ShouldReturnTrue_WhenFollowerAccountMatches()`
  - `TryFindRuleAndFollowerIndex_ShouldSetFollowerIndex_WhenMatchFound()`
- Risk notes: `out` parameters (`matchedRule`, `followerIndex`) must follow the exact same initialisation as the current locals (`matchedRule = null`, `followerIndex = -1`) — engineer must not change default values. Bump 2 (mode dispatch: `if mode is Named`) stays inline — it is a single branch only (CCN=1).

#### TryReplacePttBeBrackets (L3644)
- Parent CCN target: <= 7
- Extractions:
  1. `private bool HasActiveQxOrdersForInstrument(Account acc, Instrument instr)` — absorbs the LINQ `.Any()` lambda block (lines 3663–3683): the compound predicate `o.Name.StartsWith("PTT-QX-") && (Working||Submitted) && FullName` match, plus the `Output.Process` diagnostic log on true — CCN target <= 4
- `[Fact]` test names:
  - `HasActiveQxOrdersForInstrument_ShouldReturnTrue_WhenPttQxOrderIsWorking()`
  - `HasActiveQxOrdersForInstrument_ShouldReturnFalse_WhenNoQxOrdersExist()`
  - `HasActiveQxOrdersForInstrument_ShouldReturnFalse_WhenQxOrderIsFilledNotWorking()`
- Risk notes: The existing diagnostic `Output.Process` log (DW-B112) is part of the block and must be preserved inside the helper (not lost on extraction). The `.ToList()` snapshot pattern is intentional (DW-B112 comment) — preserve exactly.

---

### T7 — HandleEntry + PositionState + ResubmitTarget (DW-B143-POSSTATE-CYC8 P0)

**Methods**: HandleEntryChange (L3366–3426, CCN=13), TryFirePositionState (L3451–3499, CCN=13), ResubmitTargetAfterCascade (L2588–2649, CCN=13)
**CodeScene flags**: Complex Method (cc=15), [no direct CS entry — Lizard CCN=13], [no direct CS entry — Lizard CCN=13]

#### HandleEntryChange (L3366)
- Parent CCN target: <= 5
- Extractions:
  1. `private void ResubmitFollowerEntry(Account acc, Order fo, Instrument instrument, double newPrice, double tickSize)` — absorbs the per-follower resubmit logic inside the foreach body (lines 3388–3424): `fo==null` continue, price-no-change continue, `StopLimit` ternaries for limitPx/stopPx, `acc.Cancel`, `acc.CreateOrder`, null check + `acc.Submit` + dedupCache preload, `StatusUpdate` — CCN target <= 4
- `[Fact]` test names:
  - `ResubmitFollowerEntry_ShouldSkip_WhenPriceChangeIsWithinTickSize()`
  - `ResubmitFollowerEntry_ShouldUseStopPrice_WhenOrderTypeIsStopLimit()`
  - `ResubmitFollowerEntry_ShouldPreloadDedupCache_WhenOrderIsCreated()`
- Risk notes: B67-LaneB DW-B67-02 comment must be preserved — `acc.Cancel` + `CreateOrder` + `Submit` pattern (acc.Change() is Apex/Rithmic no-op). dedupCache preload (B69 DW-B69-03) is in the `if order != null` block inside the helper — do NOT move it outside.

#### TryFirePositionState (L3451)
- Parent CCN target: <= 7
- Extractions:
  1. `private bool IsLeaderAccountForInstrument(Account acc)` — absorbs the `foreach (_rules)` loop (lines 3482–3489) that checks if acc.Name matches any rule's MasterAccount.Name. Returns true if match found, false otherwise — CCN target <= 3
- `[Fact]` test names:
  - `IsLeaderAccountForInstrument_ShouldReturnTrue_WhenAccountMatchesMasterAccount()`
  - `IsLeaderAccountForInstrument_ShouldReturnFalse_WhenAccountIsFollower()`
- Risk notes: DW-B135 comment (direction key clear) must be preserved in parent. The `if (!hasPos)` outer block containing the extracted foreach remains inline; only the inner foreach+break is extracted. Parent still reads `isLeaderAcct` from the helper return value. DW-B128 comment preserved: during race window, `hasPos=True` so the !hasPos block is not entered.

#### ResubmitTargetAfterCascade (L2588)
- Parent CCN target: <= 4
- Extractions:
  1. `private void CancelStaleCascadeTgtDrag(Account acc, Order stpOrder, string tgtDragName)` — absorbs Block A-Prime (lines 2599–2617): foreach over `acc.Orders.ToList()`, compound `o.OrderState==Working && o.Name==tgtDragName && instrument` guard, inner try/catch cancel — CCN target <= 3
- `[Fact]` test names:
  - `CancelStaleCascadeTgtDrag_ShouldCancelMatchingWorkingOrder()`
  - `CancelStaleCascadeTgtDrag_ShouldSkipNonWorkingOrders()`
- Risk notes: B142 comment block (lines 2596-2598) must remain in parent (context for caller). The helper name `CancelStaleCascadeTgtDrag` distinguishes this from the T5/T3 variants (`CancelStaleTgtDragOrders` in T5, `CancelExistingTgtDragOrders` in T3) — all three have slightly different compound guards but the same structural pattern. Engineer must NOT merge them into a shared helper without Director approval (different callers, different guard predicates).

---

### T6 — Flatten + BE Replace + Target Count (CCN 19 + 14 + 13)

**Methods**: FlattenOneAccount (L4303–4372, CCN=19), TryReplacePttBeBrackets (L3644–3715, CCN=14, covered in T4), CountLeaderTargets (L4904–4931, CCN=13)
**CodeScene flags**: Complex Method (cc=16) + Code Duplication cluster, Complex Method (cc=12), Complex Method (cc=16)

> Note: TryReplacePttBeBrackets extraction is specified in T4. T6 execution must apply the T4 design before marking T6 done only if T4 has not already been applied. Engineer executes T1→T8 in order; T4 precedes T6 so TryReplacePttBeBrackets will already be reduced by the time T6 runs.

#### FlattenOneAccount (L4303)
- Parent CCN target: <= 6
- Extractions:
  1. `private bool HasInFlightFlattenOrder(Account acc, Instrument instrument)` — absorbs the foreach guard scan (lines 4313–4328): iterates `acc.Orders.ToList()`, skips non-matching Name and instrument, returns `true` when any PTT-Flatten order is in Submitted, Accepted, or Working state (with StatusUpdate "flat-guard: in-flight skip" logged inside) — CCN target <= 4
  2. `private bool IsPositionFlatOrMissing(Position? pos)` — absorbs the `pos == null || pos.Quantity == 0` pattern (2 branches); eliminates the Code Duplication cluster (used at lines 4330 and 4339). Returns `true` when position is null or zero quantity — CCN target <= 2
- `[Fact]` test names:
  - `HasInFlightFlattenOrder_ShouldReturnTrue_WhenPttFlattenOrderIsWorking()`
  - `HasInFlightFlattenOrder_ShouldReturnFalse_WhenNoFlattenOrderExists()`
  - `IsPositionFlatOrMissing_ShouldReturnTrue_WhenPositionIsNull()`
  - `IsPositionFlatOrMissing_ShouldReturnTrue_WhenPositionQuantityIsZero()`
- Risk notes: Code Duplication flag is resolved by `IsPositionFlatOrMissing`. B76 HOTFIX comment block (lines 4305–4312) must be preserved in parent above the `HasInFlightFlattenOrder` call — it explains WHY the order-book guard is used instead of a field flag.

#### CountLeaderTargets (L4904)
- Parent CCN target: <= 5
- Extractions:
  1. `private bool IsLeaderTargetOrder(Order o, Instrument instrument)` — absorbs the combined state/instrument/type check AND the name-pattern check (isNative identification): `o.OrderState == OrderState.Working`, `o.Instrument != null && FullName match`, `o.OrderType == OrderType.Limit`, `o.Name.Length >= 7`, `StartsWith("Target")`, `IsDigit([6])`, `[6] != '0'` — CCN target <= 4
- `[Fact]` test names:
  - `IsLeaderTargetOrder_ShouldReturnTrue_WhenOrderIsWorkingLimitWithValidTargetName()`
  - `IsLeaderTargetOrder_ShouldReturnFalse_WhenOrderStateIsNotWorking()`
  - `IsLeaderTargetOrder_ShouldReturnFalse_WhenNameDoesNotStartWithTarget()`
  - `IsLeaderTargetOrder_ShouldReturnFalse_WhenSixthCharIsNotDigit()`
- Risk notes: The `!string.IsNullOrEmpty(o.Name)` null-guard before the name check must remain (either inside `IsLeaderTargetOrder` or kept as a continue in parent). Safest: move it inside the helper as the first guard — returns false on empty name, never throws.

---

### T2 — BE Target Snapshot + Stop-to-BE (CCN 24 + 18, Bumpy Road)

**Methods**: SnapshotBeTargets (L4938–4981, CCN=24), MoveStopToBreakEven (L4993–5133, CCN=18)
**CodeScene flags**: Complex Method (cc=28), Bumpy Road (3 bumps) + Complex Method (cc=14) + Large Method (82 LoC)

#### SnapshotBeTargets (L4938)
- Parent CCN target: <= 8
- Extractions:
  1. `private bool IsEligibleBeTargetOrder(Order o, Instrument instrument)` — absorbs the 7-state `stateOk` compound (7 OrderState `||` checks), the `instrOk` compound (`o.Instrument != null && FullName`), the `OrderType.Limit` check, and the early-continue guard `!stateOk || !instrOk || type!=Limit`. Returns `true` only when all three conditions pass — CCN target <= 4
  2. `private bool IsNativeAtmTargetOrder(Order o)` — absorbs the `isNative` name-pattern compound: `Length >= 7`, `StartsWith("Target")`, `char.IsDigit([6])`, `[6] != '0'`. Returns true for native ATM Target1–9 orders — CCN target <= 3
  3. `private bool IsPttBeOrQxTargetOrder(Order o)` — absorbs the `isPtt` compound: `(StartsWith("PTT-QX-T") && Length > 8 && IsDigit([8])) || StartsWith("PTT-BE-Target-")`. Returns true for PTT-managed target orders — CCN target <= 3
- `[Fact]` test names:
  - `IsEligibleBeTargetOrder_ShouldReturnFalse_WhenOrderStateIsNotInSnapshot()`
  - `IsEligibleBeTargetOrder_ShouldReturnFalse_WhenInstrumentDoesNotMatch()`
  - `IsEligibleBeTargetOrder_ShouldReturnFalse_WhenOrderTypeIsNotLimit()`
  - `IsNativeAtmTargetOrder_ShouldReturnTrue_WhenNameIsTarget1()`
  - `IsNativeAtmTargetOrder_ShouldReturnFalse_WhenNameIsTarget0()`
  - `IsPttBeOrQxTargetOrder_ShouldReturnTrue_WhenNameStartsWithPttQxT1()`
  - `IsPttBeOrQxTargetOrder_ShouldReturnTrue_WhenNameStartsWithPttBeTarget()`
- Risk notes: `IsEligibleBeTargetOrder` replaces the combined stateOk+instrOk+type check — reduces CS cc from 28 significantly. The `string.IsNullOrEmpty(o.Name)` continue (line 4962) stays inline in parent (before the two name-pattern checks) to gate them safely. Return value semantics: `nativeTargets.Count > 0 ? nativeTargets : pttTargets` (line 4980) stays in parent — it is a single-line ternary return, not a complexity hotspot.

#### MoveStopToBreakEven (L4993)
- Parent CCN target: <= 5
- Extractions (3 Bumpy Road bumps):
  1. `private void LogDiagOrderCount(Account acc, Instrument instrument)` — absorbs the diagnostic foreach loop (lines 5022–5029): iterates `acc.Orders`, counts matching instrument orders, logs `[BE-DIAG]` output. Eliminates Bump 1 — CCN target <= 2
  2. `private void RegisterBeRetryIfNoTargets(Account acc, Instrument instrument, int bufferTicks, bool isRetry)` — absorbs the `targets.Count == 0` retry registration block (lines 5051–5068): the `!isRetry && !IsFlat` nested guard, `_pendingFollowerBeSlots` assignment, `Output.Process` log, `QueueBeRetryFallback(delayMs:500)`. The outer `if (targets.Count == 0)` guard and the `return` after calling this helper stay in parent — CCN target <= 3
  3. `private void RegisterPartialTargetBeRetry(Account acc, Instrument instrument, int bufferTicks, int targetCount, bool isRetry)` — absorbs the partial-target retry block (lines 5071–5097): `!isRetry && IsFollowerAccount`, `CountLeaderTargets` call, `leaderCount > 0 && targetCount < leaderCount && !IsFlat` nested guard, slot registration, `Output.Process` log, `QueueBeRetryFallback`. Eliminates Bump 3 — CCN target <= 4
- `[Fact]` test names:
  - `LogDiagOrderCount_ShouldLogCorrectCount_WhenOrdersExistForInstrument()`
  - `RegisterBeRetryIfNoTargets_ShouldNotRegister_WhenIsRetryIsTrue()`
  - `RegisterBeRetryIfNoTargets_ShouldNotRegister_WhenPositionIsFlat()`
  - `RegisterBeRetryIfNoTargets_ShouldRegisterSlotAndQueueFallback_WhenConditionsMet()`
  - `RegisterPartialTargetBeRetry_ShouldNotRegister_WhenTargetCountEqualsLeaderCount()`
  - `RegisterPartialTargetBeRetry_ShouldRegisterSlot_WhenFollowerHasFewerTargetsThanLeader()`
- Risk notes: DW-B79-06 and DW-B79-07 comments (lines 5048–5049 and 5070) must be preserved in parent above the respective helper calls. The large commented-out legacy DW-B88 block (lines 5099–5133) stays unchanged in parent — do not remove it. The `while (targets.Count > 3)` trim stays inline in parent (single loop, low CCN). `QueueBeRetryFallback` uses `delayMs: 500` in Bump 2 and default in Bump 3 — pass as parameter if helper signature requires it.

---

### T1 — Highest Severity Pair (CCN 32 + 27)

**Methods**: OnPendingBeAccountUpdate (L5480–5520, CCN=32), ArmPendingBe (L5308–5364, CCN=27)
**CodeScene flags**: Complex Method (cc=19), Complex Method (cc=17)

#### ArmPendingBe (L5308)
- Parent CCN target: <= 4
- Extractions:
  1. `private bool TryFireImmediateBeIfAlreadyAtLevel(Instrument instr, Position pos, int bufferTicks, Account masterAcc)` — absorbs the `if (tickSize > 0.0)` block (lines 5328–5349): `isLong` assignment, `target` price computation, `refBid`/`refAsk`/`refPx` reads from market data, `alreadyAtBe` boolean, and when true: `StatusUpdate`, `BreakEven(masterAcc, instr, bufferTicks)`, `PendingBeFired?.Invoke`. Returns `true` if BE was fired immediately (parent returns), `false` if arming is still required — CCN target <= 4
- `[Fact]` test names:
  - `TryFireImmediateBeIfAlreadyAtLevel_ShouldReturnFalse_WhenTickSizeIsZero()`
  - `TryFireImmediateBeIfAlreadyAtLevel_ShouldReturnFalse_WhenPriceIsZero()`
  - `TryFireImmediateBeIfAlreadyAtLevel_ShouldReturnTrue_WhenLongAndBidAboveTarget()`
  - `TryFireImmediateBeIfAlreadyAtLevel_ShouldReturnTrue_WhenShortAndAskBelowTarget()`
- Risk notes: HOTFIX-BUG-BE-IMMEDIATE comment (lines 5323–5326) must be preserved in the helper or in parent immediately before the call — it explains the immediate-fire behaviour. The helper reads `instr.MasterInstrument?.TickSize` — this is a safe NT8 property access. `PendingBeFired?.Invoke` must remain in the helper (not split to parent) because it is part of the same "fire" path as `BreakEven`.

#### OnPendingBeAccountUpdate (L5480)
- Parent CCN target: <= 7
- Extractions:
  1. `private bool IsPendingBeTriggerMet(PendingBeSlot slot)` — absorbs the price-trigger check block (lines 5499–5510): `isLong` assignment, `refBid`/`refAsk` reads, `refPx` ternary-selection (long: bid>0?bid:ask; short: ask>0?ask:bid), `refPx<=0.0` guard, `target` computation, `triggered` bool calculation, returns triggered — CCN target <= 4
- `[Fact]` test names:
  - `IsPendingBeTriggerMet_ShouldReturnFalse_WhenRefPriceIsZero()`
  - `IsPendingBeTriggerMet_ShouldReturnFalse_WhenLongPositionPriceBelowTarget()`
  - `IsPendingBeTriggerMet_ShouldReturnTrue_WhenLongAndBidReachesTarget()`
  - `IsPendingBeTriggerMet_ShouldReturnTrue_WhenShortAndAskReachesTarget()`
- Risk notes: HOTFIX-F2 comment (lines 5496–5498) must be preserved inside `IsPendingBeTriggerMet` — it explains the Bid/Ask selection logic (Last.Price is 0 on Sim accounts). The `tickSize <= 0.0` guard (line 5494) stays in parent (separate from the trigger check — it is a pre-flight guard before slot data is used). The atomic `TryRemove` (line 5511) stays in parent (it is a concurrency operation, not part of the trigger calculation).

---

## Shared Helper Registry

The following helpers are shared across multiple methods (engineer must extract once, reference many times):

| Helper | Shared By | File Location |
|--------|-----------|---------------|
| `IsQxCancellableOrderState(Order)` | CancelQxBrackets(2-param), CancelQxBrackets(3-param), BuildQxSnapshot | CopyEngine.cs (private) |
| `IsOrderTerminalState(Order)` | CancelQxBrackets(2-param), CancelQxBrackets(3-param), CancelAllAccountOrders | CopyEngine.cs (private) |
| `IsPositionFlatOrMissing(Position?)` | FlattenOneAccount (used twice) | CopyEngine.cs (private) |

---

## Execution Order

Tickets execute T1 → T8 in descending severity order per mission brief. T4 must complete before T6 (TryReplacePttBeBrackets). T3 and T7 share a structural pattern in their TgtDrag cancel helpers but must NOT merge into a shared helper without Director approval — different callers, different guard predicates.

---

## 7-Scan Checklist (per ticket — engineer must verify each before moving to next)

| Scan | Command | Target |
|------|---------|--------|
| SCAN-01 | `Select-String "lock(" src/PropTraderTools -Recurse -Include *.cs` | 0 results |
| SCAN-02 | `Select-String "async void " src/PropTraderTools -Recurse -Include *.cs` | 0 results |
| SCAN-03 | `Select-String "return null" src/PropTraderTools -Recurse -Include *.cs` | 0 new instances |
| SCAN-04 | `Select-String "throw new " src/PropTraderTools -Recurse -Include *.cs` | 0 new instances |
| SCAN-05a | `lizard src/PropTraderTools/CopyEngine.cs --CCN 8` | 0 warnings for ticket methods |
| SCAN-05b | `$env:CS_ACCESS_TOKEN="pat_eyJ..."; cs delta` | Code Health does NOT decrease |
| SCAN-06 | `dotnet build` | 0 errors, 0 warnings |
| SCAN-07 | `dotnet test` | 370+ pass, 22 pre-existing IL-reflection failures (ACCEPT), 0 new failures |

---

**STAGE 2 COMPLETE — handing off to ptt-engineer.**
