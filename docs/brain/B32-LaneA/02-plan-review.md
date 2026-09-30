# B32-LaneA Plan Review

**Reviewer**: ptt-plan-reviewer
**Phase**: 2 (Plan Review)
**Date**: 2026-07-19
**Input plan**: `docs/brain/B32-LaneA/02-architecture-plan.md`
**Input register**: `docs/brain/B32-LaneA/00-direct-repair-register.md`
**Rules checked**: `docs/standards/jane-street/RULES_CATALOG.md`, `docs/standards/NT8_COMPILER_RULES.md`

---

## Verdict: REVIEW_PASS

No P0 or P1 blocking violations found. Two P2 documentation-accuracy observations are noted
below and must be carried forward as engineer awareness notes — they do not block ticket execution.

---

## Section A — Coverage Matrix

| Requirement | Checklist item | Result | Plan location |
|---|---|---|---|
| DW-B32-TRIM-MARKET-01 addressed | All 3 defects covered | PASS | Plan §Scope + §Defect Analysis |
| DW-B32-TRIM-ANCHOR-01 addressed | All 3 defects covered | PASS | Plan §Scope + §Defect Analysis |
| DW-B32-TRIM-CLOSE-01 addressed | All 3 defects covered | PASS | Plan §Scope + §Defect Analysis |
| All 6 buffer guard locations listed | 4 in CopyEngine.cs + 2 in TradeCopierPanel.cs | PASS | Plan lines 69-75 (table: 949, 967, 1059, 1069, 808, 836) |
| ComputeLimitPx formula swap correct | ask-buf*tick long / bid+buf*tick short | PASS | Plan lines 120-123. Math verified: ask=5000.25,bid=5000.00,tick=0.25 → Long buf=1: 5000.00 ✓, Short buf=1: 5000.25 ✓, Long buf=2: 4999.75 ✓, Short buf=2: 5000.50 ✓ |
| 5 test mutations for Defect 2 | 4 rename+correct + 1 partial update | PASS | Plan lines 299-337: Change 4a (remove exitBuffer==0 case) + Changes 4b T1-T4 (4 renames + value corrections) = 5 total |
| Defect 3 approach chosen with reasoning | WARN-AND-BLOCK selected | PASS | Plan line 159 ("WARN-AND-BLOCK"), reasoning at lines 160-175 (acc.Change() silent rejection confirmed via DW-B32-07; Target slot symmetry with Stop slots proven) |

**Coverage: 7/7 PASS**

---

## Section B — NT8 Constraint Checks

| Rule | Check | Result | Plan location |
|---|---|---|---|
| NT8-007 — CreateOrder arg 12 | No new CreateOrder calls introduced | PASS | Plan line 490: "No new CreateOrder calls. Existing calls already compliant." |
| NT8-013 — No DateTime.Now | Not applicable (no date/time operations) | PASS | Plan line 491: "No DateTime.Now." |
| NT8-014 — Signal names start with PTT- | No new signal names introduced; existing PTT-Trim/PTT-Flatten unchanged | PASS | Plan line 492. Output.Process strings use "PTT-Trim:" / "PTT-Flatten:" as informational prefixes (not signal names). No new CreateOrder signal names. |
| NT8-018/NT8-003 — No lock() / volatile | acc.Orders.ToList() snapshot pattern used; no lock(), no volatile | PASS | Plan lines 381-391 (IsAtmBracketActive uses ToList()), plan line 493 |
| NT8-019 — No async void | Zero async methods introduced; all methods are synchronous void or static bool | PASS | Plan line 494 |
| NT8-029 — Tick alignment | ComputeLimitPx output consumed by existing tick-rounding at lines 1150/1183; no regression | PASS | Plan line 495 |
| NT8-031 — OrderState values | OrderState.Working and OrderState.Accepted confirmed valid in NT8 | PASS | Plan line 496 |
| acc.Cancel() on ATM-owned orders | No acc.Cancel() calls proposed | PASS | Plan arch decision section (lines 159-175) explicitly rejects any ATM bracket modification |
| acc.Change() on non-PTT orders | Not proposed; warn-and-block design avoids acc.Change() entirely | PASS | Plan lines 159-175: acc.Change() on ATM targets rejected for same silent-rejection reason confirmed in DW-B32-07 |

**NT8 checks: 9/9 PASS**

---

## Section C — Jane Street Rules (P0 Checks)

| Rule ID | Description | Check | Result |
|---|---|---|---|
| JS-021 | No lock() | Zero lock() usages proposed. IsAtmBracketActive uses acc.Orders.ToList() snapshot — the established lock-free pattern. | PASS |
| JS-001 | No throw in business logic | Zero throw statements. All error paths use StatusUpdate?.Invoke() + return. | PASS |
| JS-002 | No return null for missing values | New helpers IsAtmSlotName and IsAtmBracketActive both return bool. No nullable return types introduced. | PASS |
| JS-033 | No async void | Zero async methods introduced. All new methods are synchronous (static bool, private bool, private void). | PASS |

**JS P0 checks: 4/4 PASS**

---

## Section D — CYC Budget

All new and modified methods are within CYC ≤ 8. Two inaccuracies in the plan's stated CYC values are
noted as P2 observations (see Section F). The inaccuracies are underestimates; the true values remain
well below the CYC ≤ 8 threshold.

| Method | Plan CYC | Reviewer-verified CYC | Status | Notes |
|---|---|---|---|---|
| `IsAtmSlotName` | 3 | 4–5 | PASS (≤8) | Plan understates: null-OR guard = 2 decisions, Stop check = 1, Target check = 1 → CYC ≥ 4. See P2 observation OBS-01. |
| `IsAtmBracketActive` | 4 | 5–6 | PASS (≤8) | Plan understates: foreach (1), instrument continue (2), Working-check AND Accepted-check (2 more), name/signal check (1) → CYC ≥ 5. See P2 observation OBS-01. |
| `TrimOneAccount` | 4 (was 3) | 4 (was 3) | PASS (≤8) | One new branch added (IsAtmBracketActive guard). Accurate. |
| `FlattenOneAccount` | 4 (was 3) | 4 (was 3) | PASS (≤8) | One new branch added (IsAtmBracketActive guard). Accurate. |
| Trim/Flatten CopyEngine.cs guards (×4) | 4, 4, 5, 4 | 4, 4, 5, 4 | PASS (≤8) | One OR clause removed from each. Accurate. |
| OnTrimClick / OnFlattenClick | 3 (was 4) | 3 (was 4) | PASS (≤8) | One OR clause removed. Accurate. |

**CYC budget: PASS (all methods ≤ 8)**

---

## Section E — Test Coverage

| Check | Result | Plan location |
|---|---|---|
| At least 1 new [Fact] for Defect 3 approach | PASS — 4 new [Fact] tests for IsAtmSlotName (T-B32-T2-01 through T-B32-T2-04) covering: Target1/2/9 detection, Stop1/2 detection, PTT prefix rejection (incl. null), Target-without-digit rejection | Plan lines 448-479 |
| TrimLimit_FallsBackToMarket_WhenAskIsZero exitBuffer=0 case removal confirmed | PASS — Change 4a explicitly removes lines 1541-1542 (the `exitBuffer == 0` assertion case) and the preceding comment | Plan lines 299-307 |

**Test coverage: 2/2 PASS**

---

## Section F — 7-Scan Checklist Presence

| Scan | Listed in plan? | Result |
|---|---|---|
| 1. lock() scan | Yes — plan line 514 | PASS |
| 2. async void scan | Yes — plan line 515 | PASS |
| 3. return null scan | Yes — plan line 516 | PASS |
| 4. NT8 compiler rules scan | Yes — plan line 517 | PASS |
| 5. CYC scan | Yes — plan line 518 | PASS |
| 6. Test scan (dotnet test) | Yes — plan line 519 | PASS |
| 7. ASCII scan | Yes — plan line 520 | PASS |

Note: The 7-scan checklist is presented once as a template block (plan §7-Scan Checklist Template)
to be carried into every ticket. This satisfies the requirement that both tickets carry the checklist.
The plan correctly states the intent: "*(To be carried into every ticket unchanged)*".

**7-scan checklist: 7/7 PASS**

---

## Section G — P2 Observations (Non-blocking)

These observations do not affect REVIEW_PASS but must be noted for engineer awareness.

### OBS-01 — CYC values in plan are understated for IsAtmSlotName and IsAtmBracketActive

**Severity**: P2 (documentation accuracy)
**Files**: Plan §Ticket 2 Detail (CYC summary table, line 429-438)

The plan's stated CYC for these two methods is lower than the true cyclomatic complexity:

- `IsAtmSlotName` plan states CYC=3:
  - Actual decision points: `null || name.Length < 5` (compound OR = 2 decisions) + Stop branch + Target branch = 4 decisions → CYC = 1+4 = 5.
  - Still ≤ 8. No budget impact.

- `IsAtmBracketActive` plan states CYC=4:
  - Actual decision points: foreach (1) + instrument-continue (2) + Working!=OrderState.Working (3) + Accepted!=OrderState.Accepted (4, from the compound &&) + name/signal (5) = 5 decisions → CYC = 1+5 = 6.
  - Still ≤ 8. No budget impact.

**Engineer action**: Update CYC comment annotations in the method headers to reflect accurate counts (CYC=5 and CYC=6 respectively). The ≤8 budget remains satisfied.

### OBS-02 — Defect register "Key Context" section contains a superseded formula note

**Severity**: P2 (documentation stale)
**File**: `00-direct-repair-register.md` lines 113-118

The register's "Key Context" section states: *"ComputeLimitPx anchor (already fixed in B29, DO NOT revert)"* and describes bid-buffer as the correct long-exit anchor. This is superseded by R-B32-05 (Director-confirmed correct formula is ask-buffer for long). The plan correctly applies R-B32-05.

**Engineer action**: No code impact. The Key Context note in the register can be ignored; R-B32-05 is the authoritative specification.

---

## Section H — Spec Coverage Matrix

| Spec Req | Description | Plan section | Status |
|---|---|---|---|
| R-B32-03 | Raw market orders bypass ATM bracket → warn-and-block | §DW-B32-TRIM-CLOSE-01, §Ticket 2 Detail | ADDRESSED |
| R-B32-04 | buffer==0 falls through to market order → guard removal | §DW-B32-TRIM-MARKET-01, §Ticket 1 Detail (Changes 1a–1d, 3a–3b) | ADDRESSED |
| R-B32-05 | ComputeLimitPx wrong anchor → formula swap | §DW-B32-TRIM-ANCHOR-01, §Ticket 1 Detail (Change 2) | ADDRESSED |

**Spec coverage: 3/3 requirements addressed**

---

## Summary

| Section | Score | Verdict |
|---|---|---|
| A — Coverage | 7/7 | PASS |
| B — NT8 Constraints | 9/9 | PASS |
| C — JS Rules (P0) | 4/4 | PASS |
| D — CYC Budget | All ≤ 8 | PASS |
| E — Test Coverage | 2/2 | PASS |
| F — 7-Scan Checklist | 7/7 | PASS |
| G — P2 Observations | 2 non-blocking notes | N/A |
| H — Spec Coverage | 3/3 | PASS |

**REVIEW_PASS** — Unlocks Phase 3 (ticket generation).

Engineer must carry OBS-01 as an awareness note: update CYC comment annotations
in `IsAtmSlotName` (CYC=5) and `IsAtmBracketActive` (CYC=6) when implementing.
