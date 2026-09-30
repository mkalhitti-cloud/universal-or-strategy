# DW-B167-02 — CLOSURE RECORD

**ID:** DW-B167-02
**Original priority:** P1
**Closed as:** DORMANT — scenario not used in live workflow
**Closed by:** copier-spec mode — forensic audit session
**Date:** 2026-10-10

---

## Original Claim (Sep 18)

"Follower ATM brackets NEVER placed after leader fill — fo=NULL on all drags."

## What Was Actually Happening

The Sep 18 test session that produced fo=NULL was confounded by B167-01
(DRAIN-ABORT-FLAT). The follower entry was being killed entirely by the flat
guard — it never resubmitted, never filled, so of course no ATM brackets
existed. Once B167-01 was fixed (HadPositionAtDrainTime), the follower entry
resubmits correctly and NT8 arms ATM brackets via StartAtmStrategy automatically.

## True Residual Gap (Code)

The drain-resubmit path (SubmitDrainedEntry → SubmitEntryDirect) creates a plain
"PTT-Copy" order without StartAtmStrategy. If a user drags an unfilled entry in
Clone/ATM mode, the drain-resubmitted entry will fill without ATM brackets.

## Why This Is Dormant

Director confirmed the drag-before-fill scenario does not occur in live workflow.
The normal path (no pre-fill drag) correctly calls SendCopyWithAtm →
StartAtmStrategy → NT8 arms brackets. Output.md confirms Stop1/Stop2/Stop3 and
Target1/Target2/Target3 all present and Working on follower accounts in SIM.

## Status

DORMANT. Downgraded from P1 to P2/monitor. No pipeline required.
Reopen only if Director confirms drag-before-fill is used in live trading.

---

*Closed by copier-spec mode. Evidence: output.md L1-L18 (all brackets present);
B167/ticket-1-completion.md (HadPositionAtDrainTime fix confirmed in source);
Director verbal confirmation that drag-before-fill is not a live workflow action.*
