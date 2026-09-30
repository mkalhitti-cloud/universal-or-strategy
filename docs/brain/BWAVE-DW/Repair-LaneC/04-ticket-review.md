# Ticket Review — BWAVE-DW-REPAIR-LANEC

**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Source tickets**: `docs/brain/BWAVE-DW/Repair-LaneC/04-tickets.md`
**Source plan**: `docs/brain/BWAVE-DW/Repair-LaneC/02-architecture-plan.md` (REVIEW_PASS)
**Rules reference**: `docs/standards/jane-street/RULES_CATALOG.md`
**Date**: 2026-08-20

---

## TICKET R-LC-1 — ApplyFeatureFlags inside Dispatcher.InvokeAsync lambda in RefreshRuleRows()

### Traceability
- Spec requirement B4 / DW-C39-05b maps to architecture plan §1 scope table row `DW-C39-05b` — `TradeCopierWindow.cs`, "ApplyFeatureFlags timing". Direct 1:1 match.
- No phantom work (ticket contains no change beyond what is in the plan).
- No missing work (plan §2 R-LC-1 component map describes exactly one line added inside the lambda — present in ticket).

**Traceability: PASS**

### JS Pre-Check
| Rule | Ticket claim | Verdict |
|------|-------------|---------|
| JS-021 No lock() | `Dispatcher.InvokeAsync` is not a lock; lambda is synchronous Action on UI thread | PASS |
| JS-033 No async void | `RefreshRuleRows` is `private void`; lambda is a synchronous `Action` delegate, not async | PASS |
| JS-001 No throw in hot path | No exceptions thrown anywhere in the change | PASS |
| JS-002 No return null | `void` method; no return value | PASS |

No JS rule violations described anywhere in this ticket.

**JS Pre-Check: PASS**

### CYC Pre-Check
- `RefreshRuleRows()` before: 3. After: 3. No new branches added by a method call.
- Branch count verified: (1) `instruments.Count == 0` guard, (2) outer `foreach` loop, (3) inner `foreach` in lambda. The `ApplyFeatureFlags(...)` call adds no decision point.
- CYC 3 ≤ 8. No at-risk methods.

**CYC Pre-Check: PASS**

### NT8 Check
- Header states `NT8 sync: REQUIRED`.
- SCAN-06 (`ptt-sync-and-verify.ps1`) and SCAN-07 (F5 compile) present in checklist.
- No async/await in lifecycle method described. No `Account.All` outside Loaded handler. No `sealed` on window class. No `FontFamily`, no hardcoded hex color, no `DateTime.Now`, no `CreateOrder` call — none of these are introduced by this ticket.
- `Dispatcher.InvokeAsync(Action)` usage is an existing pattern (plan §8, confirmed line 168).

**NT8 Check: PASS**

### Test Coverage
- No new public or internal API surface is introduced (zero new methods; one line added to existing private method body).
- `RefreshRuleRows()` is `private void` — not directly unit-testable in isolation.
- Ticket explicitly states "No new `[Fact]` test required" with rationale: WPF dispatcher timing fix requires live NT8 host; SIM-only acceptance path provided.
- Architecture plan §11 R-LC-1 corroborates: no xUnit test practical.
- No method missing a [Fact] (no new testable surface exists).

**Test Coverage: PASS**

### Scan Checklist
All 7 scans present in the "7-Scan Checklist (SCAN-01 through SCAN-07)" table:

| # | Present | Command specified | Expected result specified |
|---|---------|------------------|--------------------------|
| SCAN-01 | YES | `grep -n "lock(" TradeCopierWindow.cs` | No new matches |
| SCAN-02 | YES | `grep -n "async void " TradeCopierWindow.cs` | Zero matches |
| SCAN-03 | YES | `grep -n "return null" TradeCopierWindow.cs` | Zero matches |
| SCAN-04 | YES | `grep -Pn "[^\x00-\x7F]" TradeCopierWindow.cs` | Zero matches |
| SCAN-05 | YES | `python scripts/complexity_audit.py` | `RefreshRuleRows` CYC=3 |
| SCAN-06 | YES | `powershell -File scripts\ptt-sync-and-verify.ps1` | 0 MISMATCH |
| SCAN-07 | YES | F5 in NinjaTrader 8 | Green build |

**Scan Checklist: PASS**

### File Routing
- `src/PropTraderTools/TradeCopierWindow.cs` — Wave workspace (`c:\WSGTA\universal-or-strategy\src\PropTraderTools\`). Correct.

**File Routing: PASS**

### VERDICT: TICKET_REVIEW_PASS

---

## TICKET R-LC-2 — Last-panel pending BE slots cleanup on close

### Traceability
- Spec requirement B5 / DW-C39-20 maps to architecture plan §1 scope table row `DW-C39-20` — `CopyEngine.cs`, `TradeCopierPanel.cs`, `TradeCopierAddOn.cs`, "Clear remaining pending BE slots when last panel closes". Direct 1:1 match.
- All three changes (CHANGE 1 ClearAllPendingBeSlots, CHANGE 2 IsPanelsEmpty, CHANGE 3 Detach guard) are present in both plan §2 R-LC-2 component map and in the ticket. No phantom work. No missing work.

**Traceability: PASS**

### JS Pre-Check
| Rule | Ticket claim | Verdict |
|------|-------------|---------|
| JS-021 No lock() | `ConcurrentDictionary.IsEmpty`, `.Clear()`, `foreach` are all lock-free; event `-=` is thread-safe from any thread | PASS |
| JS-033 No async void | All new methods are `internal void` / `internal static bool` / `internal bool` — no async | PASS |
| JS-001 No throw in hot path | No exceptions thrown in any new code | PASS |
| JS-002 No return null | `ClearAllPendingBeSlots` is void; `IsPanelsEmpty` returns bool; neither returns null | PASS |
| JS-025 Lock-free data structures | `ConcurrentDictionary` used throughout; no lock-protected collection substituted | PASS |

No JS rule violations described anywhere in this ticket.

**JS Pre-Check: PASS**

### CYC Pre-Check
| Method | File | CYC | Within ≤8? |
|--------|------|-----|-----------|
| `ClearAllPendingBeSlots()` | CopyEngine.cs | 3 (new) | YES |
| `IsPanelsEmpty()` | TradeCopierAddOn.cs | 1 (new) | YES |
| `Detach()` | TradeCopierPanel.cs | ~7 (was ~6) | YES |

`Detach()` branch breakdown verified in ticket: base 1 + 5 named branches = 6 (ticket states CYC 6; plan §9 states CYC 6). Well within ≤8. No at-risk methods.

**CYC Pre-Check: PASS**

### NT8 Check
- Header states `NT8 sync: REQUIRED (all 3 files)`.
- SCAN-06 and SCAN-07 present for all 3 files.
- No async/await in lifecycle methods. No `Account.All` outside Loaded. No `sealed` on window. No `FontFamily`, no hardcoded hex, no `DateTime.Now`, no `CreateOrder`.
- `ConcurrentDictionary.IsEmpty` and `.Clear()` are .NET BCL (not NT8 API). `AccountItemUpdate -= handler` pattern mirrors existing line 5758 (confirmed plan §8).

**NT8 Check: PASS**

### Test Coverage
New internal methods introduced:
1. `ClearAllPendingBeSlots()` — `internal void` — a [Fact] test IS specified:
   `ClearAllPendingBeSlots_WhenSlotsArmed_SlotsAreEmpty` with Arrange/Act/Assert skeleton and note that `[assembly: InternalsVisibleTo]` access is required. Test name and assertion are explicit.
2. `IsPanelsEmpty()` — `internal static bool` — reads a static `ConcurrentDictionary` that requires a running AddOn to populate. Ticket explains this is not independently unit-testable in isolation; SIM acceptance path provided. This rationale is sound and matches architecture plan §11.
3. `Detach()` guard (modification, not new method) — covered by existing Detach test surface and the R-LC-2 SIM acceptance path.

All new testable internal methods are either covered by a named [Fact] or have an explicit, reasoned exemption.

**Test Coverage: PASS**

### Critical Ordering Check — Detach() Guard Placement
The ticket CHANGE 3 shows the following sequence:

```
_engine.DisarmPendingBe(_leaderAccount);          ← runs first (removes own slot)
// DW-C39-20 comment
if (TradeCopierAddOn.IsPanelsEmpty())             ← guard check (after own slot removed)
    _engine.ClearAllPendingBeSlots();             ← clears remaining slots
// B32: DisarmTrailBe removed ...
```

DisarmPendingBe executes BEFORE the IsPanelsEmpty guard. This is the correct order:
- Own slot is removed from `_pendingBeSlots` first.
- Then, if `_panels` is empty, remaining slots (armed for other accounts by BE ALL) are cleared.
- ClearAll cannot fire while the current panel's slot is still armed; there is no double-clear risk.

This ordering is also confirmed in architecture plan §4 Change 3 and the data flow diagram in §7. CORRECT.

**Ordering: PASS**

### Scan Checklist
All 7 scans present in the "7-Scan Checklist (SCAN-01 through SCAN-07)" table covering all 3 modified files:

| # | Present | Commands cover all 3 files | Expected result specified |
|---|---------|---------------------------|--------------------------|
| SCAN-01 | YES | CopyEngine.cs + TradeCopierPanel.cs + TradeCopierAddOn.cs | No new lock() |
| SCAN-02 | YES | All 3 files | Zero async void in new code |
| SCAN-03 | YES | All 3 files | Zero return null in new code |
| SCAN-04 | YES | All 3 files | Zero non-ASCII |
| SCAN-05 | YES | All 3 files | ClearAllPendingBeSlots CYC=3; IsPanelsEmpty CYC=1; Detach CYC≤8 |
| SCAN-06 | YES | `ptt-sync-and-verify.ps1` | 0 MISMATCH (all 18 files OK) |
| SCAN-07 | YES | F5 in NinjaTrader 8 | Green build |

**Scan Checklist: PASS**

### File Routing
- `src/PropTraderTools/CopyEngine.cs` — Wave workspace. Correct.
- `src/PropTraderTools/TradeCopierAddOn.cs` — Wave workspace. Correct.
- `src/PropTraderTools/TradeCopierPanel.cs` — Wave workspace. Correct.

**File Routing: PASS**

### VERDICT: TICKET_REVIEW_PASS

---

## Overall: TICKET_REVIEW_PASS

Both tickets pass all checks. No violations found. Safe to spawn ptt-engineer.

| Ticket | Traceability | JS Pre-Check | CYC Pre-Check | NT8 Check | Test Coverage | Scan Checklist | File Routing | VERDICT |
|--------|-------------|-------------|--------------|-----------|---------------|----------------|-------------|---------|
| R-LC-1 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **PASS** |
| R-LC-2 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **PASS** |
