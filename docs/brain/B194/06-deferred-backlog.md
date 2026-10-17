# B194 Deferred Backlog

**Epic**: B194 -- Fix 4 confirmed NakedPositionDetector + DrainThenDispatch bugs
**Author**: ptt-plan-reviewer (Phase 5)
**Date**: 2026-10-07
**Preceding backlog**: docs/brain/B193/06-deferred-backlog.md
**Status**: Authoritative deferred backlog for the NEXT epic block (B195+)

---

## Purpose

This document records all deferred work items that remain open after B194 completion.
It supersedes docs/brain/B193/06-deferred-backlog.md as the authoritative carry-forward list.
Items closed by B194 are listed with CLOSED status for audit trail completeness.

**P0 = Compile-blocking or P0 DNA violation. Must be resolved before shipping.**
**P1 = Architecture risk. Must be resolved before next major feature wave.**
**P2 = Quality / testing gap. Track and address in near-term pipeline.**
**P3 = Low-risk cleanup / informational. Address when convenient.**

---

## B194 Block -- 2026-10-07

### Items Closed by B194

| DW ID | Description | Priority | Closed By | Closing Artifact |
|-------|-------------|----------|-----------|-----------------|
| DW-B194-01 | TryNakedDetect PTT-Copy fill false positive. PTT-Copy fill triggered NakedPositionDetector before ATM brackets arrived (structural gap: brackets sync after leader Target1:Accepted, after the fill). Guard added: `if (e.Order.Name == "PTT-Copy" && e.Order.OrderState == OrderState.Filled) return;` placed before terminal-state check. CYC: 3->4. | P0 | B194-T1 (F1) | ticket-1-verification.md (VERIFY_PASS, Cycle 2) |
| DW-B194-02 | HasNakedPosition TriggerPending stop blind spot. NT8 SIM ATM bracket stops appear as `OrderType.Market + OrderState.TriggerPending + StopPrice>0`. Old inner loop only checked Working/Submitted StopMarket/StopLimit -- TriggerPending Market stops were invisible, causing HasNakedPosition to return true even when a bracket stop existed. Fixed by extracting `IsBracketStopOrder(Order o)` helper (internal static, CYC=3) and refactoring HasNakedPosition inner loop to call it. HasNakedPosition CYC: 8->7. | P1 | B194-T2 (F2) | ticket-2-verification.md (VERIFY_PASS, Cycle 2) |
| DW-B194-03 | DrainThenDispatch Submitted order exclusion race. When drain-2 completes for Sim104, the new PTT-Copy enters Submitted state. The old entryCandidates LINQ filter (Working|Accepted only) excluded Submitted orders, causing `entryCandidates.Any() = false` on drag-3. This triggered SubmitEntryDirect while a Submitted PTT-Copy was still live, producing 2 simultaneous PTT-Copy orders. Fixed by adding `|| o.OrderState == OrderState.Submitted` to the LINQ predicate. CYC unchanged (LINQ lambda). | P0 | B194-T1 (F3) | ticket-1-verification.md (VERIFY_PASS, Cycle 2) |
| DW-B194-04 | FlattenOneAccount belt-and-suspenders debounce (BUG-3 residual path). F1 (T1) eliminates the primary loop path (PTT-Copy fill). However, PTT-Flatten fills pass the F1 guard (Name != "PTT-Copy"), and if the flatten reversed the position, HasNakedPosition returns true and re-queues FlattenOneAccount. Evidence: 9 reversals in 7s. Fixed by adding `_flattenFilledTicks ConcurrentDictionary<string,long>` field and a 500ms debounce check in FlattenOneAccount (after in-flight scan, before FindPosition). Timestamp recorded before acc.Submit. CYC: 6->7. | P0 | B194-T3 (F4) | ticket-3-verification.md (VERIFY_PASS) |

---

## Items Carried Forward (OPEN)

---

### DW-B183-03: IsLaneEEligible CYC=8 Frozen

```
Priority: P1
Description: IsLaneEEligible in CopyEngine.cs is at CYC=8 (JS-066 ceiling). Any branch
  addition to IsLaneEEligible without prior helper extraction is an automatic P1 JS-066
  violation. Not modified in B183-B194.
  Future engineers modifying Lane E eligibility MUST extract a helper method first
  (using the same pattern as DW-B192-SINST-01 guidance below and the IsBracketStopOrder
  extraction performed in B194-T2).
Blocking: No (current CYC is at limit, not over it; blocks only future branch additions
  to IsLaneEEligible)
Target Block: When Lane E is next modified
Status: OPEN (CARRIED FORWARD from B183 via B184, B185, B186, B187, B188, B189, B190, B191,
  B192, B193, now B194)
Owner: Director
Source: docs/brain/B191/06-deferred-backlog.md -> docs/brain/B193/06-deferred-backlog.md
```

---

### DW-B192-SINST-01: SetInstrument() CYC at Ceiling 8

```
Priority: P2
Description: SetInstrument() in TradeCopierPanel.cs is at CYC=8 (JS-066 ceiling). Any branch
  addition to SetInstrument() without prior helper extraction is an automatic P1 JS-066
  violation. Engineers modifying SetInstrument() MUST extract a helper method first.
  Pattern identical to DW-B183-03 (IsLaneEEligible). Not blocking; CYC is at limit, not over it.
  Documented in spec: specs/002-trade-copier-spec.html section-b193 at line 42199.
Blocking: No (current CYC is at limit, not over it; blocks only future branch additions)
Target Block: When SetInstrument() is next modified
Status: OPEN (opened B193, carried forward to B194, now B194)
Owner: Director
Source: docs/brain/B193/02-architecture-plan.md (Deferred Work Register)
        specs/002-trade-copier-spec.html (DW-B192-SINST-01 card, line 42199)
```

---

### DW-B193-01: OnQuickAllBufferChanged Old Label Strings

```
Priority: P3
Description: OnQuickAllBufferChanged in TradeCopierPanel.cs (~line 1027-1029) still uses
  old label strings "Quick ALL" and "QAll2t" in the singleton-sink callback path.
  The per-panel Up/Down path (lines 1764-1778) correctly uses "3x All" / "2x All" after
  B193-DIRECT-3. OnQuickAllBufferChanged is a singleton-sink callback -- since B193-DIRECT-2
  moved display state to per-panel, this method may be vestigial or rarely triggered.
  Low severity: visual label shows old name only on the singleton path when singleton fires.
  Not a DNA violation (no JS rule on label string consistency). Informational only.
Blocking: No
Target Block: B194+ when OnQuickAllBufferChanged is next touched
Status: OPEN (opened B193, carried forward to B194, now B194)
Owner: Director
Source: docs/brain/B193/02-architecture-plan.md (Deferred Work Register)
```

---

## New Items Opened by B194

---

### DW-B195-01: Clone Mode SubmitDrainedEntry Not ATM-Aware

```
Priority: P1
Description: In Clone mode, DrainThenDispatch cancels the original ATM-armed "Entry" order
  and replaces it via SubmitEntryDirect(). SubmitEntryDirect never calls StartAtmStrategy(),
  so the replacement entry order has no ATM brackets. After leader fills, SyncFollowerBracket
  finds fo=NULL on all followers -- no bracket order to sync. Followers have naked positions
  with no stop/target protection in Clone mode.
Root: SubmitEntryDirect is mode-agnostic; it has no ATM object or template name in scope at
  drain-complete time. The PendingDispatchDrain struct carries the entry price/qty/action but
  not the FollowerAtmMode flag.
Fix direction: SubmitDrainedEntry must be ATM-mode-aware. When copy rule is Clone mode with
  a cached ATM template name, call SendCopyWithAtm() instead of SubmitEntryDirect().
  PendingDispatchDrain payload must carry the FollowerAtmMode field (or an equivalent
  ATM template name field) so that OnOrderCancelledForDrain can select the correct dispatch path.
Blocking: Yes -- live Clone mode trading is unsafe without this fix. Any drain scenario
  (multi-drag before leader fill) in Clone mode leaves all followers with unprotected positions.
Target Block: B195 (immediately after B194 SIM gate passes)
Status: OPEN
Owner: Director
Historical: B167-DEFER-02 (opened 2026-09-18, carried through B168-B193 without resolution)
Source: docs/brain/B194/05-final-review.md Section K (Director directive)
```

---

## SIM Gate Gate Note for B194

B194 pipeline is complete (3 tickets, all VERIFY_PASS, all 7 scans aggregate PASS).
Before B195 begins, Director MUST run the SIM gate and record the result in:
`docs/brain/B194/sim-gate-completion.md`

**SIM gate criteria (8 steps)**:
1. Leader=Sim101, Followers=Sim102/Sim103/Sim104, instrument=MGC DEC26
2. Place ATM entry for Sim101. All 3 followers receive PTT-Copy Working.
3. Drag leader entry order (pre-fill). Confirm [DRAIN] fires for ALL 3 followers (including Sim104).
4. Let leader fill. All 4 accounts have open Long positions.
5. NO PTT-Flatten fires on any follower.
6. Drag leader bracket (Target1). Confirm fo=<order> (not fo=NULL) for all followers.
7. Close leader position. Followers close WITHOUT reverse position.
8. Zero infinite PTT-Flatten loops observed in the session.

All 8 criteria must PASS before B195 execution begins.

---

## Summary by Priority

| Priority | Count | Items |
|----------|-------|-------|
| P0 | 0 | (DW-B194-01, DW-B194-02, DW-B194-03, DW-B194-04 all CLOSED by B194) |
| P1 | 2 | DW-B183-03, DW-B195-01 |
| P2 | 1 | DW-B192-SINST-01 |
| P3 | 1 | DW-B193-01 |

**Total open items**: 4
**Items closed by B194**: 4 (DW-B194-01, DW-B194-02, DW-B194-03, DW-B194-04)
**New items opened by B194**: 1 (DW-B195-01)
**Items carried forward unchanged**: 3 (DW-B183-03, DW-B192-SINST-01, DW-B193-01)

---

## Recommended Next Block Priorities

1. **SIM Gate (B194)** (Required before B195): Run 8-step SIM gate for B194.
   Record result in `docs/brain/B194/sim-gate-completion.md`. B195 is blocked until PASS.

2. **DW-B195-01** (P1, Blocking): Clone mode SubmitDrainedEntry not ATM-aware.
   Immediately dangerous -- followers in Clone mode have no stop/target after a drain event.
   Must be the primary scope of B195.

3. **DW-B183-03** (P1, Non-blocking): IsLaneEEligible CYC=8 frozen.
   No action required unless a branch addition to IsLaneEEligible is planned for B195+.
   If Lane E is touched, extract a helper method first (pattern: IsBracketStopOrder extraction
   in B194-T2 is the canonical reference).

4. **DW-B192-SINST-01** (P2, Non-blocking): SetInstrument() CYC=8 ceiling.
   No action required unless SetInstrument() is modified. Helper extraction required first.

5. **DW-B193-01** (P3, Low priority): OnQuickAllBufferChanged old label strings.
   Address when the method is next touched, or at a convenient cleanup opportunity.

---

*Written by: ptt-plan-reviewer*
*Epic: B194*
*Phase: 5 (Final Review)*
*Required for FINAL_PASS gate (role definition mandate).*
*Supersedes: docs/brain/B193/06-deferred-backlog.md (as authoritative carry-forward list)*
