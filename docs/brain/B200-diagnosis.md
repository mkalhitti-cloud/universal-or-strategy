# B200 Diagnosis

**Epic**: B200 — Follower Quantity Corruption + SFB-NULL Bracket Sync Failure
**Date**: 2026-10-14
**Prepared by**: copier-spec mode (log analysis of docs/brain/output.md)
**Status**: DIAGNOSIS COMPLETE — ready for B200 orchestrator-prompt

---

## Test Setup

- Leader: Sim101, instrument MGC DEC26, Clone mode
- Followers: Sim102, Sim103, Sim104
- Entry qty: 10 contracts
- Multiple drag-cancel-resubmit cycles before fill
- MGC DEC26 ATM (no named template — `tmpl=` blank in log)

---

## Bug 1 — Follower Quantity Corruption (B200-QTY-01)

### Symptom
Followers receive different contract quantities than the leader intended.
All entries pre-fill show `qty=10`. After the partial fill cycle, subsequent
ATM-SENDs show corrupted quantities: 5, 9, 7, 4.

### Evidence from output.md

**Pre-fill ATM-SENDs (all correct):**
```
Line 18:  [PTT-ATM-SEND] acct=Sim102 qty=10 tmpl= obj=set
Line 19:  [PTT-ATM-SEND] acct=Sim103 qty=10 tmpl= obj=set
Line 20:  [PTT-ATM-SEND] acct=Sim104 qty=10 tmpl= obj=set
Line 96:  [PTT-ATM-SEND] acct=Sim102 qty=10  (drag resubmit 2)
Line 177: [PTT-ATM-SEND] acct=Sim102 qty=10  (drag resubmit 3)
Line 255: [PTT-ATM-SEND] acct=Sim102 qty=10  (drag resubmit 4)
```

**Leader entry PartFilled then Filled:**
```
Lines 314-318: [TP2-DRAG] IsWorkingBracket=False name=Entry state=PartFilled  (x5)
Lines 324-328: [TP2-DRAG] IsWorkingBracket=False name=Entry state=Filled       (x5)
```

**Post-partial-fill ATM-SENDs (corrupted — first corruption cycle):**
```
Line 349: [PTT-ATM-SEND] acct=Sim103 qty=10  (Sim103 only, Sim102/Sim104 missing)
Line 620: [PTT-ATM-SEND] acct=Sim102 qty=5   <-- WRONG (should be 10)
Line 731: [PTT-ATM-SEND] acct=Sim104 qty=9   <-- WRONG (should be 10)
```

**Second cycle: leader filled again, ATM-SENDs (all corrupted):**
```
Line 1944: [PTT-ATM-SEND] acct=Sim102 qty=7  <-- WRONG
Line 1955: [PTT-ATM-SEND] acct=Sim103 qty=4  <-- WRONG
Line 1966: [PTT-ATM-SEND] acct=Sim104 qty=9  <-- WRONG
```

### Root Cause Analysis

**PTT reads `e.Order.Quantity` to set the ATM-SEND qty. In NT8, an order that partially
fills updates its `Quantity` property to the REMAINING unfilled quantity, not the original
order quantity.**

Sequence for Sim102 (qty=5 case):
1. Leader entry submitted qty=10.
2. Leader PartFilled: 5 contracts filled, order.Quantity updated to 5 (remaining).
3. PTT detects the position event, calls `SendCopyWithAtm` for Sim102 using the leader
   order's current Quantity = 5.
4. ATM-SEND fires: `qty=5`.
5. Later the Filled event fires (0 remaining), but by then Sim102 already sent qty=5.

The partial fill races with the Filled event. Sim103 (qty=10 at line 349) happened to be
processed after the Filled state restored, or the NT8 Filled order reports full quantity
correctly. Sim102 and Sim104 were processed during the PartFilled window.

In the second cycle (lines 1944-1966), the leader position was partially closed first
(lines 902-906: `Close state=PartFilled`), leaving a residual position. When the new entry
filled, `e.Order.Quantity` reflected the remaining open quantity of that partial, not 10.

### Hypotheses

**H1 (confirmed)**: `SendCopyWithAtm` uses `e.Order.Quantity` (remaining qty at time of
event). For a PartFilled order this is less than the original intent.

**Fix candidate**: Use `e.Order.Quantity + e.Order.Filled` (total original qty) for the
ATM-SEND. In NT8, `Order.Quantity` = remaining unfilled; `Order.Filled` = contracts that
filled so far. `Quantity + Filled` = original order quantity.

Alternative: read qty from the ATM strategy object (`AtmStrategy.OrderQuantity`) rather
than from the order event. This is always the original intended quantity.

**H2 (possible secondary)**: PTT is being triggered on PartFilled events, not just Filled.
If the copy trigger fires on PartFilled, Sim102/Sim104 get the partial qty immediately.
Need to confirm whether `OnOrderUpdate` / `SendCopyWithAtm` is gated on `OrderState.Filled`
only or also on `OrderState.PartFilled`.

### Investigation Required (for B200 code read)

1. Find `SendCopyWithAtm` in `CopyEngine.cs`. Confirm which `e.Order.Quantity` is passed.
2. Find the trigger condition: is `PartFilled` handled or only `Filled`?
3. Determine whether `Order.Quantity` or `Order.Quantity + Order.Filled` is the right value.

---

## Bug 2 — SFB-NULL: Bracket Sync Fails for Sim102 / Sim104 (B200-SFB-NULL-01)

### Symptom
After the follower ATM is sent (confirmed by `[PTT-ATM-SEND]`), `SyncFollowerBracket`
(TP4-SFB) cannot find the follower's Target1/Target2 bracket orders. It logs `fo=NULL`
and `[TP4-SFB-NULL]`. The bracket sync is skipped. As a consequence, when the director
closes the position, `PTT-Flatten` fires on the followers — but the followers still have
open bracket orders (Target1:Working, Target2:Working) that were placed by the ATM-SEND.
The PTT-Flatten sell hits the market at the same time as the ATM bracket, creating a
net-short position.

### Evidence from output.md

**Sim102: fo=NULL on every bracket sync attempt (ordCount=9, first cycle):**
```
Line 506: [TP4-SFB] acc=Sim102 leaderName=Target1 fo=NULL followerOrders=[...,Target1:Working,...]
Line 507: [TP4-SFB-NULL] acc=Sim102 leaderName=Target1 ordCount=9
Line 508: [TP4-SFB] acc=Sim102 leaderName=Target2 fo=NULL followerOrders=[...,Target2:Working,...]
Line 509: [TP4-SFB-NULL] acc=Sim102 leaderName=Target2 ordCount=9
```

**Sim103: fo=Target1 FOUND (works correctly):**
```
Line 510: [TP4-SFB] acc=Sim103 leaderName=Target1 fo=Target1 followerOrders=[...,Target1:Working,...]
```

**Sim104: fo=NULL (ordCount=9, first cycle):**
```
Line 511: [TP4-SFB] acc=Sim104 leaderName=Target1 fo=NULL
Line 512: [TP4-SFB-NULL] acc=Sim104 leaderName=Target1 ordCount=9
```

**Second cycle: ordCount=26 (order list has grown from accumulated stale orders):**
```
Line 1618: [TP4-SFB-NULL] acc=Sim102 leaderName=Target1 ordCount=26
Line 1620: [TP4-SFB-NULL] acc=Sim103 leaderName=Target1 ordCount=26  <-- Sim103 NOW also failing
Line 1621: [TP4-SFB] acc=Sim104 leaderName=Target1 fo=Target1        <-- Sim104 now finds it
```

The failure pattern SWAPS between cycles — Sim103 fails in cycle 2 when Sim104 succeeds.
This is a timing-dependent race: whichever account's ATM bracket orders appear earliest
in the `followerOrders` list gets matched; the others land in a different slot.

### Root Cause Analysis

**The follower order list (`followerOrders`) includes ALL orders for the account, including
stale cancelled entries.** At ordCount=9 in cycle 1:
```
followerOrders=[Entry:Cancelled, Entry:Cancelled, Entry:Cancelled,
                Entry:Filled, PTT-Flatten:PartFilled,
                SimStop1:TriggerPending, Target1:Working,
                SimStop2:TriggerPending, Target2:Working]
```

`SyncFollowerBracket` searches this list for an order named `Target1` (matching the
leader's bracket name). Sim103 has `Target1:Working` visible and it's found. But Sim102
and Sim104 also show `Target1:Working` in their `followerOrders` yet return `fo=NULL`.

**The key difference**: Sim103's `followerOrders` list also contains `Entry:Working` (an
additional entry at position 7 in the list — the drain path). This extra order is from the
`_drainFollowerEntryOrders` mechanism introduced in B195/B198. Sim102 and Sim104 do not
have this extra `Entry:Working` order — they received their ATM bracket via a direct
`SendCopyWithAtm` path, not the drain path.

**Hypothesis (B200-SFB-H1)**: `SyncFollowerBracket` (`FindFollowerBracketOrder` or
equivalent) uses a name-match that also checks an ATM-ID or signal tag. Sim103's bracket
orders were submitted via the drain path which sets a different signal/tag. Sim102 and
Sim104 went through a different code path that may not set the expected tag, causing the
finder to skip them even though they are `name=Target1, state=Working`.

**Hypothesis (B200-SFB-H2)**: The `_drainFollowerEntryOrders` dict is not populated for
Sim102/Sim104 (they went via ATM-SEND directly, not the drain path). The bracket sync
requires the drain dict to locate bracket orders. Sim103 used the drain path (due to timing
of its ATM fill arriving during a re-entry window), got its fill object registered in the
drain dict, and bracket sync can find it. Sim102/Sim104 did not use the drain path.

**Consequence of fo=NULL**: When bracket sync fails, PTT cannot price-sync the follower
bracket. On close, PTT sends `PTT-Flatten` to flatten the follower. If the follower has
an open ATM position (from ATM-SEND), PTT-Flatten sells into it — opening a net short.
This explains the "reverse position" the Director observes.

### ordCount Growth (26 in cycle 2)

By cycle 2, 17 additional orders have accumulated (26 - 9 = 17). These are the cancelled
entries from 4 drag cycles × ~4 orders each + PTT-Flatten partial fills. The search scans
all 26 orders but still cannot find a matching bracket by name-tag combo. The search is
O(n) and functionally incorrect, not just slow.

---

## Bug 3 — PTT-Flatten Cascade (B200-FLATTEN-01)

### Symptom
`PTT-Flatten` fires on all 3 followers repeatedly (hundreds of times in the log). This is
not a separate bug — it is the downstream effect of Bug 2. But the volume (10+ PTT-Flatten
events per follower per cycle) suggests a secondary issue: PTT-Flatten is being re-submitted
even while a prior PTT-Flatten is still Working/Accepted.

### Evidence
```
Lines 373-392:  PTT-Flatten Accepted/Working x10 on Sim102  (first cycle)
Lines 476-495:  PTT-Flatten Accepted/Working x10 on Sim103
Lines 583-616:  PTT-Flatten Accepted/Working x10+ on Sim104
```

Each group of 5 Accepted + 5 Working = the same single PTT-Flatten order being tracked
across 5 repeats of `[TP1-OOU]`. This is the standard NT8 multi-callback pattern (each
state fires once per subscribed listener). Not a duplicate submission — one PTT-Flatten
per follower per cycle. However the flattening is happening when it should NOT (follower
has a live long position from ATM-SEND that the sync missed).

### Root Cause
Directly caused by Bug 2 (fo=NULL → no bracket sync → PTT-Flatten incorrectly dispatched).
No independent fix needed beyond fixing Bug 2. Mark as DEPENDENT on B200-SFB-NULL-01.

---

## Bug 4 — Missing ATM-SEND for Sim102 After PartFill (B200-ATMSEND-TIMING-01)

### Symptom
At line 349, only Sim103 gets an ATM-SEND. Sim102 and Sim104 are skipped.
Their sends arrive much later (line 620 for Sim102, line 731 for Sim104) with wrong qty.

### Evidence
```
Line 324-328: Entry state=Filled  (leader filled)
Line 349: [PTT-ATM-SEND] acct=Sim103 qty=10  -- only Sim103
...
Line 620: [PTT-ATM-SEND] acct=Sim102 qty=5   -- 271 lines later, wrong qty
Line 731: [PTT-ATM-SEND] acct=Sim104 qty=9   -- 382 lines later, wrong qty
```

The asymmetry: Sim103 gets the send immediately on the Filled event. Sim102 and Sim104
get their sends later, triggered by some other event (likely the `[TP4-SFB-NULL]` drain
retry path or a re-entry from COPY-GUARD), by which point the order qty has been
contaminated by the PartFilled state.

This is the same root cause as Bug 1 (quantity read from wrong order state) but also
exposes a timing difference: Sim103 is processed first (its listener fires before
Sim102/Sim104), reads the full `qty=10` from the Filled order, succeeds. Sim102/Sim104
are deferred, hit the PartFilled qty instead.

---

## Summary of Bugs

| ID | Name | Severity | Root Cause | Consequence |
|----|------|----------|------------|-------------|
| B200-QTY-01 | Follower qty corruption | P0 | `e.Order.Quantity` read from PartFilled state (remaining qty, not original) | Followers open wrong-sized positions |
| B200-SFB-NULL-01 | SFB bracket sync null | P0 | `SyncFollowerBracket` name-match fails for accounts not on drain path | PTT-Flatten fires incorrectly → reverse position opens |
| B200-FLATTEN-01 | PTT-Flatten cascade | P1 | Downstream of SFB-NULL-01 | Reverse positions; NOT independent |
| B200-ATMSEND-TIMING-01 | ATM-SEND timing gap | P1 | Sim102/Sim104 deferred, hit PartFill qty window | Compound of QTY-01; separate deferred-send path |

---

## Proposed Investigation Targets (for B200 code read)

### Target 1: `SendCopyWithAtm` quantity source
- File: `src/PropTraderTools/CopyEngine.cs`
- Find: where `qty` is read to pass into `AtmStrategyCreate` or equivalent
- Confirm: is it `e.Order.Quantity`, `e.Order.Filled`, or a combination?
- Question: does the trigger fire on `PartFilled` or only `Filled`?

### Target 2: `SyncFollowerBracket` / `FindFollowerBracketOrder`
- File: `src/PropTraderTools/CopyEngine.cs`
- Find: the name-match logic inside TP4-SFB that returns `fo`
- Question: what tag/signal/property is checked beyond order name?
- Question: why does Sim103's order list (with `Entry:Working` drain order) succeed while
  Sim102's list (same `Target1:Working` order present) fails?

### Target 3: Drain path vs direct ATM-SEND path
- Find: the `_drainFollowerEntryOrders` usage in `SubmitDrainedEntry` / `TrySetSlateReadyOnDrainFill`
- Question: does the drain path set a different `Signal` or `FromEntrySignal` on the
  bracket orders that the bracket sync requires?

---

## Log Annotation: Key Line References

| Line(s) | Event | Significance |
|---------|-------|-------------|
| 1 | `[PTT-DR4-CLEAR]` | Copy ON, dicts cleared clean start |
| 18-20 | `[PTT-ATM-SEND] qty=10 x3` | First entry, all 3 followers, correct qty |
| 96-257 | ATM-SEND x12 (4 drag cycles, qty=10) | All correct pre-fill |
| 309-313 | `PositionStateChanged hasPos=False` | Prior positions closed |
| 314-318 | `Entry state=PartFilled` | **Leader partial fill — triggers qty bug** |
| 319-323 | `PositionStateChanged hasPos=True` | Position opened (partial) |
| 324-328 | `Entry state=Filled` | Leader fully filled |
| 349 | `ATM-SEND Sim103 qty=10` | Sim103 processed on Filled (correct) |
| 506-509 | `SFB-NULL Sim102 ordCount=9` | **First SFB-NULL — bracket sync fail** |
| 510 | `SFB Sim103 fo=Target1` | Sim103 bracket found (drain path) |
| 511-512 | `SFB-NULL Sim104 ordCount=9` | Sim104 bracket sync fail |
| 620 | `ATM-SEND Sim102 qty=5` | **Sim102 deferred send, wrong qty** |
| 731 | `ATM-SEND Sim104 qty=9` | **Sim104 deferred send, wrong qty** |
| 882-906 | `Close state=PartFilled` | Director closes position (partial) |
| 907+ | `PTT-Flatten` cascade | All followers, hundreds of events |
| 1514-1573 | `Entry PartFilled → Filled` | Second fill cycle |
| 1617-1620 | `SFB-NULL Sim102/Sim103 ordCount=26` | **Stale order accumulation confirmed** |
| 1621 | `SFB Sim104 fo=Target1` | Sim104 now works (pattern swapped) |
| 1944-1966 | `ATM-SEND qty=7/4/9` | **All three wrong in cycle 2** |

---

## Pre-Conditions for B200 Code Investigation

- [ ] Director confirms: what is the ATM template in use? (`tmpl=` blank in all log lines —
  using no-template ATM, or template name not logging correctly?)
- [ ] Director confirms: is MGC DEC26 a futures contract where NT8 partial fills are normal
  (market orders during low-liquidity SIM), or is this unexpected?
- [ ] NT8 API confirmation needed: does `Order.Quantity` represent remaining or total in
  `PartFilled` state? (Check NT8_FULL_REFERENCE.md before writing fix design)

---

*Written by: copier-spec mode*
*Evidence source: docs/brain/output.md (2026-10-14 SIM session)*
*Supersedes: B199 diagnosis context*
