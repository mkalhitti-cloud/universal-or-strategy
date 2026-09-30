# DW-B159 Deferred Backlog
<!-- block: DW-B159 -->
<!-- wave: B159 -->
<!-- date: 2026-09-15 -->
<!-- produced-by: ptt-plan-reviewer (Phase 5) -->

---

## Block Header

| Field | Value |
|-------|-------|
| Epic | DW-B159 — Ghost Re-entry Guard + BE-All Silent Rollback Repair |
| Wave | B159 |
| Phase 5 verdict | FINAL_PASS |
| Date | 2026-09-15 |
| Entries | 5 (K1–K5) |

---

## Deferred Items

---

### DW-B159-K1 — SIM Gate Re-run

| Field | Value |
|-------|-------|
| ID | DW-B159-K1 |
| Priority | P0 |
| Target Block | B160 |
| Status | OPEN |

**Description:**  
The SIM gate (DW-B158-SIM-01) must be re-run against the final B159 source to confirm both feature modules behave correctly in a live NinjaTrader SIM environment.

**Pass criteria:**

1. FM-A: Open positions on Sim102 and Sim103 before a new leader entry.
   - Expected: `[PTT-COPY-GUARD] ghost-entry blocked: Sim102 <InstrFullName>` and `[PTT-COPY-GUARD] ghost-entry blocked: Sim103 <InstrFullName>` appear in NT8 Output Tab 1.
   - Verify: No PTT copy order is submitted to Sim102/Sim103 for that entry cycle.

2. FM-B: Execute a QX→BE-All cycle on a follower (Sim104), then trigger an ATM bulk cancel of the resulting PTT-BE-Stop.
   - Expected: `[PTT-BE-RETRY] fired: Sim104 bePrice=<price>` appears in NT8 Output Tab 1.
   - Verify: A replacement PTT-BE-Stop is submitted at the correct price.

**Next action:** Schedule SIM session for B160 gate. Engineer reads `ticket-2-completion.md` VERIFY Criterion section for grep commands.

---

### DW-B159-K2 — Unit Tests Not Implemented

| Field | Value |
|-------|-------|
| ID | DW-B159-K2 |
| Priority | P1 |
| Target Block | B160 or dedicated test sprint |
| Status | OPEN |

**Description:**  
14 xUnit `[Fact]` tests are specified across T1 (6 tests) and T2 (8 tests) but were not implemented in the B159 pipeline. The NinjaTrader AddOn testing infrastructure (`src/PropTraderTools/CopyEngineTests.cs`) requires a dedicated test project setup with NT8 type mocking before these tests can compile and run.

**Test method names (T1 — 6 tests):**
1. `ShouldSkipFollower_FollowerHasOpenPosition_ReturnsTrue`
2. `ShouldSkipFollower_FollowerNoOpenPosition_ReturnsFalse`
3. `IsFollowerPositionBlocked_NullAccount_ReturnsFalse`
4. `IsFollowerPositionBlocked_NullInstrument_ReturnsFalse`
5. `IsFollowerPositionBlocked_AccountWithNoPosition_ReturnsFalse`
6. `IsFollowerPositionBlocked_AccountWithOpenPosition_ReturnsTrue`

**Test method names (T2 — 8 tests):**
1. `TryFireBePriceRetry_CancelledOrderWithRetryKey_HasOpenPosition_StillAtNonBePrice_CallsSubmitBeStop`
2. `TryFireBePriceRetry_CancelledOrderWithRetryKey_AlreadyAtBePrice_NoSubmitBeStop`
3. `TryArmBePriceRetry_AttemptsAtCap_NoNewEntry`
4. `TryFireBePriceRetry_WorkingOrder_RemovesFromDict`
5. `TryFireBePriceRetry_NonPttBeStopPrefix_ExitsEarly`
6. `TryArmBePriceRetry_ValidInputsFirstAttempt_AddsEntryToDict`
7. `TryFireBePriceRetry_CancelledPttBeStop_KeyNotInDict_NoSubmitBeStop`
8. `StillAtNonBePrice_WorkingStopAtOriginalPrice_ReturnsTrue`

**Next action:** Set up xUnit test project with NT8 type stubs. All method signatures are `internal` and accessible via `InternalsVisibleTo`. No design work required — method specs are fully documented in `04-tickets.md`.

---

### DW-B159-K3 — Attempt Cap Hardcoded at 3

| Field | Value |
|-------|-------|
| ID | DW-B159-K3 |
| Priority | P2 |
| Target Block | future |
| Status | OPEN |

**Description:**  
`TryArmBePriceRetry` enforces a maximum of 3 retry attempts per account per trade cycle via the literal constant `3` at line 6997. This value is empirically adequate for the current NT8 ATM cancel recovery scenario but is not tunable at runtime.

**Proposed change:** Extract to a named field `_beRetryMaxAttempts` initialized from engine settings (consistent with existing `_maxReArmAttempts` pattern in `CopyEngine.cs`). Allow Director to configure via `PropTraderTools` settings panel.

**Risk if deferred:** None — the current cap of 3 is conservative and prevents any retry storm. The deferred item is a usability improvement, not a correctness fix.

**Next action:** Evaluate as part of a future settings/configuration epic. No urgency.

---

### DW-B159-K4 — `intendedBePrice` Sourcing Dependency

| Field | Value |
|-------|-------|
| ID | DW-B159-K4 |
| Priority | P2 |
| Target Block | future |
| Status | OPEN |

**Description:**  
`TryArmBePriceRetry` at line 6899 receives `newStop` from `MoveStopToBreakEven`. The value `newStop` is the computed BE stop price passed to `PttBreakEvenSwap.Execute` at line 6895. This is correct for the current BE formula (entry price = BE level).

**Dependency to track:** If the BE level formula changes in a future epic (e.g., BE = entry + N ticks buffer, or a different price basis), `newStop` will automatically reflect the new computation — no code change required in `TryArmBePriceRetry`. However, the retry will then fire at the new BE level, which may differ from the original entry price. The formulas must remain consistent.

**Next action:** When any future epic modifies `newStop` computation in `MoveStopToBreakEven`, confirm that `TryArmBePriceRetry` hook at line 6899 still receives the intended retry price. Flag this item for the architect in that epic.

---

### DW-B159-K5 — Stale Line Number in Comment

| Field | Value |
|-------|-------|
| ID | DW-B159-K5 |
| Priority | P3 |
| Target Block | future |
| Status | OPEN |

**Description:**  
The `IsFollowerPositionBlocked` header comment at source line 3070 states:

```
// Delegates to HasOpenPosition (line 5273) -- no duplicate logic.
```

`HasOpenPosition` is actually declared at line 5294 (noted by the T1 verifier as a non-violation doc discrepancy). The functional behavior is correct — the call at line 3078 resolves correctly. This is a documentation-only issue.

**Next action:** Correct the comment in a future doc-cleanup pass or during any future modification to `IsFollowerPositionBlocked`. No functional impact, no urgency.

---

## Closed Items

No prior OPEN items from preceding blocks apply to DW-B159. This is the first deferred backlog entry for this epic.

---

*Deferred backlog produced by ptt-plan-reviewer. Phase 5. Wave B159. Date 2026-09-15.*
*All 5 items are non-blocking. FINAL_PASS is confirmed for DW-B159.*
