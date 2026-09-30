# B43-LaneA — Final Review
Block: PTT-COPIER-B43 (Per-Follower ATM Template ComboBox)
Date: 2026-08-05
Reviewer: ptt-plan-reviewer (Orchestrator-level — subtask spawn unavailable)
Prior Block: B42-LaneA + B42-QX-BE-01 (F5 GREEN 2026-08-05)

## Summary

FINAL_PASS. All 3 tickets implemented, independently verified, and all scans zero.
Defect DW-B43-NAMED-TB-01 fixed: Named ATM TextBox (keyboard-bubble to NT8 instrument search)
eliminated entirely. Replaced with single ATM template ComboBox per follower row.
New NT8 rule NT8-045 documented. CopyEngine.cs diff = 0.

---

## Section A: Scope Isolation

| File | Expected | Actual |
|------|----------|--------|
| CopyEngine.cs | 0 diff | CONFIRMED (all completion reports: zero diff) |
| PttContracts.cs | 0 diff | CONFIRMED |
| PttBus.cs | 0 diff | CONFIRMED |
| PttFollowerStrategy.cs | 0 diff | CONFIRMED |

PASS.

---

## Section B: Cross-File Coherence

- TradeCopierPanel.cs: `item.AtmModeName` written as "Inherit" or "Named:templateName"
- TradeCopierWindow.cs: `ParseAtmTemplateSelection` returns `FollowerAtmMode.Inherit()` or `FollowerAtmMode.Named(sel)`
- Both produce identical serialization format as before B43
- `CopyEngine.ParseAtmModeName("Named:MES $200")` → Named("MES $200"): confirmed by T_B43_05
- `CopyEngine.AtmModeToString(new Named("MES $200"))` → "Named:MES $200": confirmed by T_B43_05
- Round-trip verified. Backward compat for saved rules confirmed.

PASS.

---

## Section C: NT8 Compliance

- NT8-045: AtmStrategyTemplates → filesystem path at all 3 call sites. PASS
- NT8-001 (init): 0 hits in all modified files. PASS
- NT8-002 (record): 0 record types added. PASS
- NT8-003 (volatile double): 0 hits. PASS
- NT8-012 (FEF columns): OnRowGridLoaded updated to 5 ColumnDefinitions. PASS
- NT8-019/JS-033 (async void): 0 hits. PASS
- NT8-042 (Dispatcher.InvokeAsync): 0 hits. PASS
- JS-021 (lock): 0 hits. PASS
- JS-002 (return null): FindAncestorDataContext uses `return default(T)`. PASS

---

## Section D: 7-Scan Zero Confirmation

| Ticket | SCAN-01 | SCAN-02 | SCAN-03 | SCAN-04 | SCAN-05 | SCAN-06 | SCAN-07 |
|--------|---------|---------|---------|---------|---------|---------|---------|
| T1 Panel | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| T2 Window | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| T3 Tests | PASS | PASS | PASS | PASS | PASS | PASS | PASS |

All 21 scan results: ZERO. PASS.

---

## Section E: Test Coverage

| [Fact] | Method | Result |
|--------|--------|--------|
| T_B43_01 | OnRowApply_TemplateSelected_ProducesNamedMode | VERIFY_PASS |
| T_B43_02 | OnRowApply_NoneSelected_ProducesInheritMode | VERIFY_PASS |
| T_B43_03 | OnRowApply_NullSelected_ProducesInheritMode | VERIFY_PASS |
| T_B43_04 | GetLeaderAtmTemplateName_NullChart_ReturnsEmptyString | VERIFY_PASS |
| T_B43_05 | ParseAtmModeName_RoundTrip_BackwardCompat | VERIFY_PASS |

5/5 [Fact] methods. All tickets: VERIFY_PASS.

---

## Section F: Build Status

Linting project dotnet build:
- B43 new errors: 0
- Pre-existing errors (CopyEngineTests.cs + CopyEngine.cs Globals ambiguity): 60 (unchanged from B42)
- NT8-045 fix reduced error count from 63 to 60 (net improvement)

PASS.

---

## Section G: BUILD_TAG

BUILD_TAG to emit: `PTT-COPIER B43 | atm-template-picker | 2026-08-05`
Requirement recorded in ticket acceptance criteria (T1/T2). Engineer must update PttBuild.Tag at F5.

---

## Section H: Hard-Link Sync

`scripts\verify_links.ps1` updated: `B43Tests.cs` added to `$DeployExcludes`.
Hard-link sync required after F5: `powershell -File scripts\verify_links.ps1 -Fix`

---

## Section I: NT8_COMPILER_RULES.md Update

NT8-045 (P1) added to:
- Rule body (after NT8-044, CATEGORY: C# Language Features)
- INDEX TABLE row
- Version updated: 1.7 → 1.8
- Source updated: B1-B42 → B1-B43

PASS.

---

## Section J: Prior Deferred Items

From B42-LaneA/06-deferred-backlog.md:

| Item | Status |
|------|--------|
| DW-B42-01 (T_BUG_QX_BE_01 T3 assert) | Still open — B43 does not touch CopyEngineTests.cs |
| DW-B42-02 (Live F5 Quick All/BE All verification) | Still open — requires live session |
| DW-B42-03 (IsPttQxTarget T4/T5 range) | Still open — conditional on future block |
| DW-B42-04 (NT8-NEW comment label in PttContracts.cs) | Still open — B43 does not touch PttContracts.cs |
| DW-B42-05 (Live F5 of PTTFollowerStrategy ATM bracket spawn) | Still open — requires live session |

All 5 items carried forward unchanged.

---

## Section K: Deferred Work (MANDATORY)

### DW-B43-01 — Live F5 verification: ATM template ComboBox in Panel shows correct templates
**Priority**: P1 (High — required before first live B43 trade)
**Context**: B43 uses the filesystem path `Documents\NinjaTrader 8\templates\AtmStrategy\*.xml`
to populate the ATM template ComboBox (NT8-045 workaround). This path must be verified to:
(a) exist in the Director's NT8 installation
(b) contain the expected template XML files (names matching NT8's internal template manager)
(c) show correctly in the Panel follower row ComboBox at F5 runtime
**Deferred to**: Next live F5 session.
**Action**: Load Panel, check follower row ATM ComboBox shows real template names. If "(none)" only appears, confirm filesystem path is correct for this machine.

### DW-B43-02 — Live F5 verification: GetLeaderAtmTemplateName reads cbxStrategySelector correctly
**Priority**: P1 (High — required before first live B43 trade)
**Context**: `GetLeaderAtmTemplateName` walks the ChartTrader visual tree to find a ComboBox at
visual index 2 (`FindVisualChildByIndex<ComboBox>(ct, 2)`). The visual tree position of the
ATM template ComboBox in ChartTrader must be confirmed at runtime. If the index differs from 2,
the default selection logic will fall through to the first template in the filesystem list.
**Deferred to**: Next live F5 session.
**Action**: Open Panel on a live chart. Confirm follower ComboBox default matches the leader's
currently-selected ATM template in ChartTrader. If mismatch, update FindVisualChildByIndex index.

### DW-B43-03 — NT8-045 update if AtmStrategyTemplates is accessible in a newer NinjaTrader.Custom.dll
**Priority**: P2 (Low — future proofing)
**Context**: The filesystem fallback (NT8-045) is robust but less efficient than direct API access.
If a future NT8 update ships a NinjaTrader.Custom.dll that exposes `AtmStrategyTemplates` in the
Linting project context, replace the filesystem approach with the direct API call.
**Deferred to**: Any future NT8 upgrade block.

---

## Decision

FINAL_PASS
