# PTT-COPIER-B21-LANE-A — Ticket Review
# Block:    PTT-COPIER-B21
# Lane:     A
# Defect:   DW-ATR-DEFAULTS-01
# Reviewer: ptt-ticket-reviewer (Phase 3.5)
# Date:     2026-07-14

---

## Ticket Review: PTT-COPIER-B21-LANE-A

### T1 — Align AtrSizingEngine Defaults and Add Regression Test

---

#### Checklist

| # | Item | Result | Citation |
|---|------|--------|----------|
| 1 | Traceability: spec req ID `DW-ATR-DEFAULTS-01` referenced in ticket | **PASS** | T1 "Spec Requirement Satisfied" section cites `DW-ATR-DEFAULTS-01` explicitly |
| 2 | All 3 edits present with exact old→new literal values | **PASS** | Edit A: `_maxRiskDollars = 150.0` → `200.0` (line 45). Edit B: `_atrFraction = 1.0` → `0.75` (line 50). Edit C: `SetParameters(150.0, pointValue)` → `SetParameters(200.0, pointValue)` + `engine.SetAtrFraction(0.75)` inserted on next line. All three have exact old-code / new-code blocks. |
| 3 | `[Fact]` test name matches spec exactly | **PASS** | Ticket method signature: `CalcContracts_DefaultValues_Use200Risk_075Fraction` — exact match to spec. |
| 4 | `[Fact]` body uses xUnit `Assert.Equal`, not NUnit/MSTest | **PASS** | Test body uses `[Fact]` decorator and `Assert.Equal(rhs, lhs)`. No `[Test]`, `[TestMethod]`, NUnit, or MSTest reference anywhere in the method. |
| 5 | 7-scan checklist present with all 7 scans | **PASS** | SCAN-01 (lock), SCAN-02 (async void), SCAN-03 (return null), SCAN-04 (volatile double), SCAN-05 (ImmutableDictionary), SCAN-06 (CYC), SCAN-07 (NUnit/MSTest) — all 7 present with PowerShell commands and expected results. |
| 6 | Write-set limited to 3 files (`AtrSizingEngine.cs`, `TradeCopierAddOn.cs`, `CopyEngineTests.cs`) | **PASS** | Write-Set table lists exactly those 3 files with absolute wave-workspace paths. |
| 7 | JS pre-check: no `lock()`, `return null`, `async void` in proposed code | **PASS** | SCAN-01/02/03 each expect 0 matches. No proposed code block introduces any banned pattern. Pre-existing `return null` in `TradeCopierAddOn.cs` visual-tree helpers is explicitly noted as unchanged and out of scope. JS-021, JS-033, JS-002 compliant. |
| 8 | CYC pre-check: no method CYC > 8 introduced | **PASS** | SCAN-06 table: field-init CYC = 1, `StartAtrEngine` CYC = 3 (unchanged), new test CYC = 1. All modified/added methods ≤ 8. Jane Street strict standard met. |
| 9 | NT8-003: no `volatile double`; NT8-004: no `ImmutableDictionary` | **PASS** | SCAN-04 checks `volatile double` (0 matches expected); SCAN-05 checks `ImmutableDictionary\|System\.Collections\.Immutable` (0 matches expected). Both `_maxRiskDollars` and `_atrFraction` are declared as plain `double`. |
| 10 | xUnit baseline: 120 → 121 stated | **PASS** | Preamble: "xUnit baseline entering this ticket: 120 [Fact] tests. xUnit count after ticket: 121 [Fact] tests (net +1)." Success Criterion #6 repeats the count check with a PowerShell verification command. |
| 11 | Success criteria present and complete | **PASS** | 8-row Success Criteria table covers: all 3 edits (criteria 1–4), test presence (criterion 5), [Fact] count (criterion 6), 7-scan pass (criterion 7), `dotnet build` (criterion 8). |
| 12 | No `TradeCopierPanel.cs` in write-set | **PASS** | `TradeCopierPanel.cs` is explicitly listed under "DO NOT TOUCH" alongside `TradeCopierWindow.cs`, `CopyEngine.cs`, and any `.md` files. It does not appear in the Write-Set table. |

---

#### Traceability

PASS — Spec requirement `DW-ATR-DEFAULTS-01` is referenced at the top of T1 and all three bugs (a), (b), (c) from the plan §2 Change Inventory are mapped to Edit A, Edit B, and Edit C respectively. No phantom work detected. No plan item is uncovered.

---

#### JS Pre-Check

PASS — No `lock()` (JS-021), `return null` (JS-002), or `async void` (JS-033) introduced by any proposed code block. All three SCAN commands confirm expected 0 matches.

---

#### CYC Pre-Check

PASS — No method exceeds CYC = 8. `StartAtrEngine` holds at CYC = 3 after the straight-line `SetAtrFraction` insertion. New test holds at CYC = 1.

---

#### NT8 Check

PASS — NT8-003 (`volatile double`) and NT8-004 (`ImmutableDictionary`) explicitly checked. NT8-001, NT8-002, NT8-007 noted compliant in the architecture plan §5 NT8 Compiler Constraints table; no new records, init-only setters, or `CreateOrder` calls introduced.

---

#### Test Coverage

PASS — Every method changed or introduced has a corresponding `[Fact]`:
- `AtrSizingEngine` field initialisers: covered by `CalcContracts_DefaultValues_Use200Risk_075Fraction` (reads the fields via reflection and asserts they produce correct output).
- `StartAtrEngine` change: the same `[Fact]` provides regression coverage by verifying the defaults that the corrected call-site aligns to.
- No new public or internal methods introduced that lack a `[Fact]`.

---

#### Scan Checklist

PASS — All 7 scans (SCAN-01 through SCAN-07) are present in T1 with:
- PowerShell `Select-String` commands scoped to the write-set
- Expected result (0 matches or manual inspection guidance)
- Rationale per scan

Defense-in-depth layers intact: ticket contract (Layer 1) established for engineer attestation (Layer 2) and verifier cross-check (Layer 3).

---

#### File Routing

PASS — All `.cs` file paths point to the wave workspace:
`c:\WSGTA\universal-or-strategy\src\PropTraderTools\`
No Director workspace path (`c:\WSGTA\universal-or-strategy-director`) referenced for any `.cs` file.

---

#### VERDICT: TICKET_REVIEW_PASS

---

## Violations

None.

---

## Overall: TICKET_REVIEW_PASS

All 12 checklist items pass. T1 is coherent, minimal, and fully satisfies `DW-ATR-DEFAULTS-01`.
No Jane Street DNA violations, no NT8 violations, no missing scan items, no scope creep.
The engineer contract is complete. Phase 4a (ptt-engineer) is unblocked.
