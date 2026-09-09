# PTT-COPIER Deferred Work Backlog — PTT-REPAIRS-03 Block

This file is the continuation of the PTT-COPIER deferred backlog for block PTT-REPAIRS-03
(reversal guard false-positive fix + phantom instrKey fix in `CopyEngine.cs`). It carries all
open items from `docs/brain/PTT-REPAIRS-02/06-deferred-backlog.md` forward, marks items closed
if addressed this block, and lists no new open items.

**Prior backlog source**: `docs/brain/PTT-REPAIRS-02/06-deferred-backlog.md`

---

## Block PTT-REPAIRS-01 (G1, R1-R6 targeted repairs)

**Block Summary**: Six targeted correctness repairs:
- G1: `.gitleaks.toml` at repo root suppresses false positive for `ConcurrentDictionary<Chart, KeyEventHandler>` in `TradeCopierAddOn.cs`.
- R1: `IsNakedConditionMet` / `IsEntryCandidateOrder` / `SnapshotTargetsPublic` — reference equality replaced by FullName comparison.
- R2: `TryDrainWatchdog` — `PendingCancelCount <= 0` guard added; `ReissueDrainCancels` helper added.
- R3: `LoadAndValidateLicense` — `dev_mode.txt` Elite bypass removed.
- R4: `TradeCopierWindow.cs` — `_modeCb.IsEnabled` fix + Elite gate in `OnCopyModeComboChanged`.
- R5: `TradeCopierWindow.cs` — `Account.All` bound in `BuildRuleRow` with null guard.
- R6: `PttGlobalQuickExit.cs` — `CancelPttBeOrders` try/catch + `TryCancelBeOrders` wrapper + `ProcessForcedTargetPosition` extraction.
- Tests: 6 new `[Fact]` methods (T_R1-T_R6).

**Final Review**: FINAL_PASS (2026-09-07)

---

## Block PTT-REPAIRS-02 (alternating-dispatch defect fix)

**Block Summary**: Single targeted correctness fix:
- T1: `EvictDedup` in `CopyEngine.cs` — `_liveEntryInstruments[instrKey]` now cleared on `OrderState.Filled` (mirrors Cancelled branch). Resolves alternating-dispatch defect.
- Test: 1 new `[Fact]` method `IsLiveEntryBlocked_ClearsOnFill_AllowsReentry`. [Fact] count: 474 → 475 (+1).

**Final Review**: FINAL_PASS (2026-09-07)

---

## Block PTT-REPAIRS-03 (reversal guard false-positive + phantom instrKey fix)

**Block Summary**: Two independent targeted correctness fixes:

- **T1 — BUG-A fix** (`ShouldSkipForReversalGuard`): `followerIsFlat` definition updated to include `&& !HasWorkingEntries(acc, instr)`. Follower with a working entry order (but no filled position) is no longer treated as flat by the reversal guard. Guard correctly allows the new-direction copy to proceed. DW-REPAIRS-03-01 promoted in-scope: `HasWorkingEntries` updated to `acc.Orders.ToList()` snapshot (JS-001). `ShouldSkipForReversalGuard` CYC: 3 → 4. `HasWorkingEntries` CYC: 5 (unchanged; stale comment corrected).

- **T2 — BUG-B fix** (`DispatchCopy` / `IsLiveEntryBlocked` split): `IsLiveEntryBlocked` deleted and replaced by two methods: `IsLiveEntryBlocked_Check` (pure predicate, CYC=4) and `SetLiveEntryDispatched` (commit, CYC=1). `DispatchCopy` adds `int dispatched` counter; `SetLiveEntryDispatched` called only when `dispatched > 0` (post-loop). `ShouldSkipFollower` helper extracted to reduce `DispatchCopy` CYC 8→7 before +1 guard = 8 final. `IsEntryDispatched` simplified (TryAdd side-effect removed, CYC 2→1). `IsLiveEntryBlocked_ForTest` shim redirected. Stale comment at line 201 updated.

- Tests: 2 new `[Fact]` methods. [Fact] count: 475 → 477 (+2).
  - `ShouldSkipForReversalGuard_AllowsEntryWhenFollowerHasWorkingOrders` (T1, line 7787)
  - `DispatchCopy_DoesNotSetPhantomInstrKey_WhenAllFollowersSkipped` (T2, line 7844)
  - `IsLiveEntryBlocked_ClearsOnFill_AllowsReentry` (PTT-REPAIRS-02, line 7809): preserved unchanged via shim redirect.

- MGC guard DW-B142-MGC-02: fully preserved across all three gate levels.
- PTT-DIAG logs (5 permanent): all confirmed present and unmodified.

**Plan Review**: REVIEW_PASS (Cycle 2 — Revision 2 V-02)
**Ticket Review**: TICKET_REVIEW_PASS (Cycle 1 — T2-TRACE-01, T2-TRACE-02, T2-TEST-01 all resolved)
**T1**: BUILD_PASS (Layer 2) + VERIFY_PASS (Layer 3). Layer 2/Layer 3 full agreement.
**T2**: BUILD_PASS (Layer 2) + VERIFY_PASS (Layer 3). Layer 2/Layer 3 full agreement.
**Final Review**: FINAL_PASS (2026-09-08)

---

## B24 Items Carried Forward (Unchanged)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B24-01 | **NT8-043 formal rule entry**: null-conditional event unsubscription runtime crash under NT8. SCAN-06 = N/A across all blocks through PTT-REPAIRS-03 (no event handlers changed). Rule watch continues; zero new evidence. | P2 | B27 or future | OPEN |
| DW-B24-02 | **Manual E2E runtime verification (escalated, scope expanded)**: Verify PTT-REPAIRS-01 fixes (R1–R6), PTT-REPAIRS-02 alternating-dispatch fix, **and PTT-REPAIRS-03 fixes**: (a) Buy dispatch allowed when follower has working Sell entry order (BUG-A); (b) rapid re-entry after reversal not blocked at gate5 (BUG-B). Must execute before production release. | P1 | ASAP post-merge | OPEN |
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

## Items Closed This Block (PTT-REPAIRS-03)

| ID | Item | Closed In | Notes |
|----|------|-----------|-------|
| DW-REPAIRS-02-02 | `[Fact]` count baseline inconsistency: re-measure at next block start | PTT-REPAIRS-03 | Architecture plan Section 0 measured 475 authoritative baseline. DW-B142 count discrepancy from PTT-REPAIRS-01 remains historically unexplained, but the operational baseline is now resolved. CLOSED. |
| DW-REPAIRS-03-01 | `HasWorkingEntries` `acc.Orders.ToList()` thread-safety fix | PTT-REPAIRS-03-T1 | Promoted from deferred to in-scope. `.ToList()` confirmed at `CopyEngine.cs` line 4869. CLOSED. |

*(No items from the B24–B26, PTT-REPAIRS-01, PTT-REPAIRS-02-01 lists were addressed in PTT-REPAIRS-03. All remain open as documented above.)*

---

## PTT-REPAIRS-03 New Items

No new deferred items. All plan scope was fully implemented and verified. The non-blocking warn from 04-ticket-review.md (stale comments at `DispatchCopy` lines 2470–2471) was resolved by the engineer during T2 implementation (confirmed: lines 2470–2472 updated at source). No residual items.

---

*ptt-plan-reviewer · PTT-REPAIRS-03 · 06-deferred-backlog.md · 2026-09-08*
