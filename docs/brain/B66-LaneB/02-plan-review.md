# B66-LaneB Plan Review

**Reviewer**: ptt-plan-reviewer
**Phase**: 2 (Plan Review)
**Date**: 2026-08-12
**Plan reviewed**: `docs/brain/B66-LaneB/02-architecture-plan.md`

---

## Gate Result: REVIEW_PASS

---

## Mandatory Read Confirmations

All 5 mandatory source reads completed before evaluation:

| File | Lines Read | Purpose |
|------|-----------|---------|
| `docs/brain/B66-LaneB/02-architecture-plan.md` | full | Plan under review |
| `docs/standards/jane-street/RULES_CATALOG.md` | full | JS-XXX rule enforcement |
| `docs/standards/NT8_FULL_REFERENCE.md` | 1710–1730 | NT8 race authority line 1721 |
| `src/PropTraderTools/CopyEngine.cs` | 440–500 | SubmitBeStop + ArmAllPendingBe bodies |
| `src/PropTraderTools/CopyEngine.cs` | 340–360 | RelayBe call site |
| `src/PropTraderTools/Features/PttGlobalBreakEven.cs` | 1–100 | Delegate field + constructors + ExecuteOne |
| `src/PropTraderTools/Core/PttContracts.cs` | 168–185 | BeEventArgs.IsLong confirmation |

---

## Checklist Results

### R-01: Bug diagnosis accuracy — PASS

**Evidence**:
- Source confirms the race: `CopyEngine.cs` line 461 re-reads `pos.MarketPosition` inside `SubmitBeStop` **after** `ArmAllPendingBe` already read it correctly at line 489. The plan accurately describes this.
- Plan cites `NT8_FULL_REFERENCE.md` line 1721 verbatim: *"Changes to positions will not be reflected till at least the next OnBarUpdate() event after an order fill."* — confirmed the text matches.
- B65 precedent (`TryDispatchLeaderFlat`) cited with correct file/line reference (CopyEngine.cs lines 651–654).

---

### R-02: All call sites covered — PASS

**Evidence** — all three call sites verified against actual source:

| # | File | Line | Current call | Source confirmed? |
|---|------|------|-------------|-------------------|
| 1 | `CopyEngine.cs` | 350 | `SubmitBeStop(acc, e.Instrument, e.BePrice)` | YES — source line 350 |
| 2 | `CopyEngine.cs` | 494 | `SubmitBeStop(acc, pos.Instrument, bePrice)` | YES — source line 494 |
| 3 | `PttGlobalBreakEven.cs` | 32 | `CopyEngine.Instance.SubmitBeStop(acc, instr, price)` | YES — source line 32 |

Plan's Call Site Map (plan section) lists all three correctly. No call site is missing.
Plan correctly notes `PttBreakEven.cs` has its own separate `SubmitBeStopLocal` (private) — not affected.

---

### R-03: PttGlobalBreakEven delegate chain correctness — PASS

**Evidence** — all four required sub-changes are present in the plan:

| Sub-change | Required | Plan location | Source baseline confirmed? |
|-----------|---------|--------------|--------------------------|
| `_submitBeStop` field: `Action<…,double>` → `Action<…,double,bool>` | YES | Change 4a | YES — source line 27 has 3-arg Action |
| Production ctor lambda: extend to 4-arg `(acc, instr, price, lng)` | YES | Change 4b | YES — source line 32 has 3-arg lambda |
| Test-injection ctor: parameter updated to 4-arg | YES | Change 4c | YES — source line 35 has 3-arg parameter |
| `ExecuteOne` call site: `_submitBeStop(…, isLong)` | YES | Change 4d | YES — source line 72 missing isLong; `isLong` already in scope at line 67 |

`BeEventArgs.IsLong` exists at `PttContracts.cs` line 173 — confirmed. Plan leverages it for RelayBe (Change 3).

---

### R-04: CYC constraint — PASS

**Evidence** — plan provides explicit branch count table for `SubmitBeStop` after fix:

| Branch | Decision | Count |
|--------|---------|-------|
| base | — | 1 |
| `if (acc == null \|\| instr == null)` | 1 decision (compound) | +1 |
| `foreach (pos in acc.Positions)` | loop | +1 |
| `if (p.Instrument == instr)` | inner if | +1 |
| `if (pos == null \|\| pos.Quantity == 0)` | 1 decision (compound) | +1 |
| `isLong ? Sell : BuyToCover` | ternary | +1 |
| `if (order != null)` | null check | +1 |
| **Total** | | **7** |

CYC = 7 ≤ 8. Independent reviewer verification: count matches plan's table. **PASS**.

All other changed methods (ArmAllPendingBe=4, RelayBe=2, ExecuteOne=4) are either unchanged or explicitly tabled. All ≤ 8.

---

### R-05: JS-DNA compliance — PASS

| Rule | Requirement | Plan compliance |
|------|------------|----------------|
| JS-021 | No `lock()` | SCAN-01: 0 hits in modified methods — stated and verified by plan |
| JS-001 | No `throw new` in hot paths | SCAN-02: 0 hits — try/catch swallow unchanged; no throw added |
| JS-002 | No `return null` | SCAN-03: 0 hits — all modified methods are `void` |
| JS-033 | No `async void` | All methods are synchronous `void` — stated in plan |
| Testing | xUnit `[Fact]` only | SCAN-05: bans NUnit/MSTest by design — PASS |
| Encoding | ASCII-only string literals | SCAN-06: all new strings verified ASCII — PASS |

No P0 violations unaddressed.

---

### R-06: Test coverage — PASS

**Evidence** — all 5 required tests present with exact required names and coverage:

| Name | Coverage | Present? |
|------|---------|---------|
| `T_B66_BE_01_LongPosition_SubmitsSellDirection` | Long → Sell | YES |
| `T_B66_BE_02_ShortPosition_SubmitsBuyToCoverDirection` | Short → BuyToCover | YES |
| `T_B66_BE_03_NullAccount_NoOrderSubmitted` | Null guard | YES |
| `T_B66_BE_04_ExecuteOne_PassesIsLongToDelegate` | ArmAllPendingBe forwarding (Long) via test-seam | YES |
| `T_B66_BE_05_RelayBe_ForwardsIsLongFromEvent` | RelayBe end-to-end | YES |

Framework: xUnit `[Fact]` only. No NUnit/MSTest. Test file: `src/PropTraderTools/Tests/B66Tests.cs` (new).

---

### R-07: No scope creep — PASS

**Evidence**:
- Fix scope is bounded to: `SubmitBeStop` signature (1 method), 2 call sites in `CopyEngine.cs` (RelayBe + ArmAllPendingBe), and 4 sub-changes in `PttGlobalBreakEven.cs` (delegate field + production ctor + test ctor + ExecuteOne call).
- Plan explicitly carries forward DW-B64-01 (`HandleEntryChange not firing`) and DW-B63-01 (spurious PTT-Copy bracket orders) as OPEN — no scope creep.
- `PttContracts.cs` is read-only for this block.
- `CopyEngineTests.cs` is untouched.
- No unrelated files listed in Affected Files table.

---

### R-08: NT8 API correctness — PASS

**Evidence** — SCAN-07 in plan verifies all 12 `CreateOrder` argument positions:

| Arg | Value | Type | Correct? |
|-----|-------|------|---------|
| 1 | `instr` | `Instrument` | YES |
| 2 | `dir` | `OrderAction` | YES — sourced from `isLong` param (same ternary, no position change) |
| 3 | `OrderType.StopMarket` | `OrderType` | YES |
| 4 | `OrderEntry.Manual` | `OrderEntry` | YES |
| 5 | `TimeInForce.Gtc` | `TimeInForce` | YES |
| 6 | `pos.Quantity` | `int` | YES |
| 7 | `0` | `double limitPrice` | YES — 0 for StopMarket |
| 8 | `bePrice` | `double stopPrice` | YES |
| 9 | `string.Empty` | `string oco` | YES |
| 10 | `"PTT-BE-Stop"` | `string name` (PTT-prefixed) | YES |
| 11 | `DateTime.MaxValue` | `DateTime gtd` | YES |
| 12 | `(CustomOrder)null` | `CustomOrder` | YES |

No argument positions changed. `limitPrice=0` (arg7) and `stopPrice=bePrice` (arg8) preserved exactly.

---

## Violations

**None.**

All 8 checklist items returned PASS. Zero P0/P1 rule violations found in the plan.

---

## Approval

**REVIEW_PASS: plan approved for ticket generation.**

The architecture plan for B66-LaneB correctly:
1. Diagnoses the `pos.MarketPosition` re-read race with NT8 authority citation and B65 precedent.
2. Identifies all three `SubmitBeStop` call sites in the codebase.
3. Covers the complete PttGlobalBreakEven delegate chain cascade (4 sub-changes).
4. Provides explicit CYC=7 branch count for the modified method (within limit 8).
5. Addresses all P0 JS-DNA rules (lock, throw, null, async void, xUnit, ASCII).
6. Plans exactly 5 xUnit tests (T_B66_BE_01 through T_B66_BE_05) with correct coverage.
7. Limits scope to the BE direction race fix — no unrelated changes.
8. Preserves the 12-arg CreateOrder signature with correct argument positions.
