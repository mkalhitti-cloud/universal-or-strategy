# Pipeline Lane Prompts — B72 / B73 / B74

Generated after all four BUG 1-4 fixes tested and confirmed (2026-08-16/17 live sessions).
Three parallel lanes formalise all direct hotfixes since B71.

---

## PRE-FLIGHT (Director runs this ONCE before pasting any lane prompt)

Remove the DIAG-MOVESTOP-01 Output.Process log lines from CopyEngine.cs.
These were diagnostic-only scaffolding — all four BE scenarios are confirmed working.
They add noise to the NT8 Output tab in production.

**Grep for removal targets:**
```
grep -n 'Output.Process.*MSTBE' src/PropTraderTools/CopyEngine.cs
```

Remove every line matching `NinjaTrader.Code.Output.Process("[MSTBE]` in `MoveStopToBreakEven`.
After removal run: `powershell -File scripts\sync-ptt-to-nt8.ps1`
Confirm `CopyEngine.cs` shows `COPIED`.

---

## SUPERSEDED / DO NOT PIPELINE (reference only)

The following were overwritten by later fixes and must NOT receive separate pipeline runs:

| Superseded ID | Superseded by | Note |
|---|---|---|
| HOTFIX-MSTBE-OCO-REUSE (_mstbeOcoSeq=0) | HOTFIX-MSTBE-OCO-TICKSEED-01 | Seed changed 0→TickCount; D5 format still in effect |
| HOTFIX-THREAD-DISPATCH-01 | HOTFIX-DISPATCH-FIX-01 + HOTFIX-BUFLABEL-02 | Same two handlers, superseded approach |
| HOTFIX-DW-B64-01 (re-insert before HandleEntryChange) | HOTFIX-ENTRY-DRAG-DEDUP (upsert in HandleEntryChange) | Both fix same Gate C dedupCache; ENTRY-DRAG-DEDUP is final |

---

## LANE A — B72-LaneA
### Engine Logic: CopyEngine.cs + PttBreakEven.cs
### 22 hotfixes — largest block

---

**PASTE THIS INTO A FRESH `ptt-orchestrator` SESSION:**

```
You are ptt-orchestrator for block B72-LaneA.
Pipeline: Ph1 ptt-architect -> Ph2 ptt-plan-reviewer -> Ph3 ptt-architect ->
          Ph3.5 ptt-ticket-reviewer -> Ph4a ptt-engineer ->
          Ph4b ptt-verifier -> Ph5 ptt-plan-reviewer.
ALL 7 phases are mandatory. None are skippable. None are combinable.
Output directory: docs/brain/B72-LaneA/
Jane Street rules: docs/standards/jane-street/RULES_CATALOG.md (JS-001..JS-110)
OKF wiki: docs/intel/jane-street/
NT8 API reference: docs/standards/NT8_FULL_REFERENCE.md
Test framework: xUnit ONLY (never NUnit, never MSTest)
After any .cs edit by Ph4a: powershell -File scripts\sync-ptt-to-nt8.ps1

PIPELINE IS RETROSPECTIVE. The code changes are already in src/.
- Ph1 + Ph3 architects DESCRIBE what is there and WHY (no new logic).
- Ph4a engineer writes xUnit TESTS only + removes DIAG output lines if still present.
  Ph4a may make trivial cleanup edits (whitespace, XML doc) but NO logic changes.
- Ph4b verifier confirms code in src/ matches the architecture plan.

════════════════════════════════════════════════════════════════
SCOPE — 22 hotfixes in CopyEngine.cs and PttBreakEven.cs
════════════════════════════════════════════════════════════════

All of the following are APPLIED and awaiting pipeline formalisation.
The repair log with full diffs is at docs/brain/NO-PIPELINE-REPAIRS.md.

B72-A-01  HOTFIX-BE-ALL-01
  File: CopyEngine.cs
  Method: ArmAllPendingBe
  Change: Replaced SubmitBeStop(...) with ArmPendingBe(pos.Instrument, acc, bufferTicks)
  Tests needed:
    T_BEALL_01: ArmAllPendingBe with 1 non-follower open account -> _pendingBeSlots populated
    T_BEALL_02: ArmAllPendingBe with follower account -> slot NOT added (skipped)
    T_BEALL_03: ArmAllPendingBe with flat account -> slot NOT added (ArmPendingBe flat guard)
    T_BEALL_04: IsPendingSlotsEmpty returns false after ArmAllPendingBe with open position

B72-A-02  HOTFIX-QX-DOUBLE-01 (CopyEngine.cs part)
  File: CopyEngine.cs
  Method: CancelQxBrackets (stateOk filter)
  Change: Added OrderState.TriggerPending to stateOk filter
  Tests needed:
    T_QX_DOUBLE_01: CancelQxBrackets with TriggerPending order -> in stale list
    T_QX_DOUBLE_02: CancelQxBrackets with Submitted order -> in stale list (regression)
    T_QX_DOUBLE_03: CancelQxBrackets with Filled order -> NOT in stale list (terminal safe)

B72-A-03  HOTFIX-QX-DOUBLE-01 (PttBreakEven.cs part)
  File: PttBreakEven.cs
  Method: CancelStaleBracketsLocal (stateOk filter)
  Change: Added TriggerPending, Submitted, Accepted to stateOk filter
  Tests needed:
    T_BE_CANCEL_01: CancelStaleBracketsLocal with TriggerPending -> order IS in stale list
    T_BE_CANCEL_02: CancelStaleBracketsLocal with Accepted -> order IS in stale list
    T_BE_CANCEL_03: CancelStaleBracketsLocal with PTT-BE-Stop (exact) -> NOT cancelled (notBe guard)

B72-A-04  HOTFIX-BUG2-BE-RESET
  File: CopyEngine.cs
  Method: OnOrderUpdate (TryFirePositionState placement)
  Change: Moved TryFirePositionState from pre-Gate-1 to post-Gate-2.5
  Tests needed:
    T_BE_RESET_01: follower Filled event does NOT fire PositionStateChanged
    T_BE_RESET_02: leader Filled event DOES fire PositionStateChanged (regression)

B72-A-05  HOTFIX-DW-B64-01 — SUPERSEDED by HOTFIX-ENTRY-DRAG-DEDUP
  NOTE: Do NOT write tests for this — it was overwritten. See B72-A-06 below.

B72-A-06  HOTFIX-ENTRY-DRAG-DEDUP (supersedes DW-B64-01)
  File: CopyEngine.cs
  Method: HandleEntryChange
  Change: TryRemove -> upsert (_dedupCache[orderId] = newPrice)
  Tests needed:
    T_DRAG_DEDUP_02: Working-state re-entry after drag -> Gate C sees delta=0 -> DispatchCopy NOT called
    T_DRAG_DEDUP_03: After HandleEntryChange -> orderId key still present in _dedupCache at newPrice
    T_DRAG_DEDUP_04: New follower order (cancel+resubmit path) -> new orderId keyed correctly

B72-A-07  HOTFIX-FIX-B-TRYFIRE-CANCELLED
  File: CopyEngine.cs
  Method: TryFirePositionState
  Change: Removed Cancelled and Rejected from trigger filter; only Filled and PartFilled remain
  Tests needed:
    T_TRYFIRE_01: Cancelled order event -> TryFirePositionState does NOT fire PositionStateChanged
    T_TRYFIRE_02: Rejected order event -> TryFirePositionState does NOT fire PositionStateChanged
    T_TRYFIRE_03: Filled order event -> TryFirePositionState DOES fire PositionStateChanged

B72-A-08  HOTFIX-BUG-BE-INSTRUMENT-REF
  File: CopyEngine.cs
  Method: MoveStopToBreakEven (instrument filter)
  Change: order.Instrument != instrument -> order.Instrument?.FullName != instrument.FullName
  Tests needed:
    T_BE_MOVE_01: MoveStopToBreakEven with different Instrument instances, same FullName -> Change() IS called
    T_BE_MOVE_02: MoveStopToBreakEven with null order.Instrument -> order skipped (no NRE)

B72-A-09  HOTFIX-BUG-BE-MOVESTOP-SIGN
  File: CopyEngine.cs
  Method: MoveStopToBreakEven (direction sign)
  Change: direction = isLong ? 1.0 : -1.0  ->  direction = isLong ? -1.0 : +1.0
  Tests needed:
    T_BE_SIGN_LONG_01: long pos, buf=1, tick=0.25 -> newStop = avgPrice - 0.25 (below entry)
    T_BE_SIGN_SHORT_01: short pos, buf=1, tick=0.25 -> newStop = avgPrice + 0.25 (above entry)
    T_BE_SIGN_ZERO: buf=0, both directions -> newStop == avgPrice (exact BE)

B72-A-10  HOTFIX-BUG-BE-IMMEDIATE
  File: CopyEngine.cs
  Method: ArmPendingBe (immediate-fire check)
  Change: Added bid/ask immediate-fire path before writing to _pendingBeSlots
  Tests needed:
    T_BE_IMM_01: ArmPendingBe long, bid >= target -> BreakEven called immediately, no slot added
    T_BE_IMM_02: ArmPendingBe short, ask <= target -> BreakEven called immediately, no slot added
    T_BE_IMM_03: ArmPendingBe refPx=0 (no market data) -> slot added normally (safe fallback)
    T_BE_IMM_04: ArmPendingBe long, bid < target -> slot added, BreakEven NOT called

B72-A-11  HOTFIX-MSTBE-STATE
  File: CopyEngine.cs
  Method: MoveStopToBreakEven (stateOk filter)
  Change: Working only -> Working || Accepted || TriggerPending
  Tests needed:
    T_BE_MOVE_03: MoveStopToBreakEven with Accepted stop -> Change() IS called
    T_BE_MOVE_04: MoveStopToBreakEven with TriggerPending stop -> Change() IS called
    T_BE_MOVE_05: MoveStopToBreakEven with Cancelled stop -> Change() NOT called (still filtered)

B72-A-12  HOTFIX-MSTBE-CANCEL-RESUBMIT
  File: CopyEngine.cs
  Method: MoveStopToBreakEven (full body)
  Change: Replaced acc.Change() with cancel+resubmit pattern (Step A snapshot, Step B cancel, Step C submit)
  Architecture note: acc.Change() is a silent no-op on ATM-bracket-owned orders from AddOn context.
    NT8_FULL_REFERENCE.md confirms this. Cancel+resubmit is the only reliable path.
    Pattern mirrors PttBreakEven.ExecuteOneAccount. OCO IDs use NextBeOcoSeq() shared counter.
  Tests needed:
    T_MSTBE_CR_01: MoveStopToBreakEven with 2 ATM targets -> 2 OCO pairs submitted (not acc.Change)
    T_MSTBE_CR_02: MoveStopToBreakEven with no targets -> bare PTT-BE-Stop submitted
    T_MSTBE_CR_03: Step B cancels stale ATM brackets but NOT PTT-BE-* orders (notBe guard)

B72-A-13  HOTFIX-MSTBE-OCO-REUSE (D5 format — still in effect; seed superseded by TICKSEED-01)
  Combined with B72-A-14 below into one test suite.

B72-A-14  HOTFIX-MSTBE-OCO-TICKSEED-01 (supersedes seed=0 from HOTFIX-MSTBE-OCO-REUSE)
  File: CopyEngine.cs
  Field: _mstbeOcoSeq = Environment.TickCount (was 0)
  Change: Seeds counter from TickCount so recompile-within-session never re-uses an OCO ID
  Tests needed:
    T_OCO_SEED_01: Two CopyEngine instances (simulating recompile) -> second instance first seq != any first-instance seq
    T_OCO_SEED_02: NextBeOcoSeq() called 1000x on single instance -> all unique (Interlocked.Increment)
    T_OCO_SEED_03: _mstbeOcoSeq initial value != 0 (TickCount seeding confirmed)
    T_OCO_SEQ_01: First call -> seq contains D5-formatted number in OCO ID
    T_OCO_SEQ_04: Pair index i=0 and i=1 with same seq -> distinct IDs ("-0" vs "-1" suffix)

B72-A-15  HOTFIX-BEALL-OCO-SEQ-SHARED-01
  File: CopyEngine.cs + PttBreakEven.cs
  Change: Added NextBeOcoSeq() on CopyEngine. Removed _beOcoSeq from PttBreakEven.
    PttBreakEven.Execute now calls CopyEngine.Instance?.NextBeOcoSeq() ?? 1.
  Tests needed:
    T_OCO_SHARED_01: PttBreakEven.Execute seq != MoveStopToBreakEven seq on same run (no collision)
    T_OCO_SHARED_02: _beOcoSeq field does NOT exist on PttBreakEven (verify via reflection or compile)

B72-A-16  HOTFIX-BUG-BE-OCO-REUSE (PttBreakEven.cs BuildBeOcoId prefix 4->8)
  File: PttBreakEven.cs
  Method: BuildBeOcoId
  Change: accName.Substring(0,4) -> accName.Substring(0,8) (or full name if shorter)
  Tests needed:
    T_OCO_ID_01: BuildBeOcoId("Sim101",1,0) != BuildBeOcoId("Sim102",1,0)
    T_OCO_ID_02: Same seq, same pair, different acc -> unique IDs
    T_OCO_ID_03: Account name shorter than 8 chars -> full name used (no OOB exception)

B72-A-17  HOTFIX-BUG-BE-STOP-PRICE-SHORT (PttBreakEven.cs sign flip)
  File: PttBreakEven.cs
  Method: ExecuteOneAccount (bePrice formula)
  Change: (isLong ? +buf : -buf) -> (isLong ? -buf : +buf)
  Tests needed:
    T_BE_PRICE_LONG_01: long at 100, buf=0, tick=0.25 -> bePrice=100
    T_BE_PRICE_LONG_02: long at 100, buf=1, tick=0.25 -> bePrice=99.75 (below entry)
    T_BE_PRICE_SHORT_01: short at 100, buf=0 -> bePrice=100
    T_BE_PRICE_SHORT_02: short at 100, buf=1, tick=0.25 -> bePrice=100.25 (above entry)
    T_BE_PRICE_VALID_SHORT: short bePrice=100.25, bid=99.0 -> IsBePriceOk returns true

B72-A-18  HOTFIX-BUG-BE-RAISE-NOTIFY-SIGN (PttBreakEven.cs RaiseBeNotify sign)
  File: PttBreakEven.cs
  Method: RaiseBeNotify
  Change: Same sign flip as B72-A-17 — aligns published bePrice with ExecuteOneAccount
  Tests needed:
    T_NOTIFY_01: long at 100, buf=1, tick=0.25 -> RaiseBeNotify publishes leaderBePrice=99.75
    T_NOTIFY_02: short at 100, buf=1, tick=0.25 -> RaiseBeNotify publishes leaderBePrice=100.25

B72-A-19  HOTFIX-ATM-T3-CANCEL-01 (CopyEngine.cs IsAtmBracketName)
  File: CopyEngine.cs
  Method: IsAtmBracketName
  Change: Hardcoded 4-name check -> generic StartsWith("Stop")+digit / StartsWith("Target")+digit
  Tests needed:
    T_ATM_T3_01: IsAtmBracketName("Stop3") -> true
    T_ATM_T3_02: IsAtmBracketName("Target3") -> true
    T_ATM_T3_03: IsAtmBracketName("Stop1") -> true (regression)
    T_ATM_T3_06: IsAtmBracketName("Stop") -> false (no digit suffix)
    T_ATM_T3_07: IsAtmBracketName("StopLoss") -> false ('L' not a digit at [4])
    T_ATM_T3_08: IsAtmBracketName("Target") -> false (no digit)
    NOTE: "Stop10" has digit at [4]='1' -> currently returns true. Document this in pipeline
          as acceptable (NT8 ATM names Stop1-Stop9 only; Stop10 would be out of spec).

B72-A-20  HOTFIX-ATM-T3-CANCEL-01 (PttBreakEven.cs notBe filter)
  File: PttBreakEven.cs
  Method: CancelStaleBracketsLocal (notBe filter)
  Change: o.Name != "PTT-BE-Stop" -> !o.Name.StartsWith("PTT-BE-", StringComparison.Ordinal)
  Tests needed:
    T_ATM_T3_04: CancelStaleBracketsLocal excludes "PTT-BE-Stop-1" (StartsWith match)
    T_ATM_T3_05: CancelStaleBracketsLocal excludes "PTT-BE-Stop" (bare name also excluded)
    T_ATM_T3_09: CancelStaleBracketsLocal excludes "PTT-BE-Target-1" (StartsWith match)
    T_ATM_T3_10: CancelStaleBracketsLocal includes "Stop3" in stale list

B72-A-21  HOTFIX-FLAT-DISARM-FOLLOWER
  File: CopyEngine.cs
  Method: OnOrderUpdate (pre-Gate-1 block)
  Change: Added narrow path: Filled + PTT-BE-Stop + non-leader -> fires PositionStateChanged
  Tests needed:
    T_FOLLOWER_FLAT_01: PTT-BE-Stop-1 fills on follower -> PositionStateChanged fired hasPos=false
    T_FOLLOWER_FLAT_02: PTT-BE-Stop-1 fills on leader -> NOT fired by this pre-Gate path (isLeader=true)
    T_FOLLOWER_FLAT_03: PTT-QX-Stop fills on follower -> NOT fired (name != PTT-BE-Stop*)
    T_FOLLOWER_FLAT_04: PTT-BE-Stop fills on follower, position still open -> hasPos=true fired correctly

B72-A-22  HOTFIX-MARKET-DEDUP-01
  File: CopyEngine.cs
  Method: IsDispatchTriggerState (signature: now takes OrderState + OrderType)
  Change: Market -> dispatch on Submitted only; Limit -> dispatch on Accepted only
  Tests needed:
    T_DEDUP_MARKET_01: Market Submitted -> IsDispatchTriggerState returns true
    T_DEDUP_MARKET_02: Market Accepted -> IsDispatchTriggerState returns FALSE
    T_DEDUP_LIMIT_01: Limit Accepted -> IsDispatchTriggerState returns true
    T_DEDUP_LIMIT_02: Limit Submitted -> IsDispatchTriggerState returns false

B72-A-23  HOTFIX-MSTBE-QX-TARGETS-01
  File: CopyEngine.cs
  Method: MoveStopToBreakEven (Step A isAtmTarget filter)
  Change: Extended isAtmTarget to also match PTT-QX-T* and PTT-BE-Target-* Limit orders
  Tests needed:
    T_QX_TARGETS_01: Step A snapshots "PTT-QX-T1" Limit order as a target
    T_QX_TARGETS_02: Step A snapshots "PTT-BE-Target-1" Limit order as a target
    T_QX_TARGETS_03: Step A snapshots "Target1" ATM name (regression)
    T_QX_TARGETS_04: Step A does NOT snapshot "PTT-QX-Stop" (stop order, not target)

════════════════════════════════════════════════════════════════
ARCHITECTURE THEMES FOR Ph1 (ptt-architect must document these)
════════════════════════════════════════════════════════════════

1. BE ALL path: OnGlobalBeClick -> PttGlobalBreakEven.Execute -> ArmAllPendingBe ->
   ArmPendingBe (per-account, per-instrument). Trigger: AccountItemUpdate -> OnPendingBeAccountUpdate
   -> BreakEven() -> MoveStopToBreakEven (cancel ATM brackets + resubmit PTT-BE-* OCO pairs).

2. acc.Change() is a silent no-op on ATM-bracket-owned orders from AddOn context.
   Cancel+resubmit is the authoritative pattern. NT8_FULL_REFERENCE.md confirms.

3. OCO ID uniqueness strategy: NextBeOcoSeq() on CopyEngine (Interlocked.Increment, seeded
   from Environment.TickCount to survive recompile-within-session). Both MoveStopToBreakEven
   and PttBreakEven.Execute share the same counter. D5 format + acc-prefix + pair-index.

4. NT8 Instrument reference equality is unreliable across account contexts. All instrument
   filtering must use FullName string comparison. This pattern applies to:
   MoveStopToBreakEven (B72-A-08), HandleEntryChange, CancelQxBrackets, SnapshotStopPrice.

5. TryFirePositionState must be leader-scoped (post-Gate-2.5). Only Filled and PartFilled
   states trigger it (not Cancelled/Rejected which are ATM bracket close noise).

6. ATM bracket state lifecycle for AddOn context: TriggerPending -> Accepted -> Working.
   Cancel candidates must include all three states. stateOk filter pattern:
   Working || Accepted || TriggerPending || Submitted || Initialized.

7. IsAtmBracketName generic pattern: Stop[digit] and Target[digit] cover Stop1-Stop9 and
   Target1-Target9. Hardcoded 4-name check was a bug source — extend to N targets.
```

---

## LANE B — B73-LaneB
### UI Logic: TradeCopierPanel.cs
### 14 hotfixes

---

**PASTE THIS INTO A FRESH `ptt-orchestrator` SESSION:**

```
You are ptt-orchestrator for block B73-LaneB.
Pipeline: Ph1 ptt-architect -> Ph2 ptt-plan-reviewer -> Ph3 ptt-architect ->
          Ph3.5 ptt-ticket-reviewer -> Ph4a ptt-engineer ->
          Ph4b ptt-verifier -> Ph5 ptt-plan-reviewer.
ALL 7 phases are mandatory. None are skippable. None are combinable.
Output directory: docs/brain/B73-LaneB/
Jane Street rules: docs/standards/jane-street/RULES_CATALOG.md (JS-001..JS-110)
OKF wiki: docs/intel/jane-street/
NT8 API reference: docs/standards/NT8_FULL_REFERENCE.md
Test framework: xUnit ONLY (never NUnit, never MSTest)
After any .cs edit by Ph4a: powershell -File scripts\sync-ptt-to-nt8.ps1

PIPELINE IS RETROSPECTIVE. The code changes are already in src/.
- Ph1 + Ph3 architects DESCRIBE what is there and WHY (no new logic).
- Ph4a engineer writes xUnit TESTS only + any trivial cleanup (whitespace, XML doc).
  NO logic changes. All WPF tests must use mocked dispatchers; no real WPF controls.
- Ph4b verifier confirms code in src/ matches the architecture plan.

DEPENDENCY NOTE: Lane B depends on CopyEngine types defined in Lane A (B72-LaneB).
Lane B can run CONCURRENTLY with Lane A because the code is already written and
B73 architect only describes what exists — no compilation needed during Ph1/Ph2/Ph3.
Ph4a engineer must confirm B72-LaneA Ph4a has COPIED before running sync for B73-LaneB.

════════════════════════════════════════════════════════════════
SCOPE — 14 hotfixes in TradeCopierPanel.cs
════════════════════════════════════════════════════════════════

All of the following are APPLIED and awaiting pipeline formalisation.
The repair log with full diffs is at docs/brain/NO-PIPELINE-REPAIRS.md.

B73-B-01  HOTFIX-DW-B72-02
  File: TradeCopierPanel.cs
  Methods: OnGlobalBeClick, OnPendingBeFiredDispatch, UpdateButtonColors
  Change: Removed _globalBeState per-panel field. All panels now read
    CopyEngine.Instance.IsPendingSlotsEmpty() as shared truth source.
    OnGlobalBeClick: if empty -> arm; else -> disarm (no local state)
    OnPendingBeFiredDispatch: if empty -> set Idle visual
    UpdateButtonColors: flat+armed -> DisarmPendingBe + Idle visual
  Tests needed:
    T_BEALL_SYNC_01: two panels simulated, arm via one -> both read IsPendingSlotsEmpty=false
    T_BEALL_SYNC_02: disarm via CopyEngine -> both panels read IsPendingSlotsEmpty=true

B73-B-02  HOTFIX-FIX-A-BE-BACKGROUND
  File: TradeCopierPanel.cs
  Method: UpdateBeVisuals
  Change: Added _beBtn2.Background = BrushInactive in BeState.Idle case
  Tests needed:
    T_BE_BG_01: UpdateBeVisuals(Idle) sets _beBtn2.Background to BrushInactive
    T_BE_BG_02: UpdateBeVisuals(Armed) sets _beBtn2.Background to BrushCaution (regression)

B73-B-03  HOTFIX-FIX-C-NO-DISARM-IN-UPDATEBUTTONCOLORS
  File: TradeCopierPanel.cs
  Method: UpdateButtonColors
  Change: Removed the DW-B72-02 blanket DisarmPendingBe block from UpdateButtonColors.
    BE ALL visual reset on flat is now handled only by OnPendingBeFiredDispatch.
  Tests needed:
    T_NO_DISARM_01: UpdateButtonColors(hasPos=true) -> DisarmPendingBe NOT called
    T_NO_DISARM_02: UpdateButtonColors(hasPos=false, _beState=Armed) -> HOTFIX-F3 fires only

B73-B-04  HOTFIX-FLAT-DISARM
  File: TradeCopierPanel.cs
  Method: UpdateButtonColors (HOTFIX-F3 block)
  Change: Inside existing !hasPosition && _beState != Idle branch:
    1. CopyEngine.Instance.DisarmPendingBe(_leaderAccount)
    2. if (IsPendingSlotsEmpty()) UpdateBeAllVisuals(BeState.Idle)
  Tests needed:
    T_FLAT_DISARM_01: flat+per-chart-armed -> DisarmPendingBe called for _leaderAccount
    T_FLAT_DISARM_02: flat+per-chart-armed -> IsPendingSlotsEmpty check -> UpdateBeAllVisuals

B73-B-05  HOTFIX-BEALL-SYNC-01
  File: TradeCopierPanel.cs
  Method: OnPendingBeArmedDispatch (new handler) + OnLoaded/Detach subscription
  Change: CopyEngine.PendingBeArmed event subscribed by all panels.
    Handler calls Dispatcher.InvokeAsync(() => UpdateBeAllVisuals(Armed)).
  Tests needed:
    T_BEALL_ARM_01: PendingBeArmed fires -> OnPendingBeArmedDispatch -> UpdateBeAllVisuals(Armed) called
    T_BEALL_ARM_02: Detach unsubscribes PendingBeArmed (no memory leak)

B73-B-06  HOTFIX-FLAT-MANUAL-CLOSE-01
  File: TradeCopierPanel.cs
  Method: OnLeaderPositionUpdate
  Change: On Operation.Remove -> fires UpdateButtonColors(false, false).
    NT8 guarantees position is gone at PositionUpdate.Remove event (no lag).
  Tests needed:
    T_MANUAL_CLOSE_01: Operation.Remove event -> UpdateButtonColors(false, false) called
    T_MANUAL_CLOSE_02: Operation.Update event -> UpdateButtonColors NOT called by this path

B73-B-07  HOTFIX-BEALL-DISARM-SYNC-01
  File: TradeCopierPanel.cs
  Methods: OnGlobalBeAllDisarmed (new handler) + OnLoaded/Detach subscription
  Change: CopyEngine.GlobalBeAllDisarmed event subscribed by all panels.
    Handler: Dispatcher.InvokeAsync(() => UpdateBeAllVisuals(BeState.Idle)).
    RaiseBeAllDisarmed() called from OnGlobalBeClick disarm path + UpdateButtonColors F3 branch.
  Tests needed:
    T_DISARM_SYNC_01: GlobalBeAllDisarmed fires -> all panels call UpdateBeAllVisuals(Idle)
    T_DISARM_SYNC_02: Detach unsubscribes GlobalBeAllDisarmed (no memory leak)

B73-B-08  HOTFIX-BEALL-BUFFER-SYNC-01 (panel wiring)
  File: TradeCopierPanel.cs
  Methods: OnGlobalBeBufferChanged (subscription + handler)
  Change: Subscribe to CopyEngine.GlobalBeBufferChanged in OnLoaded; unsubscribe in Detach.
    Handler stores buffer value and calls FormatGlobalBeBuffer to update _globalBeBtn2.Content.
  Tests needed:
    T_BUF_BE_01: GlobalBeBufferChanged fires with value 3 -> _globalBeBtn2.Content = "BE ALL +3"
    T_BUF_BE_02: Subscription wired in OnLoaded; unsubscribed in Detach

B73-B-09  HOTFIX-BUFLABEL-02 (Dispatcher.InvokeAsync wrapping)
  File: TradeCopierPanel.cs
  Methods: OnGlobalBeBufferChanged, OnQuickAllBufferChanged, new FormatQuickAllBuffer
  Change: Both handlers wrapped in Dispatcher.InvokeAsync (panel-local Dispatcher).
    New FormatQuickAllBuffer appends "t" suffix: "Quick ALL +4t".
  Tests needed:
    T_LABEL_01: IncrementQuickAll fires -> OnQuickAllBufferChanged -> label shows "+Nt"
      (verify "t" suffix present, verify Dispatcher.InvokeAsync pattern)
    T_LABEL_02: IncrementBuffer fires -> OnGlobalBeBufferChanged -> label shows "BE ALL +N"
    T_LABEL_03: FormatQuickAllBuffer(4) returns "Quick ALL +4t"
    T_LABEL_04: FormatQuickAllBuffer(10) returns "Quick ALL +10t"

B73-B-10  HOTFIX-QUICKALL-SINGLETON-01 (panel wiring)
  File: TradeCopierPanel.cs
  Methods: OnQuickAllBufferChanged subscription + OnLoaded/Detach
  Change: Subscribe to CopyEngine.GlobalQuickAllBufferChanged in OnLoaded.
    OnQuickAllUp/Down call CopyEngine.Instance.IncrementQuickAll()/DecrementQuickAll().
  Tests needed:
    T_QA_SING_01: OnQuickAllUp -> IncrementQuickAll called on CopyEngine singleton
    T_QA_SING_02: GlobalQuickAllBufferChanged fires -> OnQuickAllBufferChanged runs

B73-B-11  HOTFIX-QUICKALL-COMPILE-01
  File: TradeCopierPanel.cs
  Method: _quickAllBtn button construction
  Change: FormatBuffer("Quick ALL", _quickAllT1) -> FormatBuffer("Quick ALL", CopyEngine.Instance.GlobalQuickAllT1)
  Tests needed:
    T_QA_INIT_01: Button Content on construction reflects CopyEngine.Instance.GlobalQuickAllT1 value

B73-B-12  HOTFIX-BEALL-DISARM-CROSS-01
  File: TradeCopierPanel.cs
  Method: UpdateButtonColors (RaiseBeAllDisarmed placement)
  Change: Moved RaiseBeAllDisarmed() + UpdateBeAllVisuals(Idle) OUTSIDE IsPendingSlotsEmpty guard.
    Both fire unconditionally when flat+armed, regardless of other panel slot state.
  Tests needed:
    T_DISARM_CROSS_01: flat on panel 1, panel 2 slot still active -> RaiseBeAllDisarmed still fires
    T_DISARM_CROSS_02: both panels flat -> no double-fire issue (idempotent visual update)

B73-B-13  HOTFIX-BEALL-FLAT-RESET
  File: TradeCopierPanel.cs
  Method: UpdateButtonColors (new independent block)
  Change: Added separate !hasPosition && !IsPendingSlotsEmpty() block for BE ALL reset.
    Fires even when per-chart _beState == Idle (user only pressed BE ALL, not per-chart BE).
  Tests needed:
    T_BEALL_FLAT_01: position closes, BE ALL armed, per-chart BE NOT armed -> BE ALL resets to Idle
    T_BEALL_FLAT_02: position closes, BOTH armed -> both reset, no double-fire (DisarmPendingBe idempotent)

B73-B-14  HOTFIX-ORPHAN-STOP-CLEANUP
  File: TradeCopierPanel.cs
  Method: UpdateButtonColors (HOTFIX-ORPHAN block)
  Change: On hasPosition=false -> CopyEngine.Instance.CancelQxBrackets(_leaderAccount, _instrument).
    IsQxCancelCandidate in CopyEngine covers both PTT-BE-* and PTT-QX-* prefix orders.
    No-op when stale.Count==0.
  Tests needed:
    T_ORPHAN_01: flat with PTT-BE-Stop-1/2/3 Working -> CancelQxBrackets called
    T_ORPHAN_02: flat with no PTT-BE orders -> CancelQxBrackets is no-op (stale.Count==0)
    T_ORPHAN_03: flat with PTT-QX-Stop Working -> also cancelled (IsQxCancelCandidate covers PTT-QX-*)

ADDITIONAL (B73-B-15)  HOTFIX-FOLLOWER-LABEL-CLIP-01
  File: TradeCopierPanel.cs
  Method: BuildInlineFollowerRow
  Change: Replaced StackPanel row with DockPanel (LastChildFill=true). Name label fills
    remaining space with TextTrimming=CharacterEllipsis. ATM combo and PnL DockPanel.SetDock(Right).
  Tests needed:
    T_LABEL_CLIP_01: BuildInlineFollowerRow creates DockPanel (not StackPanel)
    T_LABEL_CLIP_02: Name TextBlock has no fixed Width set (LastChildFill)
    T_LABEL_CLIP_03: ATM combo DockPanel.GetDock == Right

════════════════════════════════════════════════════════════════
ARCHITECTURE THEMES FOR Ph1 (ptt-architect must document these)
════════════════════════════════════════════════════════════════

1. BE ALL button state is singleton-scoped (CopyEngine.IsPendingSlotsEmpty), NOT per-panel.
   Panels subscribe to broadcast events (PendingBeArmed, GlobalBeAllDisarmed, GlobalBeBufferChanged,
   GlobalQuickAllBufferChanged) and update visuals via Dispatcher.InvokeAsync (panel-local).

2. Dispatcher threading in NT8: each Chart window has its own Dispatcher.
   CopyEngine raises events via Application.Current.Dispatcher.InvokeAsync.
   Panels re-marshal to their own Dispatcher via this.Dispatcher.InvokeAsync before touching
   WPF controls. Correct pattern: OnGlobalBeAllDisarmed (line ~907).

3. UpdateButtonColors(hasPos=false) arrives ONLY via TryFirePositionState (Filled/PartFilled,
   post-Gate-2.5). It is safe to treat this as a reliable flat signal. All bracket cancel
   noise (Cancelled/Rejected events) is filtered before this point.

4. Per-chart BE state (_beState) and BE ALL state (IsPendingSlotsEmpty) are independent.
   Resetting one does not automatically reset the other. Each has its own block in
   UpdateButtonColors. Design: separate independent checks, each guarded by its own condition.

5. Orphaned PTT-BE-*/PTT-QX-* orders after manual close: CancelQxBrackets is the cleanup
   mechanism. It covers both prefixes via IsQxCancelCandidate. Called unconditionally on flat.
```

---

## LANE C — B74-LaneC
### Feature Files: PttGlobalQuickExit.cs + PttQuickExit.cs + PttGlobalBreakEven.cs
### 5 hotfixes

---

**PASTE THIS INTO A FRESH `ptt-orchestrator` SESSION:**

```
You are ptt-orchestrator for block B74-LaneC.
Pipeline: Ph1 ptt-architect -> Ph2 ptt-plan-reviewer -> Ph3 ptt-architect ->
          Ph3.5 ptt-ticket-reviewer -> Ph4a ptt-engineer ->
          Ph4b ptt-verifier -> Ph5 ptt-plan-reviewer.
ALL 7 phases are mandatory. None are skippable. None are combinable.
Output directory: docs/brain/B74-LaneC/
Jane Street rules: docs/standards/jane-street/RULES_CATALOG.md (JS-001..JS-110)
OKF wiki: docs/intel/jane-street/
NT8 API reference: docs/standards/NT8_FULL_REFERENCE.md
Test framework: xUnit ONLY (never NUnit, never MSTest)
After any .cs edit by Ph4a: powershell -File scripts\sync-ptt-to-nt8.ps1

PIPELINE IS RETROSPECTIVE. The code changes are already in src/.
- Ph1 + Ph3 architects DESCRIBE what is there and WHY (no new logic).
- Ph4a engineer writes xUnit TESTS only + trivial cleanup only. NO logic changes.
- Ph4b verifier confirms code in src/ matches the architecture plan.

DEPENDENCY NOTE: Lane C can run CONCURRENTLY with Lane A and Lane B. The feature
files (PttGlobalQuickExit, PttQuickExit, PttGlobalBreakEven) are self-contained.
Ph4a engineer must confirm B72 and B73 sync scripts have completed before running
sync for B74-LaneC (only one NT8 compile needed at the end).

════════════════════════════════════════════════════════════════
SCOPE — 5 hotfixes in feature files
════════════════════════════════════════════════════════════════

All of the following are APPLIED and awaiting pipeline formalisation.
The repair log with full diffs is at docs/brain/NO-PIPELINE-REPAIRS.md.

B74-C-01  HOTFIX-BEALL-BUFFER-SYNC-01 (PttGlobalBreakEven.cs part)
  File: PttGlobalBreakEven.cs
  Methods: IncrementBuffer, DecrementBuffer
  Change: CopyEngine.Instance.GlobalBeBufferChanged?.Invoke(...)
    -> CopyEngine.Instance.RaiseBeBufferChanged(_globalBeBuffer)
    (CS0070 fix: event may only be raised from inside declaring class)
  Tests needed:
    T_BE_BUF_RELAY_01: IncrementBuffer increments _globalBeBuffer and calls RaiseBeBufferChanged with new value
    T_BE_BUF_RELAY_02: DecrementBuffer decrements and calls RaiseBeBufferChanged (floor guard preserved)
    T_BE_BUF_RELAY_03: _globalBeBuffer floor = 0 (cannot go negative) -- regression

B74-C-02  HOTFIX-CS0070-BEBUFFER-01
  File: CopyEngine.cs (relay method) + PttGlobalBreakEven.cs (call site)
  Note: This is the same fix as B74-C-01 from the CopyEngine side. Document together.
  Change: Added internal void RaiseBeBufferChanged(int newValue) => GlobalBeBufferChanged?.Invoke(newValue)
    to CopyEngine.
  Tests needed: covered by B74-C-01 tests (relay is called from PttGlobalBreakEven).
  Architecture note: CS0070 rule — PttGlobalBreakEven cannot directly invoke an event declared on
    CopyEngine. The relay method pattern (RaiseBeBufferChanged) is the standard fix across the codebase:
    see also RaiseBeAllDisarmed (HOTFIX-BEALL-DISARM-SYNC-01).

B74-C-03  HOTFIX-QUICKALL-SINGLETON-01 (PttGlobalQuickExit.cs wiring)
  File: PttGlobalQuickExit.cs
  Method: Execute, ExecuteOne (reads CopyEngine.Instance.GlobalQuickAllT1)
  Change: Execute reads singleton tick value from CopyEngine instead of per-panel field.
    ExecuteOne passes targetCount from SnapshotTargetOrders (N-bracket path).
  Tests needed:
    T_QA_EXEC_01: Execute reads CopyEngine.Instance.GlobalQuickAllT1 for T1 ticks
    T_QA_EXEC_02: T1=0 falls back to InstrumentDefaults (ResolveQuickTicks fallback preserved)
    T_QA_EXEC_03: ExecuteOne passes snapshot to PttQuickExit.Execute

B74-C-04  HOTFIX-QUICK-T3-01
  Files: PttGlobalQuickExit.cs, PttQuickExit.cs
  Methods: PttGlobalQuickExit.Execute (passes snapshot), PttGlobalQuickExit.ExecuteOne
    (signature: now takes IList<TargetSnapshot>), new PttGlobalQuickExit.SnapshotTargetOrders,
    PttQuickExit.Execute (N-bracket for-loop)
  Change: Full description in HOTFIX-QUICK-T3-01 section of NO-PIPELINE-REPAIRS.md.
    Key points:
    - SnapshotTargetOrders scans acc.Orders for Limit target orders BEFORE cancelling anything
    - Execute passes snapshot to ExecuteOne -> PttQuickExit.Execute
    - PttQuickExit.Execute uses for-loop: targetCount = snapshot.Count ?? 2
    - tNPrice = entry +/- t1Ticks*N * tick (proportional, N=1,2,3...)
    - tNQty from snapshot[i].Qty; fallback: evenly split pos.Quantity across N
    - OCO IDs independent per pair (each pair can fill/cancel independently)
    - stop names: PTT-QX-Stop, PTT-QX-Stop2, PTT-QX-Stop3... target names: PTT-QX-T1/T2/T3...
    - Backward-compat overload (t2Ticks param) for single-chart Quick button (TradeCopierPanel.OnQuickClick)
  Tests needed:
    T_QX_T3_01: Execute with 3-target snapshot -> 3 OCO pairs submitted
    T_QX_T3_02: Execute with empty snapshot -> 2 pairs (fallback)
    T_QX_T3_03: tNPrice for i=2 (T3) = entry + t1*3*tick (proportional N=3)
    T_QX_T3_04: SnapshotTargetOrders finds ATM Target3 (StartsWith("Target")+digit)
    T_QX_T3_05: SnapshotTargetOrders finds PTT-QX-T3 (StartsWith("PTT-QX-T"))
    T_QX_T3_06: SnapshotTargetOrders finds PTT-BE-Target-1 (StartsWith("PTT-BE-Target"))
    T_QX_T3_07: SnapshotTargetOrders does NOT include stop orders
    T_QX_T3_08: tNQty uses snapshot qty when available; uses pos.Quantity/N as fallback
    T_QX_T3_09: Compat overload (t2 param) passes empty targets list -> 2-pair behavior

B74-C-05  HOTFIX-SNAPSHOT-STOP-INSTRREF
  File: PttQuickExit.cs
  Method: SnapshotStopPrice
  Change: o.Instrument != instr -> o.Instrument?.FullName != instr?.FullName (+ null guard)
  Tests needed:
    T_SNAP_STOP_01: SnapshotStopPrice with Working StopMarket, different Instrument instance, same FullName -> returns StopPrice
    T_SNAP_STOP_02: SnapshotStopPrice with null o.Instrument -> order skipped (no NRE)
    T_SNAP_STOP_03: SnapshotStopPrice with Filled order -> skipped (state filter, regression)
    T_SNAP_STOP_04: SnapshotStopPrice with no matching orders -> returns 0.0

════════════════════════════════════════════════════════════════
ARCHITECTURE THEMES FOR Ph1 (ptt-architect must document these)
════════════════════════════════════════════════════════════════

1. Quick ALL execution path: TradeCopierPanel.OnQuickAllClick -> PttGlobalQuickExit.Execute
   -> SnapshotTargetOrders (per account) -> CancelQxBrackets (via CopyEngine) -> ExecuteOne
   -> PttQuickExit.Execute (N-bracket for-loop, NT8 CreateOrder + Submit per pair).

2. N-bracket for-loop design: targetCount from snapshot, fallback 2. Proportional tick
   spacing: tNPrice = entry ± t1Ticks*N*tick for i=0..N-1 (N=1,2,3). Quantities from
   snapshot per-target if available; evenly split fallback. Independent OCO pairs per bracket.

3. GlobalQuickAllT1 singleton: stored on CopyEngine._globalQuickAllT1, incremented/decremented
   by IncrementQuickAll/DecrementQuickAll, broadcast via GlobalQuickAllBufferChanged event.
   Execute() reads CopyEngine.Instance.GlobalQuickAllT1 as t1 ticks. Unit is ticks (not points)
   for multi-instrument compatibility (MES tick=0.25, MGC tick=0.10, MCL tick=0.01).
   Label suffix "t" (e.g. "Quick ALL +4t") makes the unit explicit.
   ResolveQuickTicks fallback to InstrumentDefaults preserved when t1=0.

4. CS0070 relay pattern: CopyEngine events may only be raised (Invoke'd) from inside CopyEngine.
   PttGlobalBreakEven uses RaiseBeBufferChanged() relay. This is the same pattern as
   RaiseBeAllDisarmed() (B73-B-07). Never call event?.Invoke() from an external class.

5. Instrument FullName equality: same root cause as B72-A-08 (MoveStopToBreakEven),
   B69 DW-B69-02 (FindPosition/SubmitBeStop). SnapshotStopPrice had the same bug —
   reference equality always false for NT8 cross-account Instrument objects.
   All instrument filtering must use FullName string comparison. This is a codebase-wide pattern.
```

---

## EXECUTION ORDER

All three lanes can run in parallel — all three `ptt-orchestrator` sessions can be
started simultaneously. The code is already in src/; Ph1/Ph2/Ph3 are architectural
documentation only (no compilation). Ph4a engineers write tests only.

Sync command (ONE sync after ALL three Ph4a engineers confirm):
```powershell
powershell -File scripts\sync-ptt-to-nt8.ps1
```
Confirm CopyEngine.cs, TradeCopierPanel.cs, PttBreakEven.cs, PttGlobalBreakEven.cs,
PttGlobalQuickExit.cs, PttQuickExit.cs all show COPIED.

After sync, run build:
```powershell
powershell -File scripts\build_readiness.ps1
```
Must be zero errors. F5 in NinjaTrader to confirm NT8 compile green.

Update docs/brain/NO-PIPELINE-REPAIRS.md after all three lanes complete:
- Change all APPLIED entries to PIPELINE-COMPLETE (B72/B73/B74 references)
- Add new DEFERRED WORK entries for any open DW items surfaced in Ph5 reviews
