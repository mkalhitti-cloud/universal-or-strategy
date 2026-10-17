# B198 T2a SIM Evidence

**Date**: 2026-10-14 (B199 session)
**Epic**: B199 (collected during B199 SIM validation run)
**Status**: H1 CONFIRMED

## Evidence

From B199 output.md lines 59-61 (2026-10-14 SIM run):
- [PTT-ATM-SEND] acct=Sim102 qty=10 tmpl= obj=set
- [PTT-ATM-SEND] acct=Sim103 qty=10 tmpl= obj=set
- [PTT-ATM-SEND] acct=Sim104 qty=10 tmpl= obj=set

Also from B199 output.md line 34 (earlier dispatch in same SIM session):
- [PTT-ATM-SEND] acct=Sim104 qty=10 tmpl= obj=set

Leader qty: 10
Follower qty sent: 10 (all three followers)

## Hypothesis

H1 CONFIRMED: qty passes through unchanged (no divide needed).
The follower received qty=10, matching the leader entry qty=10.

## T2b H2 Code Assessment

T2b's H2 logic (_atmTemplateGroupQty + TryGetAtmTemplateGroupCount + qty division) is a
benign no-op: TryGetAtmTemplateGroupCount returns false when _atmTemplateGroupQty dict is
empty (no templates scanned with perGroupQty > 0 in this SIM setup), so division never runs.

Recommendation: Remove T2b H2 logic in a future cleanup wave (DW-B199-H2-CLEANUP-01, P3).
Current state: harmless. No action required for correctness.

## Closure

DW-B198-QTY-SIM-01: CLOSED.
Source: docs/brain/B199-direct-repairs.md, docs/brain/output.md lines 34, 59-61
