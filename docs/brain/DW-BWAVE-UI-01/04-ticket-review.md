# Ticket Review: DW-BWAVE-UI-01

**Reviewer**: ptt-ticket-reviewer
**Date**: 2026-08-27
**Input tickets**: `docs/brain/DW-BWAVE-UI-01/04-tickets.md`
**Plan reviewed**: `docs/brain/DW-BWAVE-UI-01/02-architecture-plan.md` (REVIEW_PASS)
**Plan review**: `docs/brain/DW-BWAVE-UI-01/02-plan-review.md` (REVIEW_PASS)
**Source confirmed**: `src/PropTraderTools/TradeCopierPanel.cs` lines 1185–1205

---

## T1 — DW-BWAVE-UI-01: Move teal Foreground/BorderThickness assignments after SetResourceReference

---

### Section A: Traceability

| Item | Check | Result | Citation |
|------|-------|--------|----------|
| A1 | Ticket references spec requirement ID DW-BWAVE-UI-01 | **PASS** | T1 §"Spec Requirement IDs Satisfied": "DW-BWAVE-UI-01 (P1)" cited verbatim |
| A2 | Fix design matches architect's approved approach (`SetResourceReference` moved BEFORE `if (s.Teal)` block) | **PASS** | T1 §"Fix Design" AFTER block: `btn.SetResourceReference(...)` appears on line 2 of AFTER block, before `if (s.Teal)`; NOTE TO ENGINEER confirms this explicitly. Matches plan §3 exactly. |
| A3 | BEFORE code in ticket matches exact current source lines 1189–1197 | **PASS** | `read_file` lines 1189–1197 confirmed: `var btn = new Button { Content = s.Content };` (1189), `if (s.Teal)` (1190), `btn.BorderBrush = BrushTeal;` (1192), `btn.Foreground = BrushTeal;` (1193), `btn.BorderThickness = new Thickness(2);` (1194), `btn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");` (1196), `btn.Background = s.Bg; // AFTER style ...` (1197). All lines match ticket BEFORE block verbatim. |

**Section A Verdict: PASS**

---

### Section B: JS Pre-Check

| Item | Check | Rule | Result | Citation |
|------|-------|------|--------|----------|
| B1 | No `lock()` introduced | JS-021 | **PASS** | T1 §"JS Rule Constraints Checked": `lock()` row = PASS. Fix is pure WPF property reorder; no locking construct. |
| B2 | No `return null` introduced | JS-002 | **PASS** | T1 §"JS Rule Constraints Checked": JS-002 row = PASS. No return statements introduced. |
| B3 | No `async void` introduced | JS-033 | **PASS** | T1 §"JS Rule Constraints Checked": JS-033 row = PASS. No async methods added. |
| B4 | No `new byte[]` heap alloc in hot path | JS-036 | **PASS** | T1 §"JS Rule Constraints Checked": JS-036 row = PASS. `new Thickness(2)` is a value-type struct (stack-allocated), not a heap allocation. Correct classification. |
| B5 | No forbidden NT8 patterns (`Account.Change`, `AtmStrategyCreate`) | NT8 mandate | **PASS** | T1 §"JS Rule Constraints Checked": "No NT8 AddOn API involved. Pure WPF UI construction." SCAN-6 explicitly targets these patterns in the 7-scan checklist. |

**Section B Verdict: PASS**

---

### Section C: CYC Pre-Check

| Item | Check | Result | Citation |
|------|-------|--------|----------|
| C1 | CYC delta stated as 0 (pure line reorder, no new branches) | **PASS** | T1 §"CYC Impact": "Delta: 0". Explicit rationale: `if (s.Teal)` branch pre-existed and is unchanged; no new conditionals, loops, switch expressions, or early returns introduced. |
| C2 | No new methods introduced | **PASS** | T1 §"Method Signature Referenced": "Do not change the method signature. This ticket is a line reorder within the existing method body only." Plan §4 confirms: New methods = None. |

**Section C Verdict: PASS**

---

### Section D: NT8 Constraints

| Item | Check | Result | Citation |
|------|-------|--------|----------|
| D1 | Fix is WPF property assignment reorder (safe for NT8 `AddOnBase` context) | **PASS** | T1 §"Fix Design" and plan §11: "No NT8 AddOn API involved. Pure WPF UI construction." `SetResourceReference` and `Foreground`/`BorderThickness` are pure WPF `DependencyProperty` calls — no NT8 lifecycle involvement. |
| D2 | No NT8 API calls introduced beyond existing code | **PASS** | BEFORE and AFTER blocks in T1 contain identical API calls; the change is positional only. `BrushTeal` and `s.Bg` are pre-existing project constants. SCAN-6 enforces zero `Account.Change`/`AtmStrategyCreate`/`AtmStrategyChangeStopTarget` matches. |

**Section D Verdict: PASS**

---

### Section E: 7-Scan Checklist Completeness

> **Note**: The architecture plan (§6) specifies 5 scans (SCAN-1 through SCAN-5). The ticket
> delivers 7 scans (SCAN-1 through SCAN-7), adding SCAN-6 (NT8 forbidden patterns) and SCAN-7
> (async void gate). The ticket exceeds the plan's scan floor — this is additive safety and
> is the correct PTT 7-scan standard. All 7 scans are evaluated below.

| Item | Scan | Present | Exact Command | Expected Result Stated | Result |
|------|------|---------|---------------|----------------------|--------|
| E1 | SCAN-1 — Post-style Foreground placement | YES | `grep -n "Foreground\|SetResourceReference" src/PropTraderTools/TradeCopierPanel.cs \| grep -A2 -B2 "BrushTeal\|NTButtonStyle"` | YES: `SetResourceReference` line N < `Foreground = BrushTeal` line N+k | **PASS** |
| E2 | SCAN-2 — Lizard CCN gate | YES | `lizard src/PropTraderTools/TradeCopierPanel.cs --CCN 8` | YES: 0 warnings for methods touched by this ticket | **PASS** |
| E3 | SCAN-3 — `lock()` forensic (JS-021) | YES | `grep -n "lock\s*(" src/PropTraderTools/TradeCopierPanel.cs` | YES: 0 results | **PASS** |
| E4 | SCAN-4 — ASCII-only check | YES | `([System.IO.File]::ReadAllBytes("src/PropTraderTools/TradeCopierPanel.cs") \| Where-Object { $_ -gt 127 } \| Measure-Object).Count` | YES: Count = 0 | **PASS** |
| E5 | SCAN-5 — Build gate | YES | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | YES: 0 errors, 0 warnings | **PASS** |
| E6 | SCAN-6 — NT8 forbidden patterns grep | YES | `grep -n "Account\.Change\|AtmStrategyCreate\|AtmStrategyChangeStopTarget" src/PropTraderTools/TradeCopierPanel.cs` | YES: 0 results | **PASS** |
| E7 | SCAN-7 — `async void` gate (JS-033) | YES | `grep -n "async void " src/PropTraderTools/TradeCopierPanel.cs` | YES: count after fix equals count before fix (zero new introductions) | **PASS** |
| E8 | Each scan has exact command and expected result | YES | All 7 scans carry verbatim PowerShell commands and explicit PASS conditions | — | **PASS** |

**Section E Verdict: PASS**

---

### Section F: Test Coverage

| Item | Check | Result | Citation |
|------|-------|--------|----------|
| F1 | No xUnit tests required (pure line reorder, no new testable logic); ticket notes this explicitly | **PASS** | T1 §"xUnit Tests": "None required." Rationale is fully stated: "pure line reorder. No new logic, no new methods, no new branches, no new state transitions are introduced. There is no testable unit of logic to add. Correctness is verified structurally via SCAN-1 and visually via the Director-owned SIM gate." |

**Section F Verdict: PASS**

---

### Section G: Completeness

| Item | Check | Result | Citation |
|------|-------|--------|----------|
| G1 | SIM gate conditions defined — all 4 buttons (BE, BE ALL, Quick, QAll2t) with explicit teal-text-at-rest acceptance | **PASS** | T1 §"SIM Gate": table lists all four buttons with criterion "Teal text **visible at rest** (not hover-only). Teal border visible at rest." Fifth row confirms DW-LaneA-06 regression check (background retained). |
| G2 | Sync step present (`ptt-sync-and-verify.ps1` + F5 instruction) | **PASS** | T1 §"Sync Step": `powershell -File scripts\ptt-sync-and-verify.ps1` with expected result "0 MISMATCH lines" and "Then press F5 in NinjaTrader 8 to recompile." |
| G3 | Acceptance criteria present and testable | **PASS** | T1 §"Acceptance Criteria": 4 criteria listed — (1) SCAN-1 line-number ordering, (2) all 7 scans pass at zero, (3) SCAN-5 build clean, (4) DW-LaneA-06 regression guard (`btn.Background = s.Bg` remains after `if` block). All criteria are machine-verifiable or structurally observable. |

**Section G Verdict: PASS**

---

### File Routing Check

| Check | Result | Citation |
|-------|--------|----------|
| C# source path points to Wave workspace `src/PropTraderTools/TradeCopierPanel.cs` | **PASS** | T1 §"File": `src/PropTraderTools/TradeCopierPanel.cs` — correct Wave workspace path. No Director workspace path referenced for `.cs` files. |

---

### Violations Found

**None.**

All sections pass. Zero rule violations detected. Source lines 1189–1197 confirmed against ticket BEFORE block — exact match. Ticket AFTER block correctly places `SetResourceReference` before the `if (s.Teal)` block. All 7 scans present with exact commands and explicit PASS conditions. SIM gate covers all 4 affected buttons. No prohibited patterns (lock, null return, async void, heap alloc, NT8 forbidden APIs) introduced or described.

---

### Decision

**TICKET_REVIEW_PASS**

All 7 sections pass. T1 is clear for Phase 4a engineer execution.
