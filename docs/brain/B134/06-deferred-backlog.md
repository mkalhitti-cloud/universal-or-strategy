# B134 Deferred Backlog

**Block**: B134
**Block Header**: B134 Deferred Items
**Produced by**: ptt-plan-reviewer (Phase 5)
**Note**: No prior 06-deferred-backlog.md exists from B133. B133 had no deferred backlog file. This document begins the deferred backlog chain at B134.

---

## Deferred Items

### DW-B134-OCO — OCO Orphan Risk (ATM Partial Fill Race)

| Field | Value |
|-------|-------|
| **ID** | DW-B134-OCO |
| **Title** | OCO orphan risk from ATM partial fills |
| **Status** | DEFERRED — awaiting director SIM data |
| **Priority** | P1 |
| **Target Block** | B5 |
| **Resolution Condition** | Director runs SIM session with ATM bracket drag during partial fill sequence; verifier confirms no orphaned orders remain open after cancel+resubmit completes |

**Description**: When `SyncAtmFollowerTarget` executes a cancel+resubmit sequence (Block A cancel → Block B create+submit), there is a window in which a partial fill may arrive concurrently on the original leg. This creates an OCO orphan: the original leg is partially filled and then cancelled; the replacement order is submitted for the full original quantity, resulting in a net position drift equal to the partial fill. Four sub-observations identified during B134 SIM gate planning:

| Obs ID | Description |
|--------|-------------|
| OBS-A | Cancel races with partial fill — cancel may be rejected with `ErrorCode.UnableToCancelOrder` after a partial fill already fired |
| OBS-B | Replacement order duplicates the partially filled quantity — follower side over-positioned |
| OBS-C | Stop side not cancelled before target replacement — creates brief unhedged position window |
| OBS-D | Net position drift on two-leg partial fill — follower's bracket position diverges from leader's after the sequence |

**Dependency**: SIM test scenario showing OCO orphan condition in a controlled fill sequence. No live SIM data collected during B134.

---

### B134-DEFER-01 — B133-DEFER-01 Carry-Forward: Gap B (ATM OCO Orphan via Partial Fill)

| Field | Value |
|-------|-------|
| **ID** | B134-DEFER-01 |
| **Title** | Gap B — ATM OCO orphan risk from partial fills (B133 carry-forward) |
| **Status** | DEFERRED — awaiting SIM data |
| **Priority** | P1 |
| **Target Block** | B5 |
| **Resolution Condition** | Director SIM confirms or refutes orphan condition; if confirmed, engineer implements quantity-aware cancel guard in `SyncAtmFollowerTarget` Block A |

**Description**: Originally identified in B133 SIM observations as Gap B. The ATM OCO mechanism relies on NT8's bracket-order pairing to cancel the opposing leg when one leg fills. If a cancel+resubmit sequence (DW-B144/DW-B145 fixed path) runs simultaneously with a partial fill on the target leg, the stop leg may become orphaned — it is not cancelled by NT8's OCO logic because the fill was partial rather than complete. This item is architecturally related to DW-B134-OCO but was raised as a separate concern in B133 context. Both items require the same SIM data to close.

---

### B134-DEFER-02 — B133-DEFER-02 Carry-Forward: Stale Orders (Multi-Session)

| Field | Value |
|-------|-------|
| **ID** | B134-DEFER-02 |
| **Title** | Stale orders from prior sessions may match `FindFollowerBracketOrder` |
| **Status** | DEFERRED — no multi-session data available |
| **Priority** | P2 |
| **Target Block** | future |
| **Resolution Condition** | Director or engineer confirms whether `follower.Orders` is cleared on NT8 disconnect/reconnect; if not, a session-epoch guard is needed in `FindFollowerBracketOrder` |

**Description**: `FindFollowerBracketOrder` iterates `follower.Orders` which may, in certain NT8 reconnect scenarios, contain orders from prior trading sessions. These orders may be in `OrderState.Working` or `OrderState.Accepted` (carried forward from the prior session's unresolved state). The DW-B144 Submitted addition does not change this risk — a prior-session order in any live state would incorrectly match the filter and return as a valid `fo`. Risk is LOW under normal trading hours (orders are cleared on disconnect), but has not been empirically confirmed across a reconnect cycle. No multi-session test data available.

---

### DW-B141 — Phase C Working (Pending Director SIM Test A)

| Field | Value |
|-------|-------|
| **ID** | DW-B141 |
| **Title** | SyncAtmFollowerTarget Phase C operable — pending SIM Test A director confirmation |
| **Status** | OPEN (awaiting SIM run) |
| **Priority** | P1 |
| **Target Block** | B5 |
| **Resolution Condition** | Director runs SIM Test A (ATM bracket drag with target order in Phase C / stop replacement path) and confirms expected follower sync behavior; reviewer marks CLOSED |

**Description**: The architecture plan §B.4 confirms that Phase C (`SyncAtmFollowerTarget` stop replacement sub-path: `DeriveLeaderBracketIndex` + `FindLeaderStopPrice`) is not modified in B134 but was confirmed as reachable and operational by the B134 SIM gate planning analysis. DW-B141 was pre-B134 and remains open pending the director's live SIM run that exercises Phase C end-to-end (drag a leader target bracket far enough to trigger stop replacement). No code changes required — this is a SIM verification gate only.

---

### DW-B138 — Follower Stop Drag Confirmed (Pending Director SIM Test B)

| Field | Value |
|-------|-------|
| **ID** | DW-B138 |
| **Title** | Follower stop leg drag sync confirmed — pending SIM Test B director confirmation |
| **Status** | OPEN (awaiting SIM run) |
| **Priority** | P1 |
| **Target Block** | B5 |
| **Resolution Condition** | Director runs SIM Test B (drag leader stop bracket; verify follower stop moves to same price within 1 tick) and confirms sync behavior; reviewer marks CLOSED |

**Description**: DW-B138 (introduced B131) adds `leaderName` param to `FindFollowerBracketOrder` for ATM name-based stop bracket identification. The B134 changes (DW-B144 Submitted state + DW-B145 leaderName exact guard) directly depend on DW-B138's ATM Name-based fallback path being wired correctly. SIM Test B would confirm that a stop drag on the leader side produces the correct `fo` via the updated `FindFollowerBracketOrder` and successfully cancels+resubmits the follower stop leg. No code changes required — this is a SIM verification gate only.

---

## Closure Log (Prior OPEN Items)

No prior 06-deferred-backlog.md exists at B133 or earlier for this chain. Items above represent the initial deferred backlog as of B134 close.

---

## Summary

| ID | Title | Priority | Block | Status |
|----|-------|----------|-------|--------|
| DW-B134-OCO | OCO orphan risk (OBS-A/B/C/D) | P1 | B5 | OPEN |
| B134-DEFER-01 | Gap B — ATM OCO orphan (B133 carry-forward) | P1 | B5 | OPEN |
| B134-DEFER-02 | Stale orders — multi-session gap | P2 | future | OPEN |
| DW-B141 | Phase C working — pending SIM Test A | P1 | B5 | OPEN |
| DW-B138 | Stop drag confirmed — pending SIM Test B | P1 | B5 | OPEN |

*5 open deferred items. All require director SIM data or explicit director confirmation before resolution.*

---

*Produced by ptt-plan-reviewer, B134 Phase 5. Required gate artifact for FINAL_PASS.*
