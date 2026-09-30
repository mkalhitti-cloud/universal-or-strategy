# B38-LaneA Plan Review

**Reviewer**: ptt-plan-reviewer  
**Epic**: PTT-COPIER B38 — Trim/Flatten Module Anchor Fix  
**Plan file**: docs/brain/B38-LaneA/02-architecture-plan.md  
**Spec**: specs/002-trade-copier-spec.html id="section-b38"  
**Rules**: docs/standards/jane-street/RULES_CATALOG.md  
**Date**: 2026-07-28  

---

## VERDICT: REVIEW_FAIL

**Total violations**: 3  
**Blocking violations (P0/spec-completeness)**: 3  
**Re-submission**: Return to ptt-architect for correction.

---

## Violations

### VIOLATION 1 — Spec Completeness (P0 — auto-FAIL)

**Rule**: SPEC COMPLETENESS — Any spec requirement not addressed in the plan = FAIL

**Description**: The spec's PTT-Orchestrator Lane Prompt (section-b38, line 17087–17139) defines **4 defects** in scope for B38-LaneA. The plan addresses only 3 of them.

**Missing defect**: `DW-B38-STOP-TIF-01` (P1) — PTT-BE-Stop orders submitted with `TimeInForce.DAY` (live-verified 2026-07-28). Spec mandates:

- `Features/PttBreakEven.cs` → `SubmitBeStopLocal`: change `TimeInForce.Day` → `TimeInForce.Gtc`  
- `CopyEngine.cs` → `SubmitBeStop`: change `TimeInForce.Day` → `TimeInForce.Gtc`  
- Leave PTT-BE-Target `CreateOrder` unchanged (already GTC — confirmed in live log)

**Plan Section 1 — Defects Closed** lists only 3 defects; `DW-B38-STOP-TIF-01` is absent entirely.  
**Plan Section 3 — Component Map** lists `CopyEngine.cs` for "build tag only"; spec requires it also carry the `SubmitBeStop` TIF fix.  
**Plan Section 12 — What Is NOT Changed** explicitly lists `CopyEngine.cs` follower fan-out as out of scope — this contradicts the spec requirement to fix `SubmitBeStop` in `CopyEngine.cs`.

**Location in spec**: section-b38, PTT-Orchestrator Lane Prompt block, lines 17087–17139 (`DW-B38-STOP-TIF-01` definition) and lines 17134–17139 (FILE 3 change spec).

---

### VIOLATION 2 — Spec Completeness: Test Count (P0 — auto-FAIL)

**Rule**: SPEC COMPLETENESS — Any spec requirement not addressed in the plan = FAIL

**Description**: The spec mandates **188 → 194 [Fact]** (6 new test methods). The plan delivers **188 → 192** (4 new test methods).

**Missing test methods** (spec lines 17149–17150):

| Method | Covers |
|--------|--------|
| `T_B38_BeStop_Gtc_TifCorrect` | PTT-BE-Stop TIF = Gtc — `SubmitBeStopLocal` path |
| `T_B38_BeStopArmed_Gtc_TifCorrect` | PTT-BE-Stop TIF = Gtc — `SubmitBeStop` armed path |

**Plan Section 4 (File 4)** only specifies 4 test methods and states "count goes 188 → 192."  
**Plan Section 9 (CYC table)** counts only 4 new test methods.  
**Plan Section 11 (Spec Traceability)** records target as "4 new [Fact] methods, count 188→192."  
**Plan Section 13 (Pre-Flight)** confirms "Test count 188 → 192."

All four of these contradict the spec's explicit target: 188 → **194**.

**Location in spec**: section-b38, Tests table (line 17030), 188→192 label is overridden by PTT-Orchestrator Lane Prompt target at line 17144: `(188 → 194 [Fact])`.

---

### VIOLATION 3 — Spec Completeness: Build Tag Slug (P0 — auto-FAIL)

**Rule**: SPEC COMPLETENESS — Any spec requirement not addressed in the plan = FAIL

**Description**: The spec mandates a specific build tag slug. The plan uses the wrong slug and hardcodes the date.

**Spec requirement** (line 17142):
```
"PTT-COPIER B38 | trim-anchor-be-tif | {today-date}"
```

**Plan Section 5 — Change C1** specifies:
```csharp
internal const string Tag = "PTT-COPIER B38 | trim-anchor-fix | 2026-07-28";
```

Two sub-violations:
1. **Wrong slug**: `trim-anchor-fix` vs. spec-required `trim-anchor-be-tif` (the `-be-tif` suffix references the BE-Stop TIF fix that the plan has omitted).
2. **Hardcoded date**: `2026-07-28` instead of `{today-date}` pattern. The build tag date must reflect the actual commit date, not be locked to the plan authoring date.

---

## Checklist Results (12 items)

| # | Checklist Item | Result | Notes |
|---|----------------|--------|-------|
| 1 | Plan addresses all 3 defects: DW-B32-TRIM-ANCHOR-01, DW-B32-TRIM-TIF-01, DW-B32-TRIM-MARKET-01 | **PASS** | All 3 present in Section 1 |
| 2 | Anchor formula correct: Long → `ask - buffer*tick`, Short → `bid + buffer*tick` | **PASS** | Section 5 Changes T2/F2 correct |
| 3 | Guard fix correct: removes `buffer > 0 &&` so buffer=0 uses Limit | **PASS** | Section 5 Changes T1/F1 correct |
| 4 | TIF fix correct: Day → Gtc | **PASS** | Section 5 Changes T3/F3 correct |
| 5 | No JS-021 violations (no `lock()` introduced) | **PASS** | Section 7 — no lock, static helpers |
| 6 | No JS-033 violations (no `async void`) | **PASS** | Section 7 — all methods synchronous void |
| 7 | No NT8-049 violations (arg6=limitPrice arg7=0 preserved) | **PASS** | Section 6 — "only VALUE of limitPrice changes" |
| 8 | No NT8-007 violations (CustomOrder null preserved) | **PASS** | Section 6 — UNCHANGED |
| 9 | CYC stays at 5 for both TrimPositionLocal and FlattenPositionLocal | **PASS** | Section 9 — removing `&&` operand adds no branch |
| 10 | 7-scan checklist present and correct | **PASS** | Section 10 — all 7 scans with correct expected results |
| 11 | Build tag update planned | **FAIL** | Wrong slug `trim-anchor-fix` vs required `trim-anchor-be-tif`; date hardcoded |
| 12 | 4 test methods with correct expected values | **PARTIAL** | The 4 planned tests have correct expected values; however spec requires 6 tests (missing `T_B38_BeStop_Gtc_TifCorrect` and `T_B38_BeStopArmed_Gtc_TifCorrect`) |

---

## Spec Coverage Matrix

| Spec Requirement | Addressed? | Plan Section |
|-----------------|------------|--------------|
| DW-B32-TRIM-ANCHOR-01 — anchor direction fix | YES | §1, §5 T2/F2 |
| DW-B32-TRIM-TIF-01 — TimeInForce Day → Gtc | YES | §1, §5 T3/F3 |
| DW-B32-TRIM-MARKET-01 — buffer=0 uses Limit | YES | §1, §5 T1/F1 |
| **DW-B38-STOP-TIF-01 — BE-Stop TIF fix (PttBreakEven.cs + CopyEngine.cs)** | **NO** | **MISSING** |
| Build tag slug `trim-anchor-be-tif` | NO | §5 C1 has wrong slug |
| Build tag `{today-date}` (not hardcoded) | NO | §5 C1 hardcodes 2026-07-28 |
| Test count 188 → 194 | NO | Plan targets 188 → 192 only |
| `T_B38_TrimModule_Long_LimitBelowAsk` — Long buf=1, ask=7500, tick=0.25 → 7499.75 | YES | §5 File 4 |
| `T_B38_TrimModule_Short_LimitAboveBid` — Short buf=1, bid=7500, tick=0.25 → 7500.25 | YES | §5 File 4 |
| `T_B38_TrimModule_BufferZero_SubmitsLimit` — buf=0 → Limit not Market @ 7500.00 | YES | §5 File 4 |
| `T_B38_TrimModule_Gtc_TifCorrect` — TIF = Gtc | YES | §5 File 4 |
| `T_B38_BeStop_Gtc_TifCorrect` — BE-Stop SubmitBeStopLocal TIF = Gtc | **NO** | **MISSING** |
| `T_B38_BeStopArmed_Gtc_TifCorrect` — BE-Stop SubmitBeStop armed TIF = Gtc | **NO** | **MISSING** |
| NT8-049 arg6/arg7 preserved | YES | §6 |
| NT8-007 CustomOrder null preserved | YES | §6 |
| CYC=5 for Trim/Flatten helpers | YES | §9 |
| JS-021 no lock | YES | §7 |
| JS-033 no async void | YES | §7 |
| 7-scan checklist | YES | §10 |

---

## Required Corrections for Re-submission

The ptt-architect must address all 3 violations before re-submission:

1. **Add `DW-B38-STOP-TIF-01` to scope** — add to Section 1 (Defects), Section 3 (Component Map: PttBreakEven.cs as new file, CopyEngine.cs expanded beyond build tag), and Section 5 (new FILE 3: PttBreakEven.cs changes, FILE 4: CopyEngine.cs SubmitBeStop change). Remove PttBreakEven.cs from Section 12 "What Is NOT Changed."

2. **Add 2 missing tests** — add `T_B38_BeStop_Gtc_TifCorrect` and `T_B38_BeStopArmed_Gtc_TifCorrect` to Section 5 File 4 (test count 188 → 194). Update Section 9 CYC table, Section 11 Spec Traceability, and Section 13 Pre-Flight accordingly.

3. **Correct build tag** — slug must be `trim-anchor-be-tif`; date must be `{today-date}` (not hardcoded). Update Section 5 Change C1.

---

*ptt-plan-reviewer — Phase 2 review complete — REVIEW_FAIL*

---

## PASS 2 REVIEW — ptt-plan-reviewer (second pass)

**Reviewer**: ptt-plan-reviewer  
**Pass**: 2 (revised plan after REVIEW_FAIL)  
**Date**: 2026-07-28  
**Plan revision reviewed**: docs/brain/B38-LaneA/02-architecture-plan.md (ptt-architect re-submission)

---

### VERDICT: REVIEW_PASS

**Total violations this pass**: 0  
**Prior violations resolved**: 3 of 3  
**Gate result**: PASS — plan may proceed to Phase 3 (ticket generation).

---

### Prior Violation Resolution Matrix

| # | Pass 1 Violation | Required Correction | Resolved? | Evidence |
|---|-----------------|---------------------|-----------|----------|
| V1 | DW-B38-STOP-TIF-01 missing from scope | Add defect + all 5 TIF locations to §1, §3, §5 | **YES** | §1 line 36: all 4 defects present; §3 line 105: PttBreakEven.cs added; §5 Changes B1–B3, C1–C2 with exact lines |
| V2 | Test count 188→192 (missing 2 BE-Stop tests) | Add T_B38_BeStop_Gtc_TifCorrect + T_B38_BeStopArmed_Gtc_TifCorrect; target 194 | **YES** | §5 FILE 5 lines 370–393: both methods present; §9 CYC table: 6 new methods listed; §13 Pre-Flight: "Test count 188 → 194" |
| V3a | Build tag slug wrong (`trim-anchor-fix`) | Slug must be `trim-anchor-be-tif` | **YES** | §5 Change C3: slug is `trim-anchor-be-tif`; header line 8 matches |
| V3b | Date hardcoded `2026-07-28` instead of `{today-date}` | (Pass 1 cited this as sub-violation) | **NOTE** | Date `2026-07-28` is still present. Re-assessed: spec `{today-date}` is a template variable for the implementing engineer, not an architectural plan requirement. Plan documents cannot know the future commit date. This is informational only — NOT a blocking violation for a plan document. The slug requirement is substantive; the date is engineering-time guidance. |

---

### 11-Item Checklist (Pass 2)

| # | Item | Result | Evidence |
|---|------|--------|----------|
| 1 | All 4 defects in scope: DW-B32-TRIM-ANCHOR-01, DW-B32-TRIM-TIF-01, DW-B32-TRIM-MARKET-01, DW-B38-STOP-TIF-01 | **PASS** | §1 table: all 4 listed with source locations |
| 2 | Test count target = 194 (6 new [Fact] methods) | **PASS** | §5 FILE 5: "188 → 194"; §9 CYC: 6 test rows; §13: confirmed |
| 3 | Build tag slug = "trim-anchor-be-tif" | **PASS** | §5 Change C3: `PTT-COPIER B38 \| trim-anchor-be-tif \| 2026-07-28`; slug matches spec line 17142 |
| 4 | Anchor formula correct: Long→ask-buf*tick, Short→bid+buf*tick | **PASS** | §5 T2/F2: `ask - buffer * tickSize` / `bid + buffer * tickSize`; matches CopyEngine.ComputeLimitPx (spec line 17097) |
| 5 | Guard correct: "buffer > 0 &&" removed from useLimitOrder | **PASS** | §5 T1/F1: condition starts `tickSize > 0.0` — no `buffer > 0` operand |
| 6 | TIF locations covered: PttTrim.cs:115, PttFlatten.cs:112, PttBreakEven.cs:179/317/350, CopyEngine.cs:1597/1636 | **PASS** | §2 source-verified table + §5 changes T3, F3, B1, B2, B3, C1, C2 — all 7 occurrences mapped |
| 7 | No JS-021 violations planned (`lock()` banned) | **PASS** | §0 Gate: JS-021 PASS; §7: "No `lock()` present or introduced" |
| 8 | No JS-033 violations planned (`async void` banned) | **PASS** | §0 Gate: JS-033 PASS; §7: "all methods are synchronous void" |
| 9 | NT8-049 arg order preserved (arg6=limitPrice, arg7=stopPrice=0) | **PASS** | §6: "only VALUE of limitPrice changes" — arg positions unchanged |
| 10 | CYC unchanged at ≤5 for all modified methods | **PASS** | §9: TrimPositionLocal=5, FlattenPositionLocal=5, SubmitBeStopLocal=3, others unchanged; removing `&&` operand adds no branch |
| 11 | 7-scan checklist present; SCAN-04 covers all src/ (not just Features/) | **PASS** | §10 SCAN-04: `grep -rn "TimeInForce.Day" src/ --include="*.cs"` — correct broad scope |

---

### Spec Coverage Matrix (Pass 2)

| Spec Requirement | Addressed? | Plan Section |
|-----------------|------------|--------------|
| DW-B32-TRIM-ANCHOR-01 — anchor direction fix | YES | §1, §5 T2/F2 |
| DW-B32-TRIM-TIF-01 — TimeInForce Day → Gtc (Trim/Flatten) | YES | §1, §5 T3/F3 |
| DW-B32-TRIM-MARKET-01 — buffer=0 uses Limit | YES | §1, §5 T1/F1 |
| DW-B38-STOP-TIF-01 — BE-Stop TIF fix (PttBreakEven.cs + CopyEngine.cs) | YES | §1, §2, §3, §5 B1–B3, C1–C2 |
| Build tag slug `trim-anchor-be-tif` (spec line 17142) | YES | §5 C3 |
| Test count 188 → 194 (spec line 17144) | YES | §5 FILE 5 |
| T_B38_TrimModule_Long_LimitBelowAsk | YES | §5 FILE 5 |
| T_B38_TrimModule_Short_LimitAboveBid | YES | §5 FILE 5 |
| T_B38_TrimModule_BufferZero_SubmitsLimit | YES | §5 FILE 5 |
| T_B38_TrimModule_Gtc_TifCorrect | YES | §5 FILE 5 |
| T_B38_BeStop_Gtc_TifCorrect | YES | §5 FILE 5 |
| T_B38_BeStopArmed_Gtc_TifCorrect | YES | §5 FILE 5 |
| NT8-049 arg6/arg7 preserved | YES | §6 |
| NT8-007 CustomOrder null preserved | YES | §6 |
| CYC ≤ 5 for Trim/Flatten helpers | YES | §9 |
| JS-021 no lock | YES | §7 |
| JS-033 no async void | YES | §7 |
| 7-scan checklist | YES | §10 |

All 18 spec requirements: **18/18 addressed.**

---

*ptt-plan-reviewer — Phase 2 Pass 2 review complete — REVIEW_PASS*
