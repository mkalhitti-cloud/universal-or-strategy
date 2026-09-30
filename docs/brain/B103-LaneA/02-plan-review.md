# Plan Review — B103-LaneA

## Review Date: 2026-08-10
## Reviewer: ptt-plan-reviewer

---

### Checklist Results

#### A. Spec Fidelity

| Item | Result | Notes |
|------|--------|-------|
| 1A — field deletion at L3868-3871 | PASS | Source confirms exact 4-line block (section comment + blank + field + blank) at those lines |
| 1B — guard replaced with ConcurrentBag reassignment | PASS | Guard at L4084-4086 matches plan exactly; replacement is correct idiomatic pattern |
| 1C — doc comment updated | **FAIL** | Comment encodes `CYC = 3` — incorrect. See Violations section. |
| 2A — PTT-QX-/PTT-BE- guard, returns false, StringComparison.Ordinal | PASS | Exact guard inserted after L1515 before foreach; semantics correct |
| Protected regions identified (IsBracketLeg, CancelOneAccount, IsAtmBracketName) | PASS | All three listed in §5 with correct line ranges |
| _rules at L178 explicitly protected | PASS | Source confirms _rules at L178; §3.3 and §5 both list it as untouched |

#### B. JS P0 Rules

| Item | Result | Notes |
|------|--------|-------|
| JS-021 no lock() | PASS | Plan §2.4 and §3.4 confirm; neither proposed snippet contains lock( |
| JS-001 no throw new Exception | PASS | Plan §2.4 and §3.4 confirm; no throw new in any proposed code |
| ASCII-only ("PTT-QX-", "PTT-BE-") | PASS | Both strings are 7-character pure ASCII; all doc comment text is ASCII |
| CYC ≤ 8 for LoadRules() | PASS | Actual CYC is 4 (simplified) or 5 (strict McCabe) — both ≤ 8; method is compliant |
| CYC ≤ 8 for TryCancelFollowerEntries() | PASS | Reviewer-counted CYC = 6 (OrderState + IsAtmBracket + name-null + OR-branch + foreach + acc-null) ≤ 8 |

#### C. File Scope

| Item | Result | Notes |
|------|--------|-------|
| Only CopyEngine.cs modified | PASS | Plan §1 states explicitly; both tickets target CopyEngine.cs only |
| No new files created in src/ | PASS | No new file creation mentioned or implied anywhere in the plan |

#### D. 7-Scan Checklist

| Item | Result | Notes |
|------|--------|-------|
| Plan contains 7-scan checklist | PASS | §6 defines exactly 7 scans |
| SCAN-01 lock() grep | PASS | Grep pattern defined; expected result stated |
| SCAN-02 throw new grep | PASS | Grep pattern defined |
| SCAN-03 non-ASCII grep | PASS | Grep pattern defined |
| SCAN-04 _persistenceLoaded 0-match grep | PASS | Expected 0 matches; correct post-deletion verification |
| SCAN-05 PTT-QX- presence grep | PASS | Verifies new guard line present |
| SCAN-06 LoadRules CYC manual count | **FAIL** | Stated CYC = 3 is incorrect. See Violations section. |
| SCAN-07 TryCancelFollowerEntries CYC manual count | PASS | CYC = 6 verified correct by reviewer |

#### E. CYC Verification

| Item | Result | Notes |
|------|--------|-------|
| LoadRules CYC count in plan | **FAIL** | Plan claims CYC = 3. Reviewer count: File.Exists guard (+1) + try/catch (+1) + if(container!=null&&container.Rules!=null) (+1) + foreach (+1) = base 1 + 4 = **CYC 5** (simplified McCabe, counting compound && as 1 decision point). Even on strict McCabe (counting each boolean operator) CYC = 6. The claim of 3 is incorrect in both counting conventions. |
| TryCancelFollowerEntries CYC OR-condition not overcounted | PASS | Plan §3.3 correctly identifies OR as one branch (+1), giving total CYC = 6 |
| Both methods ≤ 8 threshold | PASS | LoadRules actual CYC (4-5) and TryCancelFollowerEntries CYC (6) are both ≤ 8 |

#### F. Return Semantics

| Item | Result | Notes |
|------|--------|-------|
| New guard returns false = do not cancel followers | PASS | Plan §3.2 explicitly documents this semantic and explains why it is correct |
| CancelOneAccount (user-cancel path) correctly distinguished as untouched | PASS | Plan §3.3 bullet 2 explicitly states CancelOneAccount is untouched and explains that user-cancel of PTT- prefixed orders IS intentional there |

---

### Violations Found

#### V1 — CYC Claim Incorrect in Change 1C (Plan Accuracy Defect)

**Location**: §2.2 Change 1C (proposed doc comment text), §2.3 Safety Analysis, §6 SCAN-06

**Description**: The plan asserts `CYC = 3` for `LoadRules()` after fix. The actual post-fix method body contains:

```
Decision points (simplified McCabe):
  1. if (!File.Exists(path)) return;                         +1
  2. try { ... } catch (Exception) { }                       +1
  3. if (container != null && container.Rules != null)        +1
  4. foreach (var dto in container.Rules)                     +1
  Base = 1
  Total CYC = 5
```

The plan omits the `if (container != null && container.Rules != null)` null-guard already present in the body (L4099 of current source). That guard does not disappear with the change — it remains. The fix only removes the `_persistenceLoaded` one-shot guard (−1 branch), reducing CYC from the **existing** 4 → **not to 3** but to the remaining count. Under simplified CYC: existing CYC is already 5 (the plan's "CYC before = 4" also appears to omit the container null-check), so post-fix CYC is 4. Under strict McCabe: existing CYC is 5 (4 decisions + 1 for &&), post-fix is 5 (the && does not go away).

**Impact**: The engineer will copy the incorrect `CYC = 3` string into the source doc comment (change 1C). The comment will be false metadata in the committed file.

**This is not a P0/P1 JS DNA violation** (both possible correct values — 4 or 5 — are ≤ 8, so the CYC ≤ 8 threshold is satisfied). However, it is a plan factual defect that will produce incorrect documentation. The plan must be corrected before the engineer encodes it.

**Resolution required**: Update the proposed doc comment in change 1C to state `CYC = 4` (simplified McCabe, excluding the && operator as a separate decision) or `CYC = 5` (strict McCabe). Correspondingly update §2.3 Safety Analysis ("CYC drop: removing the guard drops CYC from 4 to 3" → correct to "from 5 to 4" or "from 4 to 3 if && is not counted separately") and SCAN-06 expected result.

---

### Decision

REVIEW_BLOCKED — CYC claim in change 1C is factually incorrect (plan states CYC=3, correct value is 4-5). The engineer will copy wrong metadata into the doc comment. All JS P0/P1 DNA rules pass; both methods are ≤ 8. No structural, scoping, or safety issues. One targeted correction required: update the CYC count in the change 1C doc comment, §2.3 analysis, and SCAN-06 expected result to reflect the actual post-fix CYC (4 under simplified McCabe, 5 under strict McCabe). Re-submit for Phase 3 unlock after correction.

---

## Cycle 2 Review

**Date**: 2026-08-10
**Reviewer**: ptt-plan-reviewer
**Review cycle**: 2 of 2 (correcting V1 from Cycle 1)

### Items Corrected Since Cycle 1

| Item | Cycle 1 Finding | Cycle 2 Status | Evidence |
|------|----------------|----------------|----------|
| Change 1C doc comment CYC claim | FAIL — plan stated `CYC = 3` | **CORRECTED** | §2.2 now reads `CYC = 4 (File.Exists guard + try/catch + null-check + foreach)` |
| §2.3 Safety Analysis CYC narrative | FAIL — stated drop "from 4 to 3" | **CORRECTED** | §2.3 now reads "drops CYC from 5 to 4. Remaining branches: File.Exists + try/catch + null-check + foreach." |
| SCAN-06 expected result | FAIL — stated `= 3` | **CORRECTED** | §6 SCAN-06 now reads `manual count = 4 ≤ 8` |

### CYC Independent Verification (Reviewer Count)

Source lines L4082–4112 of `CopyEngine.cs` (pre-fix state, confirmed by read):

| Branch | Line | McCabe +1 |
|--------|------|-----------|
| `if (_persistenceLoaded)` | L4084 | +1 (this guard is REMOVED by the fix) |
| `if (!File.Exists(path))` | L4089 | +1 (remains) |
| `try/catch` | L4092/L4108 | +1 (remains) |
| `if (container != null && container.Rules != null)` | L4099 | +1 simplified (remains) |
| `foreach (var dto in container.Rules)` | L4101 | +1 (remains) |

Pre-fix CYC = 1 + 5 = **5** (simplified McCabe). Post-fix (guard removed): 1 + 4 = **CYC 4**. Plan claim of `CYC = 4` is **correct**. ≤ 8 threshold: **PASS**.

### All Cycle 1 PASS Items — Confirmed Still Passing

| Item | Status |
|------|--------|
| 1A — field deletion at L3868-3871 | PASS (plan unchanged) |
| 1B — guard replaced with `ConcurrentBag` reassignment | PASS (plan unchanged) |
| 2A — PTT-QX-/PTT-BE- guard, returns false, `StringComparison.Ordinal` | PASS (plan unchanged) |
| Protected regions (IsBracketLeg, CancelOneAccount, IsAtmBracketName) | PASS (plan unchanged) |
| JS-021 no `lock()` | PASS |
| JS-001 no `throw new Exception` | PASS |
| ASCII-only | PASS |
| CYC ≤ 8 — `TryCancelFollowerEntries()` = 6 | PASS |
| File scope — CopyEngine.cs only | PASS |
| 7-scan checklist present and complete | PASS |
| Return semantics documented | PASS |
| Thread model summary provided | PASS |
| NT8 API surface — no new calls | PASS |

### New Violations Introduced by Correction

**None.** The correction was purely textual (doc comment wording, safety analysis narrative, SCAN-06 expected value). No structural, scoping, or rule-compliance changes were introduced.

### Final Decision

**REVIEW_PASS**

All three items flagged in Cycle 1 have been corrected. The plan now correctly states `CYC = 4` in:
- Change 1C proposed doc comment (§2.2)
- §2.3 Safety Analysis narrative
- §6 SCAN-06 expected result

The reviewer-counted post-fix CYC of `LoadRules()` is **4** under simplified McCabe, matching the plan exactly. Both methods satisfy the ≤ 8 threshold. All Jane Street DNA rules pass. No structural or scoping issues. No new violations.

**Phase 3 (ticket generation) is UNLOCKED.**
