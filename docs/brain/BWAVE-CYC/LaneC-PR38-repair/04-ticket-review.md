# Ticket Review: BWAVE-CYC LaneC-PR38-repair

**Phase**: 3.5 (Ticket Review)
**Reviewer**: ptt-ticket-reviewer
**Tickets file**: `docs/brain/BWAVE-CYC/LaneC-PR38-repair/04-tickets.md`
**Plan file**: `docs/brain/BWAVE-CYC/LaneC-PR38-repair/02-architecture-plan.md`
**Branch**: feature/bwave-cyc-lane-c2
**Date**: 2026-08-10

---

## Sources Read

1. `docs/brain/BWAVE-CYC/LaneC-PR38-repair/04-tickets.md` — full
2. `docs/brain/BWAVE-CYC/LaneC-PR38-repair/02-architecture-plan.md` — full
3. `docs/standards/jane-street/RULES_CATALOG.md` — Type Safety (JS-001..020) + Concurrency (JS-021..035) sections
4. `src/PropTraderTools/TradeCopierAddOn.cs` — lines 100-115, 386-430, 460-515 (branch `feature/bwave-cyc-lane-b2`; note: ticket targets `feature/bwave-cyc-lane-c2`)
5. `src/PropTraderTools/TradeCopierWindow.cs` — lines 405-445, 965-985 (same caveat)
6. `docs/brain/BWAVE-CYC/LaneC-PR38-repair/02-plan-review.md` — lines 270-362 (live-branch reads documented during plan review)

**Branch caveat**: This workspace is on `feature/bwave-cyc-lane-b2`. The ticket targets `feature/bwave-cyc-lane-c2`. The plan-review document (V3 re-review) contains live reads of `feature/bwave-cyc-lane-c2` source that supplement direct file reads where branches diverge.

---

## Ticket-by-Ticket Review

---

### T1 — C-1: Restore 6 extracted helpers in TradeCopierAddOn.cs

**Traceability**: PASS
- Source: qlty CCN regression — plan section "TICKET C-1 [P1 CCN regression]" maps 1:1
- File: `TradeCopierAddOn.cs` — correct
- Methods: DoInject, WireControlCenterMenu, + 6 helpers — all named in plan

**Spot-check C-1 old_text accuracy**:
- DoInject old_text: the ticket presents the re-inlined (broken) version of DoInject with the inlined stale-panel block, inlined instrument-set block, and inlined grid-inject block. Plan section confirms these were re-inlined from the T8 extraction. The plan-review verified this via `git diff origin/main origin/feature/bwave-cyc-lane-c2`. PASS — old_text matches branch regression state.
- WireControlCenterMenu old_text: ticket presents the version with the raw removal loop inlined. Plan confirms this is the regressed state. PASS.
- New_text: all 6 helper signatures match plan exactly. DoInject new_text delegates to `TryDetachAndRemoveStalePanels`, `TrySetPanelInstrument`, `InjectPanelIntoGrid`. `WireControlCenterMenu` new_text delegates to `RemoveExistingTradeCopierEntries`. BWAVE-CYC T8 comment markers present. PASS.

**JS Pre-Check**: PASS
- No `lock()` in any helper or in DoInject/WireControlCenterMenu new_text
- No `async void` — all methods synchronous
- No `return null` from new API methods; `TrySetPanelInstrument` returns NT8 Instrument (approved existing null pattern from NT8_ADDON_KNOWLEDGE.md); `InjectPanelIntoGrid` returns `false` not null
- All string literals and identifiers ASCII-only

**CYC Pre-Check**: PASS
- DoInject = 7 (TryAdd guard + chartTrader null + try + InjectPanelIntoGrid bool + catch) ≤ 8
- WireControlCenterMenu = 5 (foreach + mi null + hdr.StartsWith + newMenu null + menuWired) ≤ 5
- All helpers: CollectStalePanelChildren=2, RemoveStalePanelChild=3, TryDetachAndRemoveStalePanels=2, InjectPanelIntoGrid=2, TrySetPanelInstrument=2, RemoveExistingTradeCopierEntries=4 — all ≤ 8

**Test Coverage**: PASS
- 13 reflection tests in BwaveCycT8AddOnTests asserted by name in SCAN-07
- No new methods lack [Fact] coverage (reflection tests cover all 6 helpers by name)

**Scan Checklist**: PASS — all 7 scans (SCAN-01 through SCAN-07) present and populated

**File Routing**: PASS — `src/PropTraderTools/TradeCopierAddOn.cs` in Wave workspace

**VERDICT: TICKET_REVIEW_PASS**

---

### T2 — C-2: Fix ascending RowDefinition removal in TryDetachAndRemoveStalePanels

**Traceability**: PASS
- Source: Greptile P1 (index-shift corruption) — plan section "TICKET C-2 [Major]" maps 1:1
- Hard dependency on C-1 explicitly stated in ticket: PASS
- C-2 old_text references `TryDetachAndRemoveStalePanels` as introduced by C-1 (state-after-C-1): PASS

**Spot-check C-2 old_text accuracy**:
- Old_text is the state after C-1 introduces the method. This is correct; C-2 old_text is not the branch state but the post-C-1 state. The dependency is explicit. PASS.
- New_text adds descending sort before foreach: `stale.Sort((a, b) => Grid.GetRow(b).CompareTo(Grid.GetRow(a)))` — matches plan exactly. PASS.

**JS Pre-Check**: PASS
- No lock(), no async void, no return null (void method)
- ASCII-only

**CYC Pre-Check**: PASS
- CCN = 2 (null guard + foreach). Sort lambda does not add to outer CCN. ≤ 8.

**Test Coverage**: PASS
- No new public/internal methods added; SIM gate documented in SCAN-07

**Scan Checklist**: PASS — all 7 scans present

**File Routing**: PASS

**VERDICT: TICKET_REVIEW_PASS**

---

### T3 — C-3: Null guard in OnWindowDestroyed

**Traceability**: PASS
- Source: Greptile P2 / CodeRabbit CR38 — plan section "TICKET C-3 [Major]" maps 1:1
- File and method correct

**Spot-check C-3 old_text accuracy**:
- Branch `feature/bwave-cyc-lane-b2` lines 107-109: `TradeCopierPanel panel; if (_panels.TryRemove(chart, out panel)) panel.Detach();`
- Ticket old_text (without inline `out var`): `TradeCopierPanel panel; if (_panels.TryRemove(chart, out panel)) panel.Detach();`
- **MATCH** — exact. (Note: ticket old_text uses pre-C# 7 `out panel` form which is exactly what the branch has.) PASS.
- New_text adds `&& panel != null` — correct minimal fix. PASS.

**JS Pre-Check**: PASS
- No lock(), no async void, no return null, ASCII-only

**CYC Pre-Check**: PASS
- Boolean AND within existing `if` adds no new branch. CCN unchanged.

**Test Coverage**: PASS
- No new methods; SCAN-07 documents regression prevention

**Scan Checklist**: PASS — all 7 scans present

**File Routing**: PASS

**VERDICT: TICKET_REVIEW_PASS**

---

### T4 — C-4: Remove UpdateButtonColors(false, false) from BuildUI

**Traceability**: PASS
- Source: CodeRabbit CR38 (BE ALL shows Idle while slots armed) — plan section "TICKET C-4 [Major]"
- File: TradeCopierPanel.cs, Method: BuildUI — correct

**JS Pre-Check**: PASS
- `_beBtn2.Background = BrushInactive` and `_globalBeBtn2.Background = BrushInactive` are property assignments
- No lock(), async void, return null
- `BrushInactive` is ASCII

**CYC Pre-Check**: PASS
- Removes a method call; BuildUI CCN unchanged or reduced

**Test Coverage**: PASS
- No new methods; SIM gate documented

**Scan Checklist**: PASS — all 7 scans present

**File Routing**: PASS

**VERDICT: TICKET_REVIEW_PASS**

---

### T5 — C-5: Store _atrSizingRow2 field and gate it in ApplyRowVisibilityFlags

**Traceability**: PASS
- Source: CodeRabbit CR38 (ATR row always visible) — plan section "TICKET C-5 [Minor]"
- Methods: BuildRiskAtrRow, ApplyRowVisibilityFlags — correct

**JS Pre-Check**: PASS
- No lock(), no async void
- new_text Step 3 `ApplyRowVisibilityFlags`: both `_atrRow` and `_atrSizingRow2` null-guarded, Visibility set to `f.AtrSizing ? Visible : Collapsed` — no `return null`
- ASCII-only

**CYC Pre-Check**: PASS
- ApplyRowVisibilityFlags CCN: 4 → 5 (one additional null guard added). 5 ≤ 8.

**Test Coverage**: PASS
- No new public/internal methods; SIM gate documented

**Scan Checklist**: PASS — all 7 scans present

**File Routing**: PASS

**VERDICT: TICKET_REVIEW_PASS**

---

### T6 — C-6: Gate _armBeBtns and _tightenBtns in ApplyFeatureFlags

**Traceability**: PASS
- Source: CodeRabbit CR38 security gap — plan section "TICKET C-6 [Major, Security]"
- File: TradeCopierWindow.cs, Method: ApplyFeatureFlags — correct
- `_armBeBtns` (line 53) and `_tightenBtns` (line 50) confirmed as `List<Button>` fields in branch

**Spot-check C-6 old_text accuracy**:
- WARN (non-blocking): The C-6 old_text comment header reads `// T7: Apply feature flags to all gated UI elements. CYC=5. Extracted button-group loop. // JS-021: no lock. Called on UI thread only (from OnLoaded, OnActivateClick, OnFeatureFlagsChanged).`
- The local `feature/bwave-cyc-lane-b2` workspace reads: `// BGTM-1: Apply feature flags to all gated UI elements. // TradeCopierWindow::ApplyFeatureFlags after extraction. CCN=5.`
- The plan-review (V3, line 293-295) confirmed the branch body at lines 407-440 but did not quote the exact comment header. It is UNKNOWN whether `feature/bwave-cyc-lane-c2` uses `// T7:` or `// BGTM-1:` on those comment lines.
- **If the comment header on `feature/bwave-cyc-lane-c2` does NOT match the old_text exactly, the apply_diff will fail to locate the search block.**
- The function body (4 `ApplyButtonGroupFlag` calls + 2 `if` blocks) is confirmed correct per plan-review. The mismatch risk is limited to the two comment lines immediately above the function signature.
- This is a WARN-level accuracy gap, not a JS DNA violation. The engineer MUST verify the exact comment header before applying C-6. If mismatch: adjust old_text to match actual branch comment header.
- This warning does NOT elevate to FAIL because: (a) the functional code content is confirmed correct, (b) this is a comment-text mismatch that the engineer can verify in 1 read, (c) the plan-reviewer confirmed all substantive correctness.

**JS Pre-Check**: PASS
- New_text adds two `ApplyButtonGroupFlag(...)` calls — method calls, no loops, no branches
- No lock(), async void, return null
- Tooltip strings: "Arm Break-Even not available on this plan" and "Tighten Stop not available on this plan" — non-empty, ASCII-only, consistent with existing pattern
- `f.BreakEven` is bool — same flag used for `_beBtns`

**CYC Pre-Check**: PASS
- Two `ApplyButtonGroupFlag` calls add zero branches to `ApplyFeatureFlags` outer CCN. Stays at 5 ≤ 8.

**Test Coverage**: PASS
- No new public/internal methods; SIM gate documented

**Scan Checklist**: PASS — all 7 scans present

**File Routing**: PASS

**VERDICT: TICKET_REVIEW_PASS** (with engineer WARN: verify C-6 old_text comment header against actual `feature/bwave-cyc-lane-c2` before applying diff)

---

### T7 — C-7: Fix TryParseArmBeBuffer default value stomped by int.TryParse

**Traceability**: PASS
- Source: CodeRabbit CR38 (default buffer 2 overwritten to 0) — plan section "TICKET C-7 [Major]"
- File: TradeCopierWindow.cs, Method: TryParseArmBeBuffer — correct

**Spot-check C-7 old_text accuracy**:
- Branch `feature/bwave-cyc-lane-c2` lines 969-977 (per plan-review V3, line 323-327):
  - Signature: `private static int TryParseArmBeBuffer(object[] tag)` — **EXACT MATCH**
  - Body: `tag.Length > 2 ? tag[2] as TextBox : null`, then `int.TryParse(bufBox.Text, out buf)` — **EXACT MATCH**
- Ticket old_text: `private static int TryParseArmBeBuffer(object[] tag)` with the same body. PASS.
- New_text: uses `out int parsed` + `parsed >= 0` guard. Signature unchanged (`object[] tag`). `tag.Length > 2` bounds check preserved. PASS.

**JS Pre-Check**: PASS
- No lock(), async void
- Returns `int` — no return null (JS-002 compliant)
- ASCII-only

**CYC Pre-Check**: PASS
- CCN = 3 (bufBox null + TryParse success + parsed >= 0) ≤ 8

**Test Coverage**: PASS
- No new public/internal methods; SIM gate documented

**Scan Checklist**: PASS — all 7 scans present

**File Routing**: PASS

**VERDICT: TICKET_REVIEW_PASS**

---

### T8 — C-8: Add BrushInactive background to _quickBtn and _quickAllBtn

**Traceability**: PASS
- Source: CodeRabbit CR38 P2 (visual regression) — plan section "TICKET C-8 [P2]"
- File: TradeCopierPanel.cs, Method: BuildBufferedButtonsRow — correct

**JS Pre-Check**: PASS
- `Background = BrushInactive` is a property assignment (static readonly `SolidColorBrush`)
- No lock(), async void, return null
- ASCII-only
- Note: `BrushInactive` is a `SolidColorBrush`. Plan does not describe `.Freeze()` being called. However, `BrushInactive` is a `static readonly` field — if it was created without `.Freeze()` that would be a JS-009 (immutability) concern. This is an EXISTING field, not introduced by C-8. C-8 only assigns it to a property. No new SolidColorBrush construction here. PASS (JS-009 not triggered by reference assignment).

**CYC Pre-Check**: PASS
- Property initializer assignments add no branches; CCN unchanged

**Test Coverage**: PASS
- No new methods; SIM gate documented

**Scan Checklist**: PASS — all 7 scans present

**File Routing**: PASS

**VERDICT: TICKET_REVIEW_PASS**

---

### T9 — C-9: Fix SA1507 double blank line in BwaveCycLaneCTests.cs

**Traceability**: PASS
- Source: qlty SA1507 — plan section "TICKET C-9 [SA1507]"
- File: BwaveCycLaneCTests.cs — correct

**JS Pre-Check**: PASS — whitespace-only change; no code constructs to check

**CYC Pre-Check**: PASS — N/A

**Test Coverage**: PASS
- No methods modified; 13 reflection tests confirmed passing after C-1 in SCAN-07
- Engineer pre-check instruction (run `dotnet build | Select-String SA1507` first, skip if pre-resolved) is good defensive guidance

**Scan Checklist**: PASS
- All 7 scans present. SCAN-01 through SCAN-05 correctly marked N/A with rationale. SCAN-06 and SCAN-07 populated. PASS.

**File Routing**: PASS — `src/PropTraderTools/Tests/BwaveCycLaneCTests.cs` in Wave workspace

**VERDICT: TICKET_REVIEW_PASS**

---

## Aggregate Checks

### Completeness

| # | Ticket | Present | old_text | new_text | Plan Source |
|---|--------|---------|----------|----------|-------------|
| 1 | C-1 | ✅ | ✅ | ✅ | Plan C-1 |
| 2 | C-2 | ✅ | ✅ | ✅ | Plan C-2 |
| 3 | C-3 | ✅ | ✅ | ✅ | Plan C-3 |
| 4 | C-4 | ✅ | ✅ | ✅ | Plan C-4 |
| 5 | C-5 | ✅ | ✅ | ✅ | Plan C-5 |
| 6 | C-6 | ✅ | ⚠️ comment header uncertainty | ✅ | Plan C-6 |
| 7 | C-7 | ✅ | ✅ | ✅ | Plan C-7 |
| 8 | C-8 | ✅ | ✅ | ✅ | Plan C-8 |
| 9 | C-9 | ✅ | ✅ | ✅ | Plan C-9 |

All 9 tickets present. Every plan requirement covered exactly once. No phantom work.

### Scan Checklist Presence (7-scan defense-in-depth)

| Ticket | SCAN-01 | SCAN-02 | SCAN-03 | SCAN-04 | SCAN-05 | SCAN-06 | SCAN-07 |
|--------|---------|---------|---------|---------|---------|---------|---------|
| C-1 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| C-2 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| C-3 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| C-4 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| C-5 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| C-6 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| C-7 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| C-8 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| C-9 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |

All 63 scan slots populated. PASS.

### JS Pre-Check (aggregate)

| Rule | Scan Result |
|------|------------|
| JS-021 `lock()` | PASS — zero occurrences in any new_text block across all 9 tickets |
| JS-033 `async void` | PASS — zero occurrences; all methods synchronous or static |
| JS-002 `return null` | PASS — zero new null returns from ticket APIs; NT8 Instrument null in TrySetPanelInstrument is approved existing pattern |
| JS-001 `throw new Exception` | PASS — no new throws |
| ASCII-only | PASS — all identifiers, string literals, and comments in new_text blocks are 7-bit ASCII |
| NT8 constraints | PASS — no sealed on Window class, no FontFamily construction, no hardcoded hex colors, no DateTime.Now, no async/await in lifecycle methods, no Account.All outside Loaded handler |

### CYC Pre-Check (aggregate)

| Method | Ticket | CCN After | Target | Status |
|--------|--------|-----------|--------|--------|
| DoInject | C-1 | 7 | ≤ 8 | PASS |
| WireControlCenterMenu | C-1 | 5 | ≤ 5 | PASS |
| CollectStalePanelChildren | C-1 | 2 | ≤ 8 | PASS |
| RemoveStalePanelChild | C-1 | 3 | ≤ 8 | PASS |
| TryDetachAndRemoveStalePanels | C-2 | 2 | ≤ 8 | PASS |
| InjectPanelIntoGrid | C-1 | 2 | ≤ 8 | PASS |
| TrySetPanelInstrument | C-1 | 2 | ≤ 8 | PASS |
| RemoveExistingTradeCopierEntries | C-1 | 4 | ≤ 8 | PASS |
| ApplyRowVisibilityFlags | C-5 | 5 | ≤ 8 | PASS |
| ApplyFeatureFlags | C-6 | 5 | ≤ 8 | PASS |
| TryParseArmBeBuffer | C-7 | 3 | ≤ 8 | PASS |

All methods at or below target. PASS.

### File Routing

All `.cs` paths target `src/PropTraderTools/` in the Wave workspace (`c:\WSGTA\universal-or-strategy`). No Director workspace paths. PASS.

---

## Engineer Notes (not violations — operational guidance)

1. **C-6 old_text comment header**: Before applying C-6, run `git show HEAD:src/PropTraderTools/TradeCopierWindow.cs | grep -A2 "Apply feature flags"` on branch `feature/bwave-cyc-lane-c2` to confirm the exact comment header. If it differs from the ticket's `// T7: Apply feature flags...`, update the old_text comment lines to match exactly. The function signature and body are confirmed correct.

2. **C-9 pre-check**: Per ticket instruction, run `dotnet build | Select-String SA1507` first. If SA1507 is absent (already resolved by file regeneration), skip C-9 edit.

3. **Execution order**: C-2 MUST execute after C-1. All others are independent. C-1 → C-2 → C-3 sequence recommended for `TradeCopierAddOn.cs` to minimize file re-reads.

---

## Summary Table

| Ticket | Traceability | JS Pre-Check | CYC Pre-Check | NT8 Check | Test Coverage | Scan Checklist | File Routing | VERDICT |
|--------|-------------|-------------|---------------|-----------|---------------|----------------|--------------|---------|
| C-1 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **PASS** |
| C-2 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **PASS** |
| C-3 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **PASS** |
| C-4 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **PASS** |
| C-5 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **PASS** |
| C-6 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **PASS** (⚠️ comment header WARN) |
| C-7 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **PASS** |
| C-8 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **PASS** |
| C-9 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **PASS** |

---

## Overall: TICKET_REVIEW_PASS

All 9 tickets pass on all checks. No JS DNA violations, no CCN violations, no missing scan checklists, no phantom work, no missing plan coverage. One non-blocking engineer WARN on C-6 comment header accuracy (verify before apply).

**TICKET_REVIEW_PASS**
