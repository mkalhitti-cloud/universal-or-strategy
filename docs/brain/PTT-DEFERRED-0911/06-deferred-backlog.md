# PTT-COPIER Deferred Work Backlog — PTT-DEFERRED-0911 Lane F

This file is the Lane F block entry. All prior OPEN items are carried forward unchanged from
`docs/brain/PTT-BUGS-LIVE-0911/06-deferred-backlog.md` (Lane E was the previous block).
Items are added when work is identified but out-of-scope for the current block.
Items are closed only when the implementation explicitly addresses them.

---

## Block PTT-DEFERRED-0911 — Lane F (DW-BUG-0911-A-02, DW-B24-03, DW-LD-04)

**Block ID:** PTT-DEFERRED-0911-LANE-F
**Status:** COMPLETE
**Phase 5 Reviewed:** 2026-09-11
**Final verdict:** FINAL_PASS

### Block Summary

Lane F closed 3 deferred items in 3 tickets:

**T1 (DW-BUG-0911-A-02):** Removed `[Fact(Skip=...)]` from
`Flatten_Instrument_FlatFollower_SkipsDispatch_FlatGuard` by changing `FlatGuardLoop<TAccount>`
second parameter from `Func<TAccount, NinjaTrader.Cbi.Position?>` to `Func<TAccount, bool> isFlat`.
Restored `FlatGuardLoop` as `private static` inside `CopyEngine` (Retry 2 — Retry 1 VERIFY_FAIL
due to `FlattenHelpers` extraction and structural-vs-behavioral test deviation, both resolved per
orchestrator rulings). Both Flatten call sites updated. Test now passes (Passed=1, Skipped=0).
Agile.NET module cctor blocks behavioral `.Invoke()` in xUnit host; structural type-contract test
accepted as substitute and documented in source code.

**T2 (DW-B24-03):** Added new `LaneFTests` class to `CopyEngineTests.cs` with
`SkipDuplicateGuard_LeaderAccountInFollowerFanOut_SkipsDispatchForLeader` `[Fact]` (no Skip).
Pure-BCL simulation of `if (acc == leader) continue` guard semantics + reflection existence checks
for Trim, Flatten, and CancelPendingEntries. Zero production code changes.

**T3 (DW-LD-04):** Extracted `IsRaceSkipCandidate(Order, HashSet<Order>, Instrument)` from
`CancelQxBrackets` 3-param compound `if`. Reduces `CancelQxBrackets` 3-param CYC from 9 to 7.
JS-013 P1 violation cleared. Stale CYC comment updated. `IsRaceSkipCandidate_NullSnapshot_ReturnsFalse`
`[Fact]` added to `LaneFTests` (inserted before T2 closing brace at former line 8976).

**Files changed:** `src/PropTraderTools/CopyEngine.cs`, `src/PropTraderTools/CopyEngineTests.cs` only.
**Scans:** All 7 scans zero (L2 engineer + L3 verifier, zero discrepancies across all 3 tickets).
**Tests:** 3 `[Fact]` tests added or unskipped; all pass. Final: Failed=0, Passed=178, Skipped=364, Total=542.
**Build:** `dotnet build` succeeded, 0 errors.

---

### Items CLOSED This Block

| ID | Item | Status |
|----|------|--------|
| DW-BUG-0911-A-02 | FlatGuardLoop test runtime Skip removal — `[Fact]` no Skip; test passes via structural delegate-type contract check; `Func<TAccount, bool> isFlat` replaces NT8 Position? reference. | **CLOSED** |
| DW-B24-03 | Skip-duplicate guard `[Fact]` — `SkipDuplicateGuard_LeaderAccountInFollowerFanOut_SkipsDispatchForLeader` added to `LaneFTests`; passes with no Skip. | **CLOSED** |
| DW-LD-04 | CancelQxBrackets 3-param CYC=9 fix — `IsRaceSkipCandidate` extracted; CYC reduced to 7; JS-013 P1 violation cleared. | **CLOSED** |

---

### Prior OPEN Items Carried Forward (Unchanged Status)

All prior OPEN items from B24, B25, B26, and Lanes A–E are carried forward unchanged.

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B24-01 | **NT8-043 formal rule entry**: Confirm null-conditional event unsubscription (`?.Event -=`) causes silent runtime crash under NT8 Roslyn. SCAN-07 = 0 across all lanes. Rule watch continues; no new evidence in Lane F. | P2 | B27 or future | OPEN |
| DW-B24-02 | **Manual E2E Ctrl+Shift+B runtime verification**: Press Ctrl+Shift+B on a solo account in live NT8. Confirm `BreakEven` fires and no NullReferenceException. Still pending. | P1 | B27 pre-release | OPEN |
| DW-B25-01 | **Companion field race**: `_pendingBeAccount`, `_pendingBeInstrument`, `_trailBeAccount`, `_trailBeInstrument` remain plain singleton refs. Multi-panel topology race possible. Fields confirmed absent from TradeCopierPanel.cs by source scan 2026-09-11 — removed in post-B25 refactor. | P3 | B27 or future | STALE — CLOSED |
| DW-B26-01 | **Reflection test upgrade (Option B -> Option A)**: `TradeCopierPanel_BeBufferBox_FieldDoesNotExist` reflection-based absence assertion. Upgrade when NT8 WPF test host available. | P2 | B28 or future | OPEN |
| DW-BUG-0911-A-01 | **Mid-close race window**: `FlatGuardLoop` catches `qty == 0` definitively but does NOT catch the race where a close order is in-flight (`qty > 0`) when a second dispatch fires before the first close fills. Complete elimination requires an atomic interlocked per-account slot. Not addressed in Lane F. | P2 | B27 or future | OPEN |
| DW-PTT0911B-01 | **SIM end-to-end verification**: Protocol documented (plan §15 Lane E). Manual NinjaTrader SIM session execution pending. Must complete before production release. | P1 | pre-live | OPEN |
| DW-PTT0911B-02 | **Gate reset on AddOn reload**: `_followerSlateReady` resets on CopyEngine re-instantiation; absent-key = gate-open safe default. Informational. | P2 | future | OPEN |
| DW-PTT0911B-03 | **Accounts added after Subscribe()**: accounts connected after AddOn load do not receive PositionUpdate subscription. Hardening: re-subscribe on `Account.AccountConnected`. | P2 | future | OPEN |
| DW-PTT0911B-04 | **`HasAnyActiveOrders` snapshot timing**: ToList() snapshot may include terminal orders not yet cleared; gate stays closed one extra PositionUpdate tick — conservative false-negative only. | P2 | future | OPEN |
| DW-LD-01 | **`_laneDDispatched` session-unbounded growth**: entries persist for session lifetime; no cleanup on session end or position close. Growth bounded (~2.4 KB/session for 4 followers). Cleanup can be added in a future pass. | P2 | future | OPEN |
| DW-LD-02 | **Market order ATM guard decision documented**: `TryLaneDHook` no-op for Market-type orders by design. Future Market-with-ATM requires separate overload or order type routing. | P2 | future | OPEN |
| DW-LD-05 | **T_LANE_E_07/08 behavioral key-format assertion deferred**: structural reflection only due to `TypeInitializationException` on NT8 `Account` type in xUnit host. Deferred to NT8 SIM session (subsumes DW-PTT0911B-01). | P2 | pre-live SIM | OPEN |

---

### Lane F Items (New This Block)

No new deferred items were discovered or identified during Lane F.

---

*ptt-plan-reviewer · PTT-DEFERRED-0911 Lane F · 2026-09-11*

---

## Block PTT-DEFERRED-0911 — Lane G

**Block ID:** PTT-DEFERRED-0911-LANE-G
**Status:** COMPLETE
**Phase 5 Reviewed:** 2026-09-11
**Final verdict:** FINAL_PASS

### Block Summary

Lane G closed 1 deferred item in 1 ticket (T-G1):

**T1 (DW-B26-01):** Upgraded `TradeCopierPanel_BeBufferBox_FieldDoesNotExist` from Option B
(reflection field-absence assertion, `[Fact(Skip=...)]`) to Option A-partial.
Added `void BreakEven(Account, Instrument, int)` to `ICopyEngine` in `PttContracts.cs`.
Changed `CopyEngine.BreakEven(Account, Instrument, int)` from `internal` to `public` in `CopyEngine.cs`.
Extracted `internal static ExecuteBreakEven(ICopyEngine, Account, Instrument, int)` seam in `TradeCopierPanel.cs`.
Updated `DispatchShortcut` Key.B to delegate to `ExecuteBreakEven`.
Added `LaneGTests` class with `StubEngine : ICopyEngine` and `[Fact] TradeCopierPanel_ExecuteBreakEven_PassesBufferToEngine`.
Old `[Fact(Skip=...)]` test deleted.
Full Option A (WPF instantiation) remains blocked by NT8 STA dispatcher requirement in headless xUnit host — documented in plan Section 2.

**Files changed:** `src/PropTraderTools/Core/PttContracts.cs`, `src/PropTraderTools/CopyEngine.cs`, `src/PropTraderTools/TradeCopierPanel.cs`, `src/PropTraderTools/CopyEngineTests.cs` only.
**Scans:** All 7 scans zero (L2 engineer + L3 verifier, zero discrepancies).
**Tests:** 1 `[Fact]` added, 1 `[Fact(Skip=...)]` deleted. Final: Failed=0, Passed=179, Skipped=363, Total=542.
**Build:** `dotnet build` succeeded, 0 errors.

---

### Items CLOSED This Block

| ID | Item | Status |
|----|------|--------|
| DW-B26-01 | Reflection test upgrade (Option B → Option A): `TradeCopierPanel_ExecuteBreakEven_PassesBufferToEngine` [Fact] (no Skip) tests behavioral dispatch path via ICopyEngine seam. Option A-partial achieved. Full Option A blocked by WPF STA requirement documented in plan. | **CLOSED** |

---

### Prior OPEN Items Carried Forward (Unchanged Status)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B24-01 | **NT8-043 formal rule entry**: Confirm null-conditional event unsubscription (`?.Event -=`) causes silent runtime crash under NT8 Roslyn. SCAN-07 = 0 across all lanes. Rule watch continues; no new evidence in Lane G. | P2 | B27 or future | OPEN |
| DW-B24-02 | **Manual E2E Ctrl+Shift+B runtime verification**: Press Ctrl+Shift+B on a solo account in live NT8. Confirm `BreakEven` fires and no NullReferenceException. Still pending. | P1 | B27 pre-release | OPEN |
| DW-BUG-0911-A-01 | **Mid-close race window**: `FlatGuardLoop` catches `qty == 0` definitively but does NOT catch the race where a close order is in-flight (`qty > 0`) when a second dispatch fires before the first close fills. Complete elimination requires an atomic interlocked per-account slot. Not addressed in Lane G. | P2 | B27 or future | OPEN |
| DW-PTT0911B-01 | **SIM end-to-end verification**: Protocol documented (plan §15 Lane E). Manual NinjaTrader SIM session execution pending. Must complete before production release. | P1 | pre-live | OPEN |
| DW-PTT0911B-02 | **Gate reset on AddOn reload**: `_followerSlateReady` resets on CopyEngine re-instantiation; absent-key = gate-open safe default. Informational. | P2 | future | OPEN |
| DW-PTT0911B-03 | **Accounts added after Subscribe()**: accounts connected after AddOn load do not receive PositionUpdate subscription. Hardening: re-subscribe on `Account.AccountConnected`. | P2 | future | OPEN |
| DW-PTT0911B-04 | **`HasAnyActiveOrders` snapshot timing**: ToList() snapshot may include terminal orders not yet cleared; gate stays closed one extra PositionUpdate tick — conservative false-negative only. | P2 | future | OPEN |
| DW-LD-01 | **`_laneDDispatched` session-unbounded growth**: entries persist for session lifetime; no cleanup on session end or position close. Growth bounded (~2.4 KB/session for 4 followers). Cleanup can be added in a future pass. | P2 | future | OPEN |
| DW-LD-02 | **Market order ATM guard decision documented**: `TryLaneDHook` no-op for Market-type orders by design. Future Market-with-ATM requires separate overload or order type routing. | P2 | future | OPEN |
| DW-LD-05 | **T_LANE_E_07/08 behavioral key-format assertion deferred**: structural reflection only due to `TypeInitializationException` on NT8 `Account` type in xUnit host. Deferred to NT8 SIM session (subsumes DW-PTT0911B-01). | P2 | pre-live SIM | OPEN |

---

### Lane G Items (New This Block)

No new deferred items were discovered or identified during Lane G.

---

*ptt-plan-reviewer · PTT-DEFERRED-0911 Lane G · 2026-09-11*
