# B173 -- AtrWidget Live ATR Fix
# Final Review -- Phase 5
# Author: ptt-plan-reviewer
# Status: FINAL_PASS

---

## Evidence Read

| File | Purpose |
|------|---------|
| `docs/brain/B173/02-architecture-plan.md` | Architecture contract |
| `docs/brain/B173/04-ticket-review.md` | Ticket review (TICKET_REVIEW_PASS) |
| `docs/brain/B173/ticket-1-completion.md` | T1 engineer report (BUILD_PASS) |
| `docs/brain/B173/ticket-1-verification.md` | T1 verifier report (VERIFY_PASS) |
| `docs/brain/B173/ticket-2-completion.md` | T2 engineer report (BUILD_PASS) |
| `docs/brain/B173/ticket-2-verification.md` | T2 verifier report (VERIFY_PASS) |
| `docs/brain/B173/ticket-3-completion.md` | T3 engineer report (BUILD_PASS) |
| `docs/brain/B173/ticket-3-verification.md` | T3 verifier report (VERIFY_PASS) |
| `src/PropTraderTools/AtrCalc.cs` | Primary new artifact (97 lines) |
| `src/PropTraderTools/TradeCopierPanel.cs` lines 3060-3290 | AtrWidget inner class + SetAtrChartControl |
| `src/PropTraderTools/TradeCopierAddOn.cs` lines 385-415 | WireNewPanel section |
| `src/PropTraderTools/CopyEngineTests.cs` lines 10220-10258 | B173AtrCalcTests class |
| `docs/brain/B172/06-deferred-backlog.md` | Prior deferred items (READ ONLY, carry forward) |
| `glob: src/PropTraderTools/AtrSizingEngine.cs` | Deletion confirmation (no file found) |

---

## Check 1: Coherent System (end-to-end wiring)

| Link in chain | Spec requirement | Source evidence | Status |
|--------------|-----------------|----------------|--------|
| `WireNewPanel` calls `FindVisualChild<ChartControl>(chart)` → `cc` | plan Section 4 | `TradeCopierAddOn.cs` line 397: `var cc = FindVisualChild<ChartControl>(chart);` | PASS |
| `WireNewPanel` calls `panel.SetAtrChartControl(cc)` | plan Section 4 | `TradeCopierAddOn.cs` line 398: `panel.SetAtrChartControl(cc);` | PASS |
| `panel.SetAtrChartControl` delegates to `_widget?.SetChartControl(cc)` | plan Section 3b | `TradeCopierPanel.cs` lines 3079-3082: `_widget?.SetChartControl(cc)` | PASS |
| `AtrWidget.SetChartControl` stores `_chartControl` and starts timer | plan Section 3a | `TradeCopierPanel.cs` lines 3211-3226: `_chartControl = cc; ... DispatcherTimer.Tick += OnTimerTick; ... _atrPollTimer.Start()` | PASS |
| `OnTimerTick` calls `AtrCalc.Compute(_chartControl, 14)` | plan Section 3a | `TradeCopierPanel.cs` line 3249: `double atr = AtrCalc.Compute(_chartControl, 14);` | PASS |
| if `atr > 0.0` → latch `_lastAtr` + `_hasData` | plan Section 3a | `TradeCopierPanel.cs` lines 3250-3254: `if (atr > 0.0) { _lastAtr = atr; _hasData = true; }` | PASS |
| `AtrCalc.Compute(ChartControl, int)` extracts OHLC → delegates to pure overload | plan Section 2 | `AtrCalc.cs` lines 67-94: BarsArray → GetHigh/GetLow/GetClose → Compute(highs, lows, closes, period) | PASS |
| `RecalcDisplay()` formats `Math.Ceiling(_lastAtr * _atrFraction)` as `int + " pts"` | plan Section 8 | `TradeCopierPanel.cs` lines 3264-3265 | PASS |

**Result: COHERENT SYSTEM ✓**

---

## Check 2: Cross-File JS Violations

Scan scope: `src/PropTraderTools/` (all .cs files)
Evidence source: T3 Layer 3 independent scans (ticket-3-verification.md)

| Violation class | Scan | T3 Layer 3 Result | Status |
|----------------|------|------------------|--------|
| `lock()` anywhere (JS-021) | SCAN-01 | 0 actual `lock()` statements; all hits are in "no lock()" comments | PASS |
| `volatile double` field (NT8-003) | SCAN-02 | 0 field declarations; 3 comment-only hits only | PASS |
| Unicode string literals (ASCII-only) | SCAN-03 | 0 non-ASCII bytes across all .cs files | PASS |
| `AtrSizingEngine` reference (deleted) | SCAN-04 | 0 matches; terminal gate cleared | PASS |
| `throw` in `AtrCalc.Compute` or `OnTimerTick` (JS-001) | source review | Both Compute overloads: `try/catch → return 0.0`. `OnTimerTick`: no throw, no try/catch (AtrCalc absorbs per JS-001 contract). | PASS |

Note on SCAN-01 false positives: Strings such as `// JS-021: no lock()` and `// no lock anywhere` contain
the literal text `lock(` but are comments, not C# `lock` statements. Zero actual lock invocations confirmed.

**Result: NO CROSS-FILE JS VIOLATIONS ✓**

---

## Check 3: Missing Wiring

| Item | Expected state | Confirmed |
|------|---------------|-----------|
| `AtrSizingEngine.cs` deleted | File absent from `src/PropTraderTools/` | `glob` returns no result ✓ |
| `SetAtrProvider` removed from `TradeCopierPanel` | No method with that name | Source at lines 3077-3082 shows `SetAtrChartControl` only; no `SetAtrProvider` ✓ |
| `SetAtrChartControl` present on `TradeCopierPanel` | `internal void SetAtrChartControl(ChartControl cc)` | Lines 3079-3082 confirmed ✓ |
| `SetChartControl` present on `AtrWidget` | `internal void SetChartControl(ChartControl cc)` | Lines 3211-3226 confirmed ✓ |
| `FindVisualChild<ChartControl>` called in `WireNewPanel` | `var cc = FindVisualChild<ChartControl>(chart)` | `TradeCopierAddOn.cs` line 397 confirmed ✓ |
| `_provider` field removed from `AtrWidget` | No `_provider` field in `AtrWidget` | `TradeCopierPanel.cs` line 3168: `private ChartControl _chartControl;` (no `_provider`) ✓ |
| `Detach()` clears `_chartControl` | `_chartControl = null` in `Detach` | Line 3238 confirmed ✓ |

**Result: NO MISSING WIRING ✓**

---

## Check 4: Spec Requirements Coverage Matrix

| Requirement | Addressed? | Plan Section | Ticket | Source |
|-------------|-----------|-------------|--------|--------|
| B173-R1: `AtrSizingEngine.cs` deleted | YES | Section 1 | T3 | `glob` = no file; SCAN-04 = 0 |
| B173-R2: `AtrCalc.cs` with pure-array + ChartControl Compute overloads | YES | Section 2 | T1 | `AtrCalc.cs` lines 26-94 |
| B173-R3: `TradeCopierPanel.cs`: `_provider` → `_chartControl`, `SetProvider` → `SetChartControl` | YES | Section 3a/3b | T2 | Lines 3168, 3211-3226, 3079-3082 |
| B173-R4: `WireNewPanel` no longer creates `AtrSizingEngine`; wires `ChartControl` | YES | Section 4 | T3 | `TradeCopierAddOn.cs` lines 397-398 |
| B173-R5: Period=14 hardcoded | YES | Section 3a | T2 | `OnTimerTick` line 3249: `AtrCalc.Compute(_chartControl, 14)` |
| B173-R6: T-B173-T1-01 and T-B173-T1-02 pass; no regressions | YES | Section 5 | T1+T3 | `CopyEngineTests.cs` lines 10231-10255; 2/2 PASS confirmed by T1 and T3 verification |

**Result: ALL SPEC REQUIREMENTS SATISFIED ✓**

---

## Check 5: All 7 Scans Zero (terminal state -- T3 verification)

Layer 3 independent verification (ticket-3-verification.md):

| # | Scan | T3 Layer 3 Result | Gate Status |
|---|------|------------------|-------------|
| SCAN-01 | `Get-ChildItem src/PropTraderTools/ -Recurse \| Select-String "lock\("` | 0 actual `lock()` statements | PASS |
| SCAN-02 | `Get-ChildItem src/PropTraderTools/ -Recurse \| Select-String "volatile double"` | 0 field declarations (comments only) | PASS |
| SCAN-03 | Non-ASCII byte scan all .cs files | 0 non-ASCII characters | PASS |
| SCAN-04 | `Get-ChildItem src/PropTraderTools/ -Recurse \| Select-String "AtrSizingEngine"` | 0 matches -- **terminal gate cleared** | PASS |
| SCAN-05 | `dotnet build src/PropTraderTools/PropTraderTools.Tests.csproj` | 0 errors (1139 pre-existing xUnit1004 warnings; not new) | PASS |
| SCAN-06 | `dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj` | B173 tests 2/2 PASS; 8 pre-existing failures unchanged; 0 regressions | PASS |
| SCAN-07 | `powershell -File .\scripts\ptt-sync.ps1` | 18 linked, 0 errors; AtrCalc.cs linked; AtrSizingEngine.cs absent | PASS |

**Result: ALL 7 SCANS ZERO ✓**

---

## Check 6: NT8 API Validity

Plan Section 2 "ARCHITECT NOTE" required engineer to verify NT8 API calls at implementation time.

| API Used | Verification | Evidence |
|----------|-------------|---------|
| `ChartControl.BarsArray` property | Build: 0 errors with this call at `AtrCalc.cs` line 73 | SCAN-05 = 0 errors (T1, T2, T3) |
| `cc.BarsArray[0].Bars` accessor | Build: 0 errors at `AtrCalc.cs` line 75 | SCAN-05 = 0 errors |
| `bars.GetHigh(barsAgo)` / `GetLow` / `GetClose` | Build: 0 errors at `AtrCalc.cs` lines 84-86 | SCAN-05 = 0 errors |

Zero build errors with all NT8 API calls confirms API is valid for AddOn context.

**Result: NT8 API VALID ✓**

---

## Check 7: Jane Street DNA -- Full Rule Check

| Rule | Check | Source Evidence | Status |
|------|-------|----------------|--------|
| JS-001: no throw in OnTimerTick / Compute / gate chain | AtrCalc both overloads: try/catch → return 0.0; OnTimerTick: no throw, no try/catch (AtrCalc is JS-001 safe) | `AtrCalc.cs` lines 28-51, 69-93; `TradeCopierPanel.cs` lines 3245-3257 | PASS |
| JS-002: no null return where value expected | All new methods return `double` or `void` | `AtrCalc.cs` returns `double`; `SetChartControl` / `Detach` / `OnTimerTick` return `void` | PASS |
| JS-003: no magic string for discriminated state | No magic string mode discrimination anywhere in B173 changes | Source review | PASS |
| JS-008: SolidColorBrush Freeze | No brush creation in B173 changes; `SetResourceReference` used (correct NT8 pattern) | `TradeCopierPanel.cs` lines 3184, 3191, 3203 | PASS |
| JS-009: no Dictionary for shared collection | No Dictionary introduced in B173 | Source review | PASS |
| JS-010: no public constructor on singleton/static | `AtrCalc` is `internal static class` -- no constructor | `AtrCalc.cs` line 14 | PASS |
| JS-021: no lock() | SCAN-01 = 0 actual lock statements across all files | ticket-3-verification.md SCAN-01 | PASS |
| JS-023: volatile only bool/int | `_hasData volatile bool` retained; `_chartControl` non-volatile (UI thread); `_lastAtr` non-volatile | `TradeCopierPanel.cs` lines 3165, 3167, 3168 | PASS |
| NT8-003: no volatile double field | SCAN-02 = 0 field declarations | ticket-3-verification.md SCAN-02 | PASS |
| NT8: no async/await in lifecycle | No async/await in WireNewPanel, OnTimerTick, SetChartControl, or any lifecycle method | Source review | PASS |
| NT8: no Account.All in constructor | Not relevant to this block | N/A | PASS |
| NT8: no sealed TradeCopierWindow | TradeCopierWindow not modified | Not in scope | PASS |
| NT8: no FontFamily override | Not present in any B173 change | SCAN-03 + source review | PASS |
| NT8: no hardcoded #RRGGBB hex | Not present in any B173 change | Source review | PASS |
| NT8: no CreateOrder without PTT- prefix | No CreateOrder in B173 | Not in scope | PASS |
| NT8: no DateTime.Now | Not used in any B173 change | Source review | PASS |
| CYC <= 8 for all methods | All methods: AtrCalc.Compute (pure)=5, Compute(NT8)=4-5, SetChartControl=2, Detach=2, OnTimerTick=3, SetAtrChartControl=1 | CYC summary plan Section 9; source review | PASS |

**Result: ALL JANE STREET DNA RULES PASS ✓**

---

## Check 8: Threading Model Coherence

All AtrWidget operations confirmed UI-thread-only:

| Operation | Thread | Mechanism | Status |
|-----------|--------|-----------|--------|
| `WireNewPanel` → `panel.SetAtrChartControl(cc)` | UI thread | Called inside `chart.Dispatcher.InvokeAsync(...)` | PASS |
| `AtrWidget.SetChartControl(cc)` | UI thread | Called from `SetAtrChartControl` | PASS |
| `DispatcherTimer.Tick` → `OnTimerTick` | UI thread | DispatcherTimer always fires on dispatcher thread | PASS |
| `AtrCalc.Compute(_chartControl, 14)` | UI thread | Called from `OnTimerTick` | PASS |
| `_chartControl` field write | UI thread | `SetChartControl` → `_chartControl = cc` | PASS |
| `_chartControl` field read | UI thread | `OnTimerTick` → `if (_chartControl == null)` | PASS |

No cross-thread state sharing. No Dispatcher.InvokeAsync inside AtrCalc. `_chartControl` is
single-threaded; no volatile needed. `_hasData volatile bool` is harmless and documents latch intent.

**Result: THREADING MODEL COHERENT ✓**

---

## Violations Found

**None.**

No Jane Street DNA violations. No NT8 constraint violations. No missing wiring. No spec requirements
missed. No cross-file coherence gaps. All 7 scans zero at terminal state.

---

## Section K -- Deferred Work

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B173-01 | SIM GATE: Director must verify AtrWidget displays live ATR data in NinjaTrader after B173 fix. Expected: widget transitions from blank "-- pts" to "N pts" (computed as `Math.Ceiling(ATR * fraction)` cast to int) within one timer tick after chart opens with bars loaded. Root cause (AtrSizingEngine instantiated outside NinjaScript pipeline) is eliminated. Close upon Director SIM confirmation. Supersedes DW-B172-01. | P1 | B5/SIM | OPEN |
| DW-B172-01 | SIM GATE: AtrWidget live ATR display (B172 version) | P1 | -- | SUPERSEDED BY DW-B173-01 |
| DW-B170-SIM | SIM GATE: Director drags target on leader -- confirm follower stop preserved after drag (B171 fix) | P0 | B5/SIM | OPEN (PENDING SIM GATE) |
| DW-B169-K1 | SIM GATE: 3-stop simultaneous drag -- 0 missing follower stops | P0 | B5/SIM | OPEN (partially verified; Sim102 blocked by B167-DEFER-02) |
| DW-B169-K2 | SIM GATE: single-stop drag regression | P0 | B5/SIM | OPEN |
| DW-B168-K1 | Superseded by DW-B169-K1/K2 | P0 | -- | OPEN (superseded) |
| B167-DEFER-02 | fo=NULL: follower ATM brackets never placed after leader fill. Dedicated block required. | P1 | future | OPEN |
| B167-DEFER-01 | ECA-1 double-position race. Monitor at SIM. | P2 | future | OPEN |
| B167-DEFER-03 | Combined B161-B166 SIM gate pending | P1 | future | OPEN |
| B167-DEFER-04 | B167 SIM gate pending | P1 | future | OPEN |
| DW-B159-K2 | 14 xUnit tests | P2 | future | OPEN |
| DW-B159-K3 | Attempt cap hardcoded at 3 | P2 | future | OPEN |
| DW-B159-K4 | intendedBePrice sourcing | P2 | future | OPEN |
| DW-B159-K5 | Stale line comment | P3 | future | OPEN |

---

## Summary

**All checks pass. No violations. Deferred backlog written.**

- B173 fixes the root cause of DW-B172-01: `AtrSizingEngine` was instantiated via `new` outside
  the NinjaScript pipeline, giving it no `BarsArray`. Replaced by `AtrCalc` — a pure static helper
  that reads bar data directly from the already-live `ChartControl` obtained in `WireNewPanel`.
- AtrCalc.cs is correctly wired end-to-end through SetAtrChartControl → SetChartControl → OnTimerTick.
- All 7 scans zero at T3 terminal state (Layer 3 independent verification).
- 2 xUnit tests pass (pure arithmetic, no NT8 runtime dependency).
- ptt-sync: 18 linked, 0 errors. AtrCalc.cs linked. AtrSizingEngine.cs absent.
- One deferred item created: DW-B173-01 (SIM GATE — requires Director NinjaTrader verification).

---

## FINAL_PASS

*Review written: B173 Phase 5. ptt-plan-reviewer.*
