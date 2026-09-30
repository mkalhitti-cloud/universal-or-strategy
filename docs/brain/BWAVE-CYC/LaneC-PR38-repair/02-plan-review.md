# Plan Review: BWAVE-CYC LaneC-PR38-repair

**Reviewer**: ptt-plan-reviewer  
**Phase**: 2 (Plan Review)  
**Plan file**: `docs/brain/BWAVE-CYC/LaneC-PR38-repair/02-architecture-plan.md`  
**Date**: 2026-08-10  
**Verdict**: **REVIEW_FAIL**

---

## Sources Read

1. `docs/brain/BWAVE-CYC/LaneC-PR38-repair/02-architecture-plan.md` — full read
2. `docs/standards/jane-street/RULES_CATALOG.md` — full read (JS-001..JS-041+ confirmed UTF-8 clean)
3. `src/PropTraderTools/TradeCopierAddOn.cs` (current working tree — main branch baseline) — grep + read
4. `src/PropTraderTools/TradeCopierWindow.cs` (current working tree — main branch baseline) — grep + read

---

## LANE-SPLIT GATE COMPLIANCE

**Gate result in plan**: `LANE-SPLIT GATE RESULT: SINGLE-PIPELINE`

| Gate Item | Plan Answer | Reviewer Assessment | Status |
|-----------|------------|---------------------|--------|
| Gate result line present | ✅ Present | Exact string present at plan line 50 | PASS |
| Q1 rationale (same method / 50 lines) | C-1/C-2 share TryDetachAndRemoveStalePanels | Sound — C-2 cannot exist until C-1 restores the method | PASS |
| Q2 rationale (B design depends on A design) | Yes for C-2 only | Sound — all other tickets are file-independent | PASS |
| Q3 (standalone value) | Yes for all | Each ticket addresses an isolated regression | PASS |
| Q4 (independent SIM path) | Yes for all | Each ticket has a named SIM gate described | PASS |

**LANE-SPLIT GATE: PASS** — SINGLE-PIPELINE is correctly selected and rationale is sound.

---

## RULES CATALOG COMPLIANCE

| Rule | Check | Plan claim | Reviewer finding | Status |
|------|-------|-----------|-----------------|--------|
| JS-021 | No `lock(` in any scope change | PASS | Grep confirms zero `lock(` in TradeCopierAddOn.cs, TradeCopierPanel.cs, TradeCopierWindow.cs | PASS |
| JS-002 | No `return null` for new APIs | PASS — NT8 null from `TrySetPanelInstrument` called out as approved | InjectPanelIntoGrid returns `false` (not null); helpers are void; approved NT8 pattern documented | PASS |
| JS-033 | No `async void` (non-event-handler) | PASS | All restored helpers are synchronous static methods; no new async void introduced | PASS |
| ASCII | All identifiers ASCII | PASS | All method names and identifiers in plan are 7-bit ASCII | PASS |
| NT8 API | No invalid AddOn API usage | PASS — explicitly states AtmStrategyCreate/AtmStrategyChangeStopTarget are StrategyBase-only | Valid AddOn APIs used (Grid, ChartTrader, NTMenuItem). Out-of-scope NT8 APIs explicitly noted | PASS |

---

## TICKET COMPLETENESS

### TICKET C-1 [P1 CCN regression] — Restore 6 helpers

**Present**: ✅  
**6 methods named**: `RemoveExistingTradeCopierEntries`, `CollectStalePanelChildren`, `RemoveStalePanelChild`, `TryDetachAndRemoveStalePanels`, `InjectPanelIntoGrid`, `TrySetPanelInstrument` — all 6 present with exact signatures  
**DoInject delegation**: ✅ Delegates to TryDetachAndRemoveStalePanels, TrySetPanelInstrument, InjectPanelIntoGrid  
**WireControlCenterMenu delegation**: ✅ Delegates to RemoveExistingTradeCopierEntries  
**Comment block format**: ✅ `// BWAVE-CYC T8: extracted helper...` markers specified  
**CCN targets**: ✅ DoInject=7 ≤ 8, WireControlCenterMenu=5 ≤ 5  
**7-scan checklist**: ✅ Present — all 7 scans listed with PASS/action  
**Traceability**: ✅ "P1 CCN regression" — traces to qlty CCN spec requirement  

**Signature cross-check against main baseline** (verified via live grep):  
- `RemoveExistingTradeCopierEntries` — ✅ matches  
- `CollectStalePanelChildren` — ✅ matches  
- `RemoveStalePanelChild` — ✅ matches  
- `TryDetachAndRemoveStalePanels` — ✅ matches (C-2 modifies this)  
- `InjectPanelIntoGrid` — ✅ matches  
- `TrySetPanelInstrument` — ✅ matches  

**C-1: PASS**

---

### TICKET C-2 [Major] — Descending RowDef removal

**Present**: ✅  
**Depends on C-1**: ✅ Explicitly stated — "Depends on: C-1 must be complete"  
**Fix location**: ✅ In `TryDetachAndRemoveStalePanels` (NOT in `RemoveStalePanelChild` — correct per spec)  
**Sort strategy**: ✅ `List<T>.Sort(Comparison<T>)` in-place sort by `Grid.GetRow` descending  
**CCN stays at 2**: ✅ Sort lambda does not increment outer CCN  
**7-scan checklist**: ✅ Present  
**Execution order**: ✅ C-2 appears AFTER C-1 in execution order diagram  

**C-2: PASS**

---

### TICKET C-3 [Major] — Null guard in OnWindowDestroyed

**Present**: ✅  
**Fix**: Changes `if (_panels.TryRemove(chart, out panel))` to `if (_panels.TryRemove(chart, out panel) && panel != null)`  
**Rationale**: ✅ Sound — `TryAdd(chart, null)` creates a null entry; TryRemove can retrieve it  
**Verified in baseline**: `OnWindowDestroyed` at line 108-109 of main baseline does NOT have the null guard — confirming the fix is needed  
**7-scan checklist**: ✅ Present  

**C-3: PASS**

---

### TICKET C-4 [Major] — Remove UpdateButtonColors from BuildUI

**Present**: ✅  
**Fix**: Replace `UpdateButtonColors(false, false)` with direct `_beBtn2.Background = BrushInactive; _globalBeBtn2.Background = BrushInactive;`  
**Rationale**: ✅ Sound — UpdateButtonColors requires _leaderAccount which is not available at construction time  
**7-scan checklist**: ✅ Present  
**Traceability**: ✅ "CodeRabbit CR38" class of regressions  

**C-4: PASS**

---

### TICKET C-5 [Minor] — `_atrSizingRow2` field + gate in ApplyRowVisibilityFlags

**Present**: ✅  
**Field declaration**: ✅ `private FrameworkElement _atrSizingRow2;`  
**Assignment in BuildRiskAtrRow**: ✅ `_atrSizingRow2 = atrRow;`  
**Gate in ApplyRowVisibilityFlags**: ✅ Mirrors `_atrRow` visibility condition  
**Note about engineer reading existing condition**: ✅ Plan says "Engineer must read the existing `_atrRow` gating condition" — acceptable for a plan; not a gap  
**7-scan checklist**: ✅ Present  

**C-5: PASS**

---

### TICKET C-6 [Major, Security] — `_armBeBtns` + `_tightenBtns` gated in ApplyFeatureFlags

**Present**: ✅  
**Fields verified**: ✅ Grep confirms `_armBeBtns` (line 53), `_tightenBtns` (line 50) exist in TradeCopierWindow.cs  

**⚠️ VIOLATION FOUND — SPEC MISMATCH (FAIL trigger):**

The plan's "Exact fix" proposes:
```csharp
foreach (var b in _beBtns) b.IsEnabled = f.BreakEven;
foreach (var b in _armBeBtns) b.IsEnabled = f.BreakEven;
foreach (var b in _tightenBtns) b.IsEnabled = f.BreakEven;
```

**However**, on the Lane C branch (confirmed via main baseline which is the established pattern), `ApplyFeatureFlags` does NOT use raw `foreach (var b in ...) b.IsEnabled` loops. The existing codebase uses `ApplyButtonGroupFlag(collection, bool, string)` which sets **both** `IsEnabled` AND `ToolTip`. The plan's proposed fix sets only `IsEnabled`, silently omitting `ToolTip` assignment for `_armBeBtns` and `_tightenBtns`. This:

1. Produces inconsistent UX — arm/tighten buttons lack tooltip on disable while all other gated buttons have one
2. Does not match the pattern established for `_beBtns`, `_trimBtns`, `_flattenBtns`, `_cancelBtns`
3. The plan re-states the existing `_beBtns` foreach line verbatim, conflicting with the actual `ApplyButtonGroupFlag` call at line 430

The correct fix is:
```csharp
ApplyButtonGroupFlag(_armBeBtns, f.BreakEven, "Arm BE requires Pro tier");
ApplyButtonGroupFlag(_tightenBtns, f.BreakEven, "Tighten requires Pro tier");
```

**This is a spec completeness violation** (FAIL — "Any spec requirement not addressed in the plan" maps to the converse: plan spec that does not match the target codebase pattern produces a broken implementation). The plan's "Exact fix" block for C-6 will cause the engineer to write inconsistent code that omits ToolTip behavior.

**C-6: FAIL**

---

### TICKET C-7 [Major] — TryParseArmBeBuffer uses separate `parsed` variable

**Present**: ✅  
**Fix concept**: ✅ Correct — using a separate `parsed` variable prevents TryParse stomping the `buf` default  

**⚠️ VIOLATION FOUND — WRONG METHOD SIGNATURE (FAIL trigger):**

The plan states the method is at "lines 1229-1236" and specifies this signature:
```csharp
private static int TryParseArmBeBuffer(TextBox bufBox)
```

**However**, the actual method in TradeCopierWindow.cs (verified via grep and read — line 970) is:
```csharp
private static int TryParseArmBeBuffer(object[] tag)
```

The current method extracts the `TextBox` from `tag[2]` internally. The plan's "Exact fix" shows a **completely different method signature** — `TryParseArmBeBuffer(TextBox bufBox)` accepts a `TextBox` directly. If the engineer follows the plan's exact fix verbatim, they will:

1. **Change the method signature**, which would break the existing caller at line 1002 that passes `tag` (object[])
2. Produce a **compilation error** (CS1501 — wrong argument type)

The fix body (using `parsed` variable, `parsed >= 0` guard) is logically correct, but it must be applied **within the existing `object[] tag` signature** without changing the method parameter. The plan's exact fix block is non-compilable as written.

This is a plan defect that would cause the engineer to produce broken code. It is a FAIL on: "Any spec requirement not addressed in the plan" / Spec completeness.

**C-7: FAIL**

---

### TICKET C-8 [P2] — BrushInactive on `_quickBtn`/`_quickAllBtn`

**Present**: ✅  
**Fix**: ✅ Add `Background = BrushInactive` to both button initializers  
**BrushInactive field**: ✅ Confirmed as static field in TradeCopierPanel — safe at construction time  
**7-scan checklist**: ✅ Present  

**C-8: PASS**

---

### TICKET C-9 [SA1507] — Blank line fix in BwaveCycLaneCTests.cs

**Present**: ✅  
**Fix**: ✅ Remove one extra blank line at line 566  
**7-scan checklist**: ✅ Present — correctly notes SA1507 scans not applicable to concurrency/null rules  

**C-9: PASS**

---

## 7-SCAN CHECKLIST AUDIT

All 9 tickets carry 7-scan checklists. Scans verified:

| Scan | Present in all tickets | Notes |
|------|----------------------|-------|
| SCAN-01 lock() | ✅ | All 9 |
| SCAN-02 async void | ✅ | All 9 |
| SCAN-03 return null | ✅ | All 9 |
| SCAN-04 ASCII | ✅ | All 9 |
| SCAN-05 CCN | ✅ | All 9 |
| SCAN-06 build | ✅ | All 9 |
| SCAN-07 tests | ✅ | All 9 |

**7-SCAN PRESENCE: PASS**

---

## SPEC REQUIREMENT TRACEABILITY

| Ticket | Source | Addressed |
|--------|--------|-----------|
| C-1 | qlty CCN (DoInject/WireControlCenterMenu regression) | ✅ |
| C-2 | Greptile P1 (ascending row removal corruption) | ✅ |
| C-3 | Greptile P2 / CodeRabbit CR38 (NRE on fast close) | ✅ |
| C-4 | CodeRabbit CR38 (BE ALL shows Idle) | ✅ |
| C-5 | CodeRabbit CR38 (ATR row always visible) | ✅ |
| C-6 | CodeRabbit CR38 (security: Arm BE/Tighten ungated) | ✅ spec addressed; ❌ fix pattern wrong |
| C-7 | CodeRabbit CR38 (default buffer 2 stomped) | ✅ spec addressed; ❌ exact fix uses wrong signature |
| C-8 | CodeRabbit CR38 (quick button background) | ✅ |
| C-9 | qlty SA1507 (blank line) | ✅ |

---

## VIOLATION SUMMARY

| # | Ticket | Rule / Principle | Severity | Description |
|---|--------|-----------------|----------|-------------|
| V1 | C-6 | Spec Completeness (P0) | FAIL | Plan's "Exact fix" uses raw `foreach (var b in ...) b.IsEnabled` instead of `ApplyButtonGroupFlag()` pattern. Omits `ToolTip` assignment that all other gated button groups receive. Engineer following the plan exactly produces inconsistent UI (no tooltip on Arm BE / Tighten when disabled). The stated `_beBtns` foreach line also conflicts with the actual `ApplyButtonGroupFlag(_beBtns, ...)` call at line 430. |
| V2 | C-7 | Spec Completeness (P0) | FAIL | Plan's "Exact fix" specifies `private static int TryParseArmBeBuffer(TextBox bufBox)` — a **different method signature** from the actual `private static int TryParseArmBeBuffer(object[] tag)`. The fix body is conceptually correct but applying the plan's exact code block verbatim would change the method signature and break the caller (compile error CS1501). |

---

## OVERALL VERDICT

**REVIEW_FAIL**

Two specification defects found in the "Exact fix" code blocks:

- **V1 (C-6)**: Fix uses wrong button-gating pattern. Must use `ApplyButtonGroupFlag()` to match established code pattern and include `ToolTip` assignment.  
- **V2 (C-7)**: Fix code block specifies wrong method signature (`TextBox bufBox` instead of `object[] tag`). The fix logic must be applied within the existing `object[] tag` signature.

**Return path**: Plan returns to ptt-architect for repair of C-6 "Exact fix" block and C-7 "Exact fix" block only. All other tickets (C-1, C-2, C-3, C-4, C-5, C-8, C-9) are correct and do not require re-review.

**No Jane Street DNA violations found** (JS-021, JS-002, JS-033, ASCII, NT8 constraints all PASS).

---

## RE-REVIEW: Cycle 1 Repair (ptt-plan-reviewer)

**Re-review date**: 2026-08-10  
**Reviewer**: ptt-plan-reviewer  
**Cycle**: 1 (repair of V1 and V2)  
**Repaired plan**: `docs/brain/BWAVE-CYC/LaneC-PR38-repair/02-architecture-plan.md` (REVIEW_PENDING Cycle 1 repair)

### Sources Read for Re-Review

1. `docs/brain/BWAVE-CYC/LaneC-PR38-repair/02-architecture-plan.md` — full read (repaired plan)
2. `docs/brain/BWAVE-CYC/LaneC-PR38-repair/02-plan-review.md` — original review (V1, V2 violations)
3. `src/PropTraderTools/TradeCopierWindow.cs` (branch `feature/bwave-cyc-lane-c2`) — grep + targeted read:
   - Lines 407–440: `ApplyButtonGroupFlag` signature + `ApplyFeatureFlags` body
   - Lines 969–977: `TryParseArmBeBuffer` actual signature and body

---

### V1 — C-6 Re-check: `ApplyButtonGroupFlag` pattern for `_armBeBtns` / `_tightenBtns`

**Original violation**: Plan used raw `foreach (var b in ...) b.IsEnabled` loops, omitting `ToolTip` assignment. Inconsistent with established `ApplyButtonGroupFlag()` pattern.

**Repaired plan (lines 469–471)**:
```csharp
ApplyButtonGroupFlag(_beBtns, f.BreakEven, "Break Even requires Pro tier");
ApplyButtonGroupFlag(_armBeBtns, f.BreakEven, "Arm Break-Even not available on this plan");
ApplyButtonGroupFlag(_tightenBtns, f.BreakEven, "Tighten Stop not available on this plan");
```

**Branch code verification** (lines 407–440, live read):
- `ApplyButtonGroupFlag` signature confirmed: `private static void ApplyButtonGroupFlag(IEnumerable<Button> btns, bool enabled, string disabledMessage)` — sets both `IsEnabled` and `ToolTip`.
- `ApplyFeatureFlags` body confirmed: uses `ApplyButtonGroupFlag(...)` for all existing gated groups (`_trimBtns`, `_flattenBtns`, `_cancelBtns`, `_beBtns`) — no raw `foreach`.
- `_armBeBtns` confirmed at line 53 (`private readonly List<Button>`), `_tightenBtns` at line 50.

**Assessment**: The repaired plan correctly uses `ApplyButtonGroupFlag()` for both new calls. Tooltips are provided. Pattern is consistent with all other gated button groups. The plan also retains the existing `_beBtns` call unchanged, correctly showing the three calls together.

**V1: RESOLVED** ✅

---

### V2 — C-7 Re-check: `TryParseArmBeBuffer(object[] tag)` signature and `tag[2]` index

**Original violation**: Plan specified `private static int TryParseArmBeBuffer(TextBox bufBox)` — wrong signature that would break caller and produce compile error CS1501.

**Repaired plan (lines 518–552)**:
```csharp
// BWAVE-CYC T6: TryParseArmBeBuffer -- parses buffer ticks from tag[2] TextBox.
// Default = 2. JS-002: returns int (never null). CCN=3.
private static int TryParseArmBeBuffer(object[] tag)
{
    int buf = 2;
    var bufBox = tag.Length > 2 ? tag[2] as TextBox : null;
    if (bufBox != null)
        if (int.TryParse(bufBox.Text?.Trim(), out int parsed) && parsed >= 0)
            buf = parsed;
    return buf;
}
```

**Branch code verification** (lines 969–977, live read):
- Actual signature: `private static int TryParseArmBeBuffer(object[] tag)` — **exact match**.
- Actual body: `tag.Length > 2 ? tag[2] as TextBox : null` — plan preserves this bounds check.
- Caller at line 1002: `int buf = TryParseArmBeBuffer(tag)` — passes `object[]`; repaired plan does not change this signature, so caller is unaffected.
- Bug confirmed in branch: `int.TryParse(bufBox.Text, out buf)` at line 975 stomps `buf` to 0 on failure. Repaired plan's `parsed` variable pattern correctly isolates the default.

**Assessment**: Signature is correct. `tag[2]` index is correct. `tag.Length > 2` bounds check preserved. `parsed` variable prevents default-stomping. `parsed >= 0` guard is a sound additional defensive check. Caller unaffected.

**V2: RESOLVED** ✅

---

### Confirmation: No New Violations Introduced

Re-reading the repaired C-6 and C-7 sections for DNA compliance:

| Rule | C-6 | C-7 |
|------|-----|-----|
| JS-021 `lock(` | PASS — no lock | PASS — no lock |
| JS-002 `return null` | PASS — void return | PASS — returns int |
| JS-033 `async void` | PASS | PASS — static method |
| ASCII | PASS — all identifiers ASCII | PASS |
| NT8 API | PASS — WPF/BCL only | PASS — WPF/BCL only |
| CCN | PASS — `ApplyButtonGroupFlag` calls add zero branches to `ApplyFeatureFlags` outer CCN | PASS — CCN=3 (bufBox null check + TryParse success + parsed≥0) ≤ 8 |

No Jane Street DNA violations introduced by the repairs.

---

### RE-REVIEW VERDICT

| Violation | Original Status | Re-review Status |
|-----------|----------------|-----------------|
| V1 (C-6): Wrong button-gating pattern | FAIL | **RESOLVED** |
| V2 (C-7): Wrong method signature | FAIL | **RESOLVED** |

All other tickets (C-1, C-2, C-3, C-4, C-5, C-8, C-9) were PASS in original review. No regressions introduced by the repairs to C-6 and C-7.

**REVIEW_PASS**
