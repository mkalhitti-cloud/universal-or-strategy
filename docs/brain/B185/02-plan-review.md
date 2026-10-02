# B185 Plan Review
## ptt-plan-reviewer Phase 2 (Cycle 2)

**Date**: 2026-07-08 (Cycle 1: 2026-07-08 | Cycle 2: 2026-07-08)
**Epic**: B185 -- BE ALL Instrument Scope + DispatchCopy CYC Headroom + Click-Trader Qty + B184 Tests
**Plan reviewed**: `docs/brain/B185/02-architecture-plan.md` (Cycle 2 -- REVIEW_FAIL correction applied)
**Spec reviewed**: `docs/brain/B185/00-orchestrator-prompt.md`
**Backlog reviewed**: `docs/brain/B184/06-deferred-backlog.md`
**Rules reviewed**: `docs/standards/jane-street/RULES_CATALOG.md` (JS-001..JS-041 on disk; JS-066/JS-081 enforced via DNA block)
**Source verified**: `src/PropTraderTools/CopyEngine.cs` lines 2519-2587 (TryLaneDHook + TryLaneDDispatchFollower)
**Reviewer**: ptt-plan-reviewer

---

## VERDICT: REVIEW_PASS

**Violations found**: 0
**Cycle 1 blocking violation**: RESOLVED (T3 AC4 method target -- source-verified in cycle 2)
**All 7-scan checklists**: PRESENT for all 4 tickets
**All spec ACs**: TRACEABLE
**CYC budgets**: ALL <= 8
**DNA rules**: ALL PASS

---

## Cycle 2 Resolution: T3 AC4 Method Target Determination

### Source Evidence (read from `src/PropTraderTools/CopyEngine.cs`)

| Item | Lines | Evidence |
|------|-------|---------|
| `TryLaneDHook` body | 2519-2547 | Method declaration line 2519; closing brace line 2547 |
| `TryLaneDDispatchFollower` body | 2554-2587 | Method declaration line 2554; closing brace line 2587 |
| `_laneDDispatched.TryAdd(baseSignal.OrderId + "#" + acc.Name, 0)` | **Line 2585** | Inside `TryLaneDDispatchFollower`, NOT in `TryLaneDHook` |

**Conclusion**: The spec's parenthetical "(line 2585)" is the authoritative anchor. Line 2585 is physically
inside `TryLaneDDispatchFollower`. The spec's method name "TryLaneDHook" is an internal labeling error
in the spec document — `TryLaneDHook` ends at line 2547 and contains no `TryAdd` call.

The plan's technical justification is **correct and source-verified**. The cycle 2 plan's three-row
evidence table accurately reflects the file. Eviction placement in `TryLaneDDispatchFollower`
(the method whose body contains the `TryAdd` at line 2585) satisfies the spec intent: eviction fires
when and only when `TryAdd` fires.

### CYC Impact Verification

The spec stated "CYC impact: +1 on `TryLaneDHook`". Because the eviction branch is in
`TryLaneDDispatchFollower` (not `TryLaneDHook`), the actual CYC distribution is:

| Method | Before | After | Within Budget? |
|--------|--------|-------|----------------|
| `TryLaneDHook` | 6 | 6 (UNCHANGED) | YES (6 <= 8) |
| `TryLaneDDispatchFollower` | 4 | 5 (eviction branch +1) | YES (5 <= 8) |

Spec AC5 ("TryLaneDHook CYC <= 8 after addition") is satisfied: CYC=6 <= 8. JS-066: PASS.

### T3 AC4 Final Text (Cycle 2 Plan)

The plan's AC4 reads:
> "Eviction branch added after `TryAdd` in `TryLaneDDispatchFollower` at line 2585. **Technical
> justification**: `TryAdd` does not exist in `TryLaneDHook` (which ends at line 2547);
> `_laneDDispatched.TryAdd(...)` is at line 2585 inside `TryLaneDDispatchFollower` (lines 2554-2587).
> The spec's own line reference (2585) confirms `TryLaneDDispatchFollower` is the correct target.
> Spec intent satisfied: eviction fires when and only when `TryAdd` fires (same method body)."

This wording is **ACCEPTED**. The technical justification is complete, source-cited, and unambiguous.
The ptt-verifier can confirm AC4 by checking line 2585's enclosing method in the source file.

**T3 AC4 resolution**: ACCEPTED. V1 from cycle 1 is CLOSED.

---

## Violations

**None.** Zero violations in cycle 2.

---

## Passing Checks

### Lane-Split Gate Compliance

| Check | Result |
|-------|--------|
| Gate result present | PASS -- `LANE-SPLIT GATE RESULT: LANES-APPROVED` stated |
| Q1 (same method / within 50 lines?) | PASS -- Q1 = NO (different methods, different files, 1200+ lines apart) |
| Q2 (Fix B depends on Fix A final design?) | PASS -- Q2 = NO (all tickets independent) |
| Q3 (each fix has standalone value?) | PASS -- Q3 = YES |
| Q4 (each fix has independent SIM path?) | PASS -- Q4 = YES |
| Single-pipeline mandate stated | PASS -- sequential T1->T2->T3->T4 per orchestrator mandate noted |

Note: LANES-APPROVED gate result is valid even with sequential execution. The gate certifies
eligibility, not obligation.

---

### DNA / Jane Street Rule Compliance Matrix

| Rule | Check | T1 | T2 | T3 | T4 | Result |
|------|-------|----|----|----|----|----|
| JS-021 No lock() | Any `lock(` in new code | None | None | None | None | **PASS** |
| JS-001 No throw | Any `throw new` in new code | None | None | None | None | **PASS** |
| JS-002 No return null | Any `return null;` in new code | void/void | bool/void | bool/int/void | void | **PASS** |
| JS-033 No async void | Any `async void` in new code | None | None | None | None | **PASS** |
| JS-066 CYC <= 8 | All modified methods | CYC=7 | CYC=8/1/1 | CYC=5/6 | CYC=1 each | **PASS** |
| JS-081 ASCII-only | All new string literals / identifiers | PASS | PASS | PASS | PASS | **PASS** |
| NT8: AtmStrategyCreate | Not AddOnBase | Not used | Not used | Not used | Not used | **PASS** |
| NT8: Account.All | AddOnBase available | Pre-existing use | N/A | N/A | N/A | **PASS** |

ConcurrentDictionary.TryGetValue and .Clear() are lock-free -- JS-021 compliant throughout. PASS

---

### CYC Budget Detail

| Method | Ticket | Before | After | Budget (<=8) | Status |
|--------|--------|--------|-------|--------------|--------|
| `PttBeSwapAll.Execute` | T1 | 6 | 7 | +1 headroom | PASS |
| `TryGetLastDirection` | T2 | new | 1 | +7 headroom | PASS |
| `RecordLastDirection` | T2 | new | 1 | +7 headroom | PASS |
| `DispatchCopy` | T2 | 8 | 8 (unchanged) | 0 headroom | PASS (extraction only) |
| `TryLaneDDispatchFollower` | T3 | 4 | 5 | +3 headroom | PASS |
| `TryLaneDHook` | T3 | 6 | 6 (unchanged) | +2 headroom | PASS |
| `OnChartMouseDown` | T3 | unchanged | unchanged | -- | PASS |
| T4 [Fact] methods (14) | T4 | new | 1 each | +7 headroom each | PASS |

---

### Acceptance Criteria Traceability

#### T1 (8 ACs required)

| AC | Criterion | Traceable in Plan | Status |
|----|-----------|-------------------|--------|
| AC1 | Signature `Execute(int, Instrument)` | Plan: "Method Signature Change" section | PASS |
| AC2 | `OnGlobalBeClick` passes `_instrument` | Plan: line 136-137 call site change | PASS |
| AC3 | Execute CYC = 7 | Plan: CYC Budget T1 | PASS |
| AC4 | `dotnet build` 0 errors | Plan: SCAN-07 T1 | PASS |
| AC5 | T_B185_01 instrument filter | Plan: T1 Tests | PASS |
| AC6 | T_B185_02 null instrument returns | Plan: T1 Tests | PASS |
| AC7 | T_B185_03 no matching = 0 calls | Plan: T1 Tests | PASS |
| AC8 | Spec HTML updated, DW card added | Plan: AC8 + DW-B185-BE-ALL-SCOPE-01 | PASS |

#### T2 (5 ACs required)

| AC | Criterion | Traceable in Plan | Status |
|----|-----------|-------------------|--------|
| AC1 | TryGetLastDirection + RecordLastDirection present | Plan: T2 New Private Helpers | PASS |
| AC2 | DispatchCopy no inline _lastLeaderDirection calls | Plan: T2 DispatchCopy Body Changes | PASS |
| AC3 | DispatchCopy CYC = 8 (unchanged) | Plan: CYC Budget T2 | PASS |
| AC4 | `dotnet build` 0 errors | Plan: SCAN-07 T2 | PASS |
| AC5 | T_B185_04/05/06 pass | Plan: T2 Tests | PASS |

#### T3 (7 ACs required)

| AC | Criterion | Traceable in Plan | Status |
|----|-----------|-------------------|--------|
| AC1 | `_clickQty` field, default=1 | Plan: T3 Change A | PASS |
| AC2 | `OnChartMouseDown` uses `_clickQty` | Plan: T3 Change A | PASS |
| AC3 | `LaneDEvictThreshold = 10000` | Plan: T3 Change B | PASS |
| AC4 | Eviction branch after `TryAdd` in `TryLaneDDispatchFollower` line 2585 | Plan: T3 AC4 + Source Verification subsection; line 2585 source-confirmed in cycle 2 | **PASS** |
| AC5 | `TryLaneDHook` CYC <= 8 (and TryLaneDDispatchFollower CYC <= 8) | Plan: CYC=6 unchanged / CYC=5 after eviction | PASS |
| AC6 | `dotnet build` 0 errors | Plan: SCAN-07 T3 | PASS |
| AC7 | T_B185_07/08 pass | Plan: T3 Tests | PASS |

#### T4 (3 ACs required)

| AC | Criterion | Traceable in Plan | Status |
|----|-----------|-------------------|--------|
| AC1 | All 14 named tests present and pass | Plan: T4 full test list | PASS |
| AC2 | dotnet test >= 77 [Fact] (plan: 85) | Plan: Test Count Summary | PASS |
| AC3 | No new build warnings | Plan: T4 SCAN-07 note | PASS |

---

### 7-Scan Checklist (per ticket)

| Scan | T1 | T2 | T3 | T4 | Status |
|------|----|----|----|----|----|
| SCAN-01 lock() | Listed | Listed | Listed | Listed | PASS |
| SCAN-02 return null | Listed | Listed | Listed | Listed | PASS |
| SCAN-03 async void | Listed | Listed | Listed | Listed | PASS |
| SCAN-04 CYC<=8 | Listed | Listed | Listed | Listed | PASS |
| SCAN-05 ASCII-only | Listed | Listed | Listed | Listed | PASS |
| SCAN-06 throw new | Listed | Listed | Listed | Listed | PASS |
| SCAN-07 dotnet build | Listed | Listed | Listed | Listed | PASS |

All 7 scans listed with applicability and notes for every ticket. PASS

---

### Post-T1 Mandatory Scripts

| Script | Required | In Plan |
|--------|----------|---------|
| GUARDIAN (`verify_critical_symbols.ps1`) | YES | PASS -- stated in "Post-T1 Mandatory Scripts" section |
| ptt-sync (`ptt-sync.ps1`) | YES | PASS -- stated; "0 errors" requirement noted |

---

### Deferred Backlog Closure Verification

| DW ID | Priority | B184 Status | Closed By in Plan | Plan Section |
|-------|----------|-------------|-------------------|--------------|
| DW-B184-01 | P1 | OPEN | T2 | Plan line 45 PASS |
| DW-B184-02 | P2 | OPEN | T3 | Plan line 46 PASS |
| DW-B184-03 | P2 | OPEN | T4 | Plan line 47 PASS |
| DW-B183-04 | P3 | OPEN | T3 | Plan line 48 PASS |
| DW-B184-04 | P3 | OPEN | Carry-forward (not B185 scope) | Plan "Items NOT in B185 scope" PASS |
| DW-B183-01 | P2 | OPEN | Carry-forward | Plan "Items NOT in B185 scope" PASS |
| DW-B183-02 | P2 | OPEN | Carry-forward | Plan "Items NOT in B185 scope" PASS |
| DW-B183-03 | P1 | OPEN | Carry-forward | Plan "Items NOT in B185 scope" PASS |
| DW-B183-05 | P1 | OPEN | Carry-forward | Plan "Items NOT in B185 scope" PASS |

New DW opened by B185: DW-B185-BE-ALL-SCOPE-01 PASS (added to plan deferred backlog table)

---

### Test Strategy Coverage

| Group | Count | Tests Named | B185 Tests | B184 Tests |
|-------|-------|-------------|------------|------------|
| T1 (BE ALL scope) | 3 | T_B185_01/02/03 | PASS | -- |
| T2 (DispatchCopy helpers) | 3 | T_B185_04/05/06 | PASS | -- |
| T3 (clickQty + eviction) | 2 | T_B185_07/08 | PASS | -- |
| T4 B184-T1 group | 6 | T_B184_T1_01..06 | -- | PASS |
| T4 B184-T2 group | 8 | T_B184_T2_01..08 | -- | PASS |
| **Total new** | **22** | All named | PASS | PASS |
| **B184 baseline** | 63 | (existing) | -- | -- |
| **B185 total target** | **85** | | PASS | PASS |

All 8 B185 tests and all 14 B184 tests are individually named with setup/action/assert detail. PASS

---

### Call Site Verification

| Claim | Plan Reference | Assessment |
|-------|----------------|------------|
| `PttBeSwapAll.Execute` has 1 call site: `TradeCopierPanel.cs:1243` | Plan: "grep confirms 0 Window call sites" | PASS (grep confirmation cited) |
| `_instrument` is pre-existing field on TradeCopierPanel | Plan: "set by SetInstrument() from the ChartTrader host" | PASS (no new field needed) |
| `TryLaneDDispatchFollower` is private | Plan: "Tests use BindingFlags.NonPublic" | PASS |
| `TryLaneDHook` ends at line 2547 | Source verified: line 2547 is closing brace | PASS |
| `TryLaneDDispatchFollower` begins at line 2554, ends at 2587 | Source verified | PASS |
| `_laneDDispatched.TryAdd(...)` at line 2585 is inside `TryLaneDDispatchFollower` | **Source verified** | PASS |

---

### NT8 API Compliance (AddOnBase constraints)

| API | Plan Claim | AddOnBase Valid? |
|-----|-----------|-----------------|
| `Account.All` | Pre-existing pattern (PttBeSwapAll.Execute) | YES -- plan explicitly notes AddOnBase available |
| `ConcurrentDictionary.TryGetValue` | Lock-free, JS-021 compliant | YES -- .NET primitive, no NT8 restriction |
| `ConcurrentDictionary.Clear()` | Thread-safe, no lock needed | YES -- .NET primitive, no NT8 restriction |
| `pos.Instrument?.FullName` | Null-conditional read | YES -- NT8 Position property read |

No `AtmStrategyCreate`, `AtmStrategyChangeStopTarget`, or `async/await` in AddOn lifecycle methods. PASS

---

## Spec Coverage Matrix

| Requirement | Addressed | Plan Section | Status |
|-------------|-----------|--------------|--------|
| T1: BE ALL instrument-scoped | YES | T1 full section | PASS |
| T1: signature change | YES | Method Signature Change | PASS |
| T1: null guard (combined gate) | YES | CYC Budget T1, CYC=7 | PASS |
| T1: pos.Instrument?.FullName filter | YES | T1 Change B.3 | PASS |
| T1: log line update | YES | T1 Change B.2 | PASS |
| T1: file header update | YES | T1 Change B.1 | PASS |
| T1: single call site update | YES | T1 Change -- TradeCopierPanel.cs:1243 | PASS |
| T2: extract TryGetLastDirection | YES | T2 New Private Helpers | PASS |
| T2: extract RecordLastDirection | YES | T2 New Private Helpers | PASS |
| T2: DispatchCopy body inline removal | YES | T2 DispatchCopy Body Changes | PASS |
| T2: DispatchCopy CYC unchanged | YES | T2 CYC Budget | PASS |
| T3: _clickQty field added | YES | T3 Change A | PASS |
| T3: int qty = _clickQty; | YES | T3 Change A | PASS |
| T3: LaneDEvictThreshold constant | YES | T3 Change B | PASS |
| T3: eviction branch location (line 2585) | YES | T3 Change B / AC4 Source Verification (cycle 2) | **PASS** |
| T4: all 14 B184 tests written | YES | T4 full test list | PASS |
| Post-T1 GUARDIAN + ptt-sync | YES | Post-T1 Mandatory Scripts | PASS |

---

## Cycle Summary

| Cycle | Date | Result | Violations | Status |
|-------|------|--------|------------|--------|
| 1 | 2026-07-08 | REVIEW_FAIL | 1 (T3 AC4 method target, SPEC COMPLETENESS P0) | CLOSED |
| 2 | 2026-07-08 | REVIEW_PASS | 0 | CURRENT |

---

*Written by: ptt-plan-reviewer (Phase 2, Cycle 2)*
*Epic: B185*
*Source read: `src/PropTraderTools/CopyEngine.cs` lines 2519-2587 (direct verification)*
*TryLaneDHook: lines 2519-2547. TryLaneDDispatchFollower: lines 2554-2587. TryAdd at 2585 confirmed in TryLaneDDispatchFollower.*
*Return: REVIEW_PASS (0 violations, 2 cycles consumed of 2 maximum)*
*Phase 3 UNLOCKED: ptt-architect may proceed to write docs/brain/B185/04-tickets.md*
