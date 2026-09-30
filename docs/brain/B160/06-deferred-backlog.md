# DW-B160 Deferred Backlog
<!-- block: DW-B160 -->
<!-- wave: B160 -->
<!-- date: 2026-09-15 -->
<!-- produced-by: ptt-plan-reviewer (Phase 5) -->

---

## Block Header

| Field | Value |
|-------|-------|
| Epic | DW-B160 — FM-A Ghost Re-entry Guard Race Repair |
| Wave | B160 |
| Phase 5 verdict | FINAL_PASS |
| Date | 2026-09-15 |
| Entries carried in | 5 (K1–K5 from B159) |
| Entries closed this block | 1 (DW-B159-K5) |
| New entries this block | 0 |

---

## Closed items from prior epics (carried into B160)

### DW-B159-K5 — Stale Line Number in Comment

| Field | Value |
|-------|-------|
| ID | DW-B159-K5 |
| Priority | P3 |
| Target Block | B160 |
| Status | **CLOSED** |

**Resolution:** The stale `"line 5273"` / `"line 5294"` reference in the `IsFollowerPositionBlocked`
header comment was removed in B160. The B160 comment block was installed at L3121-3129 with no
stale line references. Verifier confirmed closure:

```
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "line 5273|line 5294|B159 DW-B159-FM-A"
```

Result: empty — zero matches.

**Closed by:** B160-T1 implementation (REQ-9, ticket §6.7). Confirmed by ptt-verifier
(ticket-1-verification.md §Stale Comment Removal).

---

### DW-B159-K1 — SIM Gate Re-run

| Field | Value |
|-------|-------|
| ID | DW-B159-K1 |
| Priority | P0 |
| Target Block | B161 / next SIM session |
| Status | **OPEN** |

**B160 status:** Implementation prerequisite met. B160 adds Signal 2 and Signal 3 to
`IsFollowerPositionBlocked`. The confirmed OCO-settle race window (output.md L456-507) is
now guarded by `HasSettlingBeTargets`. The SIM session itself has not been run.

**Updated pass criteria for B160:**
The original K1 SIM criteria remain valid with updated expected log strings:

1. **Race window test (Signal 3):** Execute QX→BE-All on all follower accounts with a
   3-contract position. Trigger rapid fill of PTT-BE-Stop-1/2/3. Fire a new leader entry
   during the OCO-cancel settle window.
   - Expected: `[PTT-COPY-GUARD] ghost-entry blocked (settling-be-targets): Sim102\nMES DEC26`
     (and Sim103, Sim104) in Output Tab 1.
   - Verify: Zero new Stop1/Target1 brackets appear on follower accounts.
   - Verify: `grep -c "PTT-COPY-GUARD" output.log` > 0 (was 0 in pre-B160 log).

2. **Normal guard test (Signal 1):** Open position on Sim102/103, fire new leader entry.
   - Expected: `[PTT-COPY-GUARD] ghost-entry blocked (has-position): Sim102\nMES DEC26`.

3. **FM-B retry test (unchanged from B159-K1):** Execute QX→BE-All on Sim104, trigger
   ATM bulk cancel of resulting PTT-BE-Stop.
   - Expected: `[PTT-BE-RETRY] fired: Sim104 bePrice=<price>` in Output Tab 1.

**Next action:** Schedule SIM session. Both B159-FM-A guard and B160-OCO-settle guard must
pass before K1 closes.

---

## Open items carried forward (unchanged from B159)

### DW-B159-K2 — Unit Tests Not Implemented

| Field | Value |
|-------|-------|
| ID | DW-B159-K2 |
| Priority | P1 |
| Target Block | dedicated test sprint |
| Status | OPEN |

**B160 addition:** B160 adds 6 new `[Fact]` test stubs (documented in 04-tickets.md §7).
Total deferred test count is now **20** (14 from B159 + 6 from B160).

**All 20 [Fact] names:**

B159 T1 (6 tests):
1. `ShouldSkipFollower_FollowerHasOpenPosition_ReturnsTrue`
2. `ShouldSkipFollower_FollowerNoOpenPosition_ReturnsFalse`
3. `IsFollowerPositionBlocked_NullAccount_ReturnsFalse`
4. `IsFollowerPositionBlocked_NullInstrument_ReturnsFalse`
5. `IsFollowerPositionBlocked_AccountWithNoPosition_ReturnsFalse`
6. `IsFollowerPositionBlocked_AccountWithOpenPosition_ReturnsTrue`

B159 T2 (8 tests):
1. `TryFireBePriceRetry_CancelledOrderWithRetryKey_HasOpenPosition_StillAtNonBePrice_CallsSubmitBeStop`
2. `TryFireBePriceRetry_CancelledOrderWithRetryKey_AlreadyAtBePrice_NoSubmitBeStop`
3. `TryArmBePriceRetry_AttemptsAtCap_NoNewEntry`
4. `TryFireBePriceRetry_WorkingOrder_RemovesFromDict`
5. `TryFireBePriceRetry_NonPttBeStopPrefix_ExitsEarly`
6. `TryArmBePriceRetry_ValidInputsFirstAttempt_AddsEntryToDict`
7. `TryFireBePriceRetry_CancelledPttBeStop_KeyNotInDict_NoSubmitBeStop`
8. `StillAtNonBePrice_WorkingStopAtOriginalPrice_ReturnsTrue`

B160 T1 (6 tests):
1. `HasActivePttBeExits_ReturnsTrue_WhenBeStopWorking`
2. `HasActivePttBeExits_ReturnsFalse_WhenBeStopFilled`
3. `HasSettlingBeTargets_ReturnsTrue_WhenTargetCancelPending`
4. `HasSettlingBeTargets_ReturnsFalse_WhenTargetCancelled`
5. `IsFollowerPositionBlocked_ReturnsTrue_WhenSettlingTargetsPresent`
6. `IsFollowerPositionBlocked_ReturnsFalse_WhenAllTerminal`

All methods are `internal`; accessible via `InternalsVisibleTo` (established in B159 at
CopyEngine.cs L46). No design work required — all specs are fully documented.

---

### DW-B159-K3 — Attempt Cap Hardcoded at 3

| Field | Value |
|-------|-------|
| ID | DW-B159-K3 |
| Priority | P2 |
| Target Block | future |
| Status | OPEN |

Unchanged. See B159/06-deferred-backlog.md for description. No B160 impact.

---

### DW-B159-K4 — `intendedBePrice` Sourcing Dependency

| Field | Value |
|-------|-------|
| ID | DW-B159-K4 |
| Priority | P2 |
| Target Block | future |
| Status | OPEN |

Unchanged. See B159/06-deferred-backlog.md for description. No B160 impact.

---

## New deferred items from B160

None.

The B160 implementation is minimal and self-contained. All planned work was completed
within scope. No new technical debt was introduced.

---

## Summary Table

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B159-K1 | SIM gate re-run (implementation prerequisite met by B160) | P0 | B161 / next SIM session | OPEN |
| DW-B159-K2 | 20 xUnit [Fact] stubs pending test infrastructure | P1 | dedicated test sprint | OPEN |
| DW-B159-K3 | Attempt cap hardcoded at 3 | P2 | future | OPEN |
| DW-B159-K4 | `intendedBePrice` sourcing dependency watch | P2 | future | OPEN |
| DW-B159-K5 | Stale line reference in IsFollowerPositionBlocked comment | P3 | B160 | **CLOSED** |

---

### B160 block complete: 2026-09-15

*Deferred backlog produced by ptt-plan-reviewer. Phase 5. Wave B160. Date 2026-09-15.*
*Prior backlog: docs/brain/B159/06-deferred-backlog.md.*
*All items non-blocking to FINAL_PASS except DW-B159-K1 (P0), which is unblocked by B160*
*implementation and pending SIM execution only.*
