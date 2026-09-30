# B116 Deferred Backlog

Block: B116 (DW-B124 fix — Option B, leader qty array passthrough)
Date: 2026-08-28
Status: PIPELINE_COMPLETE (Ph1–Ph5 FINAL_PASS)

---

## Items CLOSED This Block

| Item | Description | Closed By |
|------|-------------|-----------|
| DW-B124 | CalcTNQty fallback wrong split when BE-ALL consumes native ATM brackets before QX-ALL (Combo C) | B116-T1 |

---

## Block: B116
## Date: 2026-08-28
## Item: DW-B120-MONITOR

Description: CalcTNQty arithmetic split still used in non-BE QX path. When follower snapshot is empty
AND leaderTargets is also empty (Sim103 async ATM lag, no BE-ALL preceding QX-ALL), ResolveFollowerTargets
branch (2) returns the empty followerSnapshot unchanged and CalcTNQty fires. Acceptable for equal-qty
accounts (scale=1.0 => same arithmetic result). No code change required unless live evidence of wrong
split in a non-BE scenario with unequal account sizes is observed.
Priority: P2
Status: DEFERRED
Required gate: Combo C live session data — if non-BE path produces wrong split, escalate to B117.

---

## Item: COMBO-C-LIVE-GATE

Description: B116 fix applied (DW-B124). Awaiting NT8 F5 recompile confirmation and live Combo C
session (BE-ALL then QX-ALL with equal-qty accounts: Sim101=7, Sim102=7, Sim103=7, Sim104=7) to
confirm T1=4, T2=2, T3=1 split on all followers. ptt-sync-and-verify.ps1 confirmed 16/16 MD5 OK,
0 MISMATCH. F5 gate is Director-owned (requires local NinjaTrader 8 session).
Priority: P1
Status: PENDING
Required gate: NT8 F5 recompile (Compilation succeeded, 0 errors) + live Combo C session PASS

---

## Item: PARTIAL-SNAPSHOT-VARIANT

Description: Sim104 variant (count=1): if exactly one PTT-BE-Target-* reaches Working before
SnapshotTargetOrders runs, followerSnapshot.Count=1 > 0 and ResolveFollowerTargets branch (1)
returns the partial snapshot unchanged. T2 and T3 submission depends on partial snapshot having
only 1 entry => PttQuickExit may submit wrong qty for T2/T3 or miss them. This is the pre-existing
DW-B120 P1 monitor scope — not a new defect introduced by B116. Monitor for live evidence.
Priority: P1
Status: DEFERRED
Required gate: Combo C live session with Sim104 T2/T3 order event CSV confirmation.

---

## Carry-Forward Items (from B107 — status unchanged by B116)

B116 changes are scoped to PttGlobalQuickExit.cs only. None of the following items
are affected by B116.

---

### B107-DEFER-01 — F5 NinjaTrader 8 Compilation Gate (B107 changes)

Priority: P0
Status: PENDING (Director-owned)
See: docs/brain/B107/06-deferred-backlog.md

---

### B107-DEFER-02 — Combo C Live Re-Test (B107 changes)

Priority: P1
Status: PENDING (superseded by COMBO-C-LIVE-GATE above which covers B116 fix as well)
See: docs/brain/B107/06-deferred-backlog.md

---

### DW-B107 — MoveStopToBreakEven Step A snapshots stale PTT-BE-Target-* on followers

Priority: P2
Status: DEFERRED to B108
See: docs/brain/B107/06-deferred-backlog.md

---

### DW-PTT-BE-FIX-03 — Pre-existing test build errors (CopyEngineTests.cs, B76Tests.cs, B43Tests.cs)

Priority: P1
Status: DEFERRED (pre-existing, unaffected by B116)
See: docs/brain/B107/06-deferred-backlog.md

---

### DW-B89-DEFERRED items (01 through 06)

Status: UNCHANGED. Carry-forward from B107.
See: docs/brain/B107/06-deferred-backlog.md for full list.

---

## Summary

| Category | Count | Items |
|----------|-------|-------|
| Closed this block | 1 | DW-B124 |
| New deferred (B116 pipeline) | 3 | DW-B120-MONITOR, COMBO-C-LIVE-GATE, PARTIAL-SNAPSHOT-VARIANT |
| Carry-forward from B107 (unchanged) | 10+ | B107-DEFER-01/02, DW-B107, DW-PTT-BE-FIX-03, DW-B89-DEFERRED-01..06 |

**Blocking gate before live trading**: COMBO-C-LIVE-GATE (P1) — NT8 F5 + live Combo C session.
