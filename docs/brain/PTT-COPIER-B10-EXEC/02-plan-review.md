# PTT-COPIER-B10-EXEC Architecture Plan Review
# Phase 2 Plan Review — Cycle 3 (FINAL)
# Reviewer: ptt-plan-reviewer
# Status: REVIEW_PASS
# Date: 2026-07-10

---

## Executive Summary

**Verdict: REVIEW_PASS**

All four blocking violations from Cycles 1 and 2 are **FIXED**. No new P0 or P1 violations
found. Two non-blocking advisories from Cycle 2 remain — both are naming divergences that do
not constitute DNA violations and will not cause build errors.

**Blocking violation count: 0**
**Advisory count: 2 (unchanged from Cycle 2 — non-blocking)**

---

## Section 1 — Cycle Violation Resolution Matrix

### VIOLATION-1: MoveStopToBreakEven acc.Change() path + IsStopAlreadyAtBe

| Check Point | Location in Plan | Result |
|-------------|------------------|--------|
| MoveStopToBreakEven uses acc.Change() for ALL stop types | Section 1 (DW-B9-GAP-001b/001d), Section 2.1, Section 3.1, Section 4.1, Section 5.1 | ✅ FIXED |
| No cancel+replace in T1 MoveStopToBreakEven | Section 5.1: "PTT-BE-Stop CreateOrder signal removed from T1 scope entirely" | ✅ FIXED |
| IsStopAlreadyAtBe() helper present | Section 2.1, 3.1 (CYC=2), Section 4.1 data flow | ✅ FIXED |
| Trail survives acc.Change() — GAP-001d verdict adopted | Section 5.1 | ✅ FIXED |

**VIOLATION-1: RESOLVED ✅**

---

### VIOLATION-2: volatile int _pendingBeState + AccountItemUpdate + AccountItemEventArgs

| Check Point | Location in Plan | Result |
|-------------|------------------|--------|
| Uses acc.AccountItemUpdate (NOT Instrument.MarketData) | Section 2.1, 3.1, 4.2, 5.2, Section 6 API table | ✅ FIXED |
| volatile int _pendingBeState (NOT volatile bool/double) | Section 2.1 fields, Section 5.4 | ✅ FIXED |
| Handler signature: AccountItemEventArgs | Section 3.1: `private void OnPendingBeAccountUpdate(object sender, NinjaTrader.Cbi.AccountItemEventArgs e)` | ✅ FIXED |
| Fires on NT8 account background thread | Section 5.2, Section 7 threading table | ✅ FIXED |

**VIOLATION-2: RESOLVED ✅**

---

### VIOLATION-3: GAP-002b disposition reflects acc.Change() path (no cancel+replace)

| Check Point | Location in Plan | Result |
|-------------|------------------|--------|
| DW-B10-GAP-002b disposition updated | Section 1: "T1 implements acc.Change() for trailing stops in MoveStopToBreakEven (GAP-001d confirmed path). T2's ArmPendingBe fires BreakEven() when UnrealizedPnL crosses zero, using the same acc.Change() path. No cancel+replace in either T1 or T2." | ✅ FIXED |

**VIOLATION-3: RESOLVED ✅**

---

### VIOLATION-4: T4 WPF overlay ATR box (BuildAtrOverlayRow + UpdateAtrOverlay + xUnit test)

| Check Point | Location in Plan | Result |
|-------------|------------------|--------|
| BuildAtrOverlayRow(Panel chartTraderRoot) method present | Section 2.4 (WPF overlay field + new methods block) | ✅ FIXED |
| BuildAtrOverlayRow CYC annotation | Section 2.4: CYC=1 (straight-line widget construction; no branches) | ✅ FIXED |
| UpdateAtrOverlay(string atrDisplay) method present | Section 2.4, Section 3.4 | ✅ FIXED |
| UpdateAtrOverlay CYC annotation | Section 3.4: CYC=2 (null guard on _atrOverlayLabel(1), Dispatcher.InvokeAsync(2)) | ✅ FIXED |
| Overlay format string correct | Section 2.4: "ATR=N.NN pts -> stopTicks=T -> qty=Q" (spec: "ATR=N.NN pts → stopTicks=T → qty=Q"; ASCII-safe substitution of →) | ✅ FIXED |
| Overlay injected into ChartTrader panel | Section 2.4: "creates Border + TextBlock, injects into ChartTrader panel Grid" | ✅ FIXED |
| Live update path via AtrSizingEngine.AtrUpdated event | Section 2.4: `internal event Action<string> AtrUpdated;` in AtrSizingEngine.cs; Section 4.4 data flow | ✅ FIXED |
| xUnit test for overlay | Section 10: `[Fact] UpdateAtrOverlay_FormatsDisplayString_CorrectText` (T4) | ✅ FIXED |
| Test count total updated | Section 10: "Total: 22 [Fact] tests" | ✅ FIXED |

**VIOLATION-4: RESOLVED ✅**

---

## Section 2 — Full Checklist A–O

### A. All 9 OPEN Deferred Items Explicitly Addressed

| ID | Disposition | Ticket | Plan Location | Status |
|----|-------------|--------|---------------|--------|
| DW-B9-GAP-001a | Skip follower orders where order.TrailPrice > 0 in HandleBracketChange (Option B) | T1 | Section 1 | ✅ ADDRESSED |
| DW-B9-GAP-001b | MoveStopToBreakEven uses acc.Change() for all stop types; IsStopAlreadyAtBe guard | T1 | Section 1 | ✅ ADDRESSED |
| DW-B9-GAP-001c | TightenTicks field on CopyRule + TightenStop engine method + UI buttons | T3 | Section 1 | ✅ ADDRESSED |
| DW-B9-GAP-001d | GAP-001d CONFIRMED verdict adopted: trail survives acc.Change() | T1 | Section 1 | ✅ ADDRESSED |
| DW-B9-01 | SHELVED THIS BLOCK — ATR box visualization drawn on chart canvas | N/A | Section 1 | ✅ EXPLICIT SHELVE |
| DW-B9-02 | T4: chart attachment investigation (3-path) + WPF overlay ATR box in ChartTrader panel | T4 | Section 1 | ✅ ADDRESSED |
| DW-B9-03 | SHELVED THIS BLOCK — click trader Bid+1/Ask-1 auto-offset | N/A | Section 1 | ✅ EXPLICIT SHELVE |
| DW-B10-GAP-002a | ArmPendingBe + OnPendingBeAccountUpdate + acc.AccountItemUpdate + Panel 3-state | T2 | Section 1 | ✅ ADDRESSED |
| DW-B10-GAP-002b | T1 acc.Change() trail path + T2 ArmPendingBe fires BreakEven via same path; no cancel+replace | T1+T2 | Section 1 | ✅ ADDRESSED |

**CHECK A: PASS ✅** — All 9 items explicitly addressed or shelved.

Note on DW-B9-01 vs DW-B9-02/T4 overlay: DW-B9-01 (ATR box drawn on chart canvas) is
correctly shelved. T4's WPF overlay (live ATR text in ChartTrader panel) is a different item
addressed under DW-B9-02 / DW-B10-CHART-ATTACH-01. Plan correctly distinguishes these in
Section 2.4 note: "DW-B9-01 (ATR box visualization drawn directly on chart canvas) remains
SHELVED this block. The overlay added here is a ChartTrader PANEL text display."

---

### B. Per-Ticket Structural Quality

| Ticket | Spec Req IDs | Files | Method Signatures | JS Constraints | xUnit [Fact] Names |
|--------|-------------|-------|-------------------|----------------|-------------------|
| T1 | DW-B9-GAP-001a/b/d, DW-B10-GAP-002b (partial) | CopyEngine.cs only | ✅ Section 3.1 (6 signatures with CYC) | ✅ Section 9 | ✅ Section 10: 6 facts incl. MoveStopToBreakEven_TrailingStop_ChangesStopViaChange, HandleBracketChange_FollowerTrailingStop_Skips, IsTrailingStop_*, MoveStopToBreakEven_StopAlreadyAtBe_Skips |
| T2 | DW-B10-GAP-002a, DW-B10-GAP-002b (partial) | CopyEngine.cs + TradeCopierPanel.cs | ✅ Section 3.1–3.2 (5 new sigs with CYC) | ✅ Section 9 | ✅ Section 10: 7 facts incl. ArmPendingBe_SetsStateArmed, OnPendingBeAccountUpdate_UnrealizedPnlPositive_FiresPendingBeFired, OnPendingBeAccountUpdate_TriggeredOnce_DisarmsBeforeFiring |
| T3 | DW-B9-GAP-001c (DW-B10-TIGHTEN-STOP-01) | CopyEngine.cs + TradeCopierPanel.cs + TradeCopierWindow.cs | ✅ Section 3.1–3.3 (3 new sigs with CYC) | ✅ Section 9 | ✅ Section 10: 7 facts incl. TightenStop_TrailingStop_CancelsAndReplaces, CopyRule_TightenTicks_OldXmlBackwardCompat |
| T4 | DW-B9-02 (DW-B10-CHART-ATTACH-01) | TradeCopierAddOn.cs + AtrSizingEngine.cs | ✅ Section 3.4 (3 sigs with CYC) | ✅ Section 9 | ✅ Section 10: 3 facts incl. UpdateAtrOverlay_FormatsDisplayString_CorrectText |

**CHECK B: PASS ✅**

---

### C. No lock() — JS-021 (P0)

Section 7 explicitly states: "No lock() anywhere. All new state follows existing project
patterns." Section 9 preflight: "JS-021 no lock() — Zero lock() in all new/modified methods."
T2 uses `Interlocked.CompareExchange` (correct lock-free disarm pattern). ConcurrentBag
rebuild (existing pattern) unchanged.

**CHECK C: PASS ✅ (JS-021 clean)**

---

### D. No async void except FlashBeFired — JS-033 (P0)

Only `FlashBeFired` is declared `async void`. Section 5.6 justification: (1) UI event handler
invoked via Dispatcher.InvokeAsync (not a library method); (2) task mandate explicitly allows
it. All other new methods return void, bool, int, or nothing async. `OnPendingBeFiredDispatch`
is a plain (non-async) void method that calls Dispatcher.InvokeAsync.

**CHECK D: PASS ✅ (JS-033 — only FlashBeFired exemption, correctly justified)**

---

### E. No return null — JS-002 (P0)

All new methods:
- `ArmPendingBe`, `DisarmPendingBe`, `TightenStop`, `TightenOneStop`, `SyncFollowerBracket`,
  `BuildBeArmRow`, `OnBEArmClick`, `UpdateBEArmVisuals`, `OnPendingBeFiredDispatch`,
  `FlashBeFired`, `OnTightenStop`, `OnRuleTightenStop`, `BuildAtrOverlayRow`,
  `UpdateAtrOverlay`, `OnPendingBeAccountUpdate` — all return void.
- `IsTrailingStop`, `IsStopAlreadyAtBe` — return bool (never null).

No new method with a reference return type returns null.

**CHECK E: PASS ✅ (JS-002 clean)**

---

### F. CYC <= 8 per method

All new/modified methods from Section 9 preflight:

| Method | CYC | <= 8? |
|--------|-----|-------|
| MoveStopToBreakEven | 6 | ✅ |
| HandleBracketChange | 6 | ✅ |
| SyncFollowerBracket | 5 | ✅ |
| IsStopAlreadyAtBe | 2 | ✅ |
| IsTrailingStop | 1 | ✅ |
| ArmPendingBe | 4 | ✅ |
| DisarmPendingBe | 3 | ✅ |
| OnPendingBeAccountUpdate | 5 | ✅ |
| TightenStop | 5 | ✅ |
| TightenOneStop | 4 | ✅ |
| FlashBeFired | 2 | ✅ |
| OnBEArmClick | 3 | ✅ |
| UpdateBEArmVisuals | 2 | ✅ |
| OnPendingBeFiredDispatch | 1 | ✅ |
| BuildBeArmRow | 1 | ✅ |
| OnTightenStop | 3 | ✅ |
| OnRuleTightenStop | 4 | ✅ |
| StartAtrEngine (modified) | 4 | ✅ |
| BuildAtrOverlayRow | 1 | ✅ |
| UpdateAtrOverlay | 2 | ✅ |

**CHECK F: PASS ✅ — all 20 methods CYC <= 8**

---

### G. No volatile double — NT8-003

Section 9: "NT8-003 no volatile double — No volatile double anywhere.
_pendingBeAccount and _pendingBeInstrument are plain refs (not volatile).
_pendingBeState is volatile int (correct)."
Section 7 threading table confirms no volatile double fields.

**CHECK G: PASS ✅ (NT8-003 clean)**

---

### H. Math.Max/Min not Math.Clamp (.NET 4.8)

Section 9: TightenTicks clamped using `Math.Max(1, Math.Min(500, ticks))`. Math.Clamp is
not available in .NET Framework 4.8.

**CHECK H: PASS ✅**

---

### I. ASCII-only strings

Section 9 confirms all new literals are ASCII-only: "PTT-Tighten-Stop", "Arm BE", "Tighten",
"tks", "0 selected", "armed", "trailing skip", "ATR=-.-- pts -> stopTicks=-- -> qty=--", etc.
Note: Plan uses `->` (ASCII hyphen-greater-than) instead of spec's `→` (Unicode arrow) in the
display format string. This is correct — Unicode characters in NT8 string literals are a SCAN-03
violation. Plan correctly uses ASCII-safe substitute.

**CHECK I: PASS ✅**

---

### J. PTT- prefix on CreateOrder signals (SCAN-05)

Section 4.3 / Section 6: Only T3 uses CreateOrder, with signal name "PTT-Tighten-Stop".
T1 and T2 use acc.Change() — no CreateOrder call, no signal name needed.

**CHECK J: PASS ✅**

---

### K. No FontFamily override / no hardcoded hex colors (SCAN-03, SCAN-04)

Section 9: "No new FontFamily. New buttons use existing MakeBrush statics
(BrushActive/BrushCaution/BrushInactive)." No #RRGGBB hex color literals planned.

**CHECK K: PASS ✅**

---

### L. No abstract record / ImmutableDictionary / { get; init; }

- No new record types introduced anywhere in the plan.
- No ImmutableDictionary usage. `List<Button> _tightenBtns` uses existing allowed collection.
- CopyRuleDto.TightenTicks uses `{ get; set; } = 0;` (correct for XmlSerializer compatibility,
  NOT `{ get; init; }` which is NT8-001 banned).
- CopyRule.TightenTicks is a `readonly int` field on the existing `readonly struct` (correct).

**CHECK L: PASS ✅ (NT8-001, NT8-002, NT8-004 clean)**

---

### M. Shelved items not planned

DW-B9-01 (ATR visualization drawn on chart canvas) and DW-B9-03 (click trader Bid+1/Ask-1
auto-offset) are both explicitly marked "SHELVED THIS BLOCK" in Section 1 with no planned
implementation. No ticket touches either item.

**CHECK M: PASS ✅**

---

### N. MarketData/AccountItemUpdate approach feasible

- **AccountItemUpdate (T2):** GAP-002 CONFIRMED 2026-07-09 — fires in AddOn context,
  10 events observed. AccountItem.UnrealizedProfitLoss is the confirmed event filter.
  `e.Value >= 0` trigger condition correctly captures "price at or past entry" via PnL proxy.
  Existing `OnAccountItemUpdate` in TradeCopierPanel.cs:253 (using Dispatcher.InvokeAsync)
  confirms the pattern compiles and works at runtime.
- **MarketData (T3):** `instrument.MarketData?.Bid/Ask` used on UI thread (button handler).
  Fallback to `pos.AveragePrice` if MarketData null/0. Conservative and safe. Section 5.3.

**CHECK N: PASS ✅**

---

### O. T4 chart attachment correct

Three-path investigation in Section 4.4:
1. `chart.NinjaScripts.Add(engine)` — try first
2. `chart.Indicators.Add(engine)` — try if path 1 fails CS1061
3. Event-based fallback: `chart.BarsArray[0].Bars.BarUpdate += (_, _) => engine.ManualOnBarUpdate()` — always compiles; no chart attachment API dependency

Path 3 is always-compiles guaranteed. Engineer notes that `ManualOnBarUpdate()` calls
`ATR(Period)[0]` internally. Plan correctly defers the choice to runtime investigation and
mandates documenting the chosen path in NT8_ADDON_KNOWLEDGE.md.

WPF overlay creation is conditional: `if (chartTraderRoot != null)` guard ensures graceful
fallback if visual tree traversal fails. Engine still fires AtrUpdated even if overlay creation
is skipped.

`ResolveChartTraderPanel(chart)` helper noted as traversing the visual tree (Section 4.4 note).
This follows the existing B7 pattern ("visual tree walk to ChartTrader.Content Grid, 5
iterations to solve" per spec line 2138).

**CHECK O: PASS ✅**

---

## Section 3 — Jane Street / NT8 DNA Full Compliance Table

| Rule ID | Rule | Check | Status |
|---------|------|-------|--------|
| JS-021 | No lock() | Zero lock() in all new/modified methods. Interlocked.CompareExchange used in T2 (lock-free CAS). | ✅ PASS |
| JS-033 | No async void (except event handlers) | Only FlashBeFired. Section 5.6 justification: UI event handler via Dispatcher.InvokeAsync. | ✅ PASS |
| JS-002 | No return null | All new methods return void or bool. No new reference-returning method. | ✅ PASS |
| JS-001 | No throw in hot path | acc.Change/Cancel/CreateOrder wrapped in try/catch logging via StatusUpdate. Section 9. | ✅ PASS |
| JS-023 | UI updates via Dispatcher.InvokeAsync only | OnPendingBeFiredDispatch and UpdateAtrOverlay both use Dispatcher.InvokeAsync. AccountItemUpdate callback never touches UI directly. | ✅ PASS |
| JS-009 | No Dictionary for shared/thread-touched state | No new Dictionary. _atrEngines uses existing Dictionary (UI-thread-only). | ✅ PASS |
| JS-010 | No public constructor on singleton/struct | CopyRule.Create factory unchanged. No new public constructor added to any singleton. | ✅ PASS |
| NT8-001 | No { get; init; } | CopyRuleDto.TightenTicks uses `{ get; set; } = 0;` (correct). No init-only properties. | ✅ PASS |
| NT8-002 | No abstract record / sealed record | No new record types. | ✅ PASS |
| NT8-003 | No volatile double | _pendingBeState = volatile int. _pendingBeBufferTicks = volatile int. No volatile double. | ✅ PASS |
| NT8-004 | No ImmutableDictionary | Not used. | ✅ PASS |
| NT8-007 | CreateOrder arg 12 = (CustomOrder)null | Section 4.3, 6: T3 uses `(NinjaTrader.Cbi.CustomOrder)null`. T1/T2 use acc.Change() only. | ✅ PASS |
| SCAN-01 | No lock() | Confirmed zero lock() in scope. | ✅ PASS |
| SCAN-02 | CYC <= 8 | All 20 enumerated methods <= 8. Max is 6. | ✅ PASS |
| SCAN-03 | ASCII-only literals | All new string literals use 7-bit ASCII. → replaced with -> in format strings. | ✅ PASS |
| SCAN-04 | No DateTime.Now | No time logging in new methods. | ✅ PASS |
| SCAN-05 | PTT- prefix on CreateOrder | "PTT-Tighten-Stop" only. T1/T2 use acc.Change(). | ✅ PASS |
| SCAN-06 | No FontFamily / no hex colors / no Math.Clamp | MakeBrush statics used. Math.Max/Min pattern. | ✅ PASS |
| SCAN-07 | Dispatcher.InvokeAsync for all UI updates from bg threads | OnPendingBeFiredDispatch + UpdateAtrOverlay both use Dispatcher.InvokeAsync. | ✅ PASS |
| NT8-misc | Account.All not in constructors | Section 6: "only in Loaded handlers, never constructors." | ✅ PASS |
| NT8-misc | No async/await in OnInitialize/OnDestroyed/OnWindowCreated | Not planned. | ✅ PASS |
| NT8-misc | sealed TradeCopierWindow | Not a concern — no new window class. | ✅ PASS |

**All 22 DNA/NT8 checks: PASS ✅**

---

## Section 4 — Spec Requirement Coverage Matrix

| Spec Ticket ID | Requirement | Plan Coverage | Status |
|----------------|-------------|---------------|--------|
| DW-B10-TRAILING-STOP-01 (T1) | IsTrailingStop helper (TrailPrice > 0) | Section 2.1, 3.1 (CYC=1), 4.1 | ✅ ADDRESSED |
| DW-B10-TRAILING-STOP-01 (T1) | HandleBracketChange skip for trailing stops | Section 2.1, 4.1: `SyncFollowerBracket` skips if `IsTrailingStop(fo)` | ✅ ADDRESSED |
| DW-B10-TRAILING-STOP-01 (T1) | IsStopAlreadyAtBe guard (idempotency) | Section 2.1, 3.1 (CYC=2), 4.1 | ✅ ADDRESSED |
| DW-B10-TRAILING-STOP-01 (T1) | MoveStopToBreakEven acc.Change() path (GAP-001d) | Section 2.1, 4.1, 5.1 | ✅ ADDRESSED |
| DW-B10-PENDING-BE-01 (T2) | ArmPendingBe state machine, volatile int _pendingBeState | Section 2.1, 3.1 (CYC=4), 4.2, 5.4 | ✅ ADDRESSED |
| DW-B10-PENDING-BE-01 (T2) | acc.AccountItemUpdate (UnrealizedProfitLoss) subscription | Section 3.1, 4.2, 5.2, 6 | ✅ ADDRESSED |
| DW-B10-PENDING-BE-01 (T2) | Panel BE button grey→amber→green-flash toggle | Section 2.2, 4.2: UpdateBEArmVisuals + FlashBeFired | ✅ ADDRESSED |
| DW-B10-TIGHTEN-STOP-01 (T3) | TightenTicks field on CopyRule + CopyRuleDto | Section 2.1, serialization backward compat (Section 5.5) | ✅ ADDRESSED |
| DW-B10-TIGHTEN-STOP-01 (T3) | TightenStop engine method + Panel/Window buttons | Section 2.1–2.3, 3.1–3.3, 4.3 | ✅ ADDRESSED |
| DW-B10-TIGHTEN-STOP-01 (T3) | T3 uses acc.Change() for fixed stops (spec: "pure acc.Change() path") | Section 4.3, Section 1 DW-B9-GAP-001c | ✅ ADDRESSED |
| DW-B10-CHART-ATTACH-01 (T4) | Resolve chart.NinjaScripts.Add (or fallback API) | Section 2.4 (3-path investigation), 3.4, 4.4 | ✅ ADDRESSED |
| DW-B10-CHART-ATTACH-01 (T4) | WPF overlay ATR box in ChartTrader panel (ATR=N.NN pts → stopTicks=T → qty=Q) | Section 2.4 (BuildAtrOverlayRow + UpdateAtrOverlay), 3.4, 4.4 | ✅ ADDRESSED |

**All 12 spec requirements addressed. ✅**

---

## Section 5 — Threading Model Assessment

| Field / Path | Thread Write | Thread Read | Mechanism | Assessment |
|--------------|-------------|-------------|-----------|------------|
| `_pendingBeState` (volatile int) | UI thread (ArmPendingBe) | NT8 account bg (AccountItemUpdate) | volatile + Interlocked CAS disarm | ✅ Correct |
| `_pendingBeBufferTicks` (volatile int) | UI thread | NT8 account bg | volatile read/write | ✅ Correct |
| `_pendingBeAccount` (Account ref) | UI thread, once before ARM | NT8 account bg | volatile store on _pendingBeState provides release fence (x64 TSO) | ✅ Correct (Section 5.4) |
| `_pendingBeInstrument` (Instrument ref) | UI thread, once before ARM | NT8 account bg | Same release fence | ✅ Correct (Section 5.4) |
| `_beArmState` (bool) | UI thread only | UI thread only | plain bool | ✅ Correct |
| `_atrOverlayLabel` (TextBlock) | UI thread (BuildAtrOverlayRow) | UI thread (Dispatcher.InvokeAsync) | plain ref, all access on UI thread | ✅ Correct |
| UI update from AccountItemUpdate bg thread | — | — | OnPendingBeFiredDispatch → Dispatcher.InvokeAsync → FlashBeFired | ✅ JS-023 compliant |
| UI update from AtrUpdated engine event | — | — | UpdateAtrOverlay → Dispatcher.InvokeAsync | ✅ JS-023 compliant |
| lock() | — | — | Zero usage | ✅ JS-021 compliant |

---

## Section 6 — Advisories (Non-Blocking, Unchanged from Cycle 2)

### ADVISORY-1: Callback name diverges from spec (non-blocking)

**Plan name**: `OnPendingBeAccountUpdate` (Sections 2.1, 3.1, 4.2)
**Spec name**: `OnPendingBePriceTick` (spec HTML lines 2587, 2643)

The plan uses `OnPendingBeAccountUpdate` which is more accurate: it is an
`AccountItemUpdate` handler, not a price-tick handler. This is a deliberate rename
and does not constitute a DNA violation. Will not cause a build error. Engineer should
document the rename rationale in the T2 ticket completion report.

### ADVISORY-2: TightenStop method name diverges from spec (non-blocking)

**Plan name**: `TightenStop(Instrument instrument, int ticks)` (Section 3.1)
**Spec name**: `TightenAllStops(rule)` (spec HTML line 2660)

The plan signature takes `(Instrument, int)` directly; the spec calls it with a rule.
Behavior is equivalent. The plan adds an overload-compatible approach that reads ticks
from the button's TextBox directly rather than from rule.TightenTicks. Both are valid.
Engineer should note this in T3 ticket execution.

---

## Section 7 — SCAN Template (07-Scan Pre-flight)

| Scan | Check | Status |
|------|-------|--------|
| SCAN-01 | No lock() in new/modified methods | ✅ PASS |
| SCAN-02 | CYC <= 8 for every new/modified method | ✅ PASS |
| SCAN-03 | ASCII-only in all new string literals | ✅ PASS |
| SCAN-04 | No DateTime.Now (use DateTime.UtcNow if needed) | ✅ PASS |
| SCAN-05 | All CreateOrder signal names start with "PTT-" | ✅ PASS |
| SCAN-06 | No FontFamily, no hardcoded hex colors, no Math.Clamp | ✅ PASS |
| SCAN-07 | Dispatcher.InvokeAsync for all UI updates from background threads | ✅ PASS |

---

## Section 8 — Diag Row Disposition Assessment

Plan Section 11 correctly defers diag row removal to B11. Rationale: T4 actively uses
`RunGap002Test` for chart attachment verification. The "REMOVE AFTER TESTS" comment in
source correctly indicates deferred removal intent. This is a correct plan decision.

---

## Final Verdict

```
REVIEW_PASS

Cycle 3 final review. All 4 blocking violations from Cycles 1 and 2 are FIXED:
  VIOLATION-1 (FIXED): MoveStopToBreakEven uses acc.Change(); IsStopAlreadyAtBe present
  VIOLATION-2 (FIXED): volatile int _pendingBeState; acc.AccountItemUpdate; AccountItemEventArgs
  VIOLATION-3 (FIXED): GAP-002b disposition reflects acc.Change() path; no cancel+replace
  VIOLATION-4 (FIXED): T4 has BuildAtrOverlayRow(CYC=1) + UpdateAtrOverlay(CYC=2) with
                        AtrUpdated event wiring + [Fact] UpdateAtrOverlay_FormatsDisplayString_CorrectText

Zero P0 violations. Zero P1 violations.
Advisories: 2 (handler name divergence, T3 method name divergence — non-blocking, noted).

Tickets may proceed.
```
