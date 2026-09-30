# B35-LaneB Ticket Review
# Reviewer: ptt-ticket-reviewer
# Block: B35 | Lane: B | DW-B32-queue | 5x P0 BE Defects
# Input: docs/brain/B35-LaneB/04-tickets.md
# Plan: docs/brain/B35-LaneB/02-architecture-plan.md (REVIEW_PASS)
# Plan Review: docs/brain/B35-LaneB/02-plan-review.md (REVIEW_PASS)
# Spec: specs/002-trade-copier-spec.html id="section-b35" (lines 14408-14473)
# Rules: docs/standards/jane-street/RULES_CATALOG.md
#        docs/standards/NT8_COMPILER_RULES.md
# Date: 2026-07-23

---

## Ticket Review: B35-LaneB

---

### T1 — DW-B32-01b | IsStopAlreadyAtBe Short Branch Fix

**Traceability**: PASS
- Ticket maps to DW-B32-01b (spec line 14430-14432, plan Section B.1).
- No phantom work. No missing plan work.

**JS Pre-Check**: PASS
- JS-021: SCAN-01 confirms 0 `lock(` in IsStopAlreadyAtBe. No lock described.
- JS-002: Method returns `bool`, not null. PASS.
- JS-001: No throw in method body. PASS.
- JS-033: No async described. PASS.

**NT8 Check**: PASS
- NT8-046: IsStopAlreadyAtBe calls no `acc.Change()`. PASS.
- NT8-013/SCAN-04: No `DateTime.Now` in lines 602-617. PASS.
- NT8-031: No `OrderState.PendingSubmit` reference. PASS.

**CYC Pre-Check**: PASS
- SCAN-05: IsStopAlreadyAtBe CYC=2 (null guard + isLong branch). <= 8. PASS.

**Test Coverage**: PASS
- [Fact] `IsStopAlreadyAtBe_Short_ReturnsTrueWhenStopAtOrBelowEntry` is specified.
- Assert contract: null-guard returns false (both directions), signature is 3 params, returns bool.
- Behavioral short-direction logic is documented as requiring NT8 Order instantiation; structural
  contract via reflection is the appropriate substitute. PASS.
- Note: Test comment acknowledges that full behavioral path requires NT8 Order which cannot be
  instantiated in the unit test runner. Reflection-based approach is appropriate and consistent
  with existing test patterns in the file.

**Test Approach**: PASS
- Uses `BindingFlags.NonPublic | Static` via `GetMethod`. NT8 types passed as null-cast `(NinjaTrader.Cbi.Order)null`.
- No NT8 instance lifecycle required. PASS.

**Scan Checklist**: PASS
- SCAN-01 through SCAN-07 all present. PASS.

**File Routing**: PASS
- CopyEngine.cs targeted at `src/PropTraderTools/CopyEngine.cs`. Wave workspace path. PASS.
- CopyEngineTests.cs at same workspace path. PASS.

**VERDICT: TICKET_REVIEW_PASS**

---

### T2 — DW-B32-02 | MoveStopToBreakEven Accepted State Filter

**Traceability**: PASS
- Ticket maps to DW-B32-02 (spec line 14435-14437, plan Section B.2).
- No phantom work. No missing plan work.

**JS Pre-Check**: PASS
- JS-021: SCAN-01 confirms 0 `lock(` in MoveStopToBreakEven body. PASS.
- JS-002: Method is void, no null returns. PASS.
- JS-001: try/catch wraps acc.Change() — exception caught and logged, not propagated. PASS.
- JS-033: No async described. PASS.

**NT8 Check**: PASS
- NT8-046: SCAN-03 states acc.Change() is only reachable after IsAtmSlotName guard passes.
  ATM-owned stops are excluded. PASS.
- NT8-031: Fix explicitly uses `OrderState.Accepted` (not the banned PendingSubmit). PASS.
- NT8-013/SCAN-04: No `DateTime.Now` in lines 1476-1570. PASS.

**CYC Pre-Check**: PASS
- SCAN-05: MoveStopToBreakEven CYC=6. <= 8. PASS.

**Test Coverage**: FAIL
- [Fact] `MoveStopToBreakEven_IncludesAcceptedOrders_InStateFilter` is specified. Method name ✅.
- **VIOLATION**: Test body line 9 asserts:
  ```csharp
  Assert.Equal(typeof(NinjaTrader.NinjaScript.Instruments.Instrument), parms[1].ParameterType);
  ```
  The actual `MoveStopToBreakEven` signature is `(Account acc, Instrument instrument, int bufferTicks)`
  where `Instrument` is `NinjaTrader.Cbi.Instrument` (the Cbi namespace, confirmed by plan Section B.2
  and all existing tests in the file which use `NinjaTrader.Cbi.Account`).
  `NinjaTrader.NinjaScript.Instruments.Instrument` is a DIFFERENT type in a different namespace.
  This assertion will ALWAYS FAIL at `dotnet test` run-time, producing a false failure:
  `Assert.Equal() Failure: Expected: NinjaTrader.NinjaScript.Instruments.Instrument, Actual: NinjaTrader.Cbi.Instrument`
  **TICKET_REVIEW_FAIL** — Test contract is broken. Architect must correct to `typeof(NinjaTrader.Cbi.Instrument)`.

**Test Approach**: PASS (modulo the wrong type above)
- GetMethod with NonPublic|Instance is correct for a private instance method. PASS.

**Scan Checklist**: PASS
- SCAN-01 through SCAN-07 all present. PASS.

**File Routing**: PASS
- CopyEngine.cs targeted at `src/PropTraderTools/CopyEngine.cs`. PASS.

**VERDICT: TICKET_REVIEW_FAIL**
Violation: T2 [Fact] `parms[1].ParameterType` asserts `NinjaTrader.NinjaScript.Instruments.Instrument`
but the method parameter is `NinjaTrader.Cbi.Instrument`. This assertion fails at runtime.
Required fix: change `typeof(NinjaTrader.NinjaScript.Instruments.Instrument)` → `typeof(NinjaTrader.Cbi.Instrument)`.

---

### T3 — DW-B32-04b | BeState.Connected Removed (CS0117 Compile Fix)

**Traceability**: PASS
- Ticket maps to DW-B32-04b (spec line 14440-14442, plan Section B.3).
- TradeCopierPanel.cs scope is authorised — plan Section F and plan-review ADF-03 both confirm.
- No phantom work. No missing plan work.

**JS Pre-Check**: PASS
- JS-021: SCAN-01 confirms 0 `lock(` in OnBeUp, BeState area. PASS.
- JS-002: OnBeUp is void. PASS.
- JS-001: No throw described. PASS.
- JS-033: No async described. PASS.

**NT8 Check**: PASS
- NT8-046: OnBeUp calls no `acc.Change()`. PASS.
- NT8-013/SCAN-04: No `DateTime.Now` in changed lines. PASS.
- NT8-016: TradeCopierWindow not touched. PASS.

**CYC Pre-Check**: PASS
- SCAN-05: OnBeUp CYC=1. <= 8. PASS.

**Test Coverage**: PASS
- [Fact] `BeState_EnumHasExpectedValues` is specified.
- Assert contract: GetNestedType("BeState", NonPublic) not null, IsEnum, exactly 2 names,
  Contains("Idle"), Contains("Armed"), DoesNotContain("Connected") — CS0117 regression guard.
- All assertions are valid reflection-only operations; no NT8 lifecycle required. PASS.

**Test Approach**: PASS
- `typeof(TradeCopierPanel).GetNestedType("BeState", NonPublic)` + `Enum.GetNames`. Pure reflection. PASS.
- Qualification note in ticket: "Verify using/namespace declarations allow direct TradeCopierPanel
  reference; if not, use fully qualified name `PropTraderTools.TradeCopierPanel`." Appropriate advisory.

**Scan Checklist**: PASS
- SCAN-01 through SCAN-07 all present. PASS.

**File Routing**: PASS
- TradeCopierPanel.cs targeted at `src/PropTraderTools/TradeCopierPanel.cs`. PASS.

**VERDICT: TICKET_REVIEW_PASS**

---

### T4 — DW-B32-07 | IsAtmSlotName Guard in MoveStopToBreakEven

**Traceability**: PASS
- Ticket maps to DW-B32-07 (spec line 14445-14447, plan Section B.4).
- No phantom work. No missing plan work.

**JS Pre-Check**: PASS
- JS-021: SCAN-01 confirms 0 `lock(` in MoveStopToBreakEven body. PASS.
- JS-002: MoveStopToBreakEven is void. PASS.
- JS-001: No throw described. PASS.
- JS-033: No async described. PASS.

**NT8 Check**: PASS
- NT8-046: SCAN-03 explicitly confirms acc.Change() at ~line 1547 is ONLY reachable after
  IsAtmSlotName guard returns false (i.e., non-ATM order). ATM-owned stops (Stop1/Stop2) are
  skipped. PASS.
- NT8-013/SCAN-04: No `DateTime.Now` in lines 1520-1526. PASS.

**CYC Pre-Check**: PASS
- SCAN-05: MoveStopToBreakEven CYC=6, unchanged by comment-only insertion. <= 8. PASS.

**Test Coverage**: PASS
- [Fact] `MoveStopToBreakEven_SkipsNonAtmOrders_ViaIsAtmSlotNameGuard` is specified.
  (Spec name per ADF-01 advisory; plan used slightly different name — plan review
   documents the spec orchestrator prompt as the execution directive. PASS.)
- Assert contract: IsAtmSlotName is `internal static`, called directly without reflection.
  True for Stop1/Stop2/Target1/Target2. False for PTT-BE-Stop, PTT-Copy, null, "Stop" (no digit),
  "Target" (no digit). Covers the NT8-046 guard semantics directly. PASS.
- Note: No NT8 lifecycle or instance required. `CopyEngine.IsAtmSlotName` is internal static —
  directly callable from the test runner. This is the strongest possible test approach. PASS.

**Test Approach**: PASS
- Direct call to `internal static CopyEngine.IsAtmSlotName(name)`. No reflection required.
  Consistent with existing T_B32_01..04 and B34 IsAtmTargetName tests in the file. PASS.

**Scan Checklist**: PASS
- SCAN-01 through SCAN-07 all present. PASS.

**File Routing**: PASS
- CopyEngine.cs targeted at `src/PropTraderTools/CopyEngine.cs`. PASS.

**VERDICT: TICKET_REVIEW_PASS**

---

### T5 — DW-B32-08 | SubmitBeStop Unconditional in BreakEven Leader Path

**Traceability**: PASS
- Ticket maps to DW-B32-08 (spec line 14450-14452, plan Section B.5).
- Plan ADF-02 (plan-review advisory) documents architectural re-interpretation: spec describes
  the pre-B33 mechanism; working-tree uses `BreakEven -> SubmitBeStop` (B33 rewrite).
  The plan review accepted this as advisory (non-blocking). Ticket is consistent with plan. PASS.
- Build tag change in Ticket 5 Step 3 maps to spec line 14459. PASS.
- No phantom work. No missing plan work.

**JS Pre-Check**: PASS
- JS-021: SCAN-01 confirms 0 `lock(` in BreakEven(Account,Instrument,int) body. PASS.
- JS-002: BreakEven is void, returns early via plain `return;`. PASS.
- JS-001: No throw described. PASS.
- JS-033: No async described. PASS.

**NT8 Check**: PASS
- NT8-046: SCAN-03 confirms BreakEven(Account,Instrument,int) calls SubmitBeStop (creates new
  PTT-BE-Stop order via CreateOrder — not subject to NT8-046). PASS.
- NT8-013/SCAN-04: T5 SCAN-04 note — "DateTime.UtcNow in SubmitBeStop OCO-ID is existing code,
  not a changed line." NT8-013 bans DateTime.Now in CreateOrder expiry argument; DateTime.UtcNow
  used in a string comment/OCO-ID field is not in scope. PASS.
- NT8-049: SubmitBeStop is existing B33 code; ticket does not re-write it. If engineer inspects
  line 1754 only. No new CreateOrder call is introduced. PASS.

**CYC Pre-Check**: PASS
- SCAN-05: BreakEven(Account,Instrument,int) CYC=6. <= 8. PASS.

**Test Coverage**: FAIL
- [Fact] `BreakEven_WithOpenPosition_CallsSubmitBeStop_Unconditionally` is specified. Method name ✅.
- **VIOLATION**: Test body line 15 asserts:
  ```csharp
  Assert.Equal(typeof(NinjaTrader.NinjaScript.Instruments.Instrument), parms[1].ParameterType);
  ```
  The actual `BreakEven(Account leader, Instrument instrument, int bufferTicks)` signature uses
  `NinjaTrader.Cbi.Instrument` (the Cbi namespace). `NinjaTrader.NinjaScript.Instruments.Instrument`
  is a different type. This assertion will ALWAYS FAIL at `dotnet test` run-time.
- **SECOND VIOLATION (same test)**: Line 8 of test uses explicit type array to select the overload:
  ```csharp
  new[] { typeof(NinjaTrader.Cbi.Account), typeof(NinjaTrader.NinjaScript.Instruments.Instrument), typeof(int) }
  ```
  The `GetMethod` overload-resolution call uses the wrong Instrument namespace here too.
  `GetMethod` will return NULL because no overload with that exact signature exists.
  `Assert.NotNull(mi)` will FAIL immediately — the entire test fails before the parameter checks.
  **TICKET_REVIEW_FAIL** — The overload resolution itself is broken.

  Required fix: Replace ALL occurrences of `NinjaTrader.NinjaScript.Instruments.Instrument`
  with `NinjaTrader.Cbi.Instrument` in the T5 test block.

**Test Approach**: FAIL (due to wrong namespace — otherwise the reflection pattern is correct)
- GetMethod with NonPublic|Instance + explicit param type array is the correct approach for
  overload-specific reflection. FAIL due to wrong Instrument type.

**Scan Checklist**: PASS
- SCAN-01 through SCAN-07 all present. PASS.

**File Routing**: PASS
- CopyEngine.cs targeted at `src/PropTraderTools/CopyEngine.cs`. PASS.

**VERDICT: TICKET_REVIEW_FAIL**
Violation (A): T5 [Fact] `GetMethod(... new[] { ..., typeof(NinjaTrader.NinjaScript.Instruments.Instrument), ... })` 
  resolves to NULL (wrong namespace) — `Assert.NotNull(mi)` fails immediately.
Violation (B): T5 [Fact] `Assert.Equal(typeof(NinjaTrader.NinjaScript.Instruments.Instrument), parms[1].ParameterType)`
  — always fails even if GetMethod happened to succeed.
Required fix: Replace `typeof(NinjaTrader.NinjaScript.Instruments.Instrument)` →
  `typeof(NinjaTrader.Cbi.Instrument)` in ALL occurrences in T5 test body (both the overload
  resolver array and the Assert.Equal parameter type check).

---

## Advisories (Non-Blocking — Forward-Passed to Engineer)

### ADV-1 — Test Insertion Point Stale (T1-T5 in all tickets)

The ticket states "append BEFORE lines 2826-2827 (closing `}` + `}`)".
The orchestrator source baseline confirms LaneA has already landed: last [Fact] test is at
line 2859, and the file closes at lines 2882-2883 (not 2826-2827).

**Action (engineer)**: Append all 5 [Fact] tests AFTER line 2859 (end of last B35-LaneA test body),
BEFORE the closing `}\n}` at lines 2882-2883.

This is NOT a ticket rule violation — the instruction "append after the last test" is clear in
intent. The stale line numbers are a coordination artifact from the pre-LaneA ticket generation.

---

### ADV-2 — T5 SubmitBeStop Parameter Count Assert

T5 asserts: `Assert.Equal(3, submitBe.GetParameters().Length)` with comment "(Account, Instrument, double) -- NT8-049 qty removed".

If SubmitBeStop's actual signature differs from 3 params, this assertion will surface the
discrepancy at test time. This is useful regression coverage. No action needed.

---

## Overall Assessment

| Ticket | Traceability | JS Pre-Check | NT8 Check | CYC | Test Coverage | Test Approach | Scan Checklist | File Routing | Verdict |
|--------|-------------|-------------|----------|-----|--------------|--------------|----------------|-------------|---------|
| T1 (DW-B32-01b) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| T2 (DW-B32-02)  | PASS | PASS | PASS | PASS | **FAIL** | PASS | PASS | PASS | **FAIL** |
| T3 (DW-B32-04b) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| T4 (DW-B32-07)  | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| T5 (DW-B32-08)  | PASS | PASS | PASS | PASS | **FAIL** | **FAIL** | PASS | PASS | **FAIL** |

---

## Violations Summary

| Ticket | Section | Violation | Required Fix |
|--------|---------|-----------|-------------|
| T2 | [Fact] test body, parms[1] assertion | `typeof(NinjaTrader.NinjaScript.Instruments.Instrument)` used for `MoveStopToBreakEven` parameter 1; actual type is `NinjaTrader.Cbi.Instrument`. `dotnet test` will always FAIL this assertion. | Change to `typeof(NinjaTrader.Cbi.Instrument)` |
| T5 | [Fact] test body, GetMethod overload resolver | `NinjaTrader.NinjaScript.Instruments.Instrument` in explicit param type array passed to `GetMethod`. Overload resolution returns NULL → `Assert.NotNull(mi)` fails immediately. | Change to `typeof(NinjaTrader.Cbi.Instrument)` |
| T5 | [Fact] test body, parms[1] assertion | Same wrong namespace as T2. Always fails even if GetMethod somehow succeeded. | Change to `typeof(NinjaTrader.Cbi.Instrument)` |

---

## Overall: TICKET_REVIEW_FAIL

T2 and T5 contain broken [Fact] test assertions that will always fail at `dotnet test` run-time due
to wrong NT8 `Instrument` namespace. The correct type is `NinjaTrader.Cbi.Instrument`; the tickets
use `NinjaTrader.NinjaScript.Instruments.Instrument` (a different type that does not match the
method signatures). This breaks the engineer's test gate and the verifier's cross-check anchor.

**Return to ptt-architect for targeted fix:**
1. T2 test: change `typeof(NinjaTrader.NinjaScript.Instruments.Instrument)` → `typeof(NinjaTrader.Cbi.Instrument)` (1 occurrence)
2. T5 test: change `typeof(NinjaTrader.NinjaScript.Instruments.Instrument)` → `typeof(NinjaTrader.Cbi.Instrument)` (2 occurrences — overload resolver array and Assert.Equal)

T1, T3, T4 are TICKET_REVIEW_PASS and require no changes.


---

## CYCLE 2 — Re-Gate Review
# Reviewer: ptt-ticket-reviewer
# Block: B35 | Lane: B | Cycle: 2
# Input: docs/brain/B35-LaneB/04-tickets.md (corrected — 3 namespace fixes applied)
# Cycle 1 violations: T2 wrong Instrument namespace (1 occurrence), T5 wrong Instrument namespace (2 occurrences)
# Date: 2026-07-23

### Cycle 2 Scope

Cycle 1 returned TICKET_REVIEW_FAIL on two tickets only:
- **T2**: `parms[1].ParameterType` assertion used `NinjaTrader.NinjaScript.Instruments.Instrument` instead of `NinjaTrader.Cbi.Instrument`
- **T5**: GetMethod overload resolver array AND `parms[1].ParameterType` assertion both used `NinjaTrader.NinjaScript.Instruments.Instrument` instead of `NinjaTrader.Cbi.Instrument`

The corrected file was produced by surgical replacement of all 3 occurrences. Cycle 2 verifies:
1. All 3 violations are resolved
2. T1, T3, T4 are unchanged and still PASS
3. All 5 tickets still carry SCAN-01..07 checklists
4. Insertion point stale-line advisory (ADV-1) is resolved

---

### Fix Verification — 3 Namespace Occurrences

**Fix 1 — T2 `parms[1].ParameterType` assertion (line 223)**
```csharp
Assert.Equal(typeof(NinjaTrader.Cbi.Instrument), parms[1].ParameterType);
```
✅ CONFIRMED: Line 223 reads `typeof(NinjaTrader.Cbi.Instrument)`. Cycle 1 violation RESOLVED.

**Fix 2 — T5 GetMethod overload resolver array (line 585)**
```csharp
new[] { typeof(NinjaTrader.Cbi.Account), typeof(NinjaTrader.Cbi.Instrument), typeof(int) },
```
✅ CONFIRMED: Line 585 reads `typeof(NinjaTrader.Cbi.Instrument)`. Cycle 1 violation A RESOLVED.
`GetMethod` will now resolve to the correct 3-param overload. `Assert.NotNull(mi)` will PASS.

**Fix 3 — T5 `parms[1].ParameterType` assertion (line 592)**
```csharp
Assert.Equal(typeof(NinjaTrader.Cbi.Instrument),   parms[1].ParameterType);
```
✅ CONFIRMED: Line 592 reads `typeof(NinjaTrader.Cbi.Instrument)`. Cycle 1 violation B RESOLVED.

---

### T1 — DW-B32-01b | Cycle 2 Unchanged Check

Source baseline confirms T1 test body (lines 95-132) is IDENTICAL to cycle 1.
- `typeof(NinjaTrader.Cbi.Order)` at parms[0]: unchanged ✅
- Insertion point updated to "lines 2882-2883": ✅ (line 93: "BEFORE the closing `}\n}` on lines 2882-2883")
- SCAN-01..07 present at lines 136-144: ✅

**Cycle 2 Verdict: TICKET_REVIEW_PASS** (unchanged from cycle 1)

---

### T2 — DW-B32-02 | Cycle 2 Re-Gate

**Test Coverage (re-gated)**:
- [Fact] `MoveStopToBreakEven_IncludesAcceptedOrders_InStateFilter` ✅
- `parms[0].ParameterType`: `typeof(NinjaTrader.Cbi.Account)` ✅
- `parms[1].ParameterType`: `typeof(NinjaTrader.Cbi.Instrument)` ✅ (was `NinjaTrader.NinjaScript.Instruments.Instrument` — FIXED)
- `parms[2].ParameterType`: `typeof(int)` ✅
- `mi.ReturnType`: `typeof(void)` ✅
- No overload resolver array (uses single-name `GetMethod` without explicit param types — correct for unambiguous private instance method) ✅

Insertion point: "after line 2879, before lines 2882-2883" ✅
SCAN-01..07 present at lines 236-244: ✅

All other criteria unchanged from cycle 1 (Traceability PASS, JS Pre-Check PASS, NT8 Check PASS, CYC PASS, File Routing PASS).

**Cycle 2 Verdict: TICKET_REVIEW_PASS**

---

### T3 — DW-B32-04b | Cycle 2 Unchanged Check

Source baseline confirms T3 test body (lines 323-341) is IDENTICAL to cycle 1.
- No NT8 Instrument type references in this test (enum reflection only) ✅
- Insertion point updated to "lines 2882-2883": ✅ (line 321)
- SCAN-01..07 present at lines 351-359: ✅

**Cycle 2 Verdict: TICKET_REVIEW_PASS** (unchanged from cycle 1)

---

### T4 — DW-B32-07 | Cycle 2 Unchanged Check

Source baseline confirms T4 test body (lines 430-451) is IDENTICAL to cycle 1.
- No NT8 Instrument type references in this test (direct `CopyEngine.IsAtmSlotName` call) ✅
- Insertion point updated to "lines 2882-2883": ✅ (line 428)
- SCAN-01..07 present at lines 460-468: ✅

**Cycle 2 Verdict: TICKET_REVIEW_PASS** (unchanged from cycle 1)

---

### T5 — DW-B32-08 | Cycle 2 Re-Gate

**Test Coverage (re-gated)**:
- [Fact] `BreakEven_WithOpenPosition_CallsSubmitBeStop_Unconditionally` ✅
- GetMethod overload resolver array (line 584-586):
  `new[] { typeof(NinjaTrader.Cbi.Account), typeof(NinjaTrader.Cbi.Instrument), typeof(int) }` ✅
  (was `NinjaTrader.NinjaScript.Instruments.Instrument` — FIXED; `GetMethod` will now return non-null)
- `Assert.NotNull(mi)` will now PASS ✅
- `parms[0].ParameterType`: `typeof(NinjaTrader.Cbi.Account)` ✅
- `parms[1].ParameterType`: `typeof(NinjaTrader.Cbi.Instrument)` ✅ (was `NinjaTrader.NinjaScript.Instruments.Instrument` — FIXED)
- `parms[2].ParameterType`: `typeof(int)` ✅
- `mi.ReturnType`: `typeof(void)` ✅
- SubmitBeStop existence check: `Assert.NotNull(submitBe)` ✅
- SubmitBeStop param count: `Assert.Equal(3, submitBe.GetParameters().Length)` ✅

Insertion point: "after line 2879, before lines 2882-2883" ✅ (line 571)
SCAN-01..07 present at lines 607-615: ✅

All other criteria unchanged from cycle 1 (Traceability PASS, JS Pre-Check PASS, NT8 Check PASS, CYC PASS, File Routing PASS).

**Cycle 2 Verdict: TICKET_REVIEW_PASS**

---

### Advisory ADV-1 — Insertion Point Stale Line Numbers (Cycle 1)

Cycle 1 flagged a stale insertion point advisory (lines 2826-2827 used pre-LaneA line numbers).
The corrected file updates ALL insertion points across all 5 tickets to reference lines 2882-2883.

✅ RESOLVED: Every ticket now reads "after line 2879, before lines 2882-2883".
✅ TESTS INSERTION SUMMARY (lines 621-622) updated: "after line 2879 (end of last B35-LaneA test body), before lines 2882-2883".
✅ Resulting file structure diagram (lines 625-639) updated with correct line numbers.

ADV-1 is no longer advisory — it is fully resolved in the corrected file.

---

### Cycle 2 Summary Table

| Ticket | Cycle 1 Status | Cycle 2 Changes | Cycle 2 Verdict |
|--------|---------------|----------------|----------------|
| T1 (DW-B32-01b) | PASS | Insertion point updated (2826→2882). Test body unchanged. | **PASS** |
| T2 (DW-B32-02)  | **FAIL** (wrong namespace) | `parms[1]` assertion corrected to `NinjaTrader.Cbi.Instrument`. | **PASS** |
| T3 (DW-B32-04b) | PASS | Insertion point updated. Test body unchanged. | **PASS** |
| T4 (DW-B32-07)  | PASS | Insertion point updated. Test body unchanged. | **PASS** |
| T5 (DW-B32-08)  | **FAIL** (2x wrong namespace) | Overload resolver + parms[1] both corrected to `NinjaTrader.Cbi.Instrument`. | **PASS** |

All 5 tickets: SCAN-01..07 present ✅ | File routing correct ✅ | No new violations introduced ✅

---

## Cycle 2 Overall: TICKET_REVIEW_PASS

All cycle 1 violations are resolved. No new violations introduced by the corrections.
The 3 targeted namespace replacements (`NinjaTrader.NinjaScript.Instruments.Instrument` →
`NinjaTrader.Cbi.Instrument`) are the only changes from the cycle 1 draft, and all three
are confirmed correct against the source baseline (CopyEngine.cs uses `NinjaTrader.Cbi.Instrument`;
`CopyEngineTests.cs` has `using NinjaTrader.Cbi;` at line 8).

**TICKET_REVIEW_PASS — cleared for ptt-engineer execution.**
