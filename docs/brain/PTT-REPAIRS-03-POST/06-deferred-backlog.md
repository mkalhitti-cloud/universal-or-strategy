# PTT-COPIER Deferred Work Backlog — PTT-REPAIRS-03-POST Block

This file is the continuation of the PTT-COPIER deferred backlog for block PTT-REPAIRS-03-POST
(BUG-C gate5 instrKey false-block + BUG-D empty-name Limit gate0.5 block). It carries all open
items from `docs/brain/PTT-REPAIRS-03/06-deferred-backlog.md` forward, marks items closed if
addressed this block, and lists new open items.

**Prior backlog source:** `docs/brain/PTT-REPAIRS-03/06-deferred-backlog.md`

---

## Block PTT-REPAIRS-01 (G1, R1-R6 targeted repairs)

**Block Summary:** Six targeted correctness repairs (G1, R1-R6). See prior backlog for details.

**Final Review:** FINAL_PASS (2026-09-07)

---

## Block PTT-REPAIRS-02 (alternating-dispatch defect fix)

**Block Summary:** Single targeted correctness fix — `EvictDedup` Filled branch instrKey clear.

**Final Review:** FINAL_PASS (2026-09-07)

---

## Block PTT-REPAIRS-03 (reversal guard false-positive + phantom instrKey fix)

**Block Summary:** Two independent targeted correctness fixes — BUG-A (`ShouldSkipForReversalGuard`) and BUG-B (`IsLiveEntryBlocked` split into `IsLiveEntryBlocked_Check` + `SetLiveEntryDispatched`).

**Final Review:** FINAL_PASS (2026-09-08)

---

## Block PTT-REPAIRS-03-POST (BUG-C gate5 false-block + BUG-D gate0.5 over-block)

**Block Summary:** Two production-critical correctness fixes surfaced in live testing post PTT-REPAIRS-03 merge:

- **BUG-C fix** (`IsLiveEntryBlocked_Check` / `SetLiveEntryDispatched` / `EvictDedup`):
  `_liveEntryInstruments` type changed from `ConcurrentDictionary<string, byte>` to
  `ConcurrentDictionary<string, string>`. Gate5 predicate updated from `ContainsKey(instrKey)` to
  `TryGetValue(instrKey, ...) && liveOrderId == orderId`. `SetLiveEntryDispatched` changed from
  `TryAdd` to indexer overwrite. `EvictDedup` both terminal branches updated to value-guarded
  `TryRemove`. Resolves indefinite false-block of replacement orders on same instrument+direction
  when NT8 delivers `Cancelled` late.

- **BUG-D fix** (`IsExitSignalName` / `IsExitSignalNameOrAnonClose` / `DispatchCopy` gate0.5):
  Removed `name.Length == 0 → return true` branch from `IsExitSignalName` (was DW-LB-FL-01 V6
  over-broadening). New helper `IsExitSignalNameOrAnonClose(string name, OrderType orderType)` added
  (CYC=3). `DispatchCopy` gate0.5 updated to call `IsExitSignalNameOrAnonClose` instead of
  `IsExitSignalName`. Restores `T_B59_07` contract. Resolves blocking of valid empty-name Limit
  entry orders.

- **Tests added:**
  - `IsLiveEntryBlocked_DifferentOrderId_SameInstrKey_NotBlocked` (BUG-C regression, `CopyEngineTests.cs` line 7932)
  - `T_B59_AnonClose_01` through `T_B59_AnonClose_06` (BUG-D coverage, lines 3141-3182)
  - `T_B59_07` contract restored (no edit required, `Assert.False(IsExitSignalName(""))` now passes)

- **[Fact] count:** 477 → 478 (+1, TICKET-1 only; TICKET-2 source had no drift, all tests pre-existing)

**T1:** BUILD_PASS (Layer 2) + VERIFY_PASS (Layer 3). Zero discrepancies.
**T2:** BUILD_PASS (Layer 2) + VERIFY_PASS (Layer 3). Zero discrepancies.
**Final Review:** PIPELINE_COMPLETE (2026-09-06)

### New Items This Block

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-REPAIRS-03-POST-01 | **TOCTOU window in value-guarded `TryRemove`** (`EvictDedup` Cancelled and Filled branches). `TryGetValue` read and `TryRemove` write on `_liveEntryInstruments` are not atomic. Theoretical: if a second thread calls `SetLiveEntryDispatched` for the same instrKey after the equality check passes but before `TryRemove` executes, `TryRemove` could wipe the newer order's guard. Non-exploitable under NT8's single-threaded `OnOrderUpdate` callback model. Mitigation: `ClearLiveEntryForInstrument` on position flat provides a secondary safety net. Revisit if multi-threaded cancel storms are observed in future stress testing. See architecture plan Section 2e for full assessment. | P3 | B28+ / DW-B24-02 E2E | OPEN |
| DW-REPAIRS-03-POST-02 | **Missing BUG-C regression test** (`IsLiveEntryBlocked_DifferentOrderId_SameInstrKey_NotBlocked`). Required shim and full test specification in architecture plan Section 2g. Was listed as OPEN (P1) in architecture plan and prior draft. | P1 | PTT-REPAIRS-03-POST TICKET-1 | **RESOLVED — added in TICKET-1 (Ph4a). Confirmed at CopyEngineTests.cs line 7932 by independent Ph4b verification.** |

---

## B24 Items Carried Forward (Unchanged)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B24-01 | **NT8-043 formal rule entry**: null-conditional event unsubscription runtime crash under NT8. SCAN-06 = N/A across all blocks through PTT-REPAIRS-03-POST (no event handlers changed). Rule watch continues; zero new evidence. | P2 | B27 or future | OPEN |
| DW-B24-02 | **Manual E2E runtime verification (escalated, scope expanded)**: Verify PTT-REPAIRS-01 fixes (R1-R6), PTT-REPAIRS-02 alternating-dispatch fix, PTT-REPAIRS-03 fixes (BUG-A, BUG-B), **and PTT-REPAIRS-03-POST fixes**: (a) different-orderId on same instrKey passes gate5 when prior instrKey present (BUG-C); (b) empty-name Limit entry orders are dispatched, not blocked at gate0.5 (BUG-D). Live log from PTT-REPAIRS-03-POST provides partial confirmation for BUG-C and BUG-D but does not substitute for the full E2E session. Must execute before production release. | P1 | ASAP post-merge | OPEN |
| DW-B24-03 | **Skip-duplicate guard `[Fact]`**: formal xUnit test for `if (acc == leader) continue` guard at `CopyEngine.cs:~1195`. | P2 | B27 | OPEN |

---

## B25 Items Carried Forward

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B25-01 | **Companion field race**: `_pendingBeAccount`, `_pendingBeInstrument`, `_trailBeAccount`, `_trailBeInstrument` remain plain singleton refs. Multi-panel topology race risk. | P3 | B28 or future | OPEN |

---

## B26 Items Carried Forward

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B26-01 | **Reflection test upgrade (Option B → Option A)**: `TradeCopierPanel_BeBufferBox_FieldDoesNotExist`. Requires WPF test host. | P2 | B28 or future | OPEN |

---

## PTT-REPAIRS-01 Items Carried Forward

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-REPAIRS-01-01 | **R5 `Account.All` constructor-path risk**: `BuildRuleRow` called before `Loaded` fires; null guard prevents NPE but account selectors may be empty on rule restore. | P2 | B28 or future | OPEN |
| DW-REPAIRS-01-02 | **`TryCancelBeOrders` wrapper `-1`-path `[Fact]` test**: requires mock `Account` that throws during `Cancel()`. | P2 | B28 or future | OPEN |

---

## PTT-REPAIRS-02 Items Carried Forward

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-REPAIRS-02-01 | **`_entryDispatchedOrders` NOT cleared in Filled branch of `EvictDedup`**: orderId lingers after fill unless `TryEvictFollowerBeSlot` fires (comment at line 5843). If rapid re-entry with the SAME orderId occurs, `IsLiveEntryBlocked_Check` gate5(c) would block it. Recommend: verify orderId release in DW-B24-02 E2E session. | P2 | B28+ / DW-B24-02 E2E | OPEN |

---

## Items Closed This Block (PTT-REPAIRS-03-POST)

| ID | Item | Closed In | Notes |
|----|------|-----------|-------|
| DW-REPAIRS-03-POST-02 | Missing BUG-C regression test `IsLiveEntryBlocked_DifferentOrderId_SameInstrKey_NotBlocked` | PTT-REPAIRS-03-POST TICKET-1 | Test added at `CopyEngineTests.cs` line 7932. `SetLiveEntryDispatched_ForTest` shim not required: test uses `IsLiveEntryBlocked_ForTest` (which calls `SetLiveEntryDispatched` internally) and `ClearLiveEntryForInstrument_ForTest`. All assertions confirmed by independent Ph4b verification. CLOSED. |

*(No items from B24-B26, PTT-REPAIRS-01, PTT-REPAIRS-02-01, or DW-B24-02 were addressed in PTT-REPAIRS-03-POST. All remain open as documented above.)*

---

*ptt-plan-reviewer · PTT-REPAIRS-03-POST · 06-deferred-backlog.md · 2026-09-06*
