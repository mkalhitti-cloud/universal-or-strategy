# B173 -- AtrWidget Live ATR Fix
# Ticket Review -- Phase 3.5
# Author: ptt-ticket-reviewer
# Source tickets: docs/brain/B173/04-tickets.md
# Status: TICKET_REVIEW_PASS

---

## Evidence Read

| File | Purpose |
|------|---------|
| `docs/brain/B173/04-tickets.md` | Tickets under review |
| `docs/brain/B173/02-architecture-plan.md` | Plan (REVIEW_PASS) -- traceability anchor |
| `docs/brain/B173/02-plan-review.md` | Plan review (REVIEW_PASS) -- confirms spec requirements B173-R1..R6 |
| `src/PropTraderTools/AtrSizingEngine.cs` | Ground truth: file to be deleted; confirms root cause |
| `src/PropTraderTools/TradeCopierAddOn.cs` lines 390-415 | Ground truth: WireNewPanel lines 396-399 |
| `src/PropTraderTools/TradeCopierPanel.cs` lines 3060-3300 | Ground truth: AtrWidget current implementation |
| `src/PropTraderTools/CopyEngineTests.cs` lines 10200-10222 | Ground truth: B172AtrWidgetTests closing brace + final namespace brace |

---

## Ticket Review

---

### T1 -- Create AtrCalc.cs + Append B173AtrCalcTests to CopyEngineTests.cs

**Spec requirements satisfied:** B173-R1 (AtrCalc pure static helper), B173-R2 (tests for pure math overload)

#### Traceability
**PASS**

All work in T1 maps directly to plan sections:
- `AtrCalc.cs` (new file with both Compute overloads) -- plan Section 2
- `B173AtrCalcTests` (append to CopyEngineTests.cs) -- plan Section 5

No phantom work. No plan items missing from T1's scope.

#### JS Pre-Check
**PASS**

| Rule | Finding |
|------|---------|
| JS-001 (no throw) | Both `Compute` overloads wrap the entire body in `try { ... } catch (Exception) { return 0.0; }`. No `throw`, no rethrow. PASS. |
| JS-021 (no lock) | `AtrCalc` is a static class with no instance state. No `lock(` anywhere in the file. PASS. |
| JS-023 (volatile only bool/int) | Static class has no fields at all. No volatile double. PASS. |
| NT8-003 (_lastAtr not volatile) | No `_lastAtr` in AtrCalc.cs. Not applicable. PASS. |
| ASCII-only | All string literals and comments in the code block are ASCII-only. No Unicode characters present. PASS. |

#### CYC Pre-Check
**PASS**

| Method | Stated CYC | Limit | Branches Counted | Assessment |
|--------|-----------|-------|-----------------|------------|
| `Compute(double[], double[], double[], int)` | ≤ 6 | 8 | try/catch (+1), compound null guard (+1), period guard (+1), array-length guard (+1), for loop (+1) = 5-6 | PASS |
| `Compute(ChartControl, int)` | ≤ 5 | 8 | try/catch (+1), cc==null (+1), BarsArray null/count (+1), bars null/count (+1), for loop (+1) = 4-5 | PASS |

Both methods are well within the CYC=8 hard limit.

#### NT8 Constraints
**PASS**

- `AtrCalc` is `internal static class` -- no indicator subclass, no NinjaScript inheritance.
- NT8 API verification instruction for the `Compute(ChartControl, int)` overload is present as a prominent "ENGINEER NOTE -- NT8 API VERIFICATION REQUIRED BEFORE WRITING THIS METHOD" block (lines 108-118 of ticket code listing) with 6 enumerated steps including SCAN-05 as the acceptance gate. PASS.
- No `async`/`await` in any lifecycle method. PASS.
- No `DateTime.Now`. PASS.
- No hex color literals. PASS.

#### Test Coverage
**PASS**

| Test ID | Method Name | Expected Value | Pure-Array? | No [Skip]? |
|---------|-------------|---------------|-------------|------------|
| T-B173-T1-01 | `AtrCalc_Compute_ReturnsZero_WhenInsufficientBars` | `0.0` (explicit) | YES | YES |
| T-B173-T1-02 | `AtrCalc_Compute_CorrectValue_KnownBars` | `5.5 / 3.0` (explicit, hand-verified) | YES | YES |

Both tests use the pure-array overload only -- no NT8 runtime dependency. Expected values are
not left as TODO; both are computed and shown inline in the ticket. Test math for T1-02 verified:
TR[1]=1.5, TR[2]=2.0, TR[3]=2.0; ATR(3)=5.5/3. Correct.

T2 and T3 add no new public or internal methods (only removes and renames), so no additional
[Fact] tests are required for those tickets.

#### Scan Checklist
**PASS**

All 7 scans present in T1's checklist table:

| Scan | Present | Expected Result Stated |
|------|---------|----------------------|
| SCAN-01 `grep -r "lock(" src/PropTraderTools/` | YES | 0 matches |
| SCAN-02 `grep -r "volatile double" src/PropTraderTools/` | YES | 0 matches |
| SCAN-03 `grep -rP "[^\x00-\x7F]" src/PropTraderTools/` | YES | 0 matches |
| SCAN-04 `grep -rn "AtrSizingEngine" src/PropTraderTools/` | YES | > 0 expected (AtrSizingEngine still exists at T1; zero enforced at T3 only) |
| SCAN-05 `dotnet build ...PropTraderTools.csproj` | YES | 0 errors |
| SCAN-06 `dotnet test ...PropTraderTools.csproj` | YES | all pass |
| SCAN-07 ptt-sync.ps1 | YES | N/A / skip (ptt-sync required at T3 only; T1/T2 may omit -- per role definition rule) |

SCAN-04 "expected > 0 at T1" note is correct: `AtrSizingEngine.cs` and its references in
`TradeCopierPanel.cs` and `TradeCopierAddOn.cs` still exist at T1. Terminal zero-match enforcement
deferred to T3 is the correct contract.

#### File Routing
**PASS**

- `src/PropTraderTools/AtrCalc.cs` -- Wave workspace. PASS.
- `src/PropTraderTools/CopyEngineTests.cs` -- Wave workspace. PASS.

#### VERDICT: TICKET_REVIEW_PASS

---

### T2 -- TradeCopierPanel.cs Surgical Edit (AtrWidget + outer class)

**Spec requirements satisfied:** B173-R3 (replace _provider pattern with _chartControl + AtrCalc)

#### Traceability
**PASS**

All 4 changes (2A-1 through 2A-4 inner class, 2B-1 outer class) map directly to plan Section 3a and 3b.
Removal targets verified against source ground truth:
- Line 3168 `private AtrSizingEngine _provider;` -- confirmed in source. ✓
- Lines 3207-3226 `SetProvider(AtrSizingEngine engine)` -- confirmed in source (lines 3207-3226). ✓
- Line 3238 `_provider = null;` in `Detach()` -- confirmed in source. ✓
- Lines 3241-3261 `OnTimerTick` body -- confirmed in source. ✓
- Lines 3077-3082 `SetAtrProvider(AtrSizingEngine engine)` in outer class -- confirmed in source. ✓

No phantom work. No plan items missing.

#### JS Pre-Check
**PASS**

| Rule | Finding |
|------|---------|
| JS-001 (no throw) | New `OnTimerTick` has no try/catch -- explicitly permitted because `AtrCalc.Compute` is JS-001 safe (absorbs all exceptions per T1 contract). No new throw sites anywhere in T2 changes. PASS. |
| JS-021 (no lock) | No `lock(` in any changed method: `SetChartControl`, `Detach`, `OnTimerTick`, `SetAtrChartControl`. PASS. |
| JS-023 (volatile only bool/int) | `_hasData volatile bool` retained unchanged. `_chartControl` declared non-volatile (UI-thread-only). `_lastAtr` non-volatile. No `volatile double` introduced. PASS. |
| NT8-003 (_lastAtr not volatile) | T2 post-edit verification explicitly states "_lastAtr remains non-volatile double (field at line ~3165 -- do NOT add volatile). NT8-003 PASS." Instruction is a do-not-change constraint, not a new addition. PASS. |
| ASCII-only | All replacement code blocks contain only ASCII characters. PASS. |

#### CYC Pre-Check
**PASS**

| Method | Stated CYC | Limit | Assessment |
|--------|-----------|-------|------------|
| `SetChartControl(ChartControl)` | 2 | 8 | null guard + timer create-once = 2. PASS. |
| `Detach()` | 2 | 8 | null timer guard + null clear = 2. PASS. |
| `OnTimerTick` (new) | 3 | 8 | null guard + atr>0 branch + _hasData guard = 3. Improved from prior CYC=6. PASS. |
| `SetAtrChartControl(ChartControl)` | 1 | 8 | Straight-line delegation. PASS. |

All methods ≤ 6, well under CYC=8 limit.

#### NT8 Constraints
**PASS**

- No indicator subclass in any changed code. PASS.
- `ChartControl` obtained via existing `FindVisualChild<ChartControl>` (used in T3 WireNewPanel, not T2 directly). T2 only stores the ref passed in. PASS.
- `using NinjaTrader.Gui.Chart;` confirmed at line 109 -- `ChartControl` unqualified name is valid. PASS.
- No `async`/`await`. No `DateTime.Now`. No hex color. No `FontFamily`. PASS.

#### Test Coverage
**PASS**

T2 removes and renames existing methods; it adds no new public or internal methods requiring
independent test coverage. The new `OnTimerTick` logic is covered implicitly by the integration
with `AtrCalc.Compute` (tested in T1). No [Fact] gaps.

#### Scan Checklist
**PASS**

All 7 scans present in T2's checklist table:

| Scan | Present | Expected Result Stated |
|------|---------|----------------------|
| SCAN-01 `grep -r "lock(" src/PropTraderTools/` | YES | 0 matches |
| SCAN-02 `grep -r "volatile double" src/PropTraderTools/` | YES | 0 matches |
| SCAN-03 `grep -rP "[^\x00-\x7F]" src/PropTraderTools/` | YES | 0 matches |
| SCAN-04 `grep -rn "AtrSizingEngine" src/PropTraderTools/` | YES | > 0 expected (TradeCopierAddOn.cs still references it; zero only at T3) |
| SCAN-05 `dotnet build ...PropTraderTools.csproj` | YES | 0 errors (depends on T1) |
| SCAN-06 `dotnet test ...PropTraderTools.csproj` | YES | all pass |
| SCAN-07 ptt-sync.ps1 | YES | N/A / skip (ptt-sync required at T3 only; T1/T2 may omit -- per role definition rule) |

SCAN-04 note at T2 is correct: `TradeCopierAddOn.cs` still instantiates `AtrSizingEngine` at T2.

#### File Routing
**PASS**

- `src/PropTraderTools/TradeCopierPanel.cs` -- Wave workspace. PASS.

#### VERDICT: TICKET_REVIEW_PASS

---

### T3 -- TradeCopierAddOn.cs WireNewPanel + Delete AtrSizingEngine.cs + ptt-sync

**Spec requirements satisfied:** B173-R4 (WireNewPanel wires ChartControl), B173-R5 (AtrSizingEngine deleted), B173-R6 (DW-B172-01 root cause eliminated)

#### Traceability
**PASS**

All 3 changes (3A WireNewPanel edit, 3B file delete, 3C ptt-sync) map directly to plan Section 4,
Section 1 (delete), and Section 11 T3 (ptt-sync).

Removal targets verified against source ground truth:
- Line 397 `var engine = new AtrSizingEngine();` -- confirmed in source. ✓
- Line 398 `panel.SetAtrProvider(engine);` -- confirmed in source. ✓
- Line 396 `var instr = TrySetPanelInstrument(...)` and line 399 `panel.SetChart(chart)` -- confirmed
  unchanged in source; ticket explicitly marks them UNCHANGED. ✓
- `FindVisualChild<ChartControl>` is an existing method (confirmed by plan review at lines 492-503;
  no new helper needed). ✓

No phantom work. No plan items missing.

#### JS Pre-Check
**PASS**

| Rule | Finding |
|------|---------|
| JS-001 (no throw) | WireNewPanel: `var cc = FindVisualChild<ChartControl>(chart);` and `panel.SetAtrChartControl(cc);` are straight-line calls. No new throw sites. PASS. |
| JS-021 (no lock) | No lock() added anywhere. WireNewPanel already has no lock per plan. PASS. |
| JS-023 (volatile only bool/int) | No new fields introduced in T3. No volatile change. PASS. |
| NT8-003 | No fields added. Not applicable. PASS. |
| ASCII-only | `var cc = FindVisualChild<ChartControl>(chart);` and `panel.SetAtrChartControl(cc);` are ASCII-only. PASS. |

#### CYC Pre-Check
**PASS**

No new methods added to `TradeCopierAddOn.cs` by T3. WireNewPanel CCN remains CCN=5 (no branch
added; `cc` assignment is straight-line). Ticket explicitly states this. PASS.

#### NT8 Constraints
**PASS**

- `FindVisualChild<ChartControl>` is confirmed as the existing pattern on `TradeCopierAddOn`
  (lines 492-503). Using existing helper, not a new indicator subclass. PASS.
- Null path explicitly handled: `cc` may be null if visual tree not yet loaded;
  `SetChartControl(null)` safely stops the timer. Ticket notes this. PASS.
- No `async`/`await`. No `DateTime.Now`. No hex color. PASS.

#### Test Coverage
**PASS**

T3 adds no new public or internal methods. Net change is removal of 2 lines and addition of 2 lines
within an existing method (`WireNewPanel`). No [Fact] gaps.

#### Scan Checklist
**PASS**

All 7 scans present in T3's checklist table (all mandatory at T3):

| Scan | Present | Expected Result Stated |
|------|---------|----------------------|
| SCAN-01 `grep -r "lock(" src/PropTraderTools/` | YES | 0 matches |
| SCAN-02 `grep -r "volatile double" src/PropTraderTools/` | YES | 0 matches |
| SCAN-03 `grep -rP "[^\x00-\x7F]" src/PropTraderTools/` | YES | 0 matches |
| SCAN-04 `grep -rn "AtrSizingEngine" src/PropTraderTools/` | YES | **0 matches** (file deleted + all references removed; terminal gate) |
| SCAN-05 `dotnet build ...PropTraderTools.csproj` | YES | 0 errors |
| SCAN-06 `dotnet test ...PropTraderTools.csproj` | YES | all pass |
| SCAN-07 `powershell -File .\scripts\ptt-sync.ps1` | YES | 0 errors (hard link resync after file delete) |

T3 order of operations is correct: edit WireNewPanel (3A) → delete AtrSizingEngine.cs (3B) →
SCAN-05 build → SCAN-06 test → SCAN-04 grep → SCAN-07 ptt-sync. Build gate before sync is correct.

#### File Routing
**PASS**

- `src/PropTraderTools/TradeCopierAddOn.cs` -- Wave workspace. PASS.
- `src/PropTraderTools/AtrSizingEngine.cs` (DELETE) -- Wave workspace. PASS.
- `scripts/ptt-sync.ps1` (RUN, not edited) -- Wave workspace. PASS.

#### VERDICT: TICKET_REVIEW_PASS

---

## Additional Checks

### Sequential Order Enforcement
**PASS**

Dependency header states: "T1 --> T2 --> T3 (strictly sequential; no parallel execution)".
Each ticket's Depends-on and Blocks fields are populated and form a clean linear chain.
No .cs file is shared across concurrent tickets.

### Period=14 Hardcoded
**PASS**

T2 post-edit verification explicitly states: "Period 14 is hardcoded in OnTimerTick call:
`AtrCalc.Compute(_chartControl, 14)`. No Period property added." Summary section confirms:
"Period 14 is hardcoded in OnTimerTick at `AtrCalc.Compute(_chartControl, 14)`. No Period field
or property is added anywhere." Both overloads accept `period` as a parameter for testability
only; no external property is exposed. PASS.

### ptt-sync.ps1 in Final Ticket (T3 only)
**PASS**

SCAN-07 with actual command `powershell -File .\scripts\ptt-sync.ps1` is present in T3's
checklist. Section 3C provides the full run context and explains why sync is required (file
deletion requires hard link resynchronization). T1 and T2 correctly skip SCAN-07 per the
role definition rule ("T3 only mandatory; T1/T2 may omit or include").

### Spec Coverage Aggregate (B173-R1 through R6)
**PASS**

| Requirement | Ticket | Coverage |
|-------------|--------|---------|
| B173-R1: AtrCalc pure static helper with both Compute overloads | T1 | ✓ Single coverage |
| B173-R2: Tests for pure math overload (T-B173-T1-01, T-B173-T1-02) | T1 | ✓ Single coverage |
| B173-R3: Replace _provider pattern with _chartControl + AtrCalc in TradeCopierPanel | T2 | ✓ Single coverage |
| B173-R4: WireNewPanel wires ChartControl (removes engine, adds cc) | T3 | ✓ Single coverage |
| B173-R5: AtrSizingEngine.cs deleted | T3 | ✓ Single coverage |
| B173-R6: DW-B172-01 root cause eliminated | T3 | ✓ Single coverage |

No uncovered requirements. No duplicate coverage.

---

## Violation List

**None.**

All tickets pass on all checks: traceability, JS pre-check, CYC pre-check, NT8 constraints,
test coverage, scan checklist presence, file routing, sequential order, ptt-sync placement,
and period=14 hardcoding.

---

## Overall: TICKET_REVIEW_PASS

All three tickets are cleared for Phase 4a (ptt-engineer).

The engineer reads this file first, then `docs/brain/B173/04-tickets.md`.
Execute in strict order: T1 → T2 → T3.
Each ticket's 7-scan checklist is the engineer's implementation contract.

*Review written: B173 Phase 3.5. Tickets cleared for ptt-engineer (Phase 4a).*
