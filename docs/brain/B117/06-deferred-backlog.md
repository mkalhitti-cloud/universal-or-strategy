# B117 Deferred Backlog

Block: B117 (DW-B125 fix -- ResolveFollowerTargets partial snapshot rejection)
Date: 2026-08-28
Status: PIPELINE_COMPLETE (Ph1-Ph5 FINAL_PASS)

---

## Items CLOSED This Block

| Item | Description | Closed By |
|------|-------------|-----------|
| DW-B125 | ResolveFollowerTargets branch (1) returns partial follower snapshot unchanged -- T3 missed, 4 contracts residual on Sim104 (Combo C BE-ALL then QX-ALL, leaderCount=3, followerSnapshot.Count=2) | B117-T1 (compound guard) |
| PARTIAL-SNAPSHOT-VARIANT (B116) | B116 deferred variant: count=1 (or any count < leaderCount) partial snapshot causes same T2/T3 miss. Closed by same compound guard that handles count=2. | B117-T1 (compound guard) |

---

## Block: B117
## Date: 2026-08-28
## Item: DW-B117-01 (COMBO-C-LIVE-GATE)

Description: B117 compound guard fix applied to ResolveFollowerTargets branch (1).
Awaiting NT8 F5 recompile confirmation and live Combo C session
(BE-ALL then QX-ALL: Sim101/102/103/104 all at 7 contracts).
Expected result: T1=4, T2=2, T3=1 on all four followers.
ptt-sync-and-verify.ps1 confirmed 16/16 MD5 OK, 0 MISMATCH (B117 scope).
F5 gate is Director-owned (requires local NinjaTrader 8 session).
This is the sole Combo C blocker after DW-B125 and PARTIAL-SNAPSHOT-VARIANT are closed.
Priority: P0
Status: PENDING
Required gate: NT8 F5 recompile (Compilation succeeded, 0 errors) + live Combo C session PASS

---

## Carry-Forward from B116 (status unchanged by B117)

B117 changes are scoped to PttGlobalQuickExit.cs ResolveFollowerTargets branch (1) only.
None of the following items are affected by B117.

---

### DW-B120-MONITOR

Description: CalcTNQty arithmetic split still used in non-BE QX path. When follower snapshot is empty
AND leaderTargets is also empty (Sim103 async ATM lag, no BE-ALL preceding QX-ALL), ResolveFollowerTargets
branch (2) returns the empty followerSnapshot unchanged and CalcTNQty fires. Acceptable for equal-qty
accounts (scale=1.0 => same arithmetic result). No code change required unless live evidence of wrong
split in a non-BE scenario with unequal account sizes is observed.
Priority: P2
Status: DEFERRED
Required gate: Combo C live session data -- if non-BE path produces wrong split, escalate to B118.

---

### B107-DEFER-01 -- F5 NinjaTrader 8 Compilation Gate (B107 changes)

Priority: P0
Status: PENDING (Director-owned)
See: docs/brain/B107/06-deferred-backlog.md

---

### B107-DEFER-02 -- Combo C Live Re-Test (B107 changes)

Priority: P1
Status: PENDING (superseded by DW-B117-01 above which covers B117 fix as well as all prior blocks)
See: docs/brain/B107/06-deferred-backlog.md

---

### DW-B107 -- MoveStopToBreakEven Step A snapshots stale PTT-BE-Target-* on followers

Priority: P2
Status: DEFERRED to B108
See: docs/brain/B107/06-deferred-backlog.md

---

### DW-PTT-BE-FIX-03 -- Pre-existing test build errors (CopyEngineTests.cs, B76Tests.cs, B43Tests.cs)

Priority: P1
Status: DEFERRED (pre-existing, unaffected by B117)
Note: 83 pre-existing errors in CopyEngineTests.cs confirmed again by B117 engineer and verifier.
See: docs/brain/B107/06-deferred-backlog.md

---

### DW-B89-DEFERRED items (01 through 06)

Status: UNCHANGED. Carry-forward from B107.
See: docs/brain/B107/06-deferred-backlog.md for full list.

---

## Summary

| Category | Count | Items |
|----------|-------|-------|
| Closed this block | 2 | DW-B125, PARTIAL-SNAPSHOT-VARIANT (B116) |
| New deferred (B117 pipeline) | 1 | DW-B117-01 (COMBO-C-LIVE-GATE, P0) |
| Carry-forward from B116 (unchanged) | 10+ | DW-B120-MONITOR, B107-DEFER-01/02, DW-B107, DW-PTT-BE-FIX-03, DW-B89-DEFERRED-01..06 |

**Blocking gate before live trading**: DW-B117-01 (P0) -- NT8 F5 + live Combo C session.
