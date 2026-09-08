# PTT-COPIER Deferred Work Backlog — PTT-REPAIRS-01 Block

This file is the continuation of the PTT-COPIER deferred backlog for block PTT-REPAIRS-01
(G1 + R1–R6 targeted repairs). It carries all open items from PTT-COPIER-B26/06-deferred-backlog.md
unchanged, adds new items from PTT-REPAIRS-01, and marks items closed if addressed this block.

**Prior backlog source**: `docs/brain/PTT-COPIER-B26/06-deferred-backlog.md`

---

## Block PTT-REPAIRS-01 (G1, R1-R6 targeted repairs)

**Block Summary**: Six targeted correctness repairs:
- G1: `.gitleaks.toml` at repo root suppresses false positive for `ConcurrentDictionary<Chart, KeyEventHandler>` in `TradeCopierAddOn.cs`.
- R1: `IsNakedConditionMet` / `IsEntryCandidateOrder` / `SnapshotTargetsPublic` in `CopyEngine.cs` — reference equality replaced by FullName comparison (`OrderHasInstrFn` helper + inline `?.FullName`).
- R2: `TryDrainWatchdog` — `PendingCancelCount <= 0` guard added; `ReissueDrainCancels` helper method added.
- R3: `LoadAndValidateLicense` in `TradeCopierAddOn.cs` — `dev_mode.txt` Elite bypass removed (3 lines deleted).
- R4: `TradeCopierWindow.cs` — `_modeCb.IsEnabled = f.MirrorMode` removed from `ApplyFeatureFlags` (Fix A); Elite gate added to `OnCopyModeComboChanged` (Fix B).
- R5: `TradeCopierWindow.cs` — `Account.All` bound immediately in `BuildRuleRow` with null guard; `OnLoaded` foreach re-bind loops removed.
- R6: `PttGlobalQuickExit.cs` — `CancelPttBeOrders` wraps body in try/catch and returns `-1` on exception; `TryCancelBeOrders` wrapper added; all 3 call sites updated; `ProcessForcedTargetPosition` extracted (R6.0, mandatory) to reduce `Execute(forcedTargets)` CYC from 8 to 5; `GetFollowerPositionQty` and `LogFollowerDiag` extracted from `ExecuteFollowers` (CYC enforcement).
- Tests: 6 new `[Fact]` methods (T_R1–T_R6). Final `CopyEngineTests.cs` [Fact] count: 483 (was 477).

**Plan Review Cycles**: Cycle 1 FAIL (JS-066: `Execute(forcedTargets)` CYC=9 permitted in ticket language) → Cycle 2 PASS (R6.0 extraction mandated; `Execute(forcedTargets)` CYC=5 required).

**Final Review**: FINAL_PASS (2026-09-07)

---

## B24 Items Carried Forward (All Unchanged)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B24-01 | **NT8-043 formal rule entry**: Confirm null-conditional event unsubscription (`?.Event -=`) causes silent runtime crash under NT8 Roslyn. SCAN-07 = 0 across B24, B25, B26, PTT-REPAIRS-01. Rule watch continues; no new evidence. | P2 | B27 or future | OPEN |
| DW-B24-02 | **Manual E2E runtime verification (escalated)**: Press Ctrl+Shift+B on a solo account in a live NinjaTrader 8 session. Confirm `BreakEven` fires (stop moves) and no NullReferenceException. Also now includes runtime verification of PTT-REPAIRS-01 fixes: R1 FullName equality, R2 drain watchdog behaviour, R4 Mirror mode gate under non-Elite license, R5 Account.All binding timing. Must execute before production release. | P1 | ASAP post-merge | OPEN |
| DW-B24-03 | **Skip-duplicate guard `[Fact]`**: Formal xUnit test for `if (acc == leader) continue` guard at `CopyEngine.cs:~1195`. Not addressed in PTT-REPAIRS-01 (out-of-scope). | P2 | B27 | OPEN |

---

## B25 Items Carried Forward

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B25-01 | **Companion field race**: `_pendingBeAccount`, `_pendingBeInstrument`, `_trailBeAccount`, `_trailBeInstrument` remain plain singleton refs. Multi-panel topology could race on these. Not in scope for PTT-REPAIRS-01. | P3 | B28 or future | OPEN |

---

## B26 Items Carried Forward

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B26-01 | **Reflection test upgrade (Option B → Option A)**: `TradeCopierPanel_BeBufferBox_FieldDoesNotExist` uses reflection-based field-absence assertion because WPF instantiation of `TradeCopierPanel` requires NT8 runtime unavailable in xUnit. If a test harness with `InternalsVisibleTo` + WPF test host becomes available, replace with Option A: instantiate panel, set `_beBuffer = 3` via reflection, invoke `DispatchShortcut(Key.B)`, assert `ICopyEngine.BreakEven` called with argument 3. | P2 | B28 or future | OPEN |

---

## PTT-REPAIRS-01 New Items

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-REPAIRS-01-01 | **R5 `Account.All` constructor-path risk**: `BuildRuleRow` is called from `BuildUI` (constructor path) before `Loaded` fires. The defensive null guard (`if (Account.All != null)`) prevents NPE but means `leaderCb.ItemsSource` and `followerLb.ItemsSource` are NOT set if `Account.All` is null at construction time (which `NT8_ADDON_KNOWLEDGE.md:134` warns about). If `Account.All` is null at construction, the selectors remain unbound until the next `RefreshRuleRows` call. Risk: if saved rules are restored before `Loaded` fires (i.e., `LoadRules` called from constructor), account selectors will show empty. Recommend: (a) add explicit diagnostic log if `Account.All == null` at `BuildRuleRow` time, and (b) runtime E2E test confirming account dropdowns populate after rule restore in a fresh NT8 session. | P2 | B28 or future | OPEN |
| DW-REPAIRS-01-02 | **`TryCancelBeOrders` wrapper has no `-1`-path `[Fact]` test**: `T_R6_CancelPttBeOrders_ReturnsNegativeOneOnException` verifies the wrapper exists and has the correct `(Account, Instrument) -> int` signature, but cannot exercise the `-1` return path because it requires a constructable NT8 `Account` object that throws during `Cancel()`. The null-guard path (returns 0) is tested. The exception-catch path (returns -1) requires NT8 runtime. If a mock `Account` harness becomes available (e.g., via a mock/stub pattern), add a test that passes an `Account` whose `Cancel()` throws and asserts `TryCancelBeOrders` returns `-1`. | P2 | B28 or future | OPEN |

---

## Items Closed This Block

| ID | Item | Closed In | Notes |
|----|------|-----------|-------|
| DW-B25-02 | Per-account BE state isolation | B25 (prior) | Already closed; carried here for audit trail |

*(No additional items from the open B24–B26 list were addressed in PTT-REPAIRS-01. All remain open as documented above.)*

---

*ptt-plan-reviewer · PTT-REPAIRS-01 · 2026-09-07*
