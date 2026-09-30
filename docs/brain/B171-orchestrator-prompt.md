# B171 PTT-Orchestrator Prompt
# BLOCK: B171 — DW-B171-01 + DW-B171-02: PTT-STP-Drag Duplicate Stop + fo=NULL on Stop Drag
# Produced by: ptt-orchestrator (copier-spec mode)
# Status: AWAITING DIRECTOR GO

---

## MISSION STATEMENT

Block B171 closes two bugs discovered during the B170 SIM gate run.
Both bugs share a single root cause: `SubmitReplacementStopOrder` creates the
follower's drag-replacement stop with the unsuffixed name `"PTT-STP-Drag"`
instead of `"PTT-STP-Drag-{N}"`. This is a SURGICAL two-bug fix.

File under surgery: `src/PropTraderTools/CopyEngine.cs`
Test file: `src/PropTraderTools/CopyEngineTests.cs`
Pipeline: FULL 5-PHASE (all phases mandatory, none skippable, none combinable)

---

## BUG DESCRIPTIONS

### DW-B171-01 — DUPLICATE-STP-DRAG-ON-REPEATED-TGT-DRAG (P0)

**Trigger:** Director drags any target order 2+ times, or drags multiple
different targets in sequence.

**Evidence:** `docs/output.md` lines 196-242.
- After drag #1 (Target3, line 196): 1 `PTT-STP-Drag` created (Submitted).
- After drag #2 (Target2, line 219): a SECOND `PTT-STP-Drag` is submitted while
  the first is still Accepted. Two stops now live.
- After drag #3 (Target3 again, line 242): a THIRD `PTT-STP-Drag` is stacked.

**Root cause:** `ExecutePhaseCStopReplacement` (lines 4507-4512) calls
`CreateFollowerReplacementStop` → `SubmitReplacementStopOrder` which creates
the stop named `"PTT-STP-Drag"` (UNSUFFIXED, line 4478).
There is NO `CancelLiveCollateralStop` call before creation in the Phase C
target-drag path. Compare to `ResubmitOneCollateralLeg` (stop-drag path,
lines 4038-4053) which correctly calls `CancelLiveCollateralStop` before
creating — but that method is NOT called on the target-drag path.
Each target drag stacks a new `PTT-STP-Drag` on top of the prior ones.

---

### DW-B171-02 — fo=NULL-ON-STOP-DRAG-WHEN-STP-IS-PTT-NAMED (P0)

**Trigger:** Director drags a STOP order after at least one target drag has
occurred (so the follower's live stop is named `"PTT-STP-Drag"` instead of
the original `"Stop2"` / `"Stop3"`).

**Evidence:** `docs/output.md` lines 287-339.
All three followers (Sim102, Sim103, Sim104) show `fo=NULL` on every stop
drag event. The follower order list contains multiple `PTT-STP-Drag:Accepted`
entries. Stop drag is never synced.

**Root cause:** `IsDragNameMatch` (lines 4740-4748) checks
`orderName == "PTT-STP-Drag-" + legSuffix` (SUFFIXED pattern).
`SubmitReplacementStopOrder` creates the stop as `"PTT-STP-Drag"` (UNSUFFIXED).
Exact match fails → `fo = NULL` → stop drag not synced.

**Relationship to DW-B171-01:** Both bugs are caused by the identical missing
suffix at line 4478. Fixing the name to `"PTT-STP-Drag-" + suffix` at line
4478 AND adding the pre-cancel sweep in `ExecutePhaseCStopReplacement` fixes
BOTH bugs simultaneously.

---

## CONFIRMED SOURCE EVIDENCE (do not re-read from files — embed directly)

```
Line 4478:  "PTT-STP-Drag"          <- unsuffixed (THE BUG)
Lines 4507-4512:  ExecutePhaseCStopReplacement — no CancelLiveCollateralStop call
Lines 4437-4451:  CreateFollowerReplacementStop — param signature (no suffix param)
Lines 4458-4498:  SubmitReplacementStopOrder — unsuffixed name at 4478
Lines 4058-4073:  CancelLiveCollateralStop — REUSE AS-IS, no changes
Lines 4038-4053:  ResubmitOneCollateralLeg — reference pattern (already calls CancelLiveCollateralStop)
Lines 4391-4406:  DeriveLeaderBracketIndex — returns int, 0 on null/no-digit
Lines 4740-4748:  IsDragNameMatch — checks "PTT-STP-Drag-" + legSuffix (suffixed)
```

**Caller safety (confirmed, one caller each):**
- `CreateFollowerReplacementStop`: exactly one caller = `ExecutePhaseCStopReplacement` (line 4511)
- `SubmitReplacementStopOrder`: exactly one caller = `CreateFollowerReplacementStop` (line 4450)
- No test directly calls either method

---

## WINNING REPAIR DESIGN (Best-of-5 Winner: Variant E — Adversarial Improved Baseline)

**Selected because:** Variant E is the only design that eliminates BOTH bugs
AND closes the EC-1 regression present in all other variants (cancel-before-
price-validation: if `FindLeaderStopPrice` returns <= 0, Variants A-D cancel
the existing live stop without replacing it, leaving the follower unprotected).
Variant E guards the price BEFORE the cancel call, making the cancel-then-create
sequence unconditionally safe. CYC delta is +1 on `ExecutePhaseCStopReplacement`
only (CYC 2 → 2 after replacing ternary with `if stp <= 0`; still 2 net because
the single branch is the guard). All methods stay <= 8.

### Method A — ExecutePhaseCStopReplacement (lines 4507-4512) — CYC 2 → 2

**BEFORE:**
```csharp
private void ExecutePhaseCStopReplacement(Account acc, Order fo, Order? leaderOrder)
{
    int bracketIdx = DeriveLeaderBracketIndex(leaderOrder);
    double stp = FindLeaderStopPrice(leaderOrder?.Account, bracketIdx);
    CreateFollowerReplacementStop(acc, fo.Instrument, fo.Quantity, fo.OrderAction, stp);
}
```

**AFTER:**
```csharp
// B171 DW-B171-01+DW-B171-02: derive suffix, validate price before cancel, cancel-then-replace.
// CYC=2: base(1) + stp<=0 guard(1). JS-021: no lock. JS-001: delegates to CreateFollowerReplacementStop catch.
// JS-002: void return. ASCII-only.
private void ExecutePhaseCStopReplacement(Account acc, Order fo, Order? leaderOrder)
{
    int bracketIdx = DeriveLeaderBracketIndex(leaderOrder);
    string suffix = bracketIdx.ToString();
    double stp = FindLeaderStopPrice(leaderOrder?.Account, bracketIdx);
    if (stp <= 0.0)
    {
        StatusUpdate?.Invoke(acc?.Name + ": PTT-STP-Drag-" + suffix + " skipped: stopPrice <= 0");
        return;
    }
    CancelLiveCollateralStop(acc, fo, "PTT-STP-Drag-" + suffix);
    CreateFollowerReplacementStop(acc, fo.Instrument, fo.Quantity, fo.OrderAction, stp, suffix);
}
```

**Lines changed:** +4 (suffix line, stp guard with StatusUpdate + return, CancelLiveCollateralStop call), 1 modified (CreateFollowerReplacementStop call gains `, suffix`).

---

### Method B — CreateFollowerReplacementStop (lines 4437-4451) — CYC 2 → 2

**BEFORE:**
```csharp
private void CreateFollowerReplacementStop(
    Account followerAcc,
    Instrument instr,
    int qty,
    OrderAction stopAction,
    double stopPrice
)
{
    if (stopPrice <= 0.0)
    {
        StatusUpdate?.Invoke(followerAcc?.Name + ": PTT-STP-Drag skipped: stopPrice <= 0");
        return;
    }
    SubmitReplacementStopOrder(followerAcc, instr, qty, stopAction, stopPrice);
}
```

**AFTER:**
```csharp
// B171: suffix param added (pass-through). CYC=2 unchanged.
// stopPrice guard remains as defensive check for any future caller.
// JS-021: no lock. JS-001: try/catch absorbed in SubmitReplacementStopOrder. JS-002: void return.
private void CreateFollowerReplacementStop(
    Account followerAcc,
    Instrument instr,
    int qty,
    OrderAction stopAction,
    double stopPrice,
    string suffix
)
{
    if (stopPrice <= 0.0)
    {
        StatusUpdate?.Invoke(followerAcc?.Name + ": PTT-STP-Drag-" + suffix + " skipped: stopPrice <= 0");
        return;
    }
    SubmitReplacementStopOrder(followerAcc, instr, qty, stopAction, stopPrice, suffix);
}
```

**Lines changed:** +1 (suffix param), 2 modified (StatusUpdate message gains suffix, SubmitReplacementStopOrder call gains `, suffix`).

---

### Method C — SubmitReplacementStopOrder (lines 4458-4498) — CYC 4 → 4

**BEFORE:**
```csharp
private void SubmitReplacementStopOrder(
    Account followerAcc,
    Instrument instr,
    int qty,
    OrderAction stopAction,
    double stopPrice
)
{
    try
    {
        var newStop = followerAcc.CreateOrder(
            instr,
            stopAction,
            OrderType.StopMarket,
            OrderEntry.Automated,
            TimeInForce.Day,
            qty,
            0,
            stopPrice,
            "",
            "PTT-STP-Drag",
            NinjaTrader.Core.Globals.MaxDate,
            (NinjaTrader.Cbi.CustomOrder)null
        );
        if (newStop == null)
        {
            StatusUpdate?.Invoke(followerAcc.Name + ": PTT-STP-Drag: CreateOrder returned null");
            return;
        }
        followerAcc.Submit(new[] { newStop });
        StatusUpdate?.Invoke(followerAcc.Name + ": PTT-STP-Drag placed @ " + stopPrice.ToString());
    }
    catch (Exception ex)
    {
        StatusUpdate?.Invoke(followerAcc.Name + ": PTT-STP-Drag error: " + ex.Message);
    }
}
```

**AFTER:**
```csharp
// B171 DW-B171-02: suffix param added; order name changed from "PTT-STP-Drag" to "PTT-STP-Drag-"+suffix.
// CYC=4 unchanged: base(1)+newStop null(1)+try block(1)+catch(1).
// JS-021: no lock (private, no shared state). JS-001: try/catch absorbs. JS-002: void return. ASCII-only.
private void SubmitReplacementStopOrder(
    Account followerAcc,
    Instrument instr,
    int qty,
    OrderAction stopAction,
    double stopPrice,
    string suffix
)
{
    try
    {
        var newStop = followerAcc.CreateOrder(
            instr,
            stopAction,
            OrderType.StopMarket,
            OrderEntry.Automated,
            TimeInForce.Day,
            qty,
            0,
            stopPrice,
            "",
            "PTT-STP-Drag-" + suffix,
            NinjaTrader.Core.Globals.MaxDate,
            (NinjaTrader.Cbi.CustomOrder)null
        );
        if (newStop == null)
        {
            StatusUpdate?.Invoke(followerAcc.Name + ": PTT-STP-Drag-" + suffix + ": CreateOrder returned null");
            return;
        }
        followerAcc.Submit(new[] { newStop });
        StatusUpdate?.Invoke(followerAcc.Name + ": PTT-STP-Drag-" + suffix + " placed @ " + stopPrice.ToString());
    }
    catch (Exception ex)
    {
        StatusUpdate?.Invoke(followerAcc.Name + ": PTT-STP-Drag error: " + ex.Message);
    }
}
```

**Lines changed:** +1 (suffix param), 3 modified (order name at ~line 4478, null-check StatusUpdate, placed StatusUpdate).

---

### Methods UNCHANGED (no edits)

- `CancelLiveCollateralStop` (lines 4058-4073): reused as-is
- `IsDragNameMatch` (lines 4740-4748): reused as-is
- `DeriveLeaderBracketIndex` (lines 4391-4406): reused as-is
- `ResubmitOneCollateralLeg` (lines 4038-4053): reused as-is

---

### CYC Summary

| Method | Before | After | Delta | Compliant |
|---|---|---|---|---|
| ExecutePhaseCStopReplacement | 2 | 2 | 0 | YES (<=8) |
| CreateFollowerReplacementStop | 2 | 2 | 0 | YES (<=8) |
| SubmitReplacementStopOrder | 4 | 4 | 0 | YES (<=8) |

---

## TICKETS

### B171-1 — ExecutePhaseCStopReplacement suffix + cancel sweep

**File:** `src/PropTraderTools/CopyEngine.cs`
**Lines:** 4507-4512
**Change:** Implement Method A (AFTER) above.
**Verify:** Method body matches AFTER exactly. CYC=2. No lock(). ASCII-only.

---

### B171-2 — CreateFollowerReplacementStop + SubmitReplacementStopOrder suffix threading

**File:** `src/PropTraderTools/CopyEngine.cs`
**Lines:** 4437-4451 (CreateFollowerReplacementStop), 4458-4498 (SubmitReplacementStopOrder)
**Change:** Implement Method B (AFTER) and Method C (AFTER) above.
**Verify:** Both method bodies match AFTER exactly. CYC unchanged. Line 4478 reads `"PTT-STP-Drag-" + suffix`. ASCII-only.

**DEPENDENCY:** B171-2 depends on B171-1 being committed first (ExecutePhaseCStopReplacement
must compile before CreateFollowerReplacementStop new signature is called).

---

### B171-3 — Tests

**File:** `src/PropTraderTools/CopyEngineTests.cs`
**Change:** Add xUnit tests covering:
1. Single target drag → exactly one PTT-STP-Drag-N in follower orders (name = "PTT-STP-Drag-{bracketIdx}")
2. Two consecutive target drags on same bracket → still exactly one PTT-STP-Drag-N (duplicate cancelled)
3. Two target drags on different brackets → two stops with different suffixes (neither cancelled by the other)
4. Stop drag after target drag → fo != NULL for the stop matching "PTT-STP-Drag-N"
5. stp <= 0 path: ExecutePhaseCStopReplacement skips without cancelling existing stop (EC-1 guard)

**No direct calls to private methods.** Tests exercise via the public `HandleBracketChange` surface
or via test-seam helpers (InternalsVisibleTo pattern already in use in CopyEngineTests.cs).

---

## MANDATORY PIPELINE (7-STEP — ALL PHASES MANDATORY — NONE SKIPPABLE — NONE COMBINABLE)

```
Ph1  ptt-architect       Input: this prompt
                         Output: docs/brain/B171/02-architecture-plan.md
                         Contract: architectural plan for BOTH DW-B171-01 and DW-B171-02
                                   including exact before/after per ticket, CYC table,
                                   rationale for Variant E selection over A-D.
                         Gate: none — proceed to Ph2.

Ph2  ptt-plan-reviewer   Input: 02-architecture-plan.md
                         Output: docs/brain/B171/02-plan-review.md
                         Gate: REVIEW_PASS required. Any REVIEW_FAIL sends back to Ph1.

Ph3  ptt-architect       Input: 02-plan-review.md (REVIEW_PASS)
                         Output: docs/brain/B171/04-tickets.md
                         Contract: 3 tickets (B171-1, B171-2, B171-3) with exact line numbers,
                                   exact before/after, CYC verification column per ticket.

Ph3.5 ptt-ticket-reviewer Input: 04-tickets.md
                          Output: docs/brain/B171/04-ticket-review.md
                          Gate: TICKET_REVIEW_PASS required. Any TICKET_REVIEW_FAIL sends
                                back to Ph3.

Ph4a ptt-engineer         Input: 04-ticket-review.md (TICKET_REVIEW_PASS) + 04-tickets.md
                          Output: surgical apply_diff edits to CopyEngine.cs + CopyEngineTests.cs
                                  + docs/brain/B171/ticket-N-completion.md for each ticket
                          Constraints:
                            - Use apply_diff ONLY (no write_file on CopyEngine.cs)
                            - Complete tickets IN ORDER: B171-1, then B171-2, then B171-3
                            - After ALL tickets: run powershell -File .\scripts\ptt-sync.ps1

Ph4b ptt-verifier         Input: ticket-N-completion.md files
                          Output: docs/brain/B171/ticket-N-verification.md for each ticket
                          Checks per ticket:
                            - Exact diff matches the AFTER blocks in 04-tickets.md
                            - CYC per method matches the CYC table
                            - No lock() introduced (grep -c "lock(" src/PropTraderTools/CopyEngine.cs)
                            - No Unicode/curly quotes in changed lines
                            - ptt-sync.ps1 was run after all edits
                          Gate: VERIFY_PASS required per ticket. Any VERIFY_FAIL triggers
                                a targeted fix loop (NOT a full Ph1 restart unless root
                                cause is architectural).

Ph5  ptt-plan-reviewer    Input: all ticket-N-verification.md (all VERIFY_PASS)
                          Output: docs/brain/B171/05-final-review.md
                                  docs/brain/B171/06-deferred-backlog.md
                          Contract for 06-deferred-backlog.md:
                            - DW-B171-01 and DW-B171-02: CLOSED if VERIFY_PASS
                            - DW-B170-SIM: CLOSE with note "SIM verified in B170 run"
                              if stop-drag sync confirmed in SIM
                            - All prior open items from B170/06-deferred-backlog.md
                              carried forward with updated status
                          Gate: FINAL_PASS or FINAL_FAIL.
```

---

## DEFERRED BACKLOG CARRY-FORWARD (from B170/06-deferred-backlog.md)

The following items from B170 are carried into B171 unchanged unless a ticket
in B171 explicitly closes them.

| ID | Block | Description | Priority | Status |
|----|-------|-------------|----------|--------|
| DW-B171-01 | B171 | DUPLICATE-STP-DRAG-ON-REPEATED-TGT-DRAG | P0 | OPEN — target of B171 |
| DW-B171-02 | B171 | fo=NULL-ON-STOP-DRAG-WHEN-STP-IS-PTT-NAMED | P0 | OPEN — target of B171 |
| DW-B170-SIM | B170 | SIM GATE: Director drags target on leader — confirm follower stop preserved. DW-B170-01 live regression verify. | P0 | OPEN — SIM code complete |
| DW-B169-K1 | B169 | SIM GATE: 3-stop simultaneous drag — 0 missing follower stops | P0 | OPEN (Sim102 blocked by B167-DEFER-02) |
| DW-B169-K2 | B169 | SIM GATE: single-stop drag regression | P0 | OPEN |
| DW-B168-K1 | B168 | Superseded by DW-B169-K1/K2 | P0 | OPEN (superseded) |
| B167-DEFER-02 | B167 | fo=NULL: follower ATM brackets never placed after leader fill. Dedicated block required. | P1 | OPEN |
| B167-DEFER-01 | B167 | ECA-1 double-position race. Monitor at SIM. | P2 | OPEN |
| B167-DEFER-03 | B167 | Combined B161-B166 SIM gate pending | P1 | OPEN |
| B167-DEFER-04 | B167 | B167 SIM gate pending | P1 | OPEN |
| DW-B159-K2 | B159 | 14 xUnit tests | P2 | OPEN |
| DW-B159-K3 | B159 | Attempt cap hardcoded at 3 | P2 | OPEN |
| DW-B159-K4 | B159 | intendedBePrice sourcing | P2 | OPEN |
| DW-B159-K5 | B159 | Stale line comment | P3 | OPEN |

Note: DW-B171-01 and DW-B171-02 should be CLOSED in B171/06-deferred-backlog.md
upon VERIFY_PASS of tickets B171-1 and B171-2.
DW-B170-SIM may be closed in B171 if the SIM gate run confirms stop-drag sync
is working (B171 directly addresses the root cause of DW-B170-SIM).

---

## SIM GATE (post-B171 implementation)

After B171 is implemented and verified (VERIFY_PASS all tickets), the Director
runs the following SIM sequence:

1. Open NinjaTrader SIM. Confirm leader + 3 followers connected.
2. Enter leader position (1 contract short). Confirm follower brackets appear.
3. Drag Target3 on the leader. Expected: exactly ONE `PTT-STP-Drag-3` per
   follower. Zero `PTT-STP-Drag` (unsuffixed) in follower orders.
4. Drag Target2 on the leader. Expected: exactly ONE `PTT-STP-Drag-2` per
   follower. `PTT-STP-Drag-3` from step 3 still present (different bracket).
5. Drag Target3 again. Expected: `PTT-STP-Drag-3` from step 3 is cancelled and
   replaced by a new `PTT-STP-Drag-3`. Count = still 1 per bracket.
6. Drag Stop2 on the leader. Expected: `fo != NULL` for all followers.
   Follower stop for bracket 2 moves to new price.
7. Drag Stop3 on the leader. Expected: `fo != NULL` for all followers.
   Follower stop for bracket 3 moves to new price.

Pass criteria: Steps 3-7 all show correct behavior with zero `PTT-STP-Drag`
(unsuffixed) entries and zero `fo=NULL` on stop drag events.

---

## JANE STREET COMPLIANCE CHECKLIST (apply to all tickets)

- [ ] No `lock()` statement introduced (verify: `grep -c "lock(" src/PropTraderTools/CopyEngine.cs`)
- [ ] No Unicode, curly quotes, or non-ASCII characters in any changed string literal
- [ ] No `DateTime.Now` or `FontFamily` introduced
- [ ] CYC per changed method <= 8 (verified against CYC table above)
- [ ] `apply_diff` used for all CopyEngine.cs edits (NO `write_file`)
- [ ] `ptt-sync.ps1` run after all edits
- [ ] Every changed line traces to DW-B171-01 or DW-B171-02

---

## DIRECTOR GO GATE

```
Stop. Do not begin Phase 1 until Director confirms go.
```

Paste this entire prompt into the ptt-orchestrator session, review the
winning design (Variant E) above, and confirm "go" before the ptt-architect
begins Phase 1.
