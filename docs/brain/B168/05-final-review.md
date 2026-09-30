# B168 Final Review
## COLLATERAL-STORM — Concurrent multi-stop drag kills follower stops
## Block: B168 | Design ref: DW-B168-01 | Reviewer: ptt-plan-reviewer | Phase 5

---

## Inputs Read

| Document | Status |
|---|---|
| `docs/brain/B168/02-architecture-plan.md` | READ |
| `docs/brain/B168/04-ticket-review.md` | READ |
| `docs/brain/B168/ticket-1-completion.md` | READ |
| `docs/brain/B168/ticket-2-completion.md` | READ |
| `docs/brain/B168/ticket-1-verification.md` | READ |
| `docs/brain/B168/ticket-2-verification.md` | READ |
| `docs/brain/DW-B168-01/DW-B168-01-design.md` | READ |
| `docs/brain/B167/06-deferred-backlog.md` | READ (prior block context) |
| `docs/brain/DW-B167-02/00-dw-card.md` | READ (prior OPEN item context) |

---

## Check 1 — DIFF GUARD (< 10,000 characters)

**Estimated diff character count:**

| File | Lines delta | Estimated chars |
|---|---|---|
| `CopyEngine.cs` — Site A (IsLeaderLegBeingChanged, 15 lines) | +15 | ~480 |
| `CopyEngine.cs` — Site B (IsLeaderLegBeingChangedTestable, 5 lines) | +5 | ~160 |
| `CopyEngine.cs` — Site C guard (3 lines) | +3 | ~90 |
| `CopyEngine.cs` — CYC comment update (3 lines) | +3 | ~90 |
| `CopyEngineTests.cs` — DwB168Tests class (lines 10175–10271, ~97 lines) | +97 | ~2,800 |
| **Total** | **+123** | **~3,620** |

Estimated total: ~3,620 characters. Well under 10,000-character limit.

**PASS.**

---

## Check 2 — No new lock() in diff

Verifier Check 2 (ticket-1-verification.md): independent scan of `CopyEngine.cs` returned 14 hits,
all comment-only lines referencing "no lock()" or "lock-free". Zero actual `lock(` statements in
executable code. No lock introduced in the test file (ticket-2-verification.md DNA Rule JS-021: PASS).

**PASS — 0 new lock() calls. JS-021 satisfied.**

---

## Check 3 — Hard-link sync confirmed

Ticket-1-completion.md SCAN-07: `powershell -File .\scripts\ptt-sync.ps1` exit code **0**,
output: `ptt-sync complete: 18 linked, 0 errors`.

Verifier acknowledged the sync as reported (verifier protocol: ptt-sync is NOT re-run by
verifier as it modifies the live NT8 path; engineer report accepted without discrepancy).

**PASS — ptt-sync.ps1 exit 0, all 18 source files synchronized.**

---

## Check 4 — Test results: 4 new PASS, existing suite green

Ticket-2-verification.md Check 8: exactly 4 `[Fact]` methods confirmed at correct line numbers.
Check 9: all 4 tests call `CopyEngine.IsLeaderLegBeingChangedTestable` (not direct private access).
Check 10: zero NT8 runtime dependencies in test bodies.
Check 11: all 4 exact names match spec table in DW-B168-01 §6.
Check 12: `DwB168Tests` inside `namespace PropTraderTools` (consistent with existing test classes).

No discrepancies between engineer self-report and verifier independent scan on any of 7 scans.

**PASS (subject to Director SIM gate — see Section K).**

---

## Check 5 — Zero-regression across all 3 spec scenarios

### Single-stop drag (original design case)
Only Stop2 dragged. Leader's Stop1 and Stop3 are in `Working` or `Accepted`.
`IsLeaderLegBeingChanged(Stop1)` → false (neither state is ChangePending/ChangeSubmitted).
`IsLeaderLegBeingChanged(Stop3)` → false.
Both legs fall through to `ResubmitOneCollateralLeg`. Behaviour **IDENTICAL** to pre-B168.

**PASS — zero regression on single-stop path.**

### Two-stop simultaneous drag (Stop1 + Stop2)
Stop1 Accepted fires first: leg s=2 (Stop2 is ChangeSubmitted) → skip; leg s=3 → fall through → one resubmit.
Stop2 Accepted fires second: leg s=1 (Stop1 now Accepted) → fall through → one resubmit; leg s=3 → fall through.
Net: max 1 cancel+resubmit per leg. Zero storm.

**PASS — zero storm on two-stop simultaneous drag.**

### Three-stop simultaneous drag (observed failure case)
Stop1 Accepted: leg s=2 (ChangeSubmitted) → skip; leg s=3 (ChangeSubmitted) → skip. 0 extra cancels.
Stop2 Accepted: leg s=1 (Accepted) → one clean resubmit; leg s=3 (ChangeSubmitted) → skip.
Stop3 Accepted: leg s=1 (Accepted) → one clean resubmit; leg s=2 (Accepted) → one clean resubmit.
Net: **maximum 1 cancel+resubmit per leg at any time**. CancelPending storm eliminated.
Prior behaviour: 3 cancel+resubmit cycles per leg → CancelPending storm → missing follower stops.

**PASS — storm eliminated on three-stop simultaneous drag.**

---

## Check 6 — All spec requirements from DW-B168-01 satisfied

### Spec coverage matrix

| Requirement | Source | Addressed in plan? | Plan section | Implemented? | Evidence |
|---|---|---|---|---|---|
| `IsLeaderLegBeingChanged` predicate (private static bool, null guard + state check) | DW-B168-01 §3 Method 1 | YES | §4 | YES | Verifier Check 3, lines 3804–3818 |
| States checked: `ChangePending` AND `ChangeSubmitted` | DW-B168-01 §3 Method 1 | YES | §4 | YES | Verifier Check 3: `ChangePending \|\| ChangeSubmitted` confirmed |
| `IsLeaderLegBeingChangedTestable` seam (internal static, nullable param) | DW-B168-01 §6 NOTE | YES | §5 | YES | Verifier Check 3, lines 3820–3824 |
| Seam pattern matches `IsPttStpDragCancellableTestable` | DW-B168-01 §6 NOTE | YES | §5 | YES | Ticket-review Check 3: structurally identical |
| Guard in `ResubmitCollateralLegs` (after FindLeaderCollateralOrder, before ResubmitOneCollateralLeg) | DW-B168-01 §3 Method 2 | YES | §6 | YES | Verifier Check 3: lines 3981–3985 confirmed |
| CYC comment updated (CYC=3 with DW-B168-01 annotation) | DW-B168-01 §3 Method 2 comment | YES | §4 | YES | Verifier Check 5: lines 3951–3953 confirmed |
| 4 `[Fact]` tests — ChangeSubmitted_ReturnsTrue | DW-B168-01 §6 Test A | YES | §9 | YES | Verifier Check 8, line 10242 |
| 4 `[Fact]` tests — ChangePending_ReturnsTrue | DW-B168-01 §6 Test D | YES | §9 | YES | Verifier Check 8, line 10249 |
| 4 `[Fact]` tests — Working_ReturnsFalse | DW-B168-01 §6 Test B | YES | §9 | YES | Verifier Check 8, line 10256 |
| 4 `[Fact]` tests — Null_ReturnsFalse | DW-B168-01 §6 Test C | YES | §9 | YES | Verifier Check 8, line 10263 |
| All tests use testable seam (not direct private access) | DW-B168-01 §6 NOTE | YES | §9 | YES | Verifier Check 9: 4 seam call sites confirmed |
| CYC delta <= +3 | DW-B168-01 §5 | YES | §7 | YES | IsLeaderLegBeingChanged=2, ResubmitCollateralLegs=3 |
| No new lock() | JS-021 (RULES_CATALOG.md) | YES | §12 | YES | Verifier Check 2: 0 executable lock() calls |
| Zero-regression: single-stop drag unchanged | DW-B168-01 §4 | YES | §10 | YES | Plan §10 + verifier guard positioning |
| Storm eliminated: three-stop simultaneous drag | DW-B168-01 §4 | YES | §10 | YES | Plan §10: max 1 cancel+resubmit per leg |
| ptt-sync.ps1 post-step | AGENTS.md §2 + plan §11 | YES | §11 | YES | Completion: exit 0, 18 linked |

**All 16 spec requirements addressed. PASS.**

---

## Check 7 — DNA/JS Rule Cross-Check

| Rule | Check | Source | Result |
|---|---|---|---|
| JS-001 (no throw in hot path) | `IsLeaderLegBeingChanged` is expression-body, no throw | Verifier DNA table | PASS |
| JS-002 (no null return where value expected) | Returns `bool`, not nullable | Verifier DNA table | PASS |
| JS-003 (no magic string for state) | Uses `OrderState` enum values (not strings) | Plan §4, verifier Check 3 | PASS |
| JS-008 (no mutable fields on struct) | No structs introduced | Ticket-review NT8 Check | PASS |
| JS-009 (no Dictionary for shared state) | `otherLegPrices` parameter not modified; no new Dictionary | Plan §6, verifier | PASS |
| JS-010 (no public constructor on singleton) | No singletons introduced | N/A | PASS |
| JS-021 (no lock) | 0 executable lock() calls in both files | Verifier Checks 2 + B168-2 SCAN-06 | PASS |
| JS-023 (no off-thread UI update) | No UI code introduced | Plan §12, ticket-review NT8 | PASS |
| JS-036 (no heap in hot path) | Reads existing `leaderLeg.OrderState` field; no ToList, no new objects | Plan §4, verifier DNA table | PASS |
| CYC <= 8 (all methods) | IsLeaderLegBeingChanged=2, testable seam=1, ResubmitCollateralLegs=3 | Verifier Check 2 (SCAN-02) | PASS |
| ASCII-only | 0 non-ASCII chars in both files | Verifier Checks 6 + B168-2 SCAN-05 | PASS |
| No async/await in lifecycle methods | None introduced | Ticket-review NT8 Check | PASS |
| No DateTime.Now | None introduced | Verifier DNA table | PASS |
| No sealed on NinjaTrader window | No window classes touched | Ticket-review NT8 Check | PASS |
| No hardcoded #RRGGBB hex | No UI code | Ticket-review NT8 Check | PASS |
| No PTT-name violations (CreateOrder without PTT- prefix) | No CreateOrder calls introduced | Plan §12, ticket-review NT8 | PASS |
| Diff < 10,000 chars (AGENTS.md §5) | ~3,620 chars estimated | Check 1 above | PASS |
| ptt-sync.ps1 post-step (AGENTS.md §2) | Exit 0, 18 linked | Completion SCAN-07 | PASS |

**All DNA/JS rules: PASS. Zero violations.**

---

## Check 8 — Cross-File Coherence

### CopyEngine.cs ↔ CopyEngineTests.cs wiring

| Wiring point | CopyEngine.cs location | CopyEngineTests.cs reference | Confirmed by |
|---|---|---|---|
| `IsLeaderLegBeingChangedTestable` definition | Line 3823 (`internal static bool`) | Lines 10245, 10252, 10259, 10265 (4 call sites) | Verifier Check 9 |
| `IsLeaderLegBeingChanged` (production, private) | Lines 3815–3818 | Accessed only via testable seam (no direct call) | Verifier Check 9; ticket-review Check 10 |
| `DwB168Tests` namespace | — | `namespace PropTraderTools` (line 13 of test file) | Verifier Check 12 |
| `StubOrder` helper | — | `private static`, self-contained in `DwB168Tests` | Verifier Check 10 + additional observation |

### Guard call chain

`ResubmitCollateralLegs` (line ~3958) → `FindLeaderCollateralOrder` (line 3981) → `IsLeaderLegBeingChanged(leaderLeg)` guard (line 3983) → `continue` (skip) OR `ResubmitOneCollateralLeg` (line 3985).

Guard is correctly interposed: AFTER the lookup call (so `leaderLeg` is non-null or null when passed), BEFORE the action call. Verifier Check 3 confirms exact positioning.

### No cross-file pollution

- Only `src/PropTraderTools/CopyEngine.cs` and `src/PropTraderTools/CopyEngineTests.cs` touched.
- No other files modified (plan §8, completion reports).
- Zero cross-contamination confirmed.

**PASS — cross-file coherence verified.**

---

## Section K — Deferred Work

| ID | Item | Priority | Target Block | Status |
|---|---|---|---|---|
| DW-B168-K1 | Director SIM gate: drag all 3 stops simultaneously on live SIM, confirm 0 missing stops on all 3 follower accounts | P1 | SIM session after B168 deployment | OPEN |
| DW-B168-K2 | B167-DEFER-02 carried forward: DW-B167-02 ATM bracket placement fo=NULL — follower accounts never receive Stop/Target brackets after leader ATM fill. P1. Second output.md test procedure required (per DW-B167-02/00-dw-card.md). Dedicated block required. | P1 | B169 or dedicated block | OPEN |
| DW-B168-K3 | B167-DEFER-01 carried forward: ECA-1 double-position race (pre-fill drag + manual fill). Low probability. Monitor during SIM sessions. | P2 | future / monitor | OPEN |
| DW-B168-K4 | B167-DEFER-03 carried forward: combined B161–B166 SIM gate pending. Run after B167+B168 SIM gates pass. | P1 | next SIM session | OPEN |
| DW-B168-K5 | B167-DEFER-04 carried forward: B167 SIM gate pending. Must run before B168 SIM gate. | P1 | first SIM session | OPEN |
| DW-B168-K6 | B159 K2 carried forward: 14 xUnit tests for B159 features (infrastructure required) | P2 | future | OPEN |
| DW-B168-K7 | B159 K3 carried forward: attempt cap hardcoded at 3 in TryArmBePriceRetry | P2 | future | OPEN |
| DW-B168-K8 | B159 K4 carried forward: intendedBePrice sourcing dependency | P2 | future | OPEN |
| DW-B168-K9 | B159 K5 carried forward: stale line number in IsFollowerPositionBlocked comment | P3 | future / next sweep | OPEN |

### Known Acceptable Behavior (not a deferred item — documented for clarity)

Stop3 as last Accepted handler executes one clean cancel+resubmit for legs 1 and 2 (they are in
`Accepted` state by then, not `ChangeSubmitted`). This is **correct and intended** — Stop3 is
confirming the final price for all legs. Single clean cancel+resubmit, not a storm. No further
fix needed. This is explicitly documented in spec DW-B168-01 §9 and plan §10.

### SIM Gate Note for Director

**DW-B168-K1 (P1) — Required before marking B168 fully CLOSED:**

After hot-compiling B168 changes in NinjaTrader 8:

1. Open a position with 3 ATM stops on the leader account (Sim101).
2. Drag all 3 stops simultaneously (select all, drag to new price).
3. Wait for all 3 Accepted callbacks to fire.
4. Confirm all 3 follower accounts (Sim102, Sim103, Sim104) retain all 3 stops at the new price.
5. Check NT8 Output Tab for absence of `CancelPending` state log lines during the drag.

Expected result: 0 missing stops on any follower. No `CancelPending` storm. All 3 stops updated.
Failure criteria: any follower loses a stop order, or any `CancelPending` log appears for
`PTT-STP-Drag-*` orders on Sim102/103/104.

If failure occurs: the guard positioning at line 3983 must be re-examined. Check `OrderState`
values at time of `ResubmitCollateralLegs` call per follower to confirm `ChangeSubmitted` is
the state present during simultaneous drag. Log `[B168-GUARD-STATE]` diagnostic if needed.

---

## Summary

| Check | Result | Notes |
|---|---|---|
| 1. Diff < 10,000 chars | PASS | ~3,620 chars estimated |
| 2. No new lock() | PASS | 0 executable lock() in both files |
| 3. Hard-link sync | PASS | ptt-sync exit 0, 18 linked |
| 4. 4 [Fact] tests present | PASS | Subject to Director SIM gate |
| 5. Zero-regression (3 scenarios) | PASS | All 3 scenarios confirmed |
| 6. All spec requirements satisfied | PASS | 16/16 requirements addressed |
| 7. DNA/JS rules | PASS | 18/18 rules satisfied, 0 violations |
| 8. Cross-file coherence | PASS | 4 call sites, correct guard positioning, no pollution |

**All 8 checks: PASS. Zero violations. Zero rule citations required.**

---

## VERDICT: FINAL_PASS

Block B168 implementation is complete. All spec requirements satisfied. All DNA/JS rules satisfied.
Two files changed (`CopyEngine.cs` +26 production lines, `CopyEngineTests.cs` +97 test lines).
ptt-sync.ps1 exit 0. 4 unit tests present with exact spec names via testable seam.

**FINAL_PASS is granted subject to Director SIM gate (DW-B168-K1).**

---

*Final review produced by ptt-plan-reviewer. Phase 5. Block B168.*
