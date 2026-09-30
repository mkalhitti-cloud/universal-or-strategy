# B136 Architecture Plan

**Block**: B136
**Title**: DW-B148 P1 — SignalOrNameMatches PTT-prefix fix + DW-B146 CLOSE
**Produced by**: ptt-architect (Phase 1)
**Date**: 2026-09-07
**Status**: REVIEW_PASS (Phase 2 confirmed)

---

## Section A — Root Cause Analysis

### Confirmed Trace (B135 SIM 2026-09-01)

Call site at `SyncFollowerBracket` L2247:
```
var fo = FindFollowerBracketOrder(acc, leaderOrder.FromEntrySignal, isStop, leaderOrder.Name);
```

For ATM bracket drag orders, `leaderOrder.FromEntrySignal` is **always null**. `leaderOrder.Name` is e.g. `"Target3"`.

`FindFollowerBracketOrder` (pre-B136) iterates follower orders and for each calls:
1. `SignalOrNameMatches(order, signalName=null, leaderName="Target3")`
2. If that returns true: `MatchesLeaderName(order, leaderName, isStop)` (B135 T1)

`SignalOrNameMatches` logic (L2511-2518 pre-B136):
- Branch (1): `signalName == null` → `false` (skip signal path)
- Branch (2): `leaderName == null` → `false` (skip)
- Branch (3): `return order.Name == leaderName` → `"PTT-TGT-Drag" != "Target3"` → **false → REJECTED**

Result: `MatchesLeaderName` (B135 T1, which correctly handles `PTT-TGT-Drag`) **is never reached** for the replacement drag order. `fo = null`. Sync aborted.

**Root cause confirmed**: `SignalOrNameMatches` branch (3) rejects `PTT-TGT-Drag` before `MatchesLeaderName` can run, because `order.Name == leaderName` is a strict exact match that does not recognise PTT-prefix replacements.

---

## Section B — Fix Design Decision

### Option C (CHOSEN)

Introduce `OrderPassesBracketGate(order, signalName, leaderName, isStop)` — a new private static helper (CYC=2) that **fuses** the two-guard sequence into a single predicate:

- **Signal path** (`signalName != null`): `return order.FromEntrySignal == signalName;` — strict, preserves original exclusivity.
- **ATM path** (`signalName == null`): `return MatchesLeaderName(order, leaderName, isStop);` — delegates to B135 T1, which correctly handles exact ATM names AND PTT-prefix replacements.

In `FindFollowerBracketOrder` (list overload), replace the two-guard call sequence with a single call to `OrderPassesBracketGate`. This **removes one CYC branch** from `FindFollowerBracketOrder` (was CYC=8 AT LIMIT → CYC=7).

### Why NOT Option D
Option D adds PTT-prefix branches directly to `SignalOrNameMatches`, requiring an `isStop` param and signature change. This forces updates to `SignalOrNameMatchesTestable`, all B133Tests.cs callers, and potentially other call sites. More invasive, more test breakage risk, and no CYC headroom gain. Option C is strictly superior.

### Key Properties of Option C
- `SignalOrNameMatches`: **UNCHANGED** — all B133Tests.cs tests remain GREEN
- `MatchesLeaderName`: **UNCHANGED** — all B135Tests.cs tests remain GREEN
- `FindFollowerBracketOrder` CYC: 8 → 7 (AT LIMIT RESOLVED)
- `OrderPassesBracketGate` CYC: 2

---

## Section C — Method-Level Changes

| Method | File | Current CYC | New CYC | Delta | Change |
|--------|------|-------------|---------|-------|--------|
| `FindFollowerBracketOrder` (list overload) | CopyEngine.cs | 8 | 7 | -1 | Replace `SignalOrNameMatches` + `MatchesLeaderName` guard pair with single `OrderPassesBracketGate` call. Update CYC comment. |
| `OrderPassesBracketGate` (NEW) | CopyEngine.cs | — | 2 | +2 | New private static helper. Signal path: exact FromEntrySignal match. ATM path: delegates to MatchesLeaderName. |
| `OrderPassesBracketGateTestable` (NEW) | CopyEngine.cs | — | 1 | +1 | Internal test seam, expression-body delegate to OrderPassesBracketGate. |
| `SignalOrNameMatches` | CopyEngine.cs | ≤8 | ≤8 | 0 | UNCHANGED |
| `MatchesLeaderName` | CopyEngine.cs | 5 | 5 | 0 | UNCHANGED |

---

## Section D — Test Coverage Plan

### Existing tests (must stay GREEN)
- **B133Tests.cs**: Tests `SignalOrNameMatchesTestable` — UNCHANGED, all GREEN.
- **B135Tests.cs**: 7+5=12 tests covering `MatchesLeaderNameTestable` and `FindFollowerBracketOrderTestable` — UNCHANGED, all GREEN.
- **B129-B134**: 52 tests — no touched methods, all GREEN.

### New tests — B136Tests.cs (9 [Fact] methods)
All via `OrderPassesBracketGateTestable`:

| Test Name | Scenario | Expected |
|-----------|----------|----------|
| SignalPath_MatchingSignal_ReturnsTrue | signalName="ES Entry", order.FromEntrySignal="ES Entry" | true |
| SignalPath_NonMatchingSignal_ReturnsFalse | signalName="ES Entry", order.FromEntrySignal="NQ Entry" | false |
| SignalPath_NullFromEntrySignal_ReturnsFalse | signalName="ES Entry", order.FromEntrySignal=null | false |
| AtmPath_PttTgtDrag_ReturnsTrue | signalName=null, order.Name="PTT-TGT-Drag", isStop=false | true (THE FIX) |
| AtmPath_PttStpDrag_ReturnsTrue | signalName=null, order.Name="PTT-STP-Drag", isStop=true | true (THE FIX) |
| AtmPath_PttTgtDrag_WrongLeg_ReturnsFalse | signalName=null, order.Name="PTT-TGT-Drag", isStop=true | false |
| AtmPath_NativeAtmTarget_ReturnsTrue | signalName=null, order.Name="Target3", leaderName="Target3", isStop=false | true |
| AtmPath_NativeAtmStop_ReturnsTrue | signalName=null, order.Name="Stop1", leaderName="Stop1", isStop=true | true |
| AtmPath_UnknownOrder_ReturnsFalse | signalName=null, order.Name="OtherOrder", leaderName="Target3", isStop=false | false |

---

## Section E — DW Status

### DW-B148 — CLOSED
Condition: `VERIFY_PASS` for B136-T1 issued by `ptt-verifier`. Implementation confirmed: `OrderPassesBracketGate` correctly routes ATM-path drag orders to `MatchesLeaderName`, enabling `PTT-TGT-Drag` and `PTT-STP-Drag` to be found by `FindFollowerBracketOrder`.

### DW-B146 — CLOSED (consequence of DW-B148)
Condition: DW-B148 closed. DW-B146 tracked the underlying gap that `MatchesLeaderName` was unreachable for PTT-prefix orders. That gap is closed by the same fix.

### B135 carry-forward (status UNCHANGED)
| ID | Status |
|----|--------|
| DW-B147 | DEFERRED (P2) |
| DW-B141 | OPEN — awaiting SIM Test A |
| DW-B138 | OPEN — awaiting SIM Test B |
| B135-DEFER-01 | OPEN (P1) |
| B135-DEFER-02 | OPEN (P2) |
| DW-B134-OCO-OBS A/B/C/D | OPEN (P1) |

---

## Section F — LANE-SPLIT GATE RESULT

Q1. Same method or within 50 lines? **YES** — all changes are in CopyEngine.cs, within the `FindFollowerBracketOrder` cluster (~L2596-L2690).
Q2. Fix B design depends on Fix A? N/A — single fix.
Q3. Standalone value if other blocked? N/A — single fix.
Q4. Independent SIM verification path? N/A — single fix.

**LANE-SPLIT GATE RESULT: SINGLE-PIPELINE**

---

## Section G — Deferred Work

- **DW-B147**: `IsNoPriceChange` helper extraction for `SyncAtmFollowerTarget` — deferred, P2, target B136+.
- **B135-DEFER-01**: Gap B runtime (two simultaneous entries) — deferred, P1, target B136+.
- All B135 carry-forward items remain unchanged.
