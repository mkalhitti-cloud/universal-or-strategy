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

## Two Failure Modes (updated 2026-09-15)

### Failure Mode A -- Sim102/103 ghost re-entry (original diagnosis)
- Drain-abort fires too late during multi-leg PTT-BE-Stop fill sequence
- By the time PTT-BE-Stop-3 fills (leader flat), followers already have Entry:Accepted for next trade
- `TryEnqueueFollowerEntry` fired on `hasPos=True` before drain-abort intercepted

### Failure Mode B -- Sim104 original stops survive at original stop price (refined 2026-09-15)
- Visual observation confirmed: residual position brackets are ORIGINAL ATM color (Stop1/Stop2/Stop3), NOT PTT-BE color
- This means residual contracts were protected at the ORIGINAL stop price (24t below entry), NOT at BE level
- BE-All appeared to process Sim104 (`[BE-DIAG] Sim104 orders-for-instr=8` at L872) -- BE engine saw 8 orders
- Sim104 Stop1/Stop2/Stop3 entered CancelPending -> CancelSubmitted (L875-938) as expected
- BUT: ALL Sim104 PTT-BE orders were subsequently bulk-cancelled ([BE-DIAG-CANCEL] L1071-1166)
  PTT-BE-Stop-1/2/3/4, PTT-BE-Target-3/4 all cancelled for Sim104
- No [BE-DIAG-CANCEL] for Sim104 Stop1/Stop2/Stop3 (original stops) -- they are NOT logged by this tag
- No confirmation that Sim104 Stop1/Stop2/Stop3 ever reached `Cancelled` state in output
- Net result: Sim104's BE-All transition was silently rolled back
  -- PTT-BE bracket cancelled before any fill
  -- Original Stop1/Stop2/Stop3 may have survived the cancel race (still at original stop price)
  -- 3 contracts held at full stop-loss exposure while leader/Sim102/Sim103 protected at BE
- This is the worst possible failure: BE-All fires, appears to process Sim104, but silently provides no BE protection
- Root cause hypothesis: Sim104 partial-fill bracket latency causes the BE engine order count to be inconsistent
  at the point where NT8 processes the PTT-BE bracket submission, triggering a bulk cancel of the entire PTT-BE batch

---

## Fix Design

### Fix A -- BE drain latch (addresses Failure Mode A)
- New field: `private bool _beStopDrainActive` (or `Dictionary<string, bool>` keyed on instrument)
- ARM: set on BE-All arm event (when PTT-BE bracket is created)
- CLEAR: when leader `hasPos=False` confirmed AND all PTT-BE-Stop orders in terminal state
- GATE: `TryEnqueueFollowerEntry` checks `!_beStopDrainActive` before queuing
- Log markers: `[BE-DRAIN-LATCH-ARM]`, `[BE-DRAIN-LATCH-CLEAR]`, `[DRAIN-ABORT-LATCH-ACTIVE]`

### Fix B -- PTT-BE bulk-cancel rollback guard (addresses Failure Mode B)
- After PTT-BE bracket submitted for a follower, monitor whether ALL PTT-BE orders for that follower get cancelled before any fill
- Detection condition: follower PTT-BE order count reaches terminal state (all Cancelled) with zero fills
- On detection: log `[BE-ROLLBACK-DETECTED] acc=X` + verify original Stop*/Target* order state
- If original stops still working: log `[BE-ORIG-STOPS-STILL-ACTIVE] acc=X stop=Stop1 price=P` -- this is the dangerous state
- Action: either (a) re-submit PTT-BE bracket for that follower, or (b) move original stops to BE price via Account.Change()
- PTT architect to decide action in Ph1; note Account.Change() is AddOnBase-available but may be silent no-op on ATM-owned brackets
- Also investigate why Sim104 PTT-BE bulk-cancels while Sim101/102/103 do not -- likely NT8 order-count validation inconsistency during partial-fill bracket lag

### Safety net -- SubmitDrainedEntry leader-flat guard
- Inside `SubmitDrainedEntry`: if rule leader for payload instrument is flat at submission time, abort
- Log: `[DRAIN-ABORT-SUBMIT-FLAT] leader=X instr=Y`
- Complementary to Fix A (catches race if latch clears before drain completes)

### CYC constraints
- All new helpers: CYC <= 8
- Latch arm/clear logic: if CYC > 5, decompose into `ShouldArmBeStopDrain()` + `ShouldClearBeStopDrain()`
- Rollback detection logic: if CYC > 5, decompose into `DetectBeRollback()` + `HandleBeRollback()`
- ASCII-only, no lock(), PTT pipeline (all 7 phases mandatory)

### Log markers (all new)
- `[BE-DRAIN-LATCH-ARM] leader=X instr=Y` -- Fix A: latch armed on BE-All
- `[BE-DRAIN-LATCH-CLEAR] leader=X instr=Y` -- Fix A: latch cleared on leader flat + stops terminal
- `[DRAIN-ABORT-LATCH-ACTIVE] acc=X instr=Y` -- Fix A: entry blocked by active latch
- `[BE-ROLLBACK-DETECTED] acc=X` -- Fix B: all PTT-BE orders cancelled before any fill
- `[BE-ORIG-STOPS-STILL-ACTIVE] acc=X stop=StopN price=P` -- Fix B: original stop confirmed still working

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
