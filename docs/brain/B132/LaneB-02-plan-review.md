# B132 LaneB -- Phase 2 Plan Review

**Epic**: B132 LaneB
**Defect**: DW-B138 P1 -- Stop Drag Runtime Silent (Diagnostic Phase)
**Plan Reviewed**: `docs/brain/B132/LaneB-02-architecture-plan.md`
**Reviewer**: ptt-plan-reviewer (Phase 2)
**Spec**: DW-B138 P1 -- Stop Drag Runtime Silent
**Date**: B132 LaneB Phase 2

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|------------|--------------|
| Identify root-cause candidates for zero PTT-STP-Drag output | YES | Section A |
| Source line citations for each hypothesis | YES | Section A H1-H5 |
| 4 trace points covering the full dispatch chain | YES | Section B TP1-TP4 |
| `_diagnosticMode` guard on every Print call | YES | Section B, Section C |
| Extracted helpers where inline guard would exceed CYC=8 | YES | Section B TP1, TP4 |
| Zero behavioral changes | YES | Section D |
| B131 LaneA fixes explicitly listed as UNCHANGED | YES | Section D |
| Smoke test spec | YES | Section E |
| DW items (or explicit "none") | YES | Section F |
| 7-scan checklist with zero-match requirements | YES | Section G |
| Rules Catalog Gate (Section H) | YES | Section H |

---

## Check Results

### Check 1 -- Section H: Rules Catalog Gate (STEP 0)

**Result**: PASS

Section H present at plan line 10. Gate Result explicitly = PASS. Items checked:
- JS-021 (lock ban): verified no lock() in new code
- JS-001 (no throw in hot path): verified
- JS-002 (no return null): void helpers confirmed
- JS-033 (no async void): confirmed
- CYC budget: cross-referenced with Section G SCAN-06

No violation.

---

### Check 2 -- Section A: Hypothesis grounding + H1 source citation

**Result**: PASS

All 5 hypotheses carry exact source line citations:

- **H1**: `FindFollowerBracketOrder` L2386 -- source confirmed:
  ```
  CopyEngine.cs L2386: if (order.OrderState != OrderState.Working) continue;
  ```
  `IsWorkingBracket` at L2083-2089 confirmed in source: accepts `Working` OR `Accepted`.
  Asymmetry correctly identified: leader gate passes `Accepted`, follower lookup rejects it. Logically sound.

- **H2**: `ChangeSubmitted` gate -- source confirmed: `IsWorkingBracket` at L1722 (plan L1722 = source L1722). Elimination reasoning (Working event always follows) is sound.

- **H3**: `IsStopLeg` at L3836-3844 -- source confirmed. `StartsWith("Stop")` and `EndsWith("STP")` both present. Elimination correct.

- **H4**: `HandleBracketChange` foreach at L2349-2354 -- source confirmed. Elimination reasoning (blanket failure vs drag-specific) is sound.

- **H5**: `SignalOrNameMatches` at L2361-2368 -- source confirmed. Logic inversion analysis (null==null returns true, not false) is correct.

No violation.

---

### Check 3 -- Section B / TP1: Call site, guard, CYC

**Result**: PASS

- Insert after L1299: source confirmed (`EvictDedup(e.Order.OrderId.ToString(), e.Order.OrderState);` at CopyEngine.cs L1299). Correct.
- `_diagnosticMode` guard: present in `TryLogDragTrace` body.
- CYC impact on `OnOrderUpdate`: +0 (unconditional call, no branch). Correct.
- New helper `TryLogDragTrace` CYC=4: `if (_diagnosticMode && (IsWorkingBracket(order) || order.OrderState == OrderState.ChangeSubmitted))` = base(1) + &&(1) + ||(1) + outer if(1) = 4. Correct, within budget.

No violation.

---

### Check 4 -- Section B / TP2: `TryHandleBracketDrag` inline guard

**Result**: PASS

- Insert after L1721 (opening brace of `TryHandleBracketDrag`) -- source confirmed: method body at CopyEngine.cs L1721.
- `_diagnosticMode` guard: present as inline `if (_diagnosticMode)`.
- CYC before=3 (base + 2 if-guards at L1722 and L1724), after=4. Correct, within budget.

No violation.

---

### Check 5 -- Section B / TP3: `HandleBracketChange` inline guard + CYC

**Result**: PASS WITH NOTATION

- Insert after L2347 -- source confirmed: `double newPrice = tickSize > 0 ? ...` at CopyEngine.cs L2347.
- `_diagnosticMode` guard: present as inline `if (_diagnosticMode)`.

**CYC notation** (informational, not a fail):

The plan states `HandleBracketChange` CYC before=6, after=7. Actual count from source:

| Decision | Source |
|----------|--------|
| `if (instrument == null)` | L2341 |
| `?.TickSize ?? 0.0` (null-conditional) | L2344 |
| `isStop ? ... : ...` (rawPrice ternary) | L2345 |
| `tickSize > 0 ? ... : ...` (newPrice ternary) | L2347 |
| `foreach` | L2349 |
| `if (acc == null)` | L2351 |

6 decision points + 1 base = McCabe CYC **7 before**, **8 after** (adding one `if (_diagnosticMode)` branch).

The plan's stated "before=6, after=7" omits the `?.TickSize` null-conditional branch. The **correct values are before=7, after=8**.

CYC=8 after is on the exact boundary but does NOT exceed the ≤8 budget. **No budget violation.** However, the stated numbers in Section B and Section G SCAN-06 are factually incorrect (off by one). This is a plan accuracy defect but does not cause a budget overrun.

**This does not trigger REVIEW_FAIL** because CYC=8 ≤ 8. Engineer must use actual CYC=8 value (not the plan's stated 7) when verifying in SCAN-06 post-implementation.

---

### Check 6 -- Section B / TP4: `SyncFollowerBracket` extraction + CYC

**Result**: PASS

- Insert after L2139 -- source confirmed: `var fo = FindFollowerBracketOrder(...)` at CopyEngine.cs L2139.
- `TryLogSFBTrace` extracted helper: `_diagnosticMode` guard at top of method body.
- CYC impact on `SyncFollowerBracket`: +0 (unconditional call, no branch). Stated correctly.
- `SyncFollowerBracket` before=8, after=8: UNCHANGED. Correct (budget not exceeded).
- New helper `TryLogSFBTrace` CYC=2. Within budget.

No violation.

---

### Check 7 -- Section C: `_diagnosticMode` field declaration

**Result**: PASS

- Declared `private static bool _diagnosticMode = true;`. Static bool: no torn reads in .NET (correct per plan).
- Proposed insert location: after L400 (`GlobalBeAllDisarmed` event declaration) -- source confirmed: L400 is `internal event Action GlobalBeAllDisarmed;`.
- Clean removal protocol: 4 steps, explicit list of all artifacts to remove. Clear.

No violation.

---

### Check 8 -- Section D: Non-regression + B131 prior fix preservation

**Result**: PASS

Explicit table in Section D confirms B131 LaneA artifacts as ALREADY IN SOURCE, NOT to be modified:

| Symbol | Line | Source Confirmed? |
|--------|------|-------------------|
| `SignalOrNameMatches` | L2361 | YES -- CopyEngine.cs L2361 |
| `FindFollowerBracketOrder` signature | L2375 | YES -- CopyEngine.cs L2375 |
| `SyncFollowerBracket` call site | L2139 | YES -- CopyEngine.cs L2139 |

Behavioral-change prohibition stated explicitly: "ZERO behavioral changes."

No violation.

---

### Check 9 -- Section E: Test specification

**Result**: PASS

- Smoke test `B132_LaneB_DiagnosticMode_FieldExists` specified.
- Uses `[Fact]`, `Assert.NotNull`, `Assert.Equal`. xUnit-only. No NUnit/MSTest.
- Asserts: field exists as `private static bool`, default value = `true`.
- 4 B131 regression tests listed with file/line citations.

No violation.

---

### Check 10 -- Section F: DW items

**Result**: PASS

"No DW items." stated explicitly. All NT8 API facts resolved from `NT8_FULL_REFERENCE.md` with exact line citations (L3363-3367). No ambiguities remain.

No violation.

---

### Check 11 -- Section G: 7-scan checklist

**Result**: PASS WITH NOTATION (same as Check 5)

All 7 scans present with zero-match requirements:

| Scan | Target | Requirement |
|------|--------|-------------|
| SCAN-01 | `lock(` in src/ | ZERO MATCHES (JS-021) |
| SCAN-02 | `throw new` in CopyEngine.cs | NO NEW THROWS |
| SCAN-03 | `return null` in CopyEngine.cs | EXISTING ONLY |
| SCAN-04 | `async void` in src/ | ZERO MATCHES (JS-033) |
| SCAN-05 | `DateTime.Now` in src/ | ZERO MATCHES |
| SCAN-06 | CYC budget | All methods <= 8 |
| SCAN-07 | Non-ASCII in CopyEngine.cs | ZERO MATCHES |

**Notation**: SCAN-06 table states `HandleBracketChange` CYC after=7. Correct value is 8 (see Check 5). Engineer must use CYC=8 as the verified post-implementation value for SCAN-06 sign-off.

No structural violation. 7 scans present, all have zero-match requirements.

---

### Check 12 -- OnOrderUpdate pre-existing CYC > 8

**Result**: PASS

Plan acknowledges `OnOrderUpdate` CYC approximately 11-18 (pre-existing, not introduced by this block). Extraction to `TryLogDragTrace` helper correctly ensures +0 net CYC impact on `OnOrderUpdate`. Section G SCAN-06 explicitly notes this. No violation introduced.

---

### Check 13 -- `SyncFollowerBracket` CYC at boundary

**Result**: PASS

Plan correctly identifies `SyncFollowerBracket` before=8 and uses extraction (`TryLogSFBTrace`) to keep after=8. No budget breach.

---

## Violations Summary

| # | Rule / Criteria | Section | Severity | Status |
|---|----------------|---------|----------|--------|
| V1 | `HandleBracketChange` CYC stated as 6→7; actual is 7→8 (omits `?.TickSize` null-conditional) | B/TP3, G/SCAN-06 | **Notation only** -- CYC=8 ≤ 8 budget, no breach | NOT A FAIL |

No P0 or P1 violations found. The single notation (CYC off-by-one in plan text) does not breach the ≤8 budget; actual CYC=8 remains within budget. Engineer must use CYC=8 (not 7) when signing off SCAN-06.

---

## Final Gate

**REVIEW_PASS**

All mandatory checks passed. The plan is structurally sound, grounded in verified source citations, correctly handles the CYC budget at all trace points (with the noted correction for `HandleBracketChange` being 8 not 7), includes all 7 scans, has a compliant Rules Catalog Gate (Section H), preserves B131 LaneA fixes, and introduces zero behavioral changes.

**Action for engineer**: Note that `HandleBracketChange` actual CYC after TP3 addition will be **8** (not 7 as stated). This is still within budget. SCAN-06 sign-off should record CYC=8.
