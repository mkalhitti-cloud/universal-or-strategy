# Ticket Review: PTT-COPIER-B25 Lane A
**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Source tickets**: `docs/brain/PTT-COPIER-B25-LANE-A/04-tickets.md`
**Plan reviewed against**: `docs/brain/PTT-COPIER-B25-LANE-A/02-architecture-plan.md` (REVIEW_PASS)
**Rules applied**: `docs/standards/jane-street/RULES_CATALOG.md`, `docs/standards/NT8_COMPILER_RULES.md`

---

## T1 — DW-B25-01: gate 4 StopLimit fix + IsStopLeg STP hardening

### 1. Traceability

**PASS**

- Ticket header explicitly cites **DW-B25-01** as the spec requirement ID.
- Architecture plan section 1 ("Block / Lane / Defect Summary") defines DW-B25-01 as:
  *"ATM bracket stops silently skipped by `MoveStopToBreakEven` due to gate 4 accepting only
  `OrderType.StopMarket` and `IsStopLeg` missing `STP` suffix arm."*
- All 3 edits (gate 4 fix, diagnostic log, `IsStopLeg` STP arm) trace directly to plan sections
  3.1, 3.2, and 3.3 respectively.
- No phantom work (items in ticket not in plan/spec).
- No missing work (all plan items are covered).

---

### 2. File Routing

**PASS**

Both paths are in the Wave workspace (`c:\WSGTA\universal-or-strategy\`):

| Role | Path |
|------|------|
| Source | `src/PropTraderTools/CopyEngine.cs` |
| Tests | `src/PropTraderTools/CopyEngineTests.cs` |

No path points to the Director workspace (`c:\WSGTA\universal-or-strategy-director\`). ✅

---

### 3. Edit 1 Completeness (gate 4 two-type OR)

**PASS**

Verbatim BEFORE/AFTER present. BEFORE shows single `OrderType.StopMarket` condition.
AFTER shows two-condition OR:

```csharp
if (order.OrderType != OrderType.StopMarket &&                             // (5)
    order.OrderType != OrderType.StopLimit)
    continue;
```

Comment explains rationale and cites `TightenStop` precedent. ✅

---

### 4. Edit 2 Completeness (diagnostic log placement)

**PASS**

Location specified as: inside the `try` block, AFTER the `IsTrailingStop` log line (~L1164),
BEFORE the `order.StopPrice` assignment. Placement is unambiguous. ✅

---

### 5. Edit 3 Completeness (IsStopLeg STP arm)

**PASS**

Verbatim BEFORE/AFTER present. AFTER adds:

```csharp
|| (order.Name != null && order.Name.EndsWith("STP", StringComparison.OrdinalIgnoreCase));
```

Null guard `order.Name != null` is present before `EndsWith`. ✅

---

### 6. Test Coverage

**PASS**

All 3 `[Fact]` methods specified with full names, scenarios, and assertions:

| Test | Method Name | Assert | Validates |
|------|-------------|--------|-----------|
| T_B25_01 | `T_B25_01_MoveStopToBreakEven_StopLimitBracket_MovesStop` | `Contains("StopLimit bracket stop -> acc.Change")` | Edit 1 + Edit 2 |
| T_B25_02 | `T_B25_02_MoveStopToBreakEven_StopMarket_StillPasses` | `Contains("BE moved to")` | Edit 1 regression |
| T_B25_03 | `T_B25_03_IsStopLeg_AtmSTPSuffix_ReturnsTrue` | `Assert.True(engine.IsStopLeg(order))` for `"12s Buy STP"` | Edit 3 |

Framework: xUnit `[Fact]` — NUnit/MSTest not used. ✅

---

### 7. JS Pre-Check

**PASS**

JS Rules Constraints table is present and complete:

| Rule | Status | Engineer Action Specified |
|------|--------|--------------------------|
| JS-021 (no `lock()`) | ✅ | Confirm `grep -r "lock(" src/PropTraderTools/` returns zero |
| JS-001 (no `throw` in hot path) | ✅ | Edits inside existing try/catch; no new `throw`; `?.Invoke` is null-safe |
| JS-002 (no `return null`) | ✅ | `IsStopLeg` returns `bool`; `MoveStopToBreakEven` returns `void` |
| JS-033 (no `async void`) | ✅ | No async methods introduced |

No concurrency violations (JS-021/023/025) described. No type-safety violations (JS-001/002) described.
No immutability violations (JS-008/009) described. ✅

---

### 8. NT8 Pre-Check

**PASS**

NT8-044 (`StringComparison.OrdinalIgnoreCase` requires `using System;`) explicitly addressed in
both the JS Rules Constraints table (last row) and the NT8 Compatibility Summary table:
*"SAFE — available since .NET 2.0; NT8 targets .NET 4.8. `using System;` confirmed present at
`CopyEngine.cs` file top (verified GREEN F5 baseline entering B25)."* ✅

Additional NT8 items verified in the NT8 Compatibility Summary:
- No `{ get; init; }` (NT8-001) — no new properties
- No `abstract record` / `sealed record` (NT8-002) — no new types
- No `volatile` (NT8-003) — no new fields
- No `ImmutableDictionary` (NT8-004) — no new collections
- No `async void` (NT8-033) — no new async methods
- No `sealed class TradeCopierWindow` — not in scope
- No `FontFamily` / hardcoded hex — not in scope

---

### 9. CYC Pre-Check

**PASS**

| Method | Before | After | Delta | Ceiling | Result |
|--------|--------|-------|-------|---------|--------|
| `IsStopLeg` | 2 | 3 | +1 | 8 | PASS |
| `MoveStopToBreakEven` | 6 | 7 | +1 | 8 | PASS |

Both methods remain at CYC ≤ 8 after edits. Arithmetic confirmed:
- `IsStopLeg`: 2 original branches + 1 new `||` clause = 3 ✅
- `MoveStopToBreakEven`: Edit 1 net-zero (one branch replaced by one branch); Edit 2 adds 1 `if` = 6+1 = 7 ✅

---

### 10. 7-Scan Checklist

**PASS**

All 7 scans present with grep commands and zero-match expectation:

| Scan | Pattern | Command | Expected |
|------|---------|---------|----------|
| SCAN-01 | `lock(` | `grep -rn "lock\s*(" src/PropTraderTools/` | Zero matches |
| SCAN-02 | `async void` | `grep -rn "async void " src/PropTraderTools/` | Zero matches |
| SCAN-03 | `FontFamily` | `grep -rn "FontFamily" src/PropTraderTools/` | Zero matches |
| SCAN-04 | Hardcoded hex | `grep -rn '"#[0-9A-Fa-f]\{6\}"' src/PropTraderTools/` | Zero matches |
| SCAN-05 | `CreateOrder` | `grep -rn "CreateOrder" src/PropTraderTools/` (verify PTT- prefix) | Zero bare names |
| SCAN-06 | `DateTime.Now` | `grep -rn "DateTime\.Now[^U]" src/PropTraderTools/` | Zero matches |
| SCAN-07 | `sealed class.*Window` | `grep -rn "sealed class.*Window" src/PropTraderTools/` | Zero matches |

Defense-in-depth confirmed: per-ticket contract (Layer 1) is present. ✅

---

### 11. Verification Criteria

**PASS**

All 6 verification checks present:

| # | Check | Method |
|---|-------|--------|
| V1 | `[Fact]` count = 131 (baseline 128 + 3) | `dotnet test` |
| V2 | `MoveStopToBreakEven` CYC = 7 | `python scripts/complexity_audit.py` |
| V3 | `IsStopLeg` CYC = 3 | `python scripts/complexity_audit.py` |
| V4 | Gate 4 two-condition form | `grep -A2 "OrderType.StopMarket" src/PropTraderTools/CopyEngine.cs` |
| V5 | F5 in NinjaTrader = GREEN | Manual F5 verification |
| V6 | Commit message specified | `B25 T1: DW-B25-01 gate4 StopLimit+IsStopLeg STP fallback +3 [Fact] 128->131` |

---

### 12. Scope Creep Check

**PASS**

Write-set is exactly:
- `src/PropTraderTools/CopyEngine.cs`
- `src/PropTraderTools/CopyEngineTests.cs`

No other files in scope. ✅

---

## VERDICT: T1

| Check | Result |
|-------|--------|
| 1. Traceability | ✅ PASS |
| 2. File Routing | ✅ PASS |
| 3. Edit 1 Completeness | ✅ PASS |
| 4. Edit 2 Completeness | ✅ PASS |
| 5. Edit 3 Completeness | ✅ PASS |
| 6. Test Coverage | ✅ PASS |
| 7. JS Pre-Check | ✅ PASS |
| 8. NT8 Pre-Check | ✅ PASS |
| 9. CYC Pre-Check | ✅ PASS |
| 10. 7-Scan Checklist | ✅ PASS |
| 11. Verification Criteria | ✅ PASS |
| 12. Scope Creep | ✅ PASS |

**T1 VERDICT: TICKET_REVIEW_PASS**

---

## Overall: TICKET_REVIEW_PASS

All tickets pass all checks. No violations found. No JS-XXX rule violations described.
No NT8 constraint violations described. No phantom or missing work. No scope creep.
Engineer may proceed.

**Approved by**: ptt-ticket-reviewer  
**Gate**: Phase 3.5 → Phase 4a cleared
