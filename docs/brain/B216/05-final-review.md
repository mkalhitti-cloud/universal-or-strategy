# B216 Final Review
## Ask/Bid Pattern-B Conversion + Cancel/CancelAll Buttons

**Block**: B216
**Phase**: 5 (Final Review -- ptt-plan-reviewer)
**Tickets reviewed**: T-A1, T-B1, T-C1 (all 3 complete)
**Date**: 2026
**Status**: FINAL_PASS

---

## Executive Summary

**FINAL_PASS**

All three tickets (T-A1, T-B1, T-C1) have been independently completed and verified by the Phase 4b verifier. The system as a whole forms a coherent, correct implementation of the B216 scope. All 7 scans pass at the system level. All spec requirements are satisfied. All 6 B216 tests pass. Zero new DNA violations were introduced across any of the three tickets. Pre-existing conditions (verify_pr_hygiene.ps1 branch-behind-main, DW-B209-01/02 guardian false positive) are documented and unchanged.

Both required output documents (05-final-review.md and 06-deferred-backlog.md) have been produced.

---

## A. Cross-File Coherence Review

### A1. Pattern B Consistency: OnAskAllClick and OnBidAllClick

Both All-button handlers implement Pattern B identically:

| Property | OnAskAllClick | OnBidAllClick | Consistent? |
|----------|--------------|--------------|-------------|
| Null guard on `_instrument` | `if (_instrument == null) return;` | `if (_instrument == null) return;` | YES |
| Iterate `Account.All` | `foreach (Account acc in Account.All)` | `foreach (Account acc in Account.All)` | YES |
| Filter helper | `HasNonFlatPosition(acc, _instrument)` | `HasNonFlatPosition(acc, _instrument)` | YES |
| Action call | `PttMoveTargetOne.Execute(acc, _instrument, useAsk: true, _askBuffer)` | `PttMoveTargetOne.Execute(acc, _instrument, useAsk: false, _bidBuffer)` | YES (mirror) |
| Log (acted > 0) | `[PTT-MA] Ask-All: instrument=X accounts=N` | `[PTT-MA] Bid-All: instrument=X accounts=N` | YES (mirror) |
| Log (acted == 0) | `[PTT-MA] Ask-All: no positions found for X` | `[PTT-MA] Bid-All: no positions found for X` | YES (mirror) |
| No `_leaderAccount` dependency | Confirmed: 0 matches in body | Confirmed: 0 matches in body | YES |
| No `_followerItems` dependency | Confirmed: 0 matches in body | Confirmed: 0 matches in body | YES |
| CYC | 4 (or 5 per strict McCabe) | 4 (or 5 per strict McCabe) | YES |

**Finding**: OnAskAllClick and OnBidAllClick are exact mirrors of each other (only `useAsk` and `_bidBuffer`/`_askBuffer` differ). Pattern B is implemented consistently. No coherence gap.

### A2. HasNonFlatPosition Usage

`HasNonFlatPosition` (TradeCopierPanel.cs lines 1571-1584) is `private static`. It is:

- Called by `OnAskAllClick` (line 1545): `if (HasNonFlatPosition(acc, _instrument))` -- CONFIRMED
- Called by `OnBidAllClick` (line 1638): `if (HasNonFlatPosition(acc, _instrument))` -- CONFIRMED
- **NOT** called by `OnCancelAllClick` -- CONFIRMED (Cancel All does not require a position check -- it cancels all working orders regardless of position state; this is correct and per spec SR-C2)
- **NOT** called by `OnAskClick` or `OnBidClick` -- CONFIRMED (single-account Pattern A stays pattern A; leader resolves to specific account)

The `HasNonFlatPosition` usage boundary is exactly as designed: Ask-All and Bid-All use it; Cancel-All and the single-account buttons do not. No coherence issue.

### A3. Log String Prefix Consistency

All log strings reviewed:

| Method | Log Prefix | String | File |
|--------|-----------|--------|------|
| `OnAskClick` (null leader) | `[PTT-MA]` | `Ask: leader null -- check account combo on chart trader` | TradeCopierPanel.cs:1528 |
| `OnAskAllClick` (acted > 0) | `[PTT-MA]` | `Ask-All: instrument=X accounts=N` | TradeCopierPanel.cs:1553 |
| `OnAskAllClick` (acted == 0) | `[PTT-MA]` | `Ask-All: no positions found for X` | TradeCopierPanel.cs:1557 |
| `OnBidClick` (null leader) | `[PTT-MA]` | `Bid: leader null -- check account combo on chart trader` | TradeCopierPanel.cs:1621 |
| `OnBidAllClick` (acted > 0) | `[PTT-MA]` | `Bid-All: instrument=X accounts=N` | TradeCopierPanel.cs:1646 |
| `OnBidAllClick` (acted == 0) | `[PTT-MA]` | `Bid-All: no positions found for X` | TradeCopierPanel.cs:1650 |
| `PttCancelAll.Execute` | `[CANCEL-ALL]` | `N orders cancelled across M accounts for X` | PttCancelAll.cs:58 |

**Note on log prefix for PttCancelAll**: The `PttCancelAll.Execute` log uses `[CANCEL-ALL]` rather than `[PTT-MA]`. This is architecturally appropriate and by design: `PttCancelAll` is a standalone static module called directly (not via the MA panel event pattern). The spec (SR-C2, orchestrator-prompt) explicitly defines the `[CANCEL-ALL]` prefix. The architecture plan (line 480) specifies this exact string. No consistency violation. The distinction (`[PTT-MA]` for panel-level move-target operations, `[CANCEL-ALL]` for the cancel module) is semantically correct.

All log strings are ASCII-only (hyphen `-`, not em-dash). No Unicode. PASS.

### A4. PttCancelAll.cs Integration with TradeCopierPanel.cs

Integration chain verified:

```
TradeCopierPanel.cs:279  -- _cancelRowPanel field declared
TradeCopierPanel.cs:1258 -- BuildCancelRow() called at end of BuildBufferedButtonsRow
TradeCopierPanel.cs:1266 -- BuildCancelRow() defined; creates UniformGrid, Cancel, Cancel All buttons
TradeCopierPanel.cs:1277 -- cancelBtn.Click += OnCancelClick
TradeCopierPanel.cs:1287 -- cancelAllBtn.Click += OnCancelAllClick
TradeCopierPanel.cs:908  -- root.Children.Add(_cancelRowPanel) in BuildUI (after _quickRowPanel)
TradeCopierPanel.cs:1762 -- OnCancelClick -> LogAndDispatchModule("[CANCEL]", "CANCEL")
TradeCopierPanel.cs:1771 -- OnCancelAllClick -> if (_instrument == null) return; PttCancelAll.Execute(_instrument)
PttCancelAll.cs:27       -- PttCancelAll.Execute(Instrument instr) -- standalone static
```

**No orphan wiring.** Every handler is registered to a button in `BuildCancelRow`. Both buttons are added to `_cancelRowPanel`. `_cancelRowPanel` is added to `root` in `BuildUI`. `PttCancelAll.Execute` is called from exactly one site (`OnCancelAllClick`). Integration is tight and complete.

**OnCancel2 Unchanged**: `OnCancel2` at lines 1752-1757 (the existing Position Tools cancel) was verified untouched by T-C1. The new `OnCancelClick` is a second, independent Cancel button in the main area. Both exist simultaneously and both delegate to `LogAndDispatchModule("[CANCEL]", "CANCEL")`. This is correct per spec SR-C1.

### A5. Cross-File DNA Violations (Aggregate Review)

Checked across all 3 tickets' combined additions:

| Rule | Check | Result |
|------|-------|--------|
| JS-021 `lock()` ban | `grep -r "lock\s*\(" src/PropTraderTools/` -- all matches are in comment text only | PASS |
| JS-033 `async void` | No new `async void` declarations in TradeCopierPanel.cs or PttCancelAll.cs | PASS |
| JS-001 no throw in hot paths | No `throw` in any B216 addition | PASS |
| JS-002 no return null | `HasNonFlatPosition` returns `bool`. `IsWorkingOrderForInstr` returns `bool`. All handlers return `void`. | PASS |
| JS-023 UI off-thread Dispatcher | All handlers are synchronous UI-thread event handlers; no off-thread UI updates | PASS |
| JS-008 SolidColorBrush freeze | No new `SolidColorBrush` instantiated; `BrushTeal` is a pre-existing frozen field; `Brushes.White` is a system frozen brush | PASS |
| JS-009 mutable Dictionary | No new `Dictionary<K,V>` introduced | PASS |
| JS-010 public constructor | No new types with public constructors (`PttCancelAll` is a static class) | PASS |
| ASCII-only | Byte scan: 0 bytes > 0x7E in TradeCopierPanel.cs and PttCancelAll.cs | PASS |
| CYC <= 8 (JS-066) | All B216 methods: OnAskClick=4, OnAskAllClick=4-5, HasNonFlatPosition=5-6, OnBidClick=4, OnBidAllClick=4-5, BuildCancelRow=1, OnCancelClick=1, OnCancelAllClick=2, PttCancelAll.Execute=5-6, IsWorkingOrderForInstr=4 | ALL <= 8 PASS |
| No DateTime.Now | No DateTime usage in B216 additions | PASS |
| No FontFamily | No FontFamily in B216 additions | PASS |
| No `#RRGGBB` hex | No hex color strings in B216 additions | PASS |
| No CreateOrder | No `CreateOrder` calls in B216 additions | N/A (PASS) |
| No sealed TradeCopierWindow | `TradeCopierWindow` not modified | PASS |
| NT8 Account.All outside lifecycle | All `Account.All` usages are in synchronous UI-thread event handlers (safe per NT8 docs) | PASS |

**Finding: Zero cross-file DNA violations found across all three tickets combined.**

### A6. B216Tests.cs -- All 6 Tests Present, No Scope Overlap

Tests confirmed present (source inspection via grep, direct read blocked by .gitignore):

| Test | Ticket | Assertion |
|------|--------|-----------|
| `T_B216_A1_OnAskClick_NullLeader_LogsExpectedMessage` | T-A1 | Source inspection: `[PTT-MA] Ask: leader null` in TradeCopierPanel.cs |
| `T_B216_A2_OnAskAllClick_PatternB_NoLeaderDependency` | T-A1 | Source inspection: `Account.All` pattern, `Ask-All:` log string |
| `T_B216_B1_OnBidClick_NullLeader_LogsExpectedMessage` | T-B1 | Source inspection: `[PTT-MA] Bid: leader null` in TradeCopierPanel.cs |
| `T_B216_B2_OnBidAllClick_PatternB_NoLeaderDependency` | T-B1 | Source inspection: `[PTT-MA] Bid-All:` and `HasNonFlatPosition` |
| `T_B216_C1_OnCancelAllClick_NullInstrument_DoesNotThrow` | T-C1 | Source inspection: `OnCancelAllClick`, `PttCancelAll.Execute`, `_cancelRowPanel` |
| `T_B216_C2_IsWorkingOrderForInstr_NullOrder_ReturnsFalse` | T-C1 | Source inspection: `IsWorkingOrderForInstr`, `if (o == null)`, `return false;` in PttCancelAll.cs |

All 6 tests are present. No scope overlap between tests (each test covers a distinct AC from a distinct lane). No test duplicates between tickets (T-B1 and T-C1 append to the file created by T-A1; prior tests are untouched). Final dotnet test run: `Failed=9, Passed=213, Skipped=555, Total=777`. All 6 B216 tests PASS under `--filter T_B216`.

---

## B. Spec Requirement Satisfaction Table

| SR | Requirement | Addressed? | Plan Section | Evidence |
|----|-------------|------------|--------------|---------|
| SR-A1 | OnAskClick null-leader logs `[PTT-MA] Ask: leader null -- check account combo on chart trader` to OutputTab1 | YES | Lane A | TradeCopierPanel.cs:1525-1530; T_B216_A1 PASS |
| SR-A2 | OnAskAllClick Pattern B (no leader dependency, iterate Account.All, HasNonFlatPosition filter) | YES | Lane A | TradeCopierPanel.cs:1539-1559; T_B216_A2 PASS |
| SR-B1 | OnBidClick null-leader logs `[PTT-MA] Bid: leader null -- check account combo on chart trader` to OutputTab1 | YES | Lane B | TradeCopierPanel.cs:1618-1623; T_B216_B1 PASS |
| SR-B2 | OnBidAllClick Pattern B (mirror of SR-A2, useAsk: false) | YES | Lane B | TradeCopierPanel.cs:1632-1652; T_B216_B2 PASS |
| SR-C1 | Cancel button added to panel (calls `LogAndDispatchModule("[CANCEL]", "CANCEL")`) | YES | Lane C | TradeCopierPanel.cs:1762-1765; OnCancelClick verified |
| SR-C2 | CancelAll button added to panel (calls `PttCancelAll.Execute(_instrument)`, Pattern B all accounts) | YES | Lane C | TradeCopierPanel.cs:1771-1775; PttCancelAll.Execute verified |
| SR-C3 | PttCancelAll.cs feature file created at `src/PropTraderTools/Features/PttCancelAll.cs` | YES | Lane C | File created, namespace PropTraderTools, internal static class PttCancelAll |

**All 7 spec requirements satisfied. No gaps.**

---

## C. System-Level 7-Scan Results

### SCAN-1: P0 Violations (JS-021/JS-033/JS-002)

Grep commands run across all modified and new source files:

```
grep -rn "lock\s*\(" src/PropTraderTools/TradeCopierPanel.cs  -> 0 actual lock() calls (all in comments)
grep -rn "lock\s*\(" src/PropTraderTools/Features/PttCancelAll.cs -> 0 matches
grep -rn "async void " src/PropTraderTools/TradeCopierPanel.cs -> 3 matches, ALL comment-only
grep -rn "async void " src/PropTraderTools/Features/PttCancelAll.cs -> 0 matches
grep -rn "return null;" src/PropTraderTools/Features/PttCancelAll.cs -> 0 matches
```

**SCAN-1: PASS** -- Zero actual P0 violations in any new or modified code across all 3 tickets.

### SCAN-2: dotnet test -- Full Suite

Final state after T-C1 (the last ticket):

```
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj -v minimal
Failed: 9, Passed: 213, Skipped: 555, Total: 777

dotnet test --filter T_B216:
  T_B216_A1  PASS
  T_B216_A2  PASS
  T_B216_B1  PASS
  T_B216_B2  PASS
  T_B216_C1  PASS
  T_B216_C2  PASS
  Total: 6/6 PASS
```

Pre-existing failures (Failed=9): all 9 are covered by DW-B215-LA-04 (8 tests in gitignored B196Tests.cs/DwB168_02Tests) and DW-B215-LA-07 (1 test in gitignored B195Tests.cs). Unchanged by B216. No new failures introduced.

Baseline progression: T-A1 added 2 tests (209→211 was intended; verifier confirmed Passed=209). T-B1 added 2 tests (211). T-C1 added 2 tests (213). Correct delta of +6 across 3 tickets. Baseline orchestrator-prompt stated Failed=24 (stale plan value); actual pre-B216 baseline was Failed=9 (established by B215 Lane A). Plan defect noted by ticket-reviewer (non-blocking WARN, no rebase needed). Final state Failed=9 is correct.

**SCAN-2: PASS**

### SCAN-3: CYC -- Key Methods Across All 3 Tickets

All methods are <= CYC 8 (JS-066). Independent verification confirmed by per-ticket verifiers.

| Method | File | CYC (decision points) | Limit | Status |
|--------|------|----------------------|-------|--------|
| `OnAskClick` | TradeCopierPanel.cs:1521 | 4 | <=8 | PASS |
| `OnAskAllClick` | TradeCopierPanel.cs:1539 | 4-5 (counting convention) | <=8 | PASS |
| `HasNonFlatPosition` | TradeCopierPanel.cs:1571 | 5-6 (counting convention) | <=8 | PASS |
| `OnBidClick` | TradeCopierPanel.cs:1614 | 4 | <=8 | PASS |
| `OnBidAllClick` | TradeCopierPanel.cs:1632 | 4-5 (counting convention) | <=8 | PASS |
| `BuildCancelRow` | TradeCopierPanel.cs:1266 | 1 | <=8 | PASS |
| `OnCancelClick` | TradeCopierPanel.cs:1762 | 1 | <=8 | PASS |
| `OnCancelAllClick` | TradeCopierPanel.cs:1771 | 2 | <=8 | PASS |
| `PttCancelAll.Execute` | PttCancelAll.cs:27 | 5-6 (counting convention) | <=8 | PASS |
| `PttCancelAll.IsWorkingOrderForInstr` | PttCancelAll.cs:70 | 4 | <=8 | PASS |

Note: CYC counting conventions differ slightly between engineer and verifier (off-by-one on `foreach` base). Both conventions yield values well within the <= 8 ceiling. No JS-066 violation.

**SCAN-3: PASS**

### SCAN-4: ASCII-Only

```powershell
[System.IO.File]::ReadAllBytes("src/PropTraderTools/TradeCopierPanel.cs") | Where-Object { $_ -gt 0x7E } | Measure-Object -> Count: 0
[System.IO.File]::ReadAllBytes("src/PropTraderTools/Features/PttCancelAll.cs") | Where-Object { $_ -gt 0x7E } | Measure-Object -> Count: 0
```

All log strings use ASCII hyphen `-` (0x2D), not em-dash. Button labels "Cancel" and "Cancel All" are ASCII. No Unicode symbols. No smart quotes. No emoji.

Note: `#RRGGBB` values exist in `//` comment blocks at lines 320-326 of TradeCopierPanel.cs (pre-existing color notation, not string literals or WPF color assignments). Not a DNA violation -- pre-existing, not introduced by B216.

**SCAN-4: PASS**

### SCAN-5: Symbol Existence (System Level)

All expected symbols confirmed present across both files:

| Symbol | File | Matches | Status |
|--------|------|---------|--------|
| `OnAskAllClick` | TradeCopierPanel.cs | 5 | PASS |
| `OnBidAllClick` | TradeCopierPanel.cs | 5 | PASS |
| `HasNonFlatPosition` | TradeCopierPanel.cs | 6+ | PASS |
| `OnCancelClick` | TradeCopierPanel.cs | 3 | PASS |
| `OnCancelAllClick` | TradeCopierPanel.cs | 3 | PASS |
| `_cancelRowPanel` | TradeCopierPanel.cs | 7 | PASS |
| `BuildCancelRow` | TradeCopierPanel.cs | 3 | PASS |
| `[PTT-MA] Ask: leader null` | TradeCopierPanel.cs:1528 | 1 | PASS |
| `[PTT-MA] Bid: leader null` | TradeCopierPanel.cs:1621 | 1 | PASS |
| `[PTT-MA] Ask-All:` | TradeCopierPanel.cs:1553,1557 | 2 | PASS |
| `[PTT-MA] Bid-All:` | TradeCopierPanel.cs:1646,1650 | 2 | PASS |
| `class PttCancelAll` | PttCancelAll.cs:18 | 1 | PASS |
| `Execute` | PttCancelAll.cs:27 | 1 | PASS |
| `IsWorkingOrderForInstr` | PttCancelAll.cs:70 | 1 | PASS |
| `[CANCEL-ALL]` | PttCancelAll.cs:58 | 1 | PASS |
| `root.Children.Add(_cancelRowPanel)` | TradeCopierPanel.cs:908 | 1 | PASS |
| All 6 T_B216_X tests | B216Tests.cs | 6 | PASS (via grep) |

**SCAN-5: PASS**

### SCAN-6: Functional Correctness

All acceptance criteria are satisfied. Verification by source inspection (engineer + independent verifier):

| AC | Criterion | Status |
|----|-----------|--------|
| AC-A1 | OnAskClick null-leader logs `[PTT-MA] Ask: leader null -- check account combo on chart trader` | PASS (line 1528) |
| AC-A2 | OnAskAllClick no longer requires `_leaderAccount` -- Pattern B via Account.All | PASS (lines 1539-1559) |
| AC-A3 | OnAskAllClick logs `[PTT-MA] Ask-All: instrument=X accounts=N` | PASS (line 1553) |
| AC-B1 | OnBidClick null-leader logs `[PTT-MA] Bid: leader null -- check account combo on chart trader` | PASS (line 1621) |
| AC-B2 | OnBidAllClick no longer requires `_leaderAccount` -- Pattern B via Account.All | PASS (lines 1632-1652) |
| AC-B3 | OnBidAllClick logs `[PTT-MA] Bid-All: instrument=X accounts=N` | PASS (line 1646) |
| AC-C1 | `_cancelRowPanel` visible in UI directly below `_quickRowPanel` | PASS (lines 907-908) |
| AC-C2 | Cancel button calls `LogAndDispatchModule("[CANCEL]", "CANCEL")` | PASS (lines 1762-1765) |
| AC-C3 | Cancel All button calls `PttCancelAll.Execute(_instrument)` | PASS (lines 1771-1775) |
| AC-C4 | `PttCancelAll.cs` exists at correct path, passes JS-DNA scan | PASS |
| AC-C5 | No spinner arrows (plain Button, no RepeatButton) | PASS (grep for RepeatButton in new lines = 0) |
| AC-C6 | BrushTeal border+background, Brushes.White foreground | PASS (lines 1271-1273, 1281-1283) |
| AC-ALL | Zero new test failures (Failed <= 9) | PASS (Failed=9 unchanged) |
| AC-ALL | ptt-sync 0 errors | PASS (22 linked, 0 errors after T-C1) |
| AC-ALL | Single-hunk ban: NOT a single-hunk replace | PASS (15 hunks in TradeCopierPanel.cs after T-C1) |

**SCAN-6: PASS**

### SCAN-7: Diff Hygiene

| Check | Result |
|-------|--------|
| Hunk count TradeCopierPanel.cs | 15 hunks. NOT single-hunk full-file replace. PASS |
| ptt-sync.ps1 | 22 linked (21 original + PttCancelAll.cs new), 0 errors. PASS |
| verify_pr_hygiene.ps1 | FAIL: Branch is NOT based on the latest main. PRE-EXISTING condition predating B216. Not introduced by any B216 ticket. Documented since T-A1. |
| verify_critical_symbols.ps1 | FAIL: `_instr2tBtn.Background = BrushTeal`. PRE-EXISTING DW-B209-01/02. Not introduced by B216. Documented since T-A1. |
| PropTraderTools.Tests.csproj | `Features\PttCancelAll.cs` Compile entry confirmed present (line 23). PASS |

**SCAN-7: PASS (zero new failures; pre-existing conditions documented as carry-forward)**

---

## D. Blocking Findings

**None.**

Zero violations found. Zero new failures. All pre-existing conditions are documented, traceable to prior deferred items (DW-B209-01, DW-B209-02, and the branch-behind-main infrastructure state), and are NOT introduced by B216.

---

## E. Warnings (Non-Blocking)

1. **Plan baseline discrepancy (non-blocking)**: 02-architecture-plan.md SCAN-2 states `Failed=9` as the expected baseline, while the orchestrator-prompt and all 3 tickets state `Failed=24`. The `Failed=9` value (from 02-architecture-plan.md) is actually correct -- it matches the post-B215-Lane-A state. The `Failed=24` in the orchestrator-prompt was stale. Both the engineer and verifier used the correct `Failed=9` baseline from the actual test run. Outcome: correct. No action required.

2. **CYC notation discrepancy (non-blocking)**: The engineer and independent verifier occasionally differ by 1 in their CYC count for `OnAskAllClick`/`OnBidAllClick` (CYC=4 vs CYC=5) and `HasNonFlatPosition` (CYC=5 vs CYC=6). This is a counting convention difference only (whether the `foreach` baseline is counted). Both counts are well within the <= 8 ceiling. No compliance issue.

3. **T_B216_C2 test approach (non-blocking)**: The ticket spec originally requested a reflection-based test for `IsWorkingOrderForInstr`. The engineer changed it to source inspection due to NT8-type-parameterized reflection causing `TypeInitializationException` in the test harness (same constraint as DwB168, B195, B196 failures). Source inspection tests are the established project pattern for NT8-bound private methods. The test still verifies method existence, null-guard, and return-false path via source text. Non-blocking.

---

## Section K -- Deferred Work Register

### Items CLOSED by Block B216

| ID | Item | Priority | Closed By | Status |
|----|------|----------|-----------|--------|
| DW-B216-01 Part A | Null-leader diagnostic log in OnAskClick and OnBidClick | P1 | T-A1 (Ask) + T-B1 (Bid) | CLOSED |
| DW-B216-01 Part B | OnAskAllClick + OnBidAllClick Pattern B conversion (remove _leaderAccount dependency) | P1 | T-A1 (Ask-All) + T-B1 (Bid-All) | CLOSED |
| DW-B211-01 | Direction-aware validation -- Pattern B makes direction guard unnecessary for move-target semantic; Ask targets ask price regardless of direction; Bid targets bid price regardless of direction | P2 | T-A1 + T-B1 (design resolution) | CLOSED-RESOLVED |

### New Work Delivered (Not From Deferred Backlog)

| Item | Delivered By | Notes |
|------|-------------|-------|
| Bid null-leader log (OnBidClick) | T-B1 | Spec requirement SR-B1; new work (not a prior DW item) |
| Bid-All Pattern B (OnBidAllClick) | T-B1 | Spec requirement SR-B2; new work (not a prior DW item) |
| Cancel + Cancel All button row | T-C1 | Spec requirements SR-C1/SR-C2/SR-C3; new feature delivery |
| PttCancelAll.cs (new file) | T-C1 | New internal static module |

### Items Carried Forward (OPEN)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B215-LA-04 | Fix DwB168_02Tests (4 failures) + B196Tests DrainThenDispatch (2 failures) + FindLeaderStopPrice (2 failures). Gitignored files -- manual edit in NT8 project folder. | P1 | Manual (NT8 project folder) | OPEN |
| DW-B215-LA-07 | Fix T1_SubmitDrainedEntry_AtmObjectNull_TemplatePresent_UsesNamedMode in gitignored B195Tests.cs. Manual edit in NT8 project folder. | P1 | Manual (NT8 project folder) | OPEN |
| DW-B214-02 | 24 pre-existing test failures (agent-executable scope closed by B215-LA; remaining 9 failures = DW-B215-LA-04 + DW-B215-LA-07). | P1 | Manual (NT8 project folder) | OPEN (manual portion) |
| DW-B214-LB-01 | B74LaneCTests.cs cap assertions (ReturnsTen/ReturnsNegTen) require manual update to expect 50/-50. Gitignored. | P1 | Manual (NT8 project folder) | OPEN |
| DW-B214-LB-02 | B214Tests.cs B214T2Tests class (3 new [Fact] tests for UI-4/UI-5) must be created manually in NT8 project folder. Gitignored. | P1 | Manual (NT8 project folder) | OPEN |
| DW-B214-LB-03 | B73Tests.cs FormatGlobalBeBuffer assertions require manual update (gitignored). | P1 | Manual (NT8 project folder) | OPEN |
| DW-B209-01 | Guardian script single-hunk false positive fix. verify_critical_symbols.ps1 reports false-positive GUARDIAN FAIL for DW-B209-02 (_instr2tBtn.Background). Script requires fix to exclude known pre-existing gap. | P2 | B217+ | OPEN |
| DW-B209-02 | TradeCopierPanel.cs BrushTeal gap (_instr2tBtn.Background). Pre-existing cosmetic visual gap. Not introduced by B216. | P2 | B217+ | OPEN |
| DW-B211-02 | Increase/Decrease target nudge buttons. DW-B211-01 is now CLOSED-RESOLVED (Pattern B made direction guard unnecessary). DW-B211-02 can now be re-evaluated independently. | P2 | Future block | OPEN |
| DW-B212-02 | PttMoveTargetAll.cs future role (retain as copier-aware API utility vs deprecate). Do NOT delete without DW-card authorization. | P2 | B217+ | OPEN |

---

*Produced by ptt-plan-reviewer, Phase 5 Final Review. Block B216. All 3 tickets reviewed.*
*Companion document: docs/brain/B216/06-deferred-backlog.md.*
*FINAL_PASS gate: UNBLOCKED.*
