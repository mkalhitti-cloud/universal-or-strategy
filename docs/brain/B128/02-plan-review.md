# B128 Ph2 Plan Review

**Reviewer**: ptt-plan-reviewer  
**Phase**: 2 (Plan Review)  
**Date**: 2026  
**Plan file**: `docs/brain/B128/02-architecture-plan.md`

---

## Gate: REVIEW_PASS

---

## STEP 0 — Rules Catalog Gate

`docs/standards/jane-street/RULES_CATALOG.md` — UTF-8 clean, fully readable (JS-001..JS-035+ confirmed).  
No P0 violations found in the planned code.  
**GATE RESULT: PASS**

---

## Checks

| ID | Check | Result | Notes |
|----|-------|--------|-------|
| R-01 | Layout correctness: `_instrRowPanel` inserted between `_beRowPanel` and `_quickRowPanel`; `_quickRowPanel` UNCHANGED | ✅ PASS | Plan §"New Layout After B128" shows insertion at L914/L915 boundary. Source confirmed: L914=`_beRowPanel`, L915=`_quickRowPanel`. `_quickRowPanel` explicitly marked UNCHANGED. |
| R-02 | ArmPendingBe call site uses `_engine.ArmPendingBe(...)` not `GlobalBe.ArmPendingBe(...)` | ✅ PASS | Plan §OnInstrBeClick: `_engine.ArmPendingBe(_instrument, _leaderAccount, _beBuffer)`. Source `CopyEngine.cs` L3935 confirms method on `CopyEngine` instance. Plan §"Files Not Changed" explicitly notes "ArmPendingBe is on CopyEngine, not GlobalBe." |
| R-03 | ComputeInstrSplit arithmetic: t1=(n+1)/2, t2=n/2; all 4 cases correct | ✅ PASS | Manually verified: (4)→(2,2), (5)→(3,2), (1)→(1,0), (7)→(4,3). All 4 correct. |
| R-04 | Scope boundary: PttQuickExit.cs, PttGlobalBreakEven.cs, CopyEngine.cs all UNCHANGED | ✅ PASS | Plan §"Files Explicitly Not Changed" lists all three as UNCHANGED with explicit rationale. |
| R-05 | CYC budgets: BuildInstrRow<=4, ComputeInstrSplit=1, OnInstrQxClick<=3, OnInstrQxUp<=2, OnInstrQxDown<=2, OnInstrBeClick<=3 | ✅ PASS | Plan CYC table: BuildInstrRow=1, ComputeInstrSplit=1, OnInstrQxClick=3, OnInstrQxUp=2, OnInstrQxDown=2, OnInstrBeClick=3. All within budget and within JS CYC<=8. |
| R-06 | JS compliance: 0 new locks (JS-021), 0 new async void (JS-033), ASCII-only string literals | ✅ PASS | JS-021: 0 lock(); JS-033: all handlers synchronous void; "[PTT-QX-INSTR]", "[PTT-BE-INSTR]", "QX-Instr", "BE-Instr" confirmed ASCII. \u25B2/\u25BC pre-existing spinner pattern — acceptable. |
| R-07 | Four fields: `_instrQxBtn` (Button), `_instrBeBtn` (Button), `_instrRowPanel` (UniformGrid), `_instrQxT1=4` (int) | ✅ PASS | Plan §"New Fields" declares all four with correct types and default value (`_instrQxT1 = 4`). |
| R-08 | 7-scan checklist: all 7 scans listed with expected pass criteria | ✅ PASS | SCAN-01 ASCII, SCAN-02 lock(), SCAN-03 async void, SCAN-04 return null, SCAN-05 build, SCAN-06 CYC, SCAN-07 tests — all listed with 0-match / 0-error / pass criteria. |
| R-09 | Test spec: 4 [Fact] tests covering (4)→(2,2),(5)→(3,2),(1)→(1,0),(7)→(4,3); xUnit; calls ComputeInstrSplit directly | ✅ PASS | All 4 test cases listed with correct inputs, expected values, and `Assert.Equal` assertions. xUnit `[Fact]` specified. Target: `TradeCopierPanel.ComputeInstrSplit` (internal static). |
| R-10 | Files changed: exactly TradeCopierPanel.cs (modified) + Tests/B128Tests.cs (new) | ✅ PASS | Plan §"Files Changed" lists exactly 2 files. No other .cs files in changed list. |
| R-11 | Spinner pattern: RepeatButton with \u25B2/\u25BC, Width=18, Height=12 matching existing Quick cluster | ✅ PASS | Plan §BuildInstrRow: `Content = "\u25B2", Width = 18, Height = 12` and `Content = "\u25BC", Width = 18, Height = 12`. Source L1250-1260 confirms identical dimensions in existing Quick spinner. |

---

## Violations

*None.*

---

## Source Cross-Reference

| Plan Claim | Source Verification | Status |
|------------|-------------------|--------|
| `_engine` is `private CopyEngine _engine` at L118 | Grep confirms `CopyEngine _engine` in `TradeCopierPanel.cs` | ✅ |
| `ArmPendingBe(Instrument, Account, int)` at `CopyEngine.cs` L3935 | Read confirms `internal void ArmPendingBe(Instrument instr, Account masterAcc, int bufferTicks)` at L3935 | ✅ |
| `PttQuickExit.Execute(Account, Instrument, int, int, bool)` at L215 | Read confirms compat overload at L215 with matching signature | ✅ |
| `BrushTeal` at L320 | Read confirms `private static readonly SolidColorBrush BrushTeal = MakeBrush(13, 148, 136)` at L320 | ✅ |
| `_beRowPanel` at L914, `_quickRowPanel` at L915 | Read confirms `root.Children.Add(_beRowPanel)` L914, `root.Children.Add(_quickRowPanel)` L915 | ✅ |
| Spinner Width=18, Height=12 | Read confirms at L1253-1260 in existing Quick cluster | ✅ |
| `_beBuffer = 1` at L244 | Read confirms `private int _beBuffer = 1` at L244 | ✅ |

---

## Recommendation

**REVIEW_PASS → proceed to Ph3 (ticket generation).**

All 11 checks passed. The plan is architecturally sound, correctly scoped to 2 files, JS-compliant, and arithmetically verified. No violations were found against the Jane Street Rules Catalog or NT8 API constraints.
