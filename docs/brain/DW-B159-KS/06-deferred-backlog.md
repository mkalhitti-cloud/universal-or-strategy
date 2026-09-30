# DW-B159-KS Deferred Backlog
<!-- block: DW-B159-KS -->
<!-- wave: DW-B159-KS (K-Suite: K2 + K3 + K5) -->
<!-- date: 2026-10-01 -->
<!-- produced-by: ptt-plan-reviewer (Phase 5) -->

---

## Block Header

| Field | Value |
|-------|-------|
| Epic | DW-B159-KS — K-Suite: K2 + K3 + K5 |
| Wave | DW-B159-KS |
| Phase 5 verdict | FINAL_PASS |
| Date | 2026-10-01 |
| Prior backlog sources | docs/brain/B159/06-deferred-backlog.md, docs/brain/B167/06-deferred-backlog.md |

---

## Items CLOSED This Block

---

### DW-B159-K2 — Unit Tests Implemented

| Field | Value |
|-------|-------|
| ID | DW-B159-K2 |
| Priority | P1 |
| Status | **CLOSED** |
| Closed in block | DW-B159-KS |

**Closure evidence:** 14 `[Fact]` tests added to `B159KSuiteTests` in
`src/PropTraderTools/CopyEngineTests.cs`. `dotnet test` result: Failed=0, Passed=14,
Skipped=0. All 14 method names confirmed 1:1 against canonical list. VERIFY_PASS
confirmed (ticket-3-verification.md).

---

### DW-B159-K3 — Attempt Cap Extracted to Named Constant

| Field | Value |
|-------|-------|
| ID | DW-B159-K3 |
| Priority | P2 |
| Status | **CLOSED** |
| Closed in block | DW-B159-KS |

**Closure evidence:** `private const int BeRetryMaxAttempts = 3;` declared at class-scope
field region (line 342 of `CopyEngine.cs`). Literal `if (attempts >= 3)` replaced with
`if (attempts >= BeRetryMaxAttempts)` at line 7197. Exactly 2 matches for
`BeRetryMaxAttempts` in CopyEngine.cs (declaration + usage). `attempts >= 3` = 0 matches.
VERIFY_PASS confirmed (ticket-2-verification.md).

**K3-UI note:** Settings-panel wiring for `BeRetryMaxAttempts` remains deferred as
DW-B159-K3-UI (see OPEN items below).

---

### DW-B159-K5 — Stale Line Number in Comment Corrected

| Field | Value |
|-------|-------|
| ID | DW-B159-K5 |
| Priority | P3 |
| Status | **CLOSED** |
| Closed in block | DW-B159-KS |

**Closure evidence:** `IsFollowerPositionBlocked` header comment at line 3201 now reads:
`// Signal 1 (HasOpenPosition, line 5507): position Quantity > 0 (normal case, B159).`
Stale line numbers 5273 and 5294 confirmed absent (0 matches each). `HasOpenPosition`
declaration confirmed at line 5507. VERIFY_PASS confirmed (ticket-1-verification.md).

---

## Items OPEN / Deferred

---

### DW-B159-K4 — `intendedBePrice` Sourcing Dependency

| Field | Value |
|-------|-------|
| ID | DW-B159-K4 |
| Priority | P2 |
| Target Block | future |
| Status | OPEN |

**Description:** `TryArmBePriceRetry` receives `newStop` from `MoveStopToBreakEven`. If
the BE level formula changes in a future epic, the retry will fire at the new BE level.
The formulas must remain consistent. No code change required now.

**Next action:** When any future epic modifies `newStop` computation in
`MoveStopToBreakEven`, confirm that `TryArmBePriceRetry` hook still receives the intended
retry price. Flag this item for the architect in that epic.

---

### DW-B159-K3-UI — Settings-Panel Exposure of BeRetryMaxAttempts

| Field | Value |
|-------|-------|
| ID | DW-B159-K3-UI |
| Priority | P3 |
| Target Block | future |
| Status | OPEN — **NEW this block** |

**Description:** `BeRetryMaxAttempts` was extracted to a `private const` in DW-B159-KS.
The settings-panel wiring (allowing Director to configure the value via
`PropTraderTools` settings panel) was explicitly deferred out of scope for DW-B159-KS.

**Next action:** Evaluate as part of a future settings/configuration epic. No urgency.
Pattern to follow: existing `_maxReArmAttempts` pattern in `CopyEngine.cs`.

---

### B167-DEFER-01 — ECA-1 Double-Position Race (Pre-fill Drag + Manual Fill)

| Field | Value |
|-------|-------|
| ID | B167-DEFER-01 |
| Priority | P2 |
| Target Block | future / monitor at SIM gate session |
| Status | OPEN |
| DW card | None yet |

**Description:** ~100-500 ms race window where a pre-fill drag drain fires
(`HadPositionAtDrainTime=false`) AND the follower manually fills before
`SubmitDrainedEntry` can submit the resubmit entry. If the fill arrives during the drain
window, `SubmitDrainedEntry` may submit a second entry order, potentially creating a
double follower position.

**Probability:** Low. Not observed in any test session to date.

**Mitigation in B167:** `[DRAIN-SUBMIT-PREFILL]` diagnostic log (T4) fires for every
pre-fill drain path, providing forensic evidence if a race occurs.

**Action:** Monitor during B167 SIM gate session. If double-position race is observed,
create a DW card for B168+ to add a post-submit position-count guard.

---

### B167-DEFER-02 — ATM Bracket Placement (fo=NULL)

| Field | Value |
|-------|-------|
| ID | B167-DEFER-02 |
| Priority | P1 |
| Target Block | B168 (dedicated block required) |
| Status | OPEN |
| DW card | docs/brain/DW-B167-02/00-dw-card.md |

**Description:** After leader ATM fill, follower accounts never receive Stop/Target
bracket orders. Every drag on leader brackets shows `fo=NULL` in TP4-SFB logs. Follower
`followerOrders` shows only `Entry:Cancelled` — no Stop or Target orders exist.

**Root cause (preliminary):** `IsDispatchTriggerState` rejects `StopMarket` bracket
orders at all states. Gate0.5 (`IsExitSignalNameOrAnonClose`) blocks `Target1/2/3` by
name. ATM brackets (`Stop1/2/3`, `Target1/2/3`) are never copied to followers after
leader entry fill.

**Priority note:** P1 defect — followers have naked positions with no stop protection
after each leader entry fill. Must be addressed before live trading with ATM mode.

**Action:** Requires separate block (B168 or DW-B168). Director must run the second
output.md test procedure per DW-B167-02/00-dw-card.md before architect can design fix.

---

### B167-DEFER-03 — B161-B166 Combined SIM Gate Pending

| Field | Value |
|-------|-------|
| ID | B167-DEFER-03 |
| Priority | P1 |
| Target Block | next SIM session after B167 deployment |
| Status | OPEN |
| Reference | docs/brain/no-pipeline-repairs.md |

**Description:** All 7 direct repairs (B161, B162-A, B162-B, B163, B164, B165, B166)
have formal pipeline records complete. B166 SIM gate passed individually on 2026-09-15.
However, the combined SIM gate for the full B161-B166 block (all 7 signals working
together in one session) has not been run.

**Action:** Run combined B161-B166 SIM gate after B167 SIM gate passes. B167 SIM gate
is a prerequisite because B167 (pre-fill drag fix) is a dependency for observing clean
follower entry behavior during B161-B166 ghost-entry tests.

---

### B167-DEFER-04 — B167 SIM Gate Pending

| Field | Value |
|-------|-------|
| ID | B167-DEFER-04 |
| Priority | P1 |
| Target Block | first SIM session after B167 deployment |
| Status | OPEN |
| SIM criteria | docs/brain/B167/05-final-review.md §F |

**Description:** Full 12-step SIM gate for B167 (Ticket 1 pre-fill drag + post-flatten
abort + Ticket 2 SLATE-READY-DRAIN) not yet run. All production code changes have
passed Phase 4b verification (VERIFY_PASS). Unit tests pass. SIM gate is the final
confirmation.

**Action:** Hot-compile B167 changes in NinjaTrader 8. Run 12-step SIM session. Log
results in a SIM gate completion note. B167-DEFER-03 (combined B161-B166 gate) runs
after this gate passes.

---

## Prior OPEN Items Not Carried Forward

| ID | Reason |
|----|--------|
| DW-B159-K1 | SIM Gate Re-run subsumed by B167-DEFER-03 (combined B161-B166 SIM gate). Not re-listed separately. |

---

*Deferred backlog produced by ptt-plan-reviewer. Phase 5. Block DW-B159-KS. Date 2026-10-01.*
*Closed this block: DW-B159-K2, DW-B159-K3, DW-B159-K5.*
*New deferred: DW-B159-K3-UI.*
*B167-DEFER-01 through B167-DEFER-04 carried forward verbatim from docs/brain/B167/06-deferred-backlog.md.*
