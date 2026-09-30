# B40 Ticket Review

**Date**: 2026-07-30
**Reviewer**: ptt-ticket-reviewer
**Verdict**: TICKET_REVIEW_FAIL

---

## Checklist Results

| Check | T1 | T2 | T3 | Result |
|-------|----|----|-----|--------|
| Traceability — spec req mapped | PASS | PASS | **FAIL** | FAIL |
| Traceability — plan methods present | PASS | PASS | **FAIL** | FAIL |
| JS-021 no lock() | PASS | PASS | PASS | PASS |
| JS-033 no async void | PASS | PASS | PASS | PASS |
| JS-002 no return null | PASS | PASS | PASS | PASS |
| JS-001 no throw in hot paths | PASS | PASS | PASS | PASS |
| NT8-003 volatile int not double | PASS | PASS | N/A | PASS |
| NT8-001 no init properties | PASS | PASS | N/A | PASS |
| NT8-002 no record types | PASS | PASS | N/A | PASS |
| CYC: ArmAllPendingBe=5 ≤8 | PASS | N/A | N/A | PASS |
| CYC: IsPriceAlreadyAtBeForAccount=4 ≤8 | PASS | N/A | N/A | PASS |
| CYC: ComputeBePrice=2 ≤8 | PASS | N/A | N/A | PASS |
| CYC: IsPendingSlotsEmpty=1 ≤8 | PASS | N/A | N/A | PASS |
| CYC: UpdateBeAllVisuals=2, UpdateWindowBeAllVisuals=2 ≤8 | N/A | PASS | N/A | PASS |
| CYC: OnGlobalBeClick FSM stated and ≤8 | N/A | PASS (CYC=4) | N/A | PASS |
| acc.Get(AccountItem.BidPrice/AskPrice) used | PASS | N/A | N/A | PASS |
| Files: both CopyEngine.cs + PttGlobalBreakEven.cs | PASS | N/A | N/A | PASS |
| Files: both TradeCopierPanel.cs + TradeCopierWindow.cs | N/A | PASS | N/A | PASS |
| Files: CopyEngineTests.cs present | N/A | N/A | PASS | PASS |
| Build tag updated to B40 in T1 | PASS | N/A | N/A | PASS |
| T_B40_01–T_B40_12 all described with assert conditions | N/A | N/A | PASS | PASS |
| Baseline 202 → 214 stated in T3 | N/A | N/A | PASS | PASS |
| Positive AND negative boundary cases present | N/A | N/A | PASS | PASS |
| SCAN-01 through SCAN-07 all present in T1 | PASS | N/A | N/A | PASS |
| SCAN-01 through SCAN-07 all present in T2 | N/A | PASS | N/A | PASS |
| SCAN-01 through SCAN-07 all present in T3 | N/A | N/A | PASS | PASS |
| File routing: Wave workspace only | PASS | PASS | PASS | PASS |

---

## Violations

### VIOLATION-01 — TICKET_REVIEW_FAIL
**Ticket**: T3 — Tests T_B40_01–T_B40_12
**Check**: Traceability — plan items present in ticket
**Rule**: Traceability check: every plan method/test coverage item must appear in a ticket

**Violation**: Architecture plan Section 7 defines three test cases that are **entirely absent** from the ticket's 12 `[Fact]` definitions. The ticket replaces them with different tests without acknowledgment.

**Missing plan test T_B40_06** (Plan Section 7):
- Method under test: `ComputeBePrice`
- Required assertion: Long position, entry=100.0, buffer=2 ticks, tickSize=0.25 → result == 100.5
- Status in ticket T3: **NOT PRESENT**. No test for `ComputeBePrice` long case exists in ticket T3.

**Missing plan test T_B40_07** (Plan Section 7):
- Method under test: `ComputeBePrice`
- Required assertion: Short position, entry=100.0, buffer=2 ticks, tickSize=0.25 → result == 99.5
- Status in ticket T3: **NOT PRESENT**. No test for `ComputeBePrice` short case exists in ticket T3.

**Missing plan test T_B40_10** (Plan Section 7):
- Method under test: `SubmitBeStop` ocoOverride path
- Required assertion: When `ocoOverride = "PTT-BEG-00001-0"` is passed, the OCO ID in the CreateOrder call uses `"PTT-BEG-00001-0-0"` for pair 0 (verified via output capture or fake account)
- Status in ticket T3: **NOT PRESENT**. No test for `SubmitBeStop`'s `ocoOverride` parameter path exists in ticket T3.

**Impact**: `ComputeBePrice` is a pure static calculation with a tick-alignment contract. Without a direct test it cannot be verified against the formula stated in the plan. `SubmitBeStop ocoOverride` is the **primary OCO collision fix** (DW-B39-OCO-01) — the fix is in the string constructed inside `SubmitBeStop` when `ocoOverride` is non-null. Omitting this test means the P0 fix for DW-B39-OCO-01 has no direct verification.

**Resolution required**: Architect must add the following three tests to T3 (replacing or supplementing the current 12, adjusting numbering as needed):
1. A direct `ComputeBePrice` long test asserting result == 100.5.
2. A direct `ComputeBePrice` short test asserting result == 99.5.
3. A `SubmitBeStop` ocoOverride path test asserting the OCO ID string uses `ocoOverride + "-" + i` when `ocoOverride != null`.

---

### NOTE-01 — WARN (not FAIL)
**Ticket**: T1 and T3 — ComputeBePrice visibility
**Check**: Internal consistency across tickets

**Observation**: Ticket T1 declares `ComputeBePrice` as `private static double`. Ticket T3 Step 1 instructs the engineer to change it to `internal static double` to enable direct testing. The T1 method signature block still shows `private static`. This cross-ticket coordination note appears only in T3.

**Risk**: If the engineer executes T1 then T3 without noticing the T3 Step 1 visibility instruction, they will need to return to `CopyEngine.cs` to change the access modifier. This is a friction point, not a rules violation.

**Recommendation for architect**: Update T1 to declare `ComputeBePrice` as `internal static` from the start (matching what T3 requires), OR add a bold note to T1 Step 1 that the method must be `internal static` to support test coverage.

---

### NOTE-02 — WARN (not FAIL)
**Ticket**: T3 — Test numbering does not match plan
**Check**: Traceability

**Observation**: Architecture plan Section 7 defines T_B40_01–T_B40_12 in a specific order (T_B40_01 = all-flat test, T_B40_06 = ComputeBePrice long, T_B40_08 = BuildGlobalBeOcoId exact format, etc.). Ticket T3 reorders the test numbering: ticket T_B40_01 = BuildGlobalBeOcoId uniqueness test (plan's T_B40_09), ticket T_B40_03 = BuildGlobalBeOcoId exact format (plan's T_B40_08), etc.

**Risk**: Phase 5.V verifier cross-checking plan T_B40_NN against ticket T_B40_NN will find mismatches and require additional reconciliation. This is an audit overhead issue.

**Resolution**: The architect rewrite required by VIOLATION-01 should re-sequence tests to match the plan's numbering, or the plan's Section 7 table should be updated to match the ticket order.

---

## T1 Detailed Verdict

### T1 — Engine + OCO Fix
**Traceability**: PASS
- DW-B39-OCO-01 cited. DW-B39-BEHAVIOR-01 engine side cited. Plan Section 9 T1 followed.
- All 5 new CopyEngine.cs methods match plan Section 4 signatures exactly.
- PttGlobalBreakEven.cs: `BuildGlobalBeOcoId`, `_ocoSeq` field, `Execute()` body rewrite all match plan.
- `SubmitBeStop` optional `ocoOverride` parameter matches plan.
- Build tag update (`"PTT-COPIER B40 | be-all-armed-oco-fix | 2026-07-30"`) explicitly stated.

**JS Pre-Check**: PASS
- `private volatile int _beAllOcoSeq = 0` — volatile int, not double. NT8-003 PASS.
- `private volatile int _ocoSeq = 0` — volatile int, not double. NT8-003 PASS.
- `ArmAllPendingBe`: no lock(), uses ConcurrentDictionary. JS-021 PASS.
- `ArmAllPendingBe`: returns int, never null. JS-002 PASS.
- `ArmAllPendingBe`: `internal int` — not `async void`. JS-033 PASS.
- `SubmitBeStop` optional `string ocoOverride = null` — this is a default parameter value, not a returned null. JS-002: PASS (as explicitly noted in plan Section 6).
- No `throw new` in any new method. JS-001 PASS.

**CYC Pre-Check**: PASS
- IsPendingSlotsEmpty: CYC=1 (expression body). ≤8. PASS.
- ComputeBePrice: CYC=2 (base + ternary for direction). ≤8. PASS.
- IsPriceAlreadyAtBeForAccount: CYC=4 (null guard, refPx≤0 guard, isLong direction, ≥/≤ comparison). ≤8. PASS.
- ArmAllPendingBe: CYC=5 (Account.All foreach, acc.Positions foreach, IsFlat guard, IsPriceAlreadyAtBe branch, immediate/arm branch). ≤8. PASS.
- BuildGlobalBeOcoId: CYC=1. ≤8. PASS.

**NT8 Constraints**: PASS
- `acc.Get(AccountItem.BidPrice, pos.Instrument)` / `acc.Get(AccountItem.AskPrice, pos.Instrument)` — per-account API, not MarketData. PASS.
- No init properties. No record types. PASS.
- `Account.All` called from `ArmAllPendingBe` which is invoked from UI button click (post-NT8-init). NT8-021 PASS.

**Test Coverage**: PASS (tests deferred to T3 by design)
**7-Scan Checklist**: PASS — SCAN-01 through SCAN-07 all present.
**File Routing**: PASS — `c:\WSGTA\universal-or-strategy\src\PropTraderTools\` throughout.

**VERDICT: TICKET_REVIEW_PASS** *(T1 individually passes)*

---

## T2 Detailed Verdict

### T2 — UI Armed State Wiring
**Traceability**: PASS
- DW-B39-BEHAVIOR-01 (P1) UI side cited. Dependency on T1 stated explicitly. Plan Section 9 T2 followed.
- All new Panel methods match plan Section 4: `UpdateBeAllVisuals`, `_globalBeState` field, `OnGlobalBeClick` FSM, `OnPendingBeFiredDispatch` update, `Detach()` update.
- All new Window methods match plan Section 9 T2: `_windowGlobalBeState`, `OnWindowGlobalBeClick`, `OnWindowPendingBeFiredDispatch`, `UpdateWindowBeAllVisuals`.
- `WBrushCaution` and `WBrushPurple` confirmed existing (plan Section 4 notes `WBrushCaution` at Window line 65, `WBrushPurple` at line 69).

**JS Pre-Check**: PASS
- `OnGlobalBeClick(object sender, RoutedEventArgs e)` — synchronous `private void` event handler. Ticket explicitly notes: "synchronous void event handler — not async void." JS-033 PASS.
- `OnWindowGlobalBeClick` same pattern. JS-033 PASS.
- No lock() in any described method. JS-021 PASS.
- `UpdateBeAllVisuals` and `UpdateWindowBeAllVisuals` return void. No null returns. JS-002 PASS.
- No throw new. JS-001 PASS.

**CYC Pre-Check**: PASS
- UpdateBeAllVisuals: CYC=2 (null guard + ternary). ≤8. PASS.
- UpdateWindowBeAllVisuals: CYC=2. ≤8. PASS.
- OnGlobalBeClick: ticket states CYC=4 (plan stated CYC=3; ticket is the more careful count given the `if (Account.All != null)` guard). Both values ≤8. PASS.
- OnPendingBeFiredDispatch: CYC=2 (Dispatcher.InvokeAsync + if guard). ≤8. PASS.
- OnWindowPendingBeFiredDispatch: CYC=2. ≤8. PASS.

**NT8 Constraints**: PASS
- No volatile double. No init properties. No record types. PASS.
- `Dispatcher.InvokeAsync` used — confirmed as WPF UIElement Dispatcher (not NinjaTrader.Core.Globals.*). Already working in production at Panel line 758 per plan Section 6. PASS.
- `Account.All` access in disarm loop is from UI click handler / Detach() — post-NT8-init. NT8-021 PASS.
- `if (Account.All != null)` explicit null guard used instead of null-conditional compound assignment. NT8-043 PASS.

**Test Coverage**: PASS (tests deferred to T3 by design)
**7-Scan Checklist**: PASS — SCAN-01 through SCAN-07 all present.
**File Routing**: PASS — Wave workspace only.

**VERDICT: TICKET_REVIEW_PASS** *(T2 individually passes)*

---

## T3 Detailed Verdict

### T3 — Tests T_B40_01–T_B40_12
**Traceability**: **FAIL** — See VIOLATION-01 above.

Architecture plan Section 7 test coverage items T_B40_06 (ComputeBePrice long), T_B40_07 (ComputeBePrice short), and T_B40_10 (SubmitBeStop ocoOverride) are absent from ticket T3. These are not optional: `ComputeBePrice` is a pure calculation that must be directly verified, and the `SubmitBeStop ocoOverride` path is the literal code that fixes the P0 DW-B39-OCO-01 collision.

**JS Pre-Check**: PASS — No lock(), async void, return null, or throw new in any test method.
**CYC Pre-Check**: PASS — All [Fact] bodies are pure assertions. CYC=1 per test.
**NT8 Constraints**: N/A (test-only file).
**Test Count**: PASS — 12 tests described; 202 baseline → 214 total stated.
**Boundary Cases**: PASS — T_B40_09/T_B40_10 test long bid above/below threshold (positive and negative); T_B40_11/T_B40_12 test empty/non-empty slot states.
**7-Scan Checklist**: PASS — SCAN-01 through SCAN-07 all present. SCAN-06 correctly uses `dotnet test` (not `dotnet build`) with the "214/214 [Fact] passing" expected result.
**File Routing**: PASS — Wave workspace only.

**VERDICT: TICKET_REVIEW_FAIL** *(T3 fails on Traceability — 3 plan-required tests absent)*

---

## Gate Decision

**BLOCKED**

Return to ptt-architect for T3 rewrite. The architect must:

1. Add a direct `[Fact]` test for `ComputeBePrice` — **long** position (entry=100.0, buffer=2 ticks, tickSize=0.25 → expected result=100.5). Reference plan Section 7 T_B40_06.
2. Add a direct `[Fact]` test for `ComputeBePrice` — **short** position (entry=100.0, buffer=2 ticks, tickSize=0.25 → expected result=99.5). Reference plan Section 7 T_B40_07.
3. Add a direct `[Fact]` test for `SubmitBeStop ocoOverride` — when `ocoOverride = "PTT-BEG-00001-0"` is passed, the OCO ID constructed inside the per-pair loop is `"PTT-BEG-00001-0-0"` for pair 0. Reference plan Section 7 T_B40_10.
4. Update T1 to declare `ComputeBePrice` as `internal static` (not `private static`) to enable direct testing, and remove the retroactive T3 Step 1 instruction to change visibility post-T1 (NOTE-01).
5. Re-align ticket test numbering with plan Section 7 numbering, or update the plan's Section 7 table to match the ticket numbering (NOTE-02).

The overall `[Fact]` count after T3 will increase from 214 to 217 (202 + 15) if the three missing tests are added without removing any of the current 12.

---

*Reviewed by ptt-ticket-reviewer | Phase 3.5 | B40-LaneA | 2026-07-30*

---

## Rev 2 Review — B40-LaneA (Post-Fix)

**Date**: 2026-07-30
**Reviewer**: ptt-ticket-reviewer
**Cycle**: 2 of 2
**Prior verdict**: TICKET_REVIEW_FAIL (Rev 1 — T3 missing T_B40_06, T_B40_07, T_B40_10; ComputeBePrice declared private in T1; test count 214 not 217; NOTE-01 visibility cross-ticket friction; NOTE-02 numbering mismatch)

---

### Focused Violation Checks (Rev 1 FAIL items)

#### CHECK-1 — T_B40_06 (ComputeBePrice long) now present in T3

**Status**: RESOLVED ✓

Ticket T3 contains `T_B40_06_ComputeBePrice_Long_ReturnsCorrectBePrice` with explicit assert:
```
Assert.Equal(100.5, result, precision: 10);
```
Inputs: `MarketPosition.Long`, `averageEntryPrice: 100.0`, `bufferTicks: 2`, `tickSize: 0.25`.
Matches plan Section 7 T_B40_06 exactly.

#### CHECK-2 — T_B40_07 (ComputeBePrice short) now present in T3

**Status**: RESOLVED ✓

Ticket T3 contains `T_B40_07_ComputeBePrice_Short_ReturnsCorrectBePrice` with explicit assert:
```
Assert.Equal(99.5, result, precision: 10);
```
Inputs: `MarketPosition.Short`, `averageEntryPrice: 100.0`, `bufferTicks: 2`, `tickSize: 0.25`.
Matches plan Section 7 T_B40_07 exactly.

#### CHECK-3 — T_B40_10 (SubmitBeStop ocoOverride) now present in T3

**Status**: RESOLVED ✓

Ticket T3 contains `T_B40_10_SubmitBeStop_OcoOverride_UsesOverridePlusIndex` with explicit assert:
```
Assert.Equal("PTT-BEG-00001-0-0", capturedOcoId);
```
ocoOverride input: `"PTT-BEG-00001-0"`. Expected OCO ID for pair 0: `ocoOverride + "-0"`.
Matches plan Section 7 T_B40_10 exactly. Covers the literal code path that fixes DW-B39-OCO-01.

#### CHECK-4 — ComputeBePrice declared `internal static` in T1 (not private)

**Status**: RESOLVED ✓

Ticket T1 method signature block now reads:
```csharp
// internal (not private) to allow direct testing via [InternalsVisibleTo("CopyEngineTests")].
internal static double ComputeBePrice(Position pos, int bufferTicks)
```
T3 Step 1 confirms: "T1 declares it `internal static` (already corrected in T1 above). No visibility change needed in T3."
NOTE-01 from Rev 1 fully resolved — no retroactive visibility change instruction in T3.

#### CHECK-5 — Test count 202→217 (+15) in T3 SCAN-06 and all references

**Status**: RESOLVED ✓

- T3 SCAN-06 expected result: `217/217 [Fact] passing (202 baseline + 15 new)` ✓
- T3 footer: `[Fact] count after T3: 217 (was 202; +15)` ✓
- T1 SCAN-07: `[Fact] count after T1: 202 (unchanged — tests are written in T3)` ✓
- T2 SCAN-07: `[Fact] count after T2: 202 (unchanged — tests are written in T3)` ✓
- Full Scan Summary: `SCAN-06 dotnet test → 217/217 [Fact] passing` ✓
- Plan Section 7 baseline was 214 (12 tests); architect added 3 missing tests → 15 total; 202 + 15 = 217. Arithmetic correct.

#### CHECK-6 — T_B40_01–T_B40_15 numbering consistent and plan-aligned

**Status**: RESOLVED ✓

All 15 tests present: T_B40_01 through T_B40_15 in sequential order.
Numbering now aligns with plan Section 7 positions:
- Plan T_B40_06 → Ticket T_B40_06 (ComputeBePrice long) ✓
- Plan T_B40_07 → Ticket T_B40_07 (ComputeBePrice short) ✓
- Plan T_B40_08 → Ticket T_B40_08 (BuildGlobalBeOcoId exact format) ✓
- Plan T_B40_09 → Ticket T_B40_09 (same-seq different accIdx uniqueness) ✓
- Plan T_B40_10 → Ticket T_B40_10 (SubmitBeStop ocoOverride) ✓
- T_B40_13–T_B40_15 are coverage extensions beyond plan Section 7 — each traces to plan Section 7 method coverage (`IsPendingSlotsEmpty`, `ArmPendingBe` slot lifecycle).
NOTE-02 from Rev 1 resolved.

---

### Full Re-Run Checklist — Rev 2

#### T1 — Engine + OCO Fix

| Check | Result | Notes |
|-------|--------|-------|
| Traceability — spec reqs | PASS | DW-B39-OCO-01 (P0), DW-B39-BEHAVIOR-01 engine side cited. Plan Section 9 T1 followed. |
| Traceability — plan methods | PASS | All 9 plan items present: `_beAllOcoSeq`, `IsPendingSlotsEmpty`, `ComputeBePrice` (internal), `IsPriceAlreadyAtBeForAccount`, `ArmAllPendingBe`, `SubmitBeStop ocoOverride`, `BuildGlobalBeOcoId`, `_ocoSeq`, `Execute()` body rewrite. |
| JS-021 no lock() | PASS | No lock() in any new or modified method. Volatile+Interlocked+ConcurrentDictionary used. |
| JS-033 no async void | PASS | All new methods are synchronous. `Execute()` is `internal void`. No async keyword. |
| JS-002 no return null | PASS | Returns int, bool, double, string. `ocoOverride = null` is a default parameter (not a return null) — JS-002 pattern `return null;` does not apply. |
| JS-001 no throw in hot paths | PASS | No `throw new` in any new method. |
| JS-023 atomic primitives | PASS | `Interlocked.Increment(ref _beAllOcoSeq)` and `Interlocked.Increment(ref _ocoSeq)` used. |
| NT8-003 no volatile double | PASS | Both volatile fields are `volatile int`. |
| NT8-021 Account.All post-init | PASS | `ArmAllPendingBe` called from UI click handler (post-Loaded). |
| NT8-043 no null-conditional assignment | PASS | Not applicable to T1 methods. |
| CYC ArmAllPendingBe=5 ≤8 | PASS | 5 branches: Account.All foreach, acc.Positions foreach, IsFlat guard, IsPriceAlreadyAtBe branch, immediate/arm branch. |
| CYC IsPriceAlreadyAtBeForAccount=4 ≤8 | PASS | 4 branches. |
| CYC ComputeBePrice=2 ≤8 | PASS | 2 branches: base + isLong ternary. |
| CYC IsPendingSlotsEmpty=1 ≤8 | PASS | Expression body. |
| CYC BuildGlobalBeOcoId=1 ≤8 | PASS | Pure expression. |
| Build tag updated | PASS | `"PTT-COPIER B40 \| be-all-armed-oco-fix \| 2026-07-30"` in CopyEngine.cs line 41. |
| Test Coverage | PASS | Tests deferred to T3 by design; methods are `internal` or `internal static` for direct test access. |
| SCAN-01 through SCAN-07 present | PASS | All 7 scans present with commands and expected results. SCAN-07 correctly shows 202. |
| File routing | PASS | Wave workspace `c:\WSGTA\universal-or-strategy\src\PropTraderTools\` throughout. |

**T1 Rev 2 VERDICT: TICKET_REVIEW_PASS**

---

#### T2 — UI Armed State Wiring

| Check | Result | Notes |
|-------|--------|-------|
| Traceability — spec reqs | PASS | DW-B39-BEHAVIOR-01 (P1) UI side cited. T1 dependency stated. Plan Section 9 T2 followed. |
| Traceability — plan methods | PASS | All new Panel methods present. All new Window methods present including `OnWindowPendingBeFiredDispatch` (new in Rev 2, matches plan Section 9 T2 requirement for PendingBeFired subscription). |
| JS-021 no lock() | PASS | No lock() in any new or modified method. |
| JS-033 no async void | PASS | `OnGlobalBeClick` and `OnWindowGlobalBeClick` are synchronous `private void` event handlers. Not async void. |
| JS-002 no return null | PASS | All methods return void or BeState-conditional brushes. |
| JS-001 no throw in hot paths | PASS | No `throw new` in any new method. |
| NT8-043 null-conditional compound assignment banned | PASS | `if (Account.All != null) foreach (var acc in Account.All)` pattern used. No null-conditional compound assignment. |
| NT8-021 Account.All post-init | PASS | Account.All loops in `OnGlobalBeClick`, `OnWindowGlobalBeClick`, `Detach()`, and Window teardown — all UI-thread, post-Loaded. |
| WPF Dispatcher.InvokeAsync | PASS | Uses `this.Dispatcher` (WPF UIElement/Window), not NinjaTrader.Core.Globals.*. Already confirmed working in production at Panel line 758. |
| CYC UpdateBeAllVisuals=2 ≤8 | PASS | null guard + ternary. |
| CYC OnGlobalBeClick=4 ≤8 | PASS | switch (2 cases) + IsPendingSlotsEmpty check + Account.All null guard. |
| CYC OnPendingBeFiredDispatch=2 ≤8 | PASS | Dispatcher.InvokeAsync + if guard. |
| CYC UpdateWindowBeAllVisuals=2 ≤8 | PASS | null guard + ternary. |
| CYC OnWindowGlobalBeClick=4 ≤8 | PASS | Mirror of Panel handler. |
| Test Coverage | PASS | Tests deferred to T3 by design. |
| SCAN-01 through SCAN-07 present | PASS | All 7 scans present. SCAN-07 correctly shows 202. |
| File routing | PASS | Wave workspace throughout. |

**T2 Rev 2 VERDICT: TICKET_REVIEW_PASS**

---

#### T3 — Tests T_B40_01–T_B40_15

| Check | Result | Notes |
|-------|--------|-------|
| Traceability — spec reqs | PASS | Both defects DW-B39-OCO-01 and DW-B39-BEHAVIOR-01 cited. All 15 tests trace to plan Section 7 method coverage. |
| Traceability — plan tests T_B40_06 present | PASS | `T_B40_06_ComputeBePrice_Long` asserts 100.5. |
| Traceability — plan tests T_B40_07 present | PASS | `T_B40_07_ComputeBePrice_Short` asserts 99.5. |
| Traceability — plan tests T_B40_10 present | PASS | `T_B40_10_SubmitBeStop_OcoOverride` asserts `"PTT-BEG-00001-0-0"`. |
| ComputeBePrice declared internal in T1 | PASS | Confirmed in T1. T3 Step 1 confirms no retroactive change needed. |
| [Fact] count 202→217 (+15) | PASS | SCAN-06 expects `217/217`. Footer states `+15`. Plan arithmetic: 202 + 15 = 217. |
| Test numbering T_B40_01–T_B40_15 | PASS | All 15 sequentially numbered. Aligns with plan Section 7 for T_B40_01–T_B40_12; T_B40_13–T_B40_15 are coverage extensions. |
| T_B40_13–T_B40_15 traceability | PASS | T_B40_13: single below-threshold account → armed=1. T_B40_14: all slots fire → empty. T_B40_15: one slot remaining → not empty. All trace to `ArmAllPendingBe`/`IsPendingSlotsEmpty` coverage in plan Section 7. |
| JS-021 no lock() | PASS | No lock() in any [Fact] test body. |
| JS-033 no async void | PASS | All test methods are synchronous `public void`. |
| JS-002 no return null | PASS | No return null in test methods. |
| JS-001 no throw in test bodies | PASS | No throw new in test code. |
| CYC per [Fact] = 1 | PASS | All test bodies are pure arrange/act/assert sequences. |
| Test isolation — ComputeBePrice seam | PASS | Step 2 provides the test-seam overload `ComputeBePrice(MarketPosition, double, int, double)` with clear instruction. Minimal addition; production overload wraps it. |
| Test isolation — SubmitBeStop hook | PASS | `CreateForTest(onCreateOrderOcoId: ...)` factory delegate captures OCO ID. Consistent with existing B-series seam patterns. |
| Test isolation — SimulatePendingBeSlotFire | PASS | Step 4 provides `SimulatePendingBeSlotFire` seam; falls back to searching existing slot-fire patterns if already present. |
| Positive AND negative boundary cases | PASS | T_B40_11 (bid above threshold → immediate fire), T_B40_12 (bid below threshold → armed). T_B40_06 (long BE price), T_B40_07 (short BE price). T_B40_08 (exact format), T_B40_09 (uniqueness). |
| SCAN-01 through SCAN-07 present | PASS | All 7 scans present. SCAN-06 correctly uses `dotnet test` (not `dotnet build`). |
| File routing | PASS | Wave workspace `c:\WSGTA\universal-or-strategy\src\PropTraderTools\` throughout. |

**T3 Rev 2 VERDICT: TICKET_REVIEW_PASS**

---

### Rev 2 Summary

| Ticket | Traceability | JS Pre-Check | CYC | NT8 | Test Coverage | Scan Checklist | File Routing | VERDICT |
|--------|-------------|-------------|-----|-----|---------------|---------------|-------------|---------|
| T1 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **PASS** |
| T2 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **PASS** |
| T3 | PASS | PASS | PASS | N/A | PASS | PASS | PASS | **PASS** |

### Rev 1 Violations Cleared

| Violation | Status |
|-----------|--------|
| VIOLATION-01: T_B40_06 (ComputeBePrice long) missing from T3 | CLEARED |
| VIOLATION-01: T_B40_07 (ComputeBePrice short) missing from T3 | CLEARED |
| VIOLATION-01: T_B40_10 (SubmitBeStop ocoOverride) missing from T3 | CLEARED |
| NOTE-01: ComputeBePrice declared private in T1, retroactive change in T3 | CLEARED |
| NOTE-02: Test numbering mismatch vs plan Section 7 | CLEARED |

### No New Violations

No new JS, NT8, CYC, traceability, test coverage, scan checklist, or file routing violations were found in the Rev 2 tickets.

---

## Rev 2 Overall: **TICKET_REVIEW_PASS**

Gate opens for ptt-engineer. Engineer reads `04-ticket-review.md` first, then `04-tickets.md`. All three tickets carry the full 7-scan checklist contract. Per-ticket scan checklists are intact (defense-in-depth layers 1, 2, 3 preserved for engineer attestation and verifier cross-check).

*Reviewed by ptt-ticket-reviewer | Phase 3.5 | B40-LaneA | Rev 2 | 2026-07-30*
