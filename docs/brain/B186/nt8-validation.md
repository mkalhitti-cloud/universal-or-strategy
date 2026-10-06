# B186 NT8 Validation

**Epic**: B186 -- Quick ALL + QAll2t Instrument Scope
**Date**: 2026-07-08
**Baseline commit**: 9e5d949f (B186-T3 -- all pipeline commits on main)

---

## Validation Result: PASS

| Check | Result |
|-------|--------|
| NT8 compile | PASS -- 0 errors |
| ptt-sync 19/0 | PASS (confirmed by T3 verification) |
| Quick ALL (Execute(Instrument)) fires correctly | PASS |
| QAll2t (Execute(List, Instrument)) fires correctly | PASS (see bug note below) |
| BE ALL fires correctly | PASS |

---

## Sequence Confirmed Working

| Sequence | Result |
|----------|--------|
| QAll2t → BE ALL | PASS -- ATMs switch correctly (confirmed in output.md lines 13-46) |
| BE ALL → QAll2t | **BUG** -- QAll2t targets submit but stops are missing (see DW-B187-QX-STOP-SNAP-01) |

---

## Bug Discovered During Validation

**DW-B187-QX-STOP-SNAP-01**: When BE ALL is fired before QAll2t, the PTT-QX-Stop orders are
not submitted. Stops are missing because `leaderStop` is snapshotted AFTER `CancelPttBeOrders`
destroys the only Working stop reference (PTT-BE-Stop-*).

See `docs/brain/B186/output.md` for full NT8 output log.
Full analysis and fix design in B187 DW card.

---

*Written: 2026-07-08*
*B186 pipeline: FINAL_PASS*
*Outstanding: DW-B187-QX-STOP-SNAP-01 (P1 -- stops missing on BE→QX sequence)*
