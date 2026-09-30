# PTT-COPIER-B21-LANE-A -- Final Review
# Block:    PTT-COPIER-B21
# Lane:     A
# Defect:   DW-ATR-DEFAULTS-01
# Reviewer: ptt-plan-reviewer (Phase 5)
# Date:     2026-07-14

---

## §A  Block Summary

| Field | Value |
|-------|-------|
| Defect closed | `DW-ATR-DEFAULTS-01` (P1) — AtrSizingEngine field-initialiser defaults did not match the values that `TradeCopierAddOn.StartAtrEngine` configured immediately after construction |
| Files modified (production) | `AtrSizingEngine.cs`, `TradeCopierAddOn.cs` |
| Files modified (tests) | `CopyEngineTests.cs` |
| Files NOT modified | `TradeCopierPanel.cs`, `TradeCopierWindow.cs`, `CopyEngine.cs` |
| Tickets executed | 1 (T1) |
| [Fact] delta | 120 (pre-B21 baseline) → 121 (concurrent lane, `PopulateOrderMap_DedupGuard_B21`) → **122** (T1: `CalcContracts_DefaultValues_Use200Risk_075Fraction`) |
| VERIFY_PASS | 1 / 1 |
| BUILD_PASS | 1 / 1 (0 new errors; 3 pre-existing NT8-assembly errors unchanged) |

---

## §B  Coherence Check Results

| Check | Criterion | Evidence | Result |
|-------|-----------|----------|--------|
| A | Edits consistent across plan → ticket → completion → verification → source | Plan §2, ticket T1 Edits A/B/C, completion §Edit A/B/C, verification §Source Verification Items 1-4, and actual source all agree on identical literal values: `200.0` / `0.75`. No discrepancy found at any layer. | **PASS** |
| B | `SetParameters(200.0)` in `TradeCopierAddOn.cs` matches `_maxRiskDollars=200.0` in `AtrSizingEngine.cs` | Source-confirmed: `TradeCopierAddOn.cs:201` = `engine.SetParameters(200.0, pointValue);`; `AtrSizingEngine.cs:45` = `private double _maxRiskDollars  = 200.0;` | **PASS** |
| C | `SetAtrFraction(0.75)` in `TradeCopierAddOn.cs` matches `_atrFraction=0.75` in `AtrSizingEngine.cs` | Source-confirmed: `TradeCopierAddOn.cs:202` = `engine.SetAtrFraction(0.75);`; `AtrSizingEngine.cs:50` = `private double _atrFraction = 0.75;` | **PASS** |
| D | `[Fact]` present in source with exact name matching ticket spec | `CopyEngineTests.cs:2131-2132` confirmed: `[Fact]` attribute followed by `public void CalcContracts_DefaultValues_Use200Risk_075Fraction()` — exact match to spec name in plan §3 and ticket T1 §New [Fact] | **PASS** |
| E | All 7 scans zero (Layer 2 + Layer 3 agree) | Verification §3 Discrepancies table: all 7 scans show NONE discrepancy. Layer 3 independently confirms same result as Layer 2 for every scan. See §D below. | **PASS** |
| F | CYC ≤ 8 on all modified methods | `StartAtrEngine` = CYC 3 (verified: 2 null guards + 1 timer guard). New test = CYC 1. Field inits = CYC 1. No method touched or introduced exceeds CYC 8. | **PASS** |
| G | No new JS P0 violations | SCAN-01 lock() = 0 new. SCAN-03 async void = 0 new. SCAN-02 return null: 0 new (8 pre-existing `TradeCopierAddOn.cs` visual-tree helpers confirmed pre-existing and unchanged). | **PASS** |
| H | No new NT8 violations | SCAN-04 volatile double = 0 declarations (2 comment-only lines in AtrSizingEngine.cs explain the ban; not violations). SCAN-05 ImmutableDictionary = 0 in production files; pre-existing test usages not introduced by T1. NT8-001/002/007 all confirmed compliant. | **PASS** |
| I | xUnit `[Fact]` only — no NUnit/MSTest introduced | SCAN-06: 0 matches for `[Test]`, `[TestMethod]`, NUnit, MSTest in all 3 write-set files. `Assert.Equal(rhs, lhs)` is xUnit. | **PASS** |
| J | `[Fact]` count = 122 (120 baseline + 1 concurrent lane + 1 T1) | Verifier independently ran `(Select-String -Path "src\PropTraderTools\CopyEngineTests.cs" -Pattern "\[Fact\]").Count` → **122**. Accounting: pre-B21 baseline 120; `PopulateOrderMap_DedupGuard_B21_NameEqualityContract` (concurrent lane) added 1 = 121; T1 `CalcContracts_DefaultValues_Use200Risk_075Fraction` added 1 = 122. | **PASS** |

All 10 coherence checks: **PASS**.

---

## §C  Cross-File Consistency Verification

### Check B — `_maxRiskDollars` alignment

| Location | Value | Line | Confirmed |
|----------|-------|------|-----------|
| `AtrSizingEngine.cs` field initialiser | `200.0` | 45 | YES — source read |
| `TradeCopierAddOn.cs` `SetParameters` arg | `200.0` | 201 | YES — source read |
| Plan §2 Bug (a)/(c) | `200.0` | spec | YES |
| Ticket T1 Edit A / Edit C | `200.0` | contract | YES |
| Completion §Edit A / §Edit C | `200.0` | report | YES |
| Verification §Source Verification Item 1 / Item 3 | `200.0` | report | YES |

**Consistency: CONFIRMED — all layers agree on `200.0`.**

### Check C — `_atrFraction` alignment

| Location | Value | Line | Confirmed |
|----------|-------|------|-----------|
| `AtrSizingEngine.cs` field initialiser | `0.75` | 50 | YES — source read |
| `TradeCopierAddOn.cs` `SetAtrFraction` arg | `0.75` | 202 | YES — source read |
| Plan §2 Bug (b)/(c) | `0.75` | spec | YES |
| Ticket T1 Edit B / Edit C | `0.75` | contract | YES |
| Completion §Edit B / §Edit C | `0.75` | report | YES |
| Verification §Source Verification Item 2 / Item 3 | `0.75` | report | YES |

**Consistency: CONFIRMED — all layers agree on `0.75`.**

### Test body integrity

The `[Fact]` body at `CopyEngineTests.cs:2132-2157` was verified directly against the plan §3 test body plan and the ticket T1 §New [Fact] specification:
- Uses `new AtrSizingEngine()` (parameterless ctor, no seam ctor) ✓
- Uses `typeof(AtrSizingEngine).GetField(...)` directly (not the `CopyEngine`-bound helper) ✓
- Reads `_atrFraction` and `_maxRiskDollars` via `BindingFlags.NonPublic | BindingFlags.Instance` ✓
- Computes `lhs = AtrSizingEngine.CalcContracts(atrPoints * fraction, maxRisk, tickDollar)` ✓
- Computes `rhs = AtrSizingEngine.CalcContracts(atrPoints * 0.75, 200.0, tickDollar)` ✓
- Asserts `Assert.Equal(rhs, lhs)` ✓

Math confirmed: `floor(200.0 / (10.0 * 0.75 * 5.0)) = floor(200.0 / 37.5) = floor(5.333) = 5`.

---

## §D  7-Scan Final Status Table

All scans run and confirmed independently by verifier (Layer 3) against wave workspace
`c:\WSGTA\universal-or-strategy\src\PropTraderTools\`. Write-set: `AtrSizingEngine.cs`,
`TradeCopierAddOn.cs`, `CopyEngineTests.cs`.

| Scan | Rule | Pattern | Layer 2 (Engineer) | Layer 3 (Verifier) | Discrepancy | Status |
|------|------|---------|--------------------|--------------------|-------------|--------|
| SCAN-01 | JS-021 | `lock\s*\(` | 0 matches | 0 matches | NONE | **PASS** |
| SCAN-02 | JS-002 | `return null` | 0 in ATR/Tests; 8 pre-existing in AddOn (out of scope) | 0 in ATR/Tests; 8 pre-existing in AddOn (lines 470,479,489,499,518,531,537,546 — unchanged) | NONE | **PASS** |
| SCAN-03 | JS-033 | `async void` | 0 matches | 0 matches | NONE | **PASS** |
| SCAN-04 | NT8-003 | `volatile double` | 2 comment-only (ATR lines 13,49); no declarations | 2 comment-only (ATR lines 13,49); no declarations | NONE | **PASS** |
| SCAN-05 | NT8-004 | `ImmutableDictionary` | 0 in ATR/AddOn; pre-existing in Tests only | 0 in ATR/AddOn; 9 pre-existing in Tests (lines 482,511,541,612,640,684,712,827,865); 0 introduced by T1 | NONE | **PASS** |
| SCAN-06 | xUnit | `[Test]\|[TestMethod]\|NUnit\|MSTest` | 0 matches | 0 matches | NONE | **PASS** |
| SCAN-07 | Build | `dotnet build` | 3 pre-existing errors; 0 new from T1 | 3 pre-existing errors; 0 new from T1 | NONE | **PASS** |

**All 7 scans: PASS. Zero violations introduced by this lane.**

Pre-existing findings (scopes noted as out-of-lane in plan §4 SCAN-03 and ticket T1 §SCAN-03):
- `return null` (JS-002): 8 lines in `TradeCopierAddOn.cs` visual-tree helpers — pre-existing from prior blocks, not touched by T1. These are tracked in the backlog (DW-B19L2-DEFER-02 and similar are the appropriate resolution track).
- `ImmutableDictionary` (NT8-004): 9 usages in `CopyEngineTests.cs` test helpers — pre-existing from B12+, not introduced by T1.
- `dotnet build` errors: NT8 assembly (`NinjaTrader.NinjaScript.Indicators`, `Indicator`) not resolvable in standalone dotnet context; C# 8.0 nullable feature in `CopyEngine.cs:634` — all pre-existing, confirmed by `git stash` baseline test.

---

## §E  CYC Impact Table

| Method | File | CYC Before | CYC After | Delta | Compliant (≤8) |
|--------|------|-----------|----------|-------|----------------|
| `_maxRiskDollars` field init | `AtrSizingEngine.cs:45` | 1 | 1 | 0 | YES |
| `_atrFraction` field init | `AtrSizingEngine.cs:50` | 1 | 1 | 0 | YES |
| `StartAtrEngine` | `TradeCopierAddOn.cs:195` | 3 | 3 | 0 | YES |
| `CalcContracts_DefaultValues_Use200Risk_075Fraction` (new) | `CopyEngineTests.cs:2132` | — | 1 | +1 | YES |

`StartAtrEngine` CYC branches confirmed: (1) `if (chart == null)`, (2) `if (instr == null)`,
(3) `if (_atrPollTimer == null)` — the `engine.SetAtrFraction(0.75)` insertion is straight-line,
adding no branch. CYC unchanged at 3.

**No method exceeds CYC 8. Zero CYC violations.**

---

## §F  Issues and Discrepancies

### Issue 1 — [Fact] count discrepancy from ticket preamble

**Observation**: The ticket preamble stated `xUnit baseline entering this ticket: 120` and `xUnit count after ticket: 121`. The actual final count is **122**.

**Root cause**: A concurrent B21 lane (B21-LANE-B) had already committed `PopulateOrderMap_DedupGuard_B21_NameEqualityContract` (confirmed present at `CopyEngineTests.cs:2100-2129`) before Lane A executed, raising the count to 121. T1 added a second test, bringing the total to 122.

**Assessment**: This is expected behavior in parallel lane execution. The ticket's plan §3 "Key Notes" stated a final count of **121** (baseline 120 + 1), which was the planned-at-time-of-writing count. The discrepancy is a plan-authored-before-concurrent-lane issue, not an engineering error. The verifier confirmed 122 independently. The completion report §[Fact] Count correctly accounts for this discrepancy with its three-row table.

**Impact**: NONE — the test was correctly added; the count discrepancy is fully explained and documented. No violation.

### Issue 2 — Pre-existing `dotnet build` failures

**Observation**: `dotnet build` produces 3 errors (NT8 assembly missing; C# 8.0 feature).

**Assessment**: Pre-existing from prior blocks. Confirmed pre-existing by `git stash` baseline check (completion report §SCAN-07). T1 introduced 0 new errors. NT8-assembly dependency is an architectural constraint of the standalone build context — resolution requires the NinjaTrader runtime. This is tracked infrastructure, not a T1 defect.

**Impact**: NONE — BUILD_PASS is correctly reported as "no regression."

### No other issues or discrepancies found.

---

## §G  Section K — Deferred Work Ledger

Full cumulative ledger through B21-LANE-A. Prior 11 open items carry forward unchanged from
`docs/brain/PTT-COPIER-B20-LANE-A/06-deferred-backlog.md` Section 5. `DW-ATR-DEFAULTS-01` is
CLOSED by this block.

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B10-01 | Remove BuildDiagRow / OnDiagGap001d / OnDiagGap002 scaffolding | P2 | B11 | CLOSED (B11 T1) |
| DW-B10-02 | Add 3 missing AtrSizingEngine xUnit tests | P1 | B11 | CLOSED (B11 T2) |
| DW-B10-03 | TradeCopierWindow.cs Arm BE column | P2 | B11 | CLOSED (B11 T2) |
| DW-B10-04 | Update NT8_ADDON_KNOWLEDGE.md with T4 chart attachment result | P1 | B11 | CLOSED (B11 T1) |
| DW-B9-01 | ATR box visualization on chart canvas | P2 | B20/future | OPEN |
| DW-B9-03 | Click trader Bid+1/Ask-1 auto-offset | P3 | B20/future | OPEN |
| DW-B11-DEFER-01 | Convert Flatten/Trim keyboard shortcuts to Limit orders | P1 | B12 | CLOSED (B12 T1) |
| DW-B12-DEFER-01 | Full-panel mode expansion: Buy Ask / Sell Bid buttons | P2 | B20/future | OPEN |
| DW-B12-DEFER-02 | Auto-trail stop from BE CONNECTED level | P3 | B20/future | OPEN |
| DW-B12-DEFER-03 | Correct Math.Clamp ban comment attribution; add NT8-031 | P3 | B20/future | OPEN |
| DW-B12-DEFER-04 | Align CopyEngineTests.cs test names with ticket contract names | P3 | B20/future | OPEN |
| DW-B19-LIMIT-PRICE-01 | Fix limit exit price anchor Last -> Ask/Bid | P1 | B19-L2 | CLOSED (B19-L2 T1) |
| DW-B19L2-DEFER-01 | ExitBufferTicks value-object (JS-015) | P2 | B20/future | OPEN |
| DW-B19L2-DEFER-02 | Spread validation guard in GetAsk/GetBid | P2 | B20/future | OPEN |
| DW-B19L2-DEFER-03 | OnMarketData event hook to cache ask/bid in TradeCopierPanel | P2 | B20/future | OPEN |
| DW-B19L2-DEFER-04 | Telemetry: log anchor price at order placement | P3 | B20/future | OPEN |
| DW-B19-02 | PopulateOrderMap dedup guard: reference equality -> name equality | P2 | B20-LANE-A | CLOSED (B20-LANE-A T1) |
| DW-B17-SYNC-01 | CopyEnabledChanged event declaration and fire site in CopyEngine | P2 | B20-LANE-A | CLOSED (B20-LANE-A T2) |
| DW-B20-LANE-A-DEFER-01 | Lane C wiring: CopyEnabledChanged subscribers in TradeCopierPanel and TradeCopierWindow | P2 | B20-LANE-C/future | OPEN |
| DW-ATR-DEFAULTS-01 | AtrSizingEngine field-initialiser defaults misaligned with StartAtrEngine call-site | P1 | B21-LANE-A | **CLOSED (B21-LANE-A T1)** |

**Open items entering next block: 11** (DW-ATR-DEFAULTS-01 closed; 11 carry-forward items unchanged).

---

## §H  Verdict

**FINAL_PASS**

All 10 coherence checks pass. Cross-file consistency confirmed at source level. All 7 scans zero for
new violations. CYC ≤ 8 on all modified/introduced methods. No JS P0 violations. No NT8 violations.
xUnit `[Fact]` used exclusively. `[Fact]` count = 122, fully accounted for. Both issues logged in §F
are pre-existing infrastructure constraints or expected parallel-lane timing — neither constitutes a
rule violation or an engineering defect. Defect `DW-ATR-DEFAULTS-01` is correctly and completely closed.
Section K ledger present and complete. `06-deferred-backlog.md` written.
