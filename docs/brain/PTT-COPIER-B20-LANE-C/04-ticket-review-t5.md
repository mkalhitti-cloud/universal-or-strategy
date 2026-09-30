# Ticket Review: PTT-COPIER-B20-LANE-C — T5 (DW-B20-CHARTTRADER-01)

**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Date**: 2026-07-09
**Ticket Source**: `docs/brain/PTT-COPIER-B20-LANE-C/04-tickets-t5.md`
**Plan Source**: `docs/brain/PTT-COPIER-B20-LANE-C/02-architecture-plan-t5.md`
**Plan Review**: `docs/brain/PTT-COPIER-B20-LANE-C/02-plan-review-t5.md` (REVIEW_PASS — V3 final)
**Rules Source**: `docs/standards/jane-street/RULES_CATALOG.md`

---

## T5 — Fix ChartTrader Button Blockage (ATR Overlay Ownership Correction)

---

### Traceability

| Plan / Spec Item | Ticket Change | Status |
|---|---|---|
| DW-B20-CHARTTRADER-01.1 Remove `_atrOverlayLabel` | Change A1 | MAPPED |
| DW-B20-CHARTTRADER-01.5 Replace `UpdateAtrOverlay` via `_panels` | Change A2 | MAPPED |
| DW-B20-CHARTTRADER-01.2 Remove `BuildAtrOverlayRow` | Change A3 | MAPPED |
| Plan §2 A4 Trim overlay-injection block, preserve `AtrUpdated` subscription | Change A4 | MAPPED |
| DW-B20-CHARTTRADER-01.3 Remove `ResolveChartTraderPanel` (dead code) | Change A5 | MAPPED |
| DW-B20-CHARTTRADER-01.4 Add `_atrDisplayLabel` field to Panel | Change P1 | MAPPED |
| DW-B20-CHARTTRADER-01.4 Add `SetAtrText` public method to Panel | Change P2 | MAPPED |
| DW-B20-CHARTTRADER-01.4 Extend `BuildRiskAtrRow` with ATR display row | Change P3 | MAPPED |

Phantom work (in ticket, not in plan/spec): **NONE**
Missing work (in plan/spec, not in ticket): **NONE**

**Traceability: PASS**

---

### JS Pre-Check

| Rule | Check | Finding | Status |
|---|---|---|---|
| JS-021 | `lock()` anywhere | No `lock(` described. `_panels` is `ConcurrentDictionary` (existing). `FirstOrDefault()` on `.Values` snapshot is lock-free. | PASS |
| JS-033 | `async void` non-handler | No `async void` introduced. `Dispatcher.InvokeAsync` lambda is a synchronous `Action`. | PASS |
| JS-002 | `return null` where value expected | `UpdateAtrOverlay` returns `void` (early-exit guard). `SetAtrText` returns `void` (early-exit guard). `ResolveChartTraderPanel` (sole method returning null) is DELETED by A5. Net count decreases. | PASS |
| JS-001 | `throw` in hot path | No exceptions thrown in any new or modified code block. | PASS |
| JS-008 | Mutable fields on struct | No structs involved. `_atrDisplayLabel` is a class field on `TradeCopierPanel`. | PASS |
| JS-009 | `Dictionary<K,V>` on CopyRule/CopyEngine fields | Not applicable; changes are to AddOn and Panel only. | PASS |
| JS-023 | Atomic primitives for simple state | No atomic updates required; `_panels` ConcurrentDictionary already in place. | PASS |

**JS Pre-Check: PASS**

---

### NT8 Constraint Check

| Constraint | Check | Finding | Status |
|---|---|---|---|
| `Dispatcher.InvokeAsync` (not `.Invoke`) | A2 explicitly calls `Dispatcher.InvokeAsync(...)` | Correct dispatch method specified. | PASS |
| No `async`/`await` in lifecycle methods | No `async`/`await` in `StartAtrEngine` or `UpdateAtrOverlay` | Compliant. | PASS |
| `TradeCopierWindow` not sealed / not touched | Write-set is `TradeCopierAddOn.cs` + `TradeCopierPanel.cs` only | Not in scope. | PASS |
| No `FontFamily` set | P3 `Border` and `TextBlock`: no `FontFamily` property set | Compliant. | PASS |
| No hardcoded hex color | P3 `Border`: `BorderBrush`/`Background` intentionally unset; inherited from WPF theme | No `#RRGGBB` values. | PASS |
| ASCII-only string literals | P3 placeholder `"ATR=-.-- pts -> stopTicks=-- -> qty=--"` | All characters are ASCII (hyphen, greater-than, digits, letters, spaces). | PASS |
| NT8-003 no `volatile` | `_atrDisplayLabel` is `private TextBlock` | No `volatile` keyword introduced. | PASS |
| `engine.AtrUpdated += OnAtrUpdated` preserved | Change A4 explicitly states "subscription line MUST be preserved" and only the guard block is removed | Subscription retained. | PASS |
| `System.Linq` using directive | Ticket notes "Check whether `using System.Linq;` is already present... If not, add it" | Engineer is instructed to verify and add if absent. | PASS |
| No `CreateOrder` without `PTT-` prefix | Not applicable (no order creation in T5) | N/A. | PASS |
| No `DateTime.Now` | Not applicable | N/A. | PASS |
| LANE A files not in write-set (`CopyEngine.cs`, `CopyEngineTests.cs`) | Write-set listed: `TradeCopierAddOn.cs`, `TradeCopierPanel.cs` | LANE A files absent. | PASS |

**NT8 Check: PASS**

---

### CYC Pre-Check

| Method | File | Before | After | Constraint | Status |
|---|---|---|---|---|---|
| `BuildAtrOverlayRow` | TradeCopierAddOn | 1 | DELETED | Eliminated by A3 | PASS |
| `ResolveChartTraderPanel` | TradeCopierAddOn | 2 | DELETED | Eliminated by A5 (zero callers after A4) | PASS |
| `UpdateAtrOverlay` | TradeCopierAddOn | 2 | 2 | 1 null-guard + 1 InvokeAsync dispatch | PASS |
| `OnAtrUpdated` | TradeCopierAddOn | 1 | 1 | Unchanged | PASS |
| `StartAtrEngine` | TradeCopierAddOn | 4 | 3 | Guard 4 removed by A4 | PASS |
| `SetAtrText` | TradeCopierPanel | NEW | 2 | 1 null-guard + 1 assignment | PASS |
| `BuildRiskAtrRow` | TradeCopierPanel | 1 | 1 | Straight-line extension; no branches added | PASS |

Maximum CYC in any changed or new method: **3** (StartAtrEngine after).
All methods ≤ 8. Jane Street strict standard satisfied.

**CYC Pre-Check: PASS**

---

### Test Coverage

| Method | Kind | [Fact] Required | Provided | Rationale |
|---|---|---|---|---|
| `UpdateAtrOverlay` | Modified, `internal void` | Not required — routes to UI thread; structural-only change | N/A | Dispatcher marshal path; no behavioral logic change |
| `StartAtrEngine` | Modified, `private void` | Not required — private; structural-only removal | N/A | Guard block removal only |
| `SetAtrText` | New, `public void` | Formally required (public method) | Not provided | Justified: WPF label assignment; requires live WPF Application host and `TradeCopierPanel` with `CopyEngine` singleton + `NTBrushes` resource dictionary. Overhead exceeds value of CYC=2 null-guard assertion. Architecture plan §5 documents this rationale; plan review checklist item 14 confirmed PASS. |
| `BuildRiskAtrRow` | Modified, `private void` | Not required — private, CYC=1 | N/A | Straight-line extension |

**Assessment**: `SetAtrText` is a new `public void` method. The ticket explicitly states no `[Fact]` is required and provides a documented rationale in `02-architecture-plan-t5.md §5` (WPF Z-order defect; requires a full WPF Application host with `ChartTrader`, Grid, and hit-test simulation unavailable in the NT8 xUnit harness; plan review confirmed PASS at checklist item 14). The rationale is sound and consistently documented across plan and ticket. The [Fact] count stays at 120.

**Test Coverage: PASS** (exemption documented and plan-review-confirmed)

---

### Scan Checklist Presence

Verification that all 7 scans are present in the ticket with `ctx_shell` commands:

| Scan | Command Present | Expected Result Stated | Status |
|---|---|---|---|
| SCAN-01 lock() | `grep -rn "lock(" c:/WSGTA/universal-or-strategy/src/PropTraderTools/` | 0 actual `lock()` statements | PRESENT |
| SCAN-02 async void | `grep -rn "async void " ... --include="*.cs"` | 0 results | PRESENT |
| SCAN-03 return null | `grep -rn "return null;" ... --include="*.cs"` | 0 new; total count ≤ pre-T5 baseline | PRESENT |
| SCAN-04 volatile | `grep -rn "volatile" ... --include="*.cs"` | No new `volatile` fields | PRESENT |
| SCAN-05 build | `dotnet build .../PropTraderTools.csproj` | 3 pre-existing NT8 errors; 0 new errors | PRESENT |
| SCAN-06 tests | `dotnet test .../PropTraderTools.csproj` | 120 `[Fact]` pass, unchanged | PRESENT |
| SCAN-07 CYC | Manual review table (all 7 methods listed with expected CYC and how-to-verify column) | 0 methods CYC > 8 | PRESENT |

All 7 scans are present with `ctx_shell` commands and expected results. Defense-in-depth contract established for engineer attestation and verifier cross-check.

**Scan Checklist: PASS**

---

### File Routing

| File | Specified Path | Wave Workspace Check | Status |
|---|---|---|---|
| `TradeCopierAddOn.cs` | `src/PropTraderTools/TradeCopierAddOn.cs` → `c:/WSGTA/universal-or-strategy/src/PropTraderTools/` | Wave workspace ✅ | PASS |
| `TradeCopierPanel.cs` | `src/PropTraderTools/TradeCopierPanel.cs` → `c:/WSGTA/universal-or-strategy/src/PropTraderTools/` | Wave workspace ✅ | PASS |

No `.cs` paths pointing to Director workspace (`c:\WSGTA\universal-or-strategy-director`).

**File Routing: PASS**

---

### Spec Coverage (Aggregate)

| Spec Requirement | Covered By | Coverage Count |
|---|---|---|
| DW-B20-CHARTTRADER-01 (root) | T5 (this ticket) | 1 |
| DW-B20-CHARTTRADER-01.1 | A1 | 1 |
| DW-B20-CHARTTRADER-01.2 | A3 | 1 |
| DW-B20-CHARTTRADER-01.3 | A5 | 1 |
| DW-B20-CHARTTRADER-01.4 | P1 + P2 + P3 | 1 (three changes, one requirement) |
| DW-B20-CHARTTRADER-01.5 | A2 | 1 |

No uncovered requirements. No duplicate coverage.

**Spec Coverage: PASS**

---

### Spec Requirement IDs, Method Signatures, and [Fact] Names

| Engineer Contract Element | Present in Ticket | Status |
|---|---|---|
| Spec requirement IDs | Header table: DW-B20-CHARTTRADER-01 through .5 | PASS |
| Exact method signatures for all changed/new methods | `StartAtrEngine`, `UpdateAtrOverlay`, `SetAtrText`, `BuildRiskAtrRow` — all with return types and visibility | PASS |
| [Fact] test method names | No new [Fact] required (documented exemption); count stays at 120 | PASS |
| 7-scan checklist (SCAN-01 through SCAN-07) | All 7 present with `ctx_shell` commands and expected results | PASS |

---

### VERDICT

| Check | Result |
|---|---|
| Traceability | PASS |
| JS Pre-Check | PASS |
| NT8 Constraint Check | PASS |
| CYC Pre-Check | PASS |
| Test Coverage | PASS |
| Scan Checklist (SCAN-01 through SCAN-07) | PASS |
| File Routing | PASS |
| Spec Coverage | PASS |

## Overall: TICKET_REVIEW_PASS

Zero violations found across all checks. All 8 changes (A1–A5, P1–P3) are fully
traced to spec requirements and plan items. All 7 scan commands are present with
`ctx_shell` invocations and explicit expected results. CYC max = 3. No JS-021,
JS-033, JS-002, NT8-003, or NT8 WPF constraint violations described. LANE A
files and `TradeCopierWindow` are absent from the write-set. The `SetAtrText`
[Fact] exemption is documented in the architecture plan (§5) and confirmed by the
plan reviewer (V3 checklist item 14). The engineer may proceed.
