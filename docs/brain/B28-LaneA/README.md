# B28 Lane A — DW-B28-01 Diagnostic Hardening

Block: B28 | Lane: A | Defect: DW-B28-01 (P0)
Status: PIPELINE IN PROGRESS

## Defect
BE button goes "Live" (PendingBeFired fires) but acc.Change() may be throwing silently.
Stop price never moves. Zero "Change submitted" events in NT8 grid log (2026-07-16).

## Fix
Add 1 StatusUpdate line immediately before acc.Change() in MoveStopToBreakEven.
Allows Director to distinguish "got to Change()" vs "got past Change()" in next live test.

## Files
- 02-architecture-plan.md  (ptt-architect output — pending)
- 04-tickets.md            (ptt-architect output — pending)
- ticket-1-completion.md   (ptt-engineer output — pending)
- ticket-1-verification.md (ptt-verifier output — pending)

## Target
[Fact] baseline: 135 | target: 135 (no new tests — diagnostic only)
Files changed: CopyEngine.cs (1 line added)
