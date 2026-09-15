# B159 Forensic Intake

**Block ID:** B159
**Parent defect:** DW-B159 (formerly DW-B158-DRAIN-RACE-01)
**Severity:** P0 -- Live money at risk
**Status:** OPEN -- Awaiting pipeline start

---

## Defect Summary

Ghost re-entry on followers during multi-leg PTT-BE-Stop fill sequence.
`IsLeaderFlatDrainAbort` never fires. `[DRAIN-ABORT-LEADER-FLAT]` absent from 1764-line output.

**Confirmed:** SIM run 2026-09-15, Trade 3 sequence (6:18 PM).
**Evidence:** docs/output.md + docs/NinjaTrader Grid 2026-09-14 06-18 PM.csv

---

## Root Cause

### Level 1 -- Per-leg fill ordering race
- `TryEvictFollowerBeSlot` called on each PTT-BE-Stop-N fill individually
- PTT-BE-Stop-1 fills (output L1657): leader holds remaining lots -> `leaderIsFlat = false` -> drain-abort skips (correct)
- PTT-BE-Stop-3 fills (output L1711): leader now flat -> drain-abort WOULD fire
- BUT: followers' new Trade 3 Entry orders already in Accepted state (output L1735)
- Guard fires too late to stop the ghost entry

### Level 2 -- PositionStateChanged race window
- Output L1241-1248: `hasPos=False` then immediately `hasPos=True`
- Leader went flat and next trade entry partial-filled before drain-abort check ran
- `TryEnqueueFollowerEntry` fired on `hasPos=True` at L1247
- Drain-abort checks leader flatness but NOT whether followers have a queued entry in flight

### Supporting evidence
- `[TP4-SFB] acc=Sim104 leaderName=Stop1 fo=NULL` at L1314/1320: Sim104 bracket NULL (partial fill latency)
- Same stale order history accumulation (TP4-SFB fo=NULL) pattern from prior sessions

---

## Fix Design

### Primary fix -- BE drain latch
- New field: `private bool _beStopDrainActive` (or `Dictionary<string, bool>` keyed on instrument)
- ARM: set on BE-All arm event (when PTT-BE bracket is created)
- CLEAR: when leader `hasPos=False` confirmed AND all PTT-BE-Stop orders in terminal state
- GATE: `TryEnqueueFollowerEntry` checks `!_beStopDrainActive` before queuing
- Log markers: `[BE-DRAIN-LATCH-ARM]`, `[BE-DRAIN-LATCH-CLEAR]`, `[DRAIN-ABORT-LATCH-ACTIVE]`

### Safety net -- SubmitDrainedEntry leader-flat guard
- Inside `SubmitDrainedEntry`: if rule leader for payload instrument is flat at submission time, abort
- Log: `[DRAIN-ABORT-SUBMIT-FLAT] leader=X instr=Y`
- Complementary to latch (catches race if latch clears before drain completes)

### CYC constraints
- All new helpers: CYC <= 8
- Latch arm/clear logic: if CYC > 5, decompose into `ShouldArmBeStopDrain()` + `ShouldClearBeStopDrain()`
- ASCII-only, no lock(), PTT pipeline (all 7 phases mandatory)

---

## SIM Pass Criteria

1. Leader enters trade. BE-All fires. Output: `[BE-DRAIN-LATCH-ARM] leader=Sim101 instr=MES DEC26`
2. Market reverses. PTT-BE-Stop-1/2/3 fill. Each follower entry attempt: `[DRAIN-ABORT-LATCH-ACTIVE]`. No ghost Entry:Filled in CSV.
3. Leader flat confirmed (`hasPos=False`). Output: `[BE-DRAIN-LATCH-CLEAR] leader=Sim101 instr=MES DEC26`
4. Next trade signal: followers copy correctly, no interference.
5. CSV: zero ghost Buy Entry fills between PTT-BE-Stop-1 fill and `[BE-DRAIN-LATCH-CLEAR]`.

---

## Pipeline Instructions

All 7 phases mandatory. No shortcuts. No direct .cs edits.

Ph1  ptt-architect       -> 02-architecture-plan.md
Ph2  ptt-plan-reviewer   -> 02-plan-review.md       (REVIEW_PASS gate)
Ph3  ptt-architect       -> 04-tickets.md
Ph3.5 ptt-ticket-reviewer -> 04-ticket-review.md    (TICKET_REVIEW_PASS gate)
Ph4a ptt-engineer        -> src .cs edits + ticket-N-completion.md
Ph4b ptt-verifier        -> ticket-N-verification.md (VERIFY_PASS gate)
Ph5  ptt-plan-reviewer   -> 05-final-review.md + 06-deferred-backlog.md

Prior backlog: docs/brain/B158/06-deferred-backlog.md (READ ONLY)

_Intake authored by copier-spec mode. 2026-09-15._
