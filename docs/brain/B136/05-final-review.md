# B136 Final Review

**Block**: B136
**Title**: DW-B148 P1 — SignalOrNameMatches PTT-prefix fix + DW-B146 CLOSE
**Produced by**: ptt-plan-reviewer (Phase 5)
**Date**: 2026-09-07
**Status**: FINAL_PASS

---

## Section A — Pipeline Summary

| Phase | Agent | Gate | Verdict |
|-------|-------|------|---------|
| Ph1 — Architecture Plan | ptt-architect | PLAN_COMPLETE | PASS |
| Ph2 — Plan Review | ptt-plan-reviewer | REVIEW_PASS | PASS (0 violations, 8/8 checks) |
| Ph3 — Ticket Generation | ptt-architect | TICKETS_COMPLETE | PASS (1 ticket) |
| Ph3.5 — Ticket Review | ptt-ticket-reviewer | TICKET_REVIEW_PASS | PASS (0 violations, 10/10 checks) |
| Ph4a — Engineer Build | ptt-engineer | BUILD_PASS | PASS (7 scans zero, 71/71 tests) |
| Ph4b — Verifier | ptt-verifier | VERIFY_PASS | PASS (0 divergences across all 7 scans) |

**Tickets**: 1/1 complete.

**Source confirmation** (from ticket-1-verification.md):
- `OrderPassesBracketGate` present at CopyEngine.cs L2671–L2680.
- `OrderPassesBracketGateTestable` test seam present at L2684–L2689.
- `FindFollowerBracketOrder` list-overload loop body updated at L2609; CYC comment updated at L2596–L2598.
- B136Tests.cs: 9 [Fact] tests, all PASS.
- 71/71 total tests PASS.

---

## Section B — Root Cause Confirmed

**Confirmed root cause (plan §A, verified in ticket-1-verification.md §DW-B148 Fix Path Confirmed)**:

For ATM bracket drag orders, `leaderOrder.FromEntrySignal` is always `null`. The pre-B136
`FindFollowerBracketOrder` list overload called `SignalOrNameMatches(order, signalName=null, leaderName="Target3")`
first. `SignalOrNameMatches` branch (3) evaluates `order.Name == leaderName` — i.e.
`"PTT-TGT-Drag" != "Target3"` — and returns `false`, rejecting the order before
`MatchesLeaderName` (B135 T1, which correctly handles PTT-prefix names) could ever be reached.
Result: `fo = null`, sync aborted on every second drag.

**Fix path — verified end-to-end in source**:

```
SyncFollowerBracket (L2247)
  → FindFollowerBracketOrder(acc, fromEntrySignal=null, isStop=false, leaderName="Target3")  (L2609)
    → OrderPassesBracketGate(order, signalName=null, leaderName="Target3", isStop=false)     (L2671)
      → signalName == null → ATM path → MatchesLeaderName(order, "Target3", false)           (L2677)
        → !isStop && order.Name == "PTT-TGT-Drag" → true                                     (L2649)
  → fo = PTT-TGT-Drag order returned. Sync proceeds.
```

`DW-B148 CLOSED.` `DW-B146 CLOSED` as consequence (MatchesLeaderName is now reachable for
PTT-prefix replacement orders).

---

## Section C — Cross-File Coherence

| File | Item | Status |
|------|------|--------|
| CopyEngine.cs | `OrderPassesBracketGate` method present at L2671 | CONFIRMED |
| CopyEngine.cs | Signature: `private static bool OrderPassesBracketGate(Order, string?, string?, bool)` | CONFIRMED |
| CopyEngine.cs | Signal path: `if (signalName != null) return order.FromEntrySignal == signalName;` | CONFIRMED |
| CopyEngine.cs | ATM path: `return MatchesLeaderName(order, leaderName, isStop);` | CONFIRMED |
| CopyEngine.cs | `OrderPassesBracketGateTestable` test seam at L2684 | CONFIRMED |
| CopyEngine.cs | Two-guard sequence replaced by single `OrderPassesBracketGate` call at L2609 | CONFIRMED |
| CopyEngine.cs | CYC comment updated to CYC=7 (AT LIMIT RESOLVED) at L2596–L2598 | CONFIRMED |
| CopyEngine.cs | `SignalOrNameMatches` — UNCHANGED (B133Tests.cs unaffected) | CONFIRMED |
| CopyEngine.cs | `MatchesLeaderName` — UNCHANGED (B135 T1 code preserved; B135Tests.cs unaffected) | CONFIRMED |
| B136Tests.cs | 9 [Fact] methods via `OrderPassesBracketGateTestable` | CONFIRMED |
| B136Tests.cs | xUnit only — no NUnit, no MSTest | CONFIRMED |
| PropTraderTools.csproj | `<Compile Include="Tests\B136Tests.cs" />` at L164 | CONFIRMED |

**No cross-file JS violations found.** `OrderPassesBracketGate` is a private static pure
predicate: no shared mutable state (JS-021 clean), no throw (JS-001 clean), no return null
(JS-002 clean — returns bool), ASCII-only (JS-003 / SCAN-05 clean).

---

## Section D — DW Item Final Status

| ID | Title | B135 Status | B136 Status | Change |
|----|-------|-------------|-------------|--------|
| DW-B148 | SignalOrNameMatches PTT-prefix gate | OPEN | **CLOSED** | VERIFY_PASS + 9/9 B136 tests + fix path confirmed end-to-end |
| DW-B146 | Second drag fo=null | OPEN | **CLOSED** | Closed as consequence of DW-B148; MatchesLeaderName now reachable |
| DW-B147 | rawPrice==newPrice early-return guard | DEFERRED (P2) | DEFERRED (P2) | UNCHANGED |
| DW-B141 | Phase C re-confirmation — pending SIM Test A | OPEN (P1) | OPEN (P1) | UNCHANGED |
| DW-B138 | Stop drag confirmed — pending SIM Test B | OPEN (P1) | OPEN (P1) | UNCHANGED |
| B135-DEFER-01 | Gap B — two simultaneous entries | OPEN (P1) | OPEN (P1) | UNCHANGED |
| B135-DEFER-02 | Stale orders multi-session | OPEN (P2) | OPEN (P2) | UNCHANGED |
| DW-B134-OCO-OBS | OBS-A/B/C/D partial-fill race conditions | OPEN (P1) | OPEN (P1) | UNCHANGED |

---

## Section E — All 7 Scans Confirmed Zero

Source: ticket-1-verification.md §7-Scan Comparison Table (Layer 2 vs Layer 3). All scans MATCH.

| SCAN ID | Scan Description | Layer 2 (Engineer) | Layer 3 (Verifier) | Match? | Status |
|---------|-----------------|--------------------|--------------------|--------|--------|
| SCAN-01 | `grep -r "lock("` | 0 hits in new/modified code | 0 hits confirmed (grep run independently) | MATCH | PASS |
| SCAN-02 | `grep -rn "async void "` | 0 hits in new code | 0 hits confirmed | MATCH | PASS |
| SCAN-03 | `grep -rn "return null;"` | 0 hits in new methods | 0 hits confirmed (OrderPassesBracketGate returns bool) | MATCH | PASS |
| SCAN-04 | `python scripts/complexity_audit.py` | FindFollowerBracketOrder=7, OrderPassesBracketGate=2, Testable=1 | Manual count confirmed: foreach(1)+guard(1)+state(3)+isStop(1)+type(1)=7; if(1)+base(1)=2; expr-body=1 | MATCH | PASS |
| SCAN-05 | ASCII-only check | 0 non-ASCII chars in B136 code | 0 non-ASCII confirmed | MATCH | PASS |
| SCAN-06 | `dotnet build` | 0 errors, 0 new warnings | 0 errors, 0 warnings | MATCH | PASS |
| SCAN-07 | `dotnet test` | 71/71 PASS | 9/9 B136Tests + 62/62 B129-B135 = 71/71 PASS confirmed | MATCH | PASS |

**Divergences: NONE.** All 7 scans MATCH between Layer 2 and Layer 3.

---

## Section F — Spec Requirements Satisfied

| Requirement | Source | Status |
|-------------|--------|--------|
| DW-B148: `OrderPassesBracketGate` fused guard enables PTT-TGT-Drag / PTT-STP-Drag to be found on second drag | specs/002-trade-copier-spec.html §DW-B148 | SATISFIED |
| DW-B146: Second drag `fo=null` eliminated — MatchesLeaderName now reachable for ATM-path PTT-prefix orders | specs/002-trade-copier-spec.html §DW-B146 | SATISFIED |
| DW-B147 rawPrice guard | P2 deferred | UNCHANGED — not in scope for B136 |
| DW-B141 SIM Test A | P1 open | UNCHANGED — requires SIM run |
| DW-B138 SIM Test B | P1 open | UNCHANGED — requires SIM run |
| B135-DEFER-01 Gap B runtime | P1 open | UNCHANGED — requires SIM data |
| B135-DEFER-02 Stale orders | P2 open | UNCHANGED — requires SIM confirmation |
| DW-B134-OCO-OBS OBS-A/B/C/D | P1 open | UNCHANGED — requires SIM data |

---

## Section G — CYC Budget Final State

| Method | File | Pre-B136 CYC | Post-B136 CYC | Limit | Status |
|--------|------|-------------|--------------|-------|--------|
| `FindFollowerBracketOrder` (list overload) | CopyEngine.cs | 8 (AT LIMIT) | **7** (AT LIMIT RESOLVED) | 8 | PASS — headroom +1 |
| `OrderPassesBracketGate` (NEW) | CopyEngine.cs | — | **2** | 8 | PASS |
| `OrderPassesBracketGateTestable` (NEW) | CopyEngine.cs | — | **1** | 8 | PASS |
| `SignalOrNameMatches` | CopyEngine.cs | 3 | **3** (UNCHANGED) | 8 | PASS |
| `MatchesLeaderName` | CopyEngine.cs | 5 | **5** (UNCHANGED) | 8 | PASS |

All methods ≤ 8. No CYC violations. `FindFollowerBracketOrder` relieved from AT LIMIT state.

---

## Section H — Test Coverage Final State

| Suite | File | Tests | Result |
|-------|------|-------|--------|
| B136 — OrderPassesBracketGateTestable | B136Tests.cs | 9 | 9/9 PASS |
| B135 — MatchesLeaderName + FindFollowerBracketOrder | B135Tests.cs | 12 | 12/12 PASS |
| B133 — SignalOrNameMatchesTestable | B133Tests.cs | (included in B129-B135 total) | PASS |
| B129–B134 prior suites | various | 50 | 50/50 PASS |
| **TOTAL** | | **71** | **71/71 PASS** |

Note: B129-B135 total is 62 (ticket-1-completion.md) — confirming 9 new + 62 prior = 71 total.

---

## Section I — Known Issues / Edge Cases

1. **SCAN-06 minor divergence** (non-blocking): The engineer stated "0 new warnings"; the verifier
   independently confirmed "0 warnings". The phrasing difference (new vs total) reflects a pre-existing
   warning baseline acknowledged in earlier blocks. In both cases the B136 delta contribution is zero.
   No action required.

2. **NT8 SIM test still required** (non-blocking for code gate): `DW-B148` is closed at the code
   level (unit tests pass, fix path verified in source). However, a SIM run is required to confirm
   the fix works in the live NT8 environment on a real ATM second-drag event. See Section J.

3. **`SignalOrNameMatches` branch (3) still present** (by design): The strict `order.Name == leaderName`
   branch remains in `SignalOrNameMatches` and is not modified. This is intentional — `SignalOrNameMatches`
   is not called by `FindFollowerBracketOrder` after B136. Its three-branch logic is fully covered by
   B133Tests.cs and is unchanged.

---

## Section J — NT8 SIM Test Required

### SIM Test A for B136 — DW-B148 Production Confirmation

| Field | Detail |
|-------|--------|
| **Test ID** | B136-SIM-A |
| **DW Item** | DW-B148 |
| **Status** | PENDING (code verified; SIM not yet run) |
| **Priority** | P1 |

**Procedure**:
1. Open leader account + 1 follower account in NinjaTrader SIM.
2. Enter a position on the leader via ATM strategy.
3. Drag the leader target order to a new price (first drag). Confirm follower target syncs.
4. Drag the leader target order again to a second new price (second drag — **this is the fix scenario**).
5. Confirm: verifier trace shows `fo=PTT-TGT-Drag` (not null) for the second drag.
6. Confirm: follower target bracket moves to the new price within 1 tick.

**Pass criteria**: Second drag produces non-null `fo` and follower bracket syncs. `DW-B148 production CLOSED`.
**Fail criteria**: `fo=null` still observed on second drag — escalate to director for investigation.

---

### DW-B141 SIM Test A — Phase C Re-Confirmation (Carry-Forward)

| Field | Detail |
|-------|--------|
| **Test ID** | DW-B141-SIM-A |
| **Status** | PENDING (from B135 backlog) |

**Procedure** (from B135 §DW-B141): Drag leader target far enough past current stop that the stop
must relocate (Phase C trigger). Observe follower: PTT-TGT-Drag should move AND PTT-STP-Drag should
appear/move. Both required for DW-B141 CLOSED.

---

### DW-B138 SIM Test B — Stop Drag Confirmation (Carry-Forward)

| Field | Detail |
|-------|--------|
| **Test ID** | DW-B138-SIM-B |
| **Status** | PENDING (from B135 backlog) |

**Procedure** (from B135 §DW-B138): Drag leader stop bracket to a new price. Observe follower stop
bracket syncs within 1 tick. If sync occurs: DW-B138 CLOSED.

---

## Section K — Deferred Work

**New deferred items in B136**: NONE. DW-B148 and DW-B146 both CLOSED this block.

**Carry-forward from B135 (all UNCHANGED)**:

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B147 | rawPrice==newPrice early-return guard (SyncAtmFollowerBracket/SyncAtmFollowerTarget) | P2 | B136+ | DEFERRED |
| DW-B141 | Phase C re-confirmation — pending SIM Test A | P1 | B135 SIM | OPEN |
| DW-B138 | Stop drag confirmed — pending SIM Test B | P1 | B135 SIM | OPEN |
| B135-DEFER-01 | Gap B — two simultaneous leader entries | P1 | B136+ | OPEN |
| B135-DEFER-02 | Stale orders from prior sessions may match FindFollowerBracketOrder | P2 | future | OPEN |
| DW-B134-OCO-OBS | OBS-A/B/C/D partial-fill race conditions | P1 | future | OPEN |

---

*Produced by ptt-plan-reviewer, B136 Phase 5. Gate artifact for FINAL_PASS.*
