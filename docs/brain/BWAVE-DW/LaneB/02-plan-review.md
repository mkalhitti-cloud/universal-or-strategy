# BWAVE-DW LaneB Plan Review

**Reviewer**: ptt-plan-reviewer
**Date**: 2026-08-26
**Input**: docs/brain/BWAVE-DW/LaneB/02-architecture-plan.md
**Phase**: 2 (Plan Review Gate)

---

## Lane-Split Gate Check: PASS

Gate result **explicitly stated** in plan Section 1.

| Q | Question | Answer | Verified |
|---|----------|--------|---------|
| Q1 | Any tickets depend on Lane A output? | NO | ✓ |
| Q2 | Any tickets modify the same method simultaneously? | NO | ✓ |
| Q3 | Any ticket requires preceding ticket to compile? | NO | ✓ |
| Q4 | All tickets executable by single engineer, no merge conflict? | YES | ✓ |

**RESULT**: SINGLE PIPELINE declared. All four Q&A rows present. Gate check: **PASS**.

---

## Rules Catalog Gate Check: PASS

All P0 rules checked against the work scope:

| Rule ID | Category | Severity | Claim in Plan | Reviewer Verdict |
|---------|----------|----------|---------------|-----------------|
| JS-021 | No lock() | P0 | PASS: zero lock() in changed code | PASS — no lock() introduced by either active ticket |
| JS-033 | No async void | P0 | PASS: no async methods added | PASS — neither B-1 nor B-4 introduce async methods |
| JS-002 | No return null | P0 | PASS: returns value tuple | PASS — BuildFollowerMultipliers returns `(int[], string[])` value tuple, not null |
| JS-001 | No throw in hot path | P0 | PASS: no exceptions thrown | PASS — no throw statements in the proposed B-4 refactor |
| JS-036 | No byte[] heap alloc in hot path | P0 | N/A: WPF UI code | PASS — WPF construction code, not a hot path. `new int[N]` / `new string[N]` for UI data is correct. |

**Rules Catalog Gate: PASS** — zero P0 violations in plan.

---

## Factual Accuracy Check (per ticket): PASS

Each architect "already done" / "active" claim verified against live code.

### B-1: BuildArrowCluster has exactly 1 caller — VERIFIED

**Claim**: BuildArrowCluster at lines 1196–1244 is called at line 1172 inside `BuildBufferedButtonsRow` foreach loop. Method has exactly 1 caller.

**Evidence from live code**:
- `BuildArrowCluster` defined at line **1200** (comment at 1196). ✓
- Called at line **1172**: `var (cluster, btn) = BuildArrowCluster(s.Content, s.Bg, s.Teal, s.Up, s.Dn, s.Main);` — inside `foreach (var s in specs)`. ✓
- grep result shows exactly **2 non-definition occurrences**: line 1172 (call site) and line 1196 (comment). Only 1 real caller. ✓

**Conclusion**: Method is NOT dead. Decision to keep method and delete only the 3 tests is **correct**.

---

### B-2: BrushInactive already flows to all 6 buttons — VERIFIED

**Claim**: Lines 1163–1168 all 6 specs pass `BrushInactive` as the `Bg` parameter. `BuildArrowCluster` line ~1233 creates the button with `Background = mainBackground`.

**Evidence from live code** (grep on BrushInactive, lines 1163–1168):
```
Line 1163: (FormatBuffer("Trim", ...), BrushInactive, ...)     -> _trimBtn2
Line 1164: (FormatBuffer("Flatten", ...), BrushInactive, ...)  -> _flattenBtn2
Line 1165: (FormatBuffer("BE", ...), BrushInactive, ...)       -> _beBtn2
Line 1166: (FormatGlobalBeBuffer(...), BrushInactive, ...)     -> _globalBeBtn2
Line 1167: (FormatBuffer("Quick", ...), BrushInactive, ...)    -> _quickBtn
Line 1168: (FormatBuffer("Quick ALL", ...), BrushInactive, ...) -> _quickAllBtn
```
All 6 buttons including the 4 cited (`_beBtn2`, `_globalBeBtn2`, `_quickBtn`, `_quickAllBtn`) receive `BrushInactive`. ✓

**Conclusion**: B-2 ALREADY DONE claim is **correct**. Verify-only status is **correct**.

---

### B-3: All 6 WPF helpers extracted in TradeCopierWindow.cs — VERIFIED

**Claim**: All 6 helpers exist as private methods in TradeCopierWindow.cs at lines 603–811.

**Evidence from live code** (grep BuildFollowerListBox|BuildBeCluster|...):

| Helper | Line | Called From (grep verified) |
|--------|------|-----------------------------|
| `BuildFollowerListBox` | 603 | Lines 501 (BuildRuleRow), 551 (BuildDynamicRuleRow) |
| `BuildBeCluster` | 620 | Lines 509 (BuildRuleRow), 559 (BuildDynamicRuleRow) |
| `BuildTightenCluster` | 653 | Lines 516 (BuildRuleRow), 566 (BuildDynamicRuleRow) |
| `BuildArmBeCluster` | 686 | Lines 520 (BuildRuleRow), 570 (BuildDynamicRuleRow) |
| `BuildAtmColumnPanel` | 719 | Lines 506 (BuildRuleRow), 556 (BuildDynamicRuleRow) |
| `BuildActionButtons` | 750 | Lines 507 (BuildRuleRow), 557 (BuildDynamicRuleRow) |

All 6 methods present and both call sites confirmed. ✓

**Conclusion**: B-3 ALREADY DONE claim is **correct**. Verify-only status is **correct**.

---

### B-4: Target is BuildFollowerMultipliers (not BuildAtmMap) — VERIFIED

**Claim**: The remaining nested for+foreach is in `BuildFollowerMultipliers` (lines 2786–2802), not in `BuildAtmMap`.

**Evidence from live code** (read lines 2785–2810):
- `BuildFollowerMultipliers` at line 2786 contains `for (int i = 0; i < followers.Length; i++)` nested with `foreach (var item in _followerItems)`. ✓
- `BuildAtmMap` begins at line 2804 — separate method, no nested loop. ✓

**Conclusion**: B-4 target identification is **correct**.

---

### B-5: Tab order already correct — VERIFIED

**Claim**: `BuildRuleRow` and `BuildDynamicRuleRow` `Children.Add` order already matches left-to-right column order.

**Evidence**: The 6 helper methods are called in column order (followerLb→atmPanel→BuildActionButtons→beCluster→tightenCluster→armBeCluster) as confirmed by lines 501, 506, 507, 509, 516, 520. The BuildActionButtons adds 5 buttons internally in left-to-right order. ✓

**Conclusion**: B-5 ALREADY DONE claim is **correct**. Verify-only status is **correct**.

---

## Scope Completeness: PASS

All 5 original DW items addressed:

| Ticket | Original Intent | Plan Decision | Correct? |
|--------|----------------|---------------|---------|
| B-1 | Delete dead BuildArrowCluster method + 3 tests | Keep method (has 1 caller); delete 3 reflection tests only | ✓ CORRECT |
| B-2 | Add BrushInactive to 4 buttons | Verify-only — already done by BWAVE-CYC | ✓ CORRECT |
| B-3 | Extract 6 WPF helpers | Verify-only — already done by BWAVE-CYC | ✓ CORRECT |
| B-4 | Replace nested loop in BuildAtmMap | Refactor BuildFollowerMultipliers (correct target) | ✓ CORRECT |
| B-5 | Fix tab order in BuildRuleRow | Verify-only — already correct | ✓ CORRECT |

**Scope Completeness: PASS** — all 5 items addressed with correct decisions.

---

## 7-Scan Checklist Presence: PASS

Both active tickets carry a full 7-scan checklist:

| Ticket | Status | Checklist Present? | Scans |
|--------|--------|--------------------|-------|
| B-1 (delete tests) | ACTIVE | ✓ YES — plan lines 135–142 | 7 scans |
| B-4 (refactor BuildFollowerMultipliers) | ACTIVE | ✓ YES — plan lines 302–309 | 7 scans |

Global SCAN table also present in Section 10 (plan lines 404–415) listing all 7 scans with expected results.

Verify-only tickets (B-2, B-3, B-5) do not require a 7-scan checklist per plan conventions (no source edit), which is acceptable.

**7-Scan Checklist Presence: PASS**.

---

## CYC Verification: PASS

For Ticket B-4, `BuildFollowerMultipliers` after refactor:

**Preferred implementation** (plan-recommended, no LINQ):
```
base(1) + foreach(+1) + if (item.Account == null)(+1) + if (idx < 0)(+1) = CYC 4
```

**Alternative (LINQ Contains) also shown in plan**:
```
base(1) + foreach(+1) + if null(+1) + if !Contains(+1) = CYC 4
```

Both variants yield **CYC = 4**, well within the CYC <= 8 mandate. ✓

Before: CYC = base(1) + for(+1) + foreach(+1) + if account!=followers[i](+1) = 4. (Plan note on discrepancy with comment CCN=3 is a pre-existing comment error, not introduced by this plan.)

**CYC Verification: PASS** — refactored method CYC = 4, unchanged from current.

---

## Violations

**None.** No violations found across all 6 check categories.

---

## OVERALL: REVIEW_PASS
