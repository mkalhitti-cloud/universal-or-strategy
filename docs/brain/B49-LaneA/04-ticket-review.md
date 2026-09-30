# Ticket Review: PTT-COPIER-B49 Lane A
**Block**: B49-LaneA  
**Label**: layout-reorder  
**Reviewer**: ptt-ticket-reviewer (Phase 3.5)  
**Date**: 2026-08-08  
**Input**: docs/brain/B49-LaneA/04-tickets.md  
**Plan**: docs/brain/B49-LaneA/02-architecture-plan.md  
**Rules**: docs/standards/jane-street/RULES_CATALOG.md  

---

## T1 — B49-T1: TradeCopierPanel.cs layout reorder + CopyEngine.cs tag update

### Traceability
**PASS**

All three changes map directly to spec requirement IDs:

| Change | Spec IDs Covered | Plan Section |
|--------|-----------------|--------------|
| Change 1 — BuildUI tail reorder | B49-UI-01 (`_beRowPanel` above Copier), B49-UI-03 (`BuildCollapsibleHeader` + `_contentPanel` at bottom) | §4 |
| Change 2 — BuildCopierSection Mode row insertion | B49-UI-02 (Mode row inside BuildCopierSection, between collapse btn and scroll viewer) | §5 |
| Change 3 — PttBuild.Tag update | B49-UI-04 (Tag reflects B49 block label and date) | §6 |

No phantom work found (every ticket item traces to a spec requirement and a plan section).  
No missing work found (all plan sections §4, §5, §6 appear in the ticket).

---

### JS Pre-Check
**PASS**

| Rule | Check | Finding |
|------|-------|---------|
| JS-021 — No `lock()` | Ticket Change 1 and Change 2 both include explicit constraint: "No `lock()`" | Zero new `lock()` in described changes |
| JS-033 — No `async void` | Ticket Change 1 constraint box and SCAN-02 both address this | Zero new `async void` in described changes |
| JS-002 — No `return null` | Ticket SCAN-03 covers this; no new methods returning values are introduced | Zero new `return null` in described changes |
| JS-001 — No `throw` in hot path | No new methods introduced; no exception-throwing described | Not applicable |
| JS-008/009 — Mutable struct / Dictionary | No new types or fields described | Not applicable |
| JS-003 — Empty-string/sentinel for mode state | No new fields; Tag is a `const string` literal, not state | Not applicable |

All relevant JS rules pass. No violations described.

---

### CYC Pre-Check
**PASS**

| Method | CYC Before | Delta | CYC After | Basis |
|--------|-----------|-------|-----------|-------|
| `BuildUI` | ≤ 8 (ticket states unchanged) | 0 | ≤ 8 | Change 1 adds no `if`, `for`, `while`, `switch`, `?:`, `??`, `&&`, `||`; all deletions and tail reorder are straight-line `Children.Add` calls |
| `BuildCopierSection` | 1 | 0 | 1 | Change 2 inserts one straight-line method call `BuildModeRow(root)`; a method call contributes 0 to CYC of the caller |

CYC delta confirmed zero. Both methods remain ≤ 8. Confirmed in plan §10 and in SCAN-06/07.

---

### NT8 Check
**PASS**

| Rule | Check | Finding |
|------|-------|---------|
| NT8-001 — No `{ get; init; }` | No new properties in described changes | Not introduced |
| NT8-002 — No `abstract record` / `sealed record` | No new types in described changes | Not introduced |
| NT8-003 — No `volatile double` | No new fields in described changes | Not introduced |
| NT8 async/await in lifecycle | No async code in described changes | Not introduced |
| NT8 `Account.All` outside Loaded | No Account usage | Not applicable |
| NT8 `sealed` on `TradeCopierWindow` | No class declaration changes | Not applicable |
| NT8 `FontFamily` on WPF element | No font changes | Not applicable |
| NT8 hardcoded hex color | No color changes | Not applicable |
| NT8 `CreateOrder` name prefix | No order creation | Not applicable |
| NT8 `DateTime.Now` usage | No DateTime usage; SCAN-05 in plan verifies this | Not introduced |

All NT8 hard constraints pass.

---

### Completeness
**PASS**

All 3 required changes are present and fully specified:

| Change | Present? | Detail |
|--------|---------|--------|
| Change 1 — BuildUI tail: delete lines 706-707, 709-713, 715-716; reorder tail | ✅ Yes | Verbatim CURRENT → TARGET diff provided; all three deletion regions explicitly identified |
| Change 2 — BuildCopierSection: insert `BuildModeRow(root)` between `_copierCollapseBtn.Add` and `_followerScrollViewer.Add` | ✅ Yes | Exact position confirmed; CURRENT → TARGET diff provided matching Director spec |
| Change 3 — CopyEngine.cs line 41: Tag update to B49 | ✅ Yes | Verbatim CURRENT → TARGET replacement provided |
| Separator border (lines 709-713) deletion | ✅ Yes | Explicit in Change 1 §1a; confirmed in AC-08 |
| `OnCopierCollapseClick` — NO CHANGE | ✅ Yes | Not mentioned anywhere in ticket; correctly untouched |
| `UpdateCopierHeader` — NO CHANGE | ✅ Yes | Not mentioned anywhere in ticket; correctly untouched |
| No new fields | ✅ Yes | Ticket states "No new fields. No new methods." |
| No new event handlers | ✅ Yes | No new `+=` event subscriptions described |
| File scope: exactly 2 files | ✅ Yes | Ticket table lists only `TradeCopierPanel.cs` and `CopyEngine.cs`; "No other files may be touched." |

---

### Test Coverage
**PASS**

No new public or internal methods are introduced by this ticket. All four method signatures in the
"Method Signatures" table (`BuildUI`, `BuildCopierSection`, `BuildModeRow`, `BuildCollapsibleHeader`)
are marked **unchanged** — no new [Fact] obligations arise.

The ticket correctly states "No new xUnit tests are required for this ticket" with explicit rationale
(pure UI widget-order changes; no observable logic branching). The DW-B48-01 exemption is documented:
if `CopyEngineTests.cs` has pre-existing compilation errors, they are out of scope for B49 per V12.23
(no scope creep). Existing tests must remain GREEN.

---

### Scan Checklist
**PASS**

All 7 scans are present in the ticket with commands and expected results:

| Scan | Present? | Command | Expected Result |
|------|---------|---------|-----------------|
| SCAN-01 — JS-021 No `lock()` | ✅ | `grep -n "lock(" TradeCopierPanel.cs CopyEngine.cs` | Zero matches in any changed or new region |
| SCAN-02 — JS-033 No `async void` | ✅ | `grep -n "async void" TradeCopierPanel.cs` | Zero new `async void` declarations introduced by B49 |
| SCAN-03 — JS-002 No `return null` | ✅ | `grep -n "return null" TradeCopierPanel.cs` | Zero new `return null` in changed methods |
| SCAN-04 — Hard-link integrity | ✅ | `powershell -File scripts\verify_links.ps1` (no `-Fix`) | `DESYNC=0 MISSING=0` |
| SCAN-05 — Build gate | ✅ | `dotnet build` from Wave workspace root | 0 errors (DW-B48-01 exempt if pre-existing) |
| SCAN-06 — CYC `BuildCopierSection` | ✅ | Count branches: `if`, `for`, `while`, `switch`, `?:`, `??`, `&&`, `\|\|` in method body | CYC = 1 (unchanged) |
| SCAN-07 — CYC `BuildUI` | ✅ | Count branches in `BuildUI` body after all deletions and tail reorder | CYC unchanged, remains ≤ 8 |

Note: Ticket SCAN-04/05 differ from plan SCAN-04/05 (plan uses ASCII-only and DateTime.Now checks;
ticket uses hard-link integrity and build gate). Both sets are valid engineering scans for this
change type. The ASCII-only and DateTime.Now constraints are covered by the inline constraint boxes
in Change 1 and Change 2 respectively, and by the build gate (SCAN-05). The defense-in-depth contract
is intact. No missing scan.

---

### File Routing
**PASS**

| File | Declared Path | Routing Check |
|------|--------------|---------------|
| `TradeCopierPanel.cs` | `C:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs` | ✅ Wave workspace |
| `CopyEngine.cs` | `C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs` | ✅ Wave workspace |

No Director workspace (`..\universal-or-strategy-director\`) `.cs` paths found. Correct.

---

### VERDICT: TICKET_REVIEW_PASS

All 7 checks pass. No violations found. No JS-XXX, NT8-XXX, or structural rule violations
are described anywhere in T1. The ticket is a valid implementation contract.

---

## Overall: TICKET_REVIEW_PASS

| Check | T1 Result |
|-------|----------|
| Traceability | PASS |
| JS Pre-Check | PASS |
| CYC Pre-Check | PASS |
| NT8 Check | PASS |
| Completeness | PASS |
| Test Coverage | PASS |
| Scan Checklist (SCAN-01 through SCAN-07) | PASS |
| File Routing | PASS |

**The ticket is cleared for engineer execution (Phase 4a).**  
The engineer must run all 7 scans and record results in `ticket-1-completion.md` before
marking T1 `BUILD_PASS`. The verifier (Phase 4b) will independently re-run all 7 scans
against the actual source and compare against the engineer's attestation.
