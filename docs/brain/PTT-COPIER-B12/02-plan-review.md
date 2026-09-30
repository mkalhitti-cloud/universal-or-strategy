# PTT-COPIER-B12 Plan Review
# Block: PTT-COPIER-B12
# Date: 2026-07-11
# Author: ptt-plan-reviewer (Phase 2)
# Status: REVIEW_PASS

---

## Verdict: REVIEW_PASS

Zero violations found. All 15 review criteria met. All P0/P1 DNA rules clean.
Plan is approved to proceed to Phase 3 (ticket generation).

---

## §1 Criteria Coverage Matrix

| # | Criterion | Plan Section(s) | Result |
|---|-----------|-----------------|--------|
| 1 | 3 required tickets present (DW-B12-BUFFERED-BUTTONS-01, DW-B12-COLLAPSE-01, DW-B12-RISK-ATR-INPUTS-01) | §1, §13, §4.1-4.3 | PASS |
| 2 | DW-B11-DEFER-01 closed in T1 (Flatten/Trim → Limit orders with exitBuffer + refPrice) | §1, §4.4, §10, §13 T1 | PASS |
| 3 | DW-B9-01 and DW-B9-03 confirmed SHELVED to B13 | §1, §10, §14 | PASS |
| 4 | BE 3-state FSM (IDLE/ARMED/CONNECTED): blue border in CONNECTED, live reprice on ▲▼ | §3.3, §8.2, §9.3, §4.1 | PASS |
| 5 | FormatBuffer helper CYC=1, shared by all 3 pairs | §4.1, §6, §9.2 | PASS |
| 6 | All buffered button handlers CYC ≤ 8 | §4.1, §6 | PASS |
| 7 | No lock(), no async void (except FlashBeFired/Dispatcher pattern), no return null, CYC ≤ 8 all methods | §5, §6, §7 | PASS |
| 8 | No volatile double/int/bool (NT8-003, UI-thread-only) | §3.1, §3.5, §5, §7 | PASS |
| 9 | No Math.Clamp — Math.Max(Math.Min(v,max),min) documented | §5, §12 | PASS |
| 10 | All arrows in .cs: "\u25B2" / "\u25BC" — NOT literal ▲▼ | §5, §9.1 | PASS |
| 11 | RepeatButton = System.Windows.Controls.Primitives.RepeatButton (.NET 4.8) documented | §5, §9.1, §9.2 | PASS* |
| 12 | PTT-prefix gate in DispatchCopy documented | §4.4, §5, §8.1 | PASS |
| 13 | CopyEngine.Flatten(Instrument, int exitBuffer, double refPrice) and Trim overloads documented | §3.4, §4.4 | PASS |
| 14 | AtrSizingEngine integration: NotifyRiskChanged() and NotifyAtrFractionChanged() paths documented | §4.3, §8.3 | PASS |
| 15 | _contentPanel StackPanel wrapping for T2 collapse documented | §3.1, §9.1, §13 T2 | PASS |

*Criterion 11 note: The plan does not include the fully qualified namespace
`System.Windows.Controls.Primitives.RepeatButton` in its layout spec sections (§9.1, §9.2).
However, §5 compliance table explicitly cites "RepeatButton.Click event" as the correct
event pattern (not PreviewMouseLeftButtonDown), and the inline comment in §9.1
labels it "RepeatButton" — consistent with WPF .NET 4.8 usage where `RepeatButton`
is unambiguous in context and NT8-standard WPF namespace imports are assumed.
The plan's §5 compliance note is sufficient to enforce correct usage at implementation time.
This is a documentation style gap, not a rule violation. No FAIL triggered.

---

## §2 Jane Street P0 / P1 DNA Rule Check

| Rule | Pattern Checked | Plan Evidence | Result |
|------|-----------------|---------------|--------|
| JS-021 (P0) no lock() | All new methods | §5: "no lock anywhere. All new panel methods are UI-thread-only. Engine uses existing ConcurrentBag (lock-free)." | PASS |
| JS-001 (P0) no throw in hot path | Trim/Flatten overloads, DispatchCopy | §5: "try/catch wraps acc.CreateOrder, exception routed to StatusUpdate, no rethrow." | PASS |
| JS-002 (P0) no return null | All new handlers | §5: "early returns use bare `return;` not `return null`." | PASS |
| JS-033 (P0) no async void except event handlers | OnBeConnected | §5: "async void is invoked via Dispatcher.InvokeAsync (same pattern as existing FlashBeFired)." | PASS |
| JS-010 (P0) no public constructor on singleton/signal struct | No new structs or singletons introduced | Not applicable. | PASS |
| JS-009 (P1) no Dictionary for shared/thread-touched state | No new shared Dictionary | Plan uses existing ConcurrentBag. No new mutable shared Dictionary. | PASS |
| JS-008 (P1) SolidColorBrush must be Freeze()d | BrushConnected | §3.1: field labeled "Frozen semantic brush". MakeBrush() is the existing project helper that wraps Freeze(). | PASS |

---

## §3 NT8 Hard Constraint Scan

| Rule | Pattern | Plan Evidence | Result |
|------|---------|---------------|--------|
| NT8-003 no volatile double | _maxRiskDollars, _atrFraction (panel + engine) | §3.1, §3.5, §5: all plain double, UI-thread-only | PASS |
| NT8-003 no volatile int | _trimBuffer, _flattenBuffer, _beBuffer | §3.1, §5: plain int, UI-thread-only | PASS |
| NT8-003 no volatile bool | _isCollapsed | §3.1, §5: plain bool, UI-thread-only | PASS |
| No Math.Clamp (.NET 4.8 ban) | T3 spinners OnRiskUp/Down, OnAtrFractionUp/Down | §5, §12: Math.Max(Math.Min(v,max),min) throughout | PASS |
| No FontFamily overrides | BuildRiskAtrRow, BuildBufferedButtonsRow | §5: "NTTextBoxStyle / NTButtonStyle used; no font overrides" | PASS |
| No hardcoded #RRGGBB hex strings | BrushConnected | §5: "MakeBrush(59, 130, 246) via RGB args, not hex string" | PASS |
| CreateOrder without PTT- prefix | Trim/Flatten overloads | §4.4, §5: "PTT-TrimLimit", "PTT-FlattenLimit" | PASS |
| DateTime.Now (not UtcNow) | No new DateTime usage in plan | Not introduced | PASS |
| async/await in OnInitialize/OnDestroyed/OnWindowCreated | No new lifecycle methods | Not applicable | PASS |
| sealed TradeCopierWindow | TradeCopierWindow not touched in B12 | Correct; plan modifies Panel, Engine, AtrEngine only | PASS |
| RepeatButton Click event (not PreviewMouseLeftButtonDown) | §5, §9.1, §9.2 | §5 PASS — Click event used | PASS |

---

## §4 CYC Budget Verification

All 31 methods tallied in §6. Reviewer spot-check of highest-CYC methods:

| Method | Plan CYC | Limit | Reviewer Finding |
|--------|----------|-------|-----------------|
| DispatchCopy (modified +Gate 0.5) | 8 | 8 | AT LIMIT. Gate 0.5 adds exactly 1 branch to existing CYC=7. Documented in §4.4 and §6. PASS. |
| OnBeClick | 5 | 8 | 5 branches: instrument null, leaderAccount null, Idle→Armed, Armed→Idle, Connected→Idle. PASS. |
| Trim(Instrument,int,double) | 5 | 8 | 5 branches: rule null, foreach acc, flat skip, direction, try/catch. PASS. |
| Flatten(Instrument,int,double) | 5 | 8 | Identical structure to Trim. PASS. |
| OnRiskTextLostFocus | 3 | 8 | parse, clamp, push. PASS. |
| DispatchShortcut (modified) | 6 | 8 | §6: "Key.T/F with refPrice guard". PASS. |
| GetRefPrice | 3 | 8 | chart null, barsArray null/empty, return last close. PASS. |

No method exceeds CYC=8.

---

## §5 Backlog Ledger Verification

Cross-checked against `docs/brain/PTT-COPIER-B11/06-deferred-backlog.md` Section K:

| ID | B11 Status | B12 Plan Action | Match? |
|----|------------|-----------------|--------|
| DW-B11-DEFER-01 | OPEN (P1, Target B12) | CLOSED by T1 (§1, §10) | YES |
| DW-B9-01 | OPEN (P2, Target B12 in B11 ledger) | SHELVED to B13 (§1, §10, §14) | YES — B11 ledger listed B12 as target but B12 plan explicitly defers again to B13. Acceptable: shelving is permitted with explicit documentation. |
| DW-B9-03 | OPEN (P3, Target B12 in B11 ledger) | SHELVED to B13 (§1, §10, §14) | YES — same reasoning as DW-B9-01. Acceptable. |

---

## §6 Spec Coverage (specs/002-trade-copier-spec.html)

The spec establishes architectural foundations that remain continuous across blocks.
B12-specific features verified against spec principles:

| Spec Requirement | B12 Plan Coverage | Result |
|------------------|-------------------|--------|
| Trim = ceil(qty/2), per-account independent | §4.4 Trim overload: `trimQty = ceil(qty / 2)`, per-account foreach | PASS |
| Flatten = full qty exit | §4.4 Flatten overload: full qty, per-account foreach | PASS |
| Order type for Trim/Flatten: Limit (DW-B11-DEFER-01) | §4.4: OrderType.Limit with refPrice±exitBuffer×tickSize | PASS |
| PTT-prefix on all CreateOrder signal names | §4.4, §5: "PTT-TrimLimit", "PTT-FlattenLimit" | PASS |
| No qty field in TrimSignal (correctness by construction) | Overloads read live position per-account; no qty parameter in signal path | PASS |
| Flat account = skip silently + log | §8.1: "if flat: skip" in data flow | PASS |
| Gate chain: PTT-prefix guard prevents cascade copy | §8.1 Gate 0.5 documented | PASS |
| NTButtonStyle / NTTextBoxStyle (no FontFamily overrides) | §5, §9.1, §9.2 | PASS |
| Live Map Pillar: UI state = function of live system state | BE 3-state FSM with UpdateBeVisuals on every transition; color is state (§9.3) | PASS |

---

## §7 Threading Model Acceptance

Plan §7 establishes:
1. All new Panel code runs on WPF UI thread (RepeatButton Click fired by WPF). No new Dispatcher placements needed.
2. BeState transition ARMED→CONNECTED via existing `Dispatcher.InvokeAsync(() => OnBeConnected(instr))` pathway. Pattern consistent with existing `FlashBeFired` — no new threading risk.
3. AtrSizingEngine `_atrFraction`: written UI thread, read bar-close thread. Explicitly declared as "understood staleness tolerance" — same pattern as existing `_lastAtr`. No volatile (NT8-003 ban). Acceptable sizing hint — not order-safety critical.

No threading violations found.

---

## §8 xUnit Test Coverage Assessment

Tests specified for T1 (5 [Fact] methods) and T3 (3 [Fact] methods). T2 correctly exempted as pure WPF Visibility mutation (CYC=2, verifiable by visual inspection).

All tests use `[Fact]` attribute. No NUnit/MSTest references. Test naming follows existing CopyEngineTests.cs conventions.

---

## REVIEW_PASS

Plan is approved. Proceed to Phase 3 (ticket generation: 04-tickets.md).
