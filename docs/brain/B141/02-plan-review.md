# B141 Plan Review — Cycle 2

**Reviewer**: ptt-plan-reviewer
**Block**: B141
**Cycle**: REVIEW CYCLE 2 (re-review after architect revision)
**Plan file**: `docs/brain/B141/02-architecture-plan.md` (Revision Cycle 1)
**Date**: 2026-09-01
**Prior verdict**: REVIEW_FAIL (CYC counting inconsistency — catch/&& convention undocumented)

---

## Mandatory Source Reads Completed

| File | Range | Result |
|------|-------|--------|
| `docs/brain/B141/02-architecture-plan.md` | full | READ |
| `docs/standards/jane-street/RULES_CATALOG.md` | full | READ |
| `src/PropTraderTools/CopyEngine.cs` | L2240-2360 | READ |

---

## FOCUS AREA: CYC Counting Convention Verification

### Check CYC-1: Convention grounded in L2250 actual source comment

**Plan claims** (Section 5.1): The L2250 comment reads:
> *"CYC=7: fo null(1), price delta(2), ATM STP(3), ATM TGT(3b), IsTrailingStop(4), isStop branch(5)"*
> — this comment omits the catch at L2313, confirming catch=0; and uses only the `if` keyword for `&&`-containing conditions, confirming `&&`=0.

**Actual source L2250**:
```
// DW-B134/DW-B137: CYC=7: fo null(1), price delta(2), ATM STP(3), ATM TGT(3b), IsTrailingStop(4), isStop branch(5), [CYC from branching=7].
```

**Verdict**: MATCH. The actual comment lists exactly 6 named branches (1, 2, 3, 3b, 4, 5) and states CYC=7.
- The `&&`-containing conditions at L2281, L2286, L2292 are each labeled with a single branch number (not doubled), confirming `&&`=0.
- The `catch` at L2313 is not listed in the comment, confirming `catch`=0.
- Plan quotation is accurate and the deduction is logically correct.

**RESULT**: ✅ PASS

---

### Check CYC-2: Convention grounded in L2327 actual source comment

**Plan claims** (Section 5.1): The L2327 comment reads:
> *"exception handlers add 0 McCabe branches each (per codebase convention)"*

**Actual source L2327**:
```
// Two independent try/catch blocks -- exception handlers add 0 McCabe branches each (per codebase convention L2301).
```

**Verdict**: MATCH. The actual comment at L2327 explicitly states catch=0 as the codebase convention. The plan's citation is accurate.

**RESULT**: ✅ PASS

---

### Check CYC-3: `SyncFollowerBracket` baseline = CYC 7 (pre-B141)

**Source branches enumerated from L2254-2317**:

| # | Branch | Source line |
|---|--------|-------------|
| base | — | — |
| 1 | `if (fo == null)` | L2269 |
| 2 | `if (Math.Abs(newPrice - currentPrice) < tickSize)` | L2273 |
| 3 | `if (isStop && IsAtmSTPOrder(fo))` — `&&` not counted | L2281 |
| 3b | `if (!isStop && IsAtmSTPOrder(fo))` — `&&` not counted | L2286 |
| 4 | `if (isStop && IsTrailingStop(fo))` — `&&` not counted | L2292 |
| 5 | `if (isStop)` inside try block | L2300 |
| — | `catch (Exception ex)` | L2313 — 0 per convention |

**Count**: base(1) + 6 branches = **CYC 7**. Matches L2250 comment exactly.

**RESULT**: ✅ PASS — baseline CYC 7 confirmed in source.

---

### Check CYC-4: `SyncFollowerBracket` post-B141 = CYC 8 (PASS at limit)

Plan adds one branch inside the existing branch-3 body:
```csharp
if (capturedTargetPrice.HasValue)   // B141: +1
    ResubmitTargetAfterCascade(...);
```

**Post-B141 CYC**: 7 (baseline) + 1 (HasValue check) = **8**.
CYC 8 == JS-041 limit of 8. PASS.

**`leaderOrder` availability**: The actual `SyncFollowerBracket` signature at L2254-2260 includes `Order leaderOrder` as the second parameter. `leaderOrder` IS in scope in branch-3 body. Plan claim verified. ✅

**RESULT**: ✅ PASS — CYC 8, at limit, no violation.

---

### Check CYC-5: `CaptureLinkedTargetPrice` — CYC 4

Plan: base(1) + if(1) + foreach(1) + if(1) = CYC 4. `&&` inside conditions not counted.
Convention applied correctly. No branches exceed limit.

**RESULT**: ✅ PASS — CYC 4.

---

### Check CYC-6: `TryParseStopSuffix` — CYC 3

Plan: base(1) + if(1) + if(1) = CYC 3. `||` inside conditions not counted.
Convention applied correctly.

**RESULT**: ✅ PASS — CYC 3.

---

### Check CYC-7: `IsTargetOrderLive` — CYC 1

Plan: base(1). Pure boolean return expression; no `if`, no loop, no ternary. `||` not counted.

**RESULT**: ✅ PASS — CYC 1.

---

### Check CYC-8: `ResubmitTargetAfterCascade` — CYC 4

Plan: base(1) + foreach(1) + if(1) + if(1) = CYC 4. Two `catch` blocks = 0 each. All `&&` = 0.
Convention applied correctly.

**RESULT**: ✅ PASS — CYC 4.

---

## ORIGINAL CHECKS (re-verified from Cycle 1)

### Check LANE-1: Lane-Split Gate

All 4 questions answered. Q1=YES → single pipeline. Gate result stated: "SINGLE PIPELINE — one ticket (T1 only). No lane split."

**RESULT**: ✅ PASS

---

### Check SPEC-1: Dual-Resubmit approach addresses DW-B153

Section 1.3 describes the dual-resubmit approach: capture target price → accept cascade → resubmit PTT-TGT-Drag. DW-B153 re-closed in Section K.

**RESULT**: ✅ PASS

---

### Check SPEC-2: DW-B153 documented and closed

Section K: DW-B153 CLOSED (re-closed in B141). Section 11 closure summary present.

**RESULT**: ✅ PASS

---

### Check SPEC-3: DW-B154 documented

Section K: DW-B154 DOCUMENTED — `acc.Change()` confirmed no-op on ATM Stop brackets from AddOnBase. Full description and architecture implication present.

**RESULT**: ✅ PASS

---

### Check JS-1: JS-021 (no `lock()`) stated and addressed

Section 7 checklist includes JS-021. All five methods: "PASS — no lock". Section 4.3 `ResubmitTargetAfterCascade` Block A-Prime comment: "JS-021: no lock -- acc.Orders iteration safe on NT8 dispatch thread."

**RESULT**: ✅ PASS

---

### Check JS-2: JS-033 (no `async void`) stated and addressed

Section 7 checklist includes JS-033. All methods are synchronous or return `double?` / `bool` / `void`. No `async void` introduced.

**RESULT**: ✅ PASS

---

### Check JS-3: JS-002 (no reference `return null`) addressed

Section 7 includes JS-002. Plan explicitly notes: `CaptureLinkedTargetPrice` returns `double?` (nullable value type, not reference null). Section 4.2 includes the note: "`null` return from `CaptureLinkedTargetPrice` is `double?` (Nullable<double>) not a reference type — acceptable per JS-002 note."

The `suffix = null` in `TryParseStopSuffix` is an `out` parameter initializer, not a `return null` violation. No reference-type `return null` exists in any new or modified method.

**RESULT**: ✅ PASS

---

### Check JS-4: JS-001 (no `throw` in hot path) addressed

Section 7 checklist includes JS-001. All new methods use try/catch with StatusUpdate absorption. No `throw` statement in any new or modified method.

**RESULT**: ✅ PASS

---

### Check JS-5: JS-041 (CYC <= 8) — all methods

Summary from Section 5.7:

| Method | Post-B141 CYC | Limit | Result |
|--------|---------------|-------|--------|
| `SyncFollowerBracket` | 8 | 8 | PASS |
| `CaptureLinkedTargetPrice` | 4 | 8 | PASS |
| `TryParseStopSuffix` | 3 | 8 | PASS |
| `IsTargetOrderLive` | 1 | 8 | PASS |
| `ResubmitTargetAfterCascade` | 4 | 8 | PASS |

**RESULT**: ✅ PASS — zero JS-041 violations.

---

### Check TEST-1: 7 xUnit `[Fact]` tests listed

Section 6 lists exactly 7 [Fact] tests:
- T_B141_01 through T_B141_07
- Framework stated: "xUnit only (JS mandate — NEVER NUnit or MSTest)"
- Test file: `tests/PropTraderTools.Tests/B141Tests.cs`

**RESULT**: ✅ PASS

---

### Check TEST-2: Tests cover regression contract

T_B141_07 explicitly tests that `SyncAtmFollowerBracket` is called in BOTH scenarios (target found and target absent). Regression guard present.

T_B141_04 covers cancelled target → `CaptureLinkedTargetPrice` returns null → no resubmit.
T_B141_06 covers absent target → no `CreateOrder` call.

**RESULT**: ✅ PASS

---

### Check NT8-1: NT8 API references grounded

Section 2 NT8 API facts table:
- `acc.Change()` no-op on ATM Stop brackets: confirmed SIM Gate 1 FAIL (fd4a439d) ✅
- `acc.Cancel()` OCO cascade: confirmed SIM log ✅
- `CreateOrder()` 12-param signature: NT8_FULL_REFERENCE.md L2106 ✅
- `CreateOrder()` arg12 `(NinjaTrader.Cbi.CustomOrder)null`: NT8_ADDON_KNOWLEDGE.md L262; NT8-007 ✅
- `acc.Orders` returns `IEnumerable<Order>`: NT8_ADDON_KNOWLEDGE.md L219 ✅
- `AtmStrategyCreate()` StrategyBase-only: NT8_ADDON_KNOWLEDGE.md ✅
- `AtmStrategyChangeStopTarget()` StrategyBase-only: NT8_ADDON_KNOWLEDGE.md ✅

No `AtmStrategyCreate` or `AtmStrategyChangeStopTarget` invocation in the plan's designed methods. ✅

**RESULT**: ✅ PASS

---

### Check NT8-2: PTT- prefix on all new orders

`ResubmitTargetAfterCascade` creates order with `name="PTT-TGT-Drag"`. PTT- prefix present.

**RESULT**: ✅ PASS

---

### Check NT8-3: No `DateTime.Now` (SCAN-06)

`ResubmitTargetAfterCascade` uses `NinjaTrader.Core.Globals.MaxDate` for the `gtd` parameter. No `DateTime.Now` anywhere in designed code.

**RESULT**: ✅ PASS

---

### Check NT8-4: No `async/await` in lifecycle methods

No `OnInitialize`, `OnDestroyed`, or `OnWindowCreated` methods modified. All new methods are synchronous private helpers.

**RESULT**: ✅ PASS

---

### Check NT8-5: No `Account.All` in constructor

No constructor code modified or added.

**RESULT**: ✅ PASS

---

### Check SCAN-1: No `lock()` (SCAN-01 zero requirement)

No `lock(` appears in any designed code block in the plan.

**RESULT**: ✅ PASS

---

### Check SCAN-2: No hardcoded `#RRGGBB` hex (SCAN-04)

No hex color strings appear in any designed code.

**RESULT**: ✅ PASS

---

### Check SCAN-3: No `FontFamily` override (SCAN-03)

No UI code. No `FontFamily` reference.

**RESULT**: ✅ PASS

---

### Check DEFER-1: Section K deferred work present

Section K is present with:
- DW-B153: CLOSED (re-closed)
- DW-B154: DOCUMENTED
- DW-B140-01/02/03: CLOSED (superseded)
- DW-B141-STP-CYC8-WALL: OPEN, Priority P1, architectural constraint documented
- Carried-forward items from B140-LaneA: 8 items tabulated

Section 11 provides a closure summary table.

**NOTE**: Phase 2 review does not require the Section K `| ID | Item | Priority | Target Block | Status |` table format (that format is Phase 5 / FINAL REVIEW only). Section K content is complete for Phase 2 purposes.

**RESULT**: ✅ PASS

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|-----------|--------------|
| Fix naked-position window on stop drag | YES | Section 1.3, 4.1 |
| Dual-resubmit: capture target price before cascade | YES | Section 4.2 |
| Dual-resubmit: resubmit PTT-TGT-Drag after cascade | YES | Section 4.3 |
| DW-B153 re-closed | YES | Section K |
| DW-B154 documented | YES | Section K |
| Block A-Prime: prevent PTT-TGT-Drag accumulation on consecutive drags | YES | Section 4.3, Gate 3 |
| SyncAtmFollowerBracket always called (regression contract) | YES | Section 4.1 invariants, T_B141_07 |
| Single file modification (CopyEngine.cs only) | YES | Section 9 |
| 7 xUnit [Fact] tests | YES | Section 6 |
| NT8 API: cancel+resubmit (not acc.Change) | YES | Section 2, 4.3 |
| CYC <= 8 all methods | YES | Section 5 |
| JS-021/033/002/001 stated | YES | Section 7 |
| SIM verification gates | YES | Section 10 |

**All spec requirements addressed.**

---

## Per-Item Violation Register

| # | Check | Violation? | Rule | Notes |
|---|-------|-----------|------|-------|
| CYC-1 | L2250 comment matches plan claim | ✅ NO | — | Exact match confirmed |
| CYC-2 | L2327 comment matches plan claim | ✅ NO | — | Exact match confirmed |
| CYC-3 | SyncFollowerBracket baseline = CYC 7 | ✅ NO | — | Confirmed in source |
| CYC-4 | SyncFollowerBracket post-B141 = CYC 8 | ✅ NO | JS-041 | 8 == limit, PASS |
| CYC-5 | CaptureLinkedTargetPrice CYC 4 | ✅ NO | — | Well under limit |
| CYC-6 | TryParseStopSuffix CYC 3 | ✅ NO | — | Well under limit |
| CYC-7 | IsTargetOrderLive CYC 1 | ✅ NO | — | Well under limit |
| CYC-8 | ResubmitTargetAfterCascade CYC 4 | ✅ NO | — | Well under limit |
| LANE-1 | Lane-Split Gate answered | ✅ NO | — | Single pipeline |
| SPEC-1/2/3 | Dual-resubmit + DW-B153/B154 | ✅ NO | — | All addressed |
| JS-1..4 | JS-021/033/002/001 | ✅ NO | — | All stated PASS |
| TEST-1/2 | 7 [Fact] tests, regression guard | ✅ NO | — | All present |
| NT8-1..5 | NT8 API facts, prefix, no DateTime.Now | ✅ NO | — | All clean |
| SCAN-1..3 | lock/hex/FontFamily scans | ✅ NO | — | Zero instances |
| DEFER-1 | Section K deferred work | ✅ NO | — | Present and complete |

**Total violations: 0**

---

## Final Verdict

**REVIEW_PASS**

Revision Cycle 1 resolved the sole P0 violation from Cycle 1 (undocumented CYC counting convention). The revised plan:

1. Documents the project CYC counting convention explicitly in Section 4.1 and Section 5.1, citing the exact source comments at L2250 and L2327.
2. Both cited comments have been verified against the actual source in this review — they match exactly.
3. All method CYC counts are consistent with the documented convention and confirmed to be <= 8.
4. `SyncFollowerBracket` baseline CYC 7 is confirmed by direct enumeration from source (L2269-L2313).
5. All original checks (Lane-Split Gate, spec compliance, JS-DNA, test plan, NT8 API facts, Section K) remain valid and pass.

Zero P0 violations. Zero P1 violations. Zero P2 violations.

**Gate: REVIEW_PASS — proceed to Phase 3 (ticket generation).**
