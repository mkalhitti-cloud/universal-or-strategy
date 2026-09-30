# PTT Trade Copier -- Deferred Work Ledger
# Append-only. Each block adds one section. Never delete rows -- update Status only.

---

## PTT-COPIER-B1 -- Deferred Items

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B1-01 | PassesDailyCapCheck -- real P&L floor implementation (was stub) | P0 | B3 | CLOSED (B3) |
| DW-B1-02 | Per-rule ON/OFF toggle | P1 | B3 | CLOSED (B3) |
| DW-B1-03 | + Add Rule button (multi-rule support) | P1 | B3 | CLOSED (B3) |
| DW-B1-04 | Follower multi-select checklist ComboBox | P1 | B3 | CLOSED (B5) |
| DW-B1-05 | xUnit test file for CopyEngine | P1 | B3 | CLOSED (B3) |

---

## PTT-COPIER-B2 -- Deferred Items

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B2-01 | StatusUpdate handlers in CopyEngineTests.cs never unsubscribed | P3 | B5 | CLOSED (B5) |

---

## PTT-COPIER-B3 -- Deferred Items

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B3-01 | Break Even button -- move stop to entry+N ticks on both surfaces | P1 | B4 | CLOSED (B4) |
| DW-B3-02 | Follower multi-select checklist ComboBox (carried from B1) | P1 | B5 | CLOSED (B5) |
| DW-B3-03 | xUnit tests for BreakEven() | P1 | B5 | CLOSED (B5) |

---

## PTT-COPIER-B4 -- Deferred Items

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B5-01 | Follower multi-select ComboBox (both Panel and Window surfaces) | P2 | B5 | CLOSED (B5) |
| DW-B5-02 | BE keyboard shortcut in TradeCopierWindow (Shift+B per rule row) | P2 | B5 | CLOSED (B5) |
| DW-B5-03 | Rule persistence across sessions (serialize/deserialize CopyRule list) | P3 | B6 | CLOSED (B6) |
| DW-B5-04 | Spec HTML update for B3+B4 changes | P3 | B6 | CLOSED (B6) |

---

## PTT-COPIER-B5 -- Deferred Items

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B5-03 | Rule persistence across sessions (SaveRules/LoadRules via XmlSerializer, copy_rules.xml) | P3 | B6 | CLOSED (B6) |
| DW-B5-04 | Spec HTML update for B3/B4/B5 changes (BE button, Shift+B, ListBox, buffer, B6 persistence) | P3 | B6 | CLOSED (B6) |

---

## PTT-COPIER-B6 -- Deferred Items

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B5-03 | Rule persistence across sessions (SaveRules/LoadRules via XmlSerializer, copy_rules.xml) | P3 | B6 | CLOSED (B6) |
| DW-B5-04 | Spec HTML update for B3/B4/B5/B6 changes | P3 | B6 | CLOSED (B6) |
| DW-B6-01 | No new deferred items introduced in B6. Deferred backlog was empty after B6. | -- | -- | N/A |

---

## PTT-COPIER-B7 -- Deferred Items

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B7-01 | Per-account qty multiplier (1x/2x/3x) -- CopyRule DTO + serialization + UI TextBox per follower row | P2 | B8 | OPEN |
| DW-B7-02 | ATR dynamic sizing engine (AtrSizingEngine.cs, MarketData subscription, rolling ATR calculation) | P1 | B8/B9 | OPEN |
| DW-B7-03 | FollowerAtmMode behavioral wiring -- SendCopy switch on Inherit/Market/Named + Window UI dropdown | P2 | B8 | OPEN |

### Prior Item Status After B7

| ID | Item | Status After B7 |
|----|------|-----------------|
| DW-B5-03 | Rule persistence across sessions | CLOSED (B6) -- no regression in B7 |
| DW-B5-04 | Spec HTML update for B3/B4/B5/B6 changes | CLOSED (B6) -- no regression in B7 |
| DW-B6-01 | (marker row -- no new items in B6) | N/A |

### Open Items Summary (as of B7 -- feeding into B8)

| ID | Item | Priority | Target Block |
|----|------|----------|--------------|
| DW-B7-01 | Per-account qty multiplier (1x/2x/3x) | P2 | B8 |
| DW-B7-02 | ATR dynamic sizing engine | P1 | B8/B9 |
| DW-B7-03 | FollowerAtmMode behavioral wiring + UI dropdown | P2 | B8 |

---

## PTT-COPIER-B8 -- Deferred Items

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B7-01 | Per-account qty multiplier (1x/2x/3x) | P2 | B8 | CLOSED (B8) |
| DW-B7-03 | FollowerAtmMode behavioral wiring -- SendCopy switch + Window/Panel UI dropdowns | P2 | B8 | CLOSED (B8) |
| DW-B8-01 | JS-002 cleanup: replace `return null` with `Option<T>` in CopyEngine.cs -- 4 query helpers (FindRule:747/753, FindPosition:806, FindWorkingEntry:437) | P2 | B9 | OPEN |
| DW-B8-02 | Hook path: pre_task_rules_gate.py scans universal-or-strategy-director/src/ -- update to detect PropTraderTools repo when running in trade copier context | P2 | B9 | OPEN |
| DW-B8-03 | Named ATM inline template name input -- TextBox that appears on "Named" ComboBox selection | P2 | B9 | OPEN |

### Notes
- DW-B8-01: NT8 boundary `return null` in TradeCopierAddOn.cs (FindVisualChild) and TradeCopierWindow.cs (FindInstrument) are **exempt** -- WPF visual-tree walkers and NT8 API wrappers at platform boundary are idiomatic null returns per JS-015 (parse at boundaries). No ticket needed.
- DW-B8-02: Gate correctly blocked on V12 violations (93 in V12 repo src/) but those are pre-existing V12 technical debt unrelated to trade copier work. Gate is still useful as a new-violation detector per session.

### Open Items Summary (as of B8 -- feeding into B9)

| ID | Item | Priority | Target Block |
|----|------|----------|--------------|
| DW-B7-02 | ATR dynamic sizing engine (MarketData/AddOnBase incompatibility -- needs embedded Indicator host) | P1 | B9 |
| DW-B8-04 | Click trader (chart-click entry -- ChartControl.MouseDown + armed state overlay) | P1 | B9 |
| DW-B8-05 | ATR box visualization on chart (depends on DW-B7-02 engine) | P2 | B9 |
| DW-B8-06 | Full mirror mode / Mode 2 (OnOrderUpdate relay for modifications) | P2 | B9 |
| DW-B8-01 | JS-002 return null cleanup in CopyEngine.cs | P2 | B9 |
| DW-B8-02 | Gate hook path fix for PropTraderTools repo detection | P2 | B9 |

---

## PTT-COPIER-B9 -- Deferred Items

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B9-GAP-001d | **Sim101 verification**: does `acc.Change(StopPrice)` on a trailing stop order preserve or kill the trail? Document result. PREREQUISITE for GAP-001a/b/c. | P1 | B10 | OPEN |
| DW-B9-GAP-001a | **Mode 2 trailing stop relay policy**: choose Option A (freeze) / B (skip trailing stops) / C (re-arm) in `HandleBracketChange`. Recommendation: Option B. Full spec: docs/brain/PTT-COPIER-B9/GAP-001-trailing-stop-order-type-preservation.md | P1 | B10 | OPEN |
| DW-B9-GAP-001b | **BE button trailing stop handling**: implement cancel+replace instead of `acc.Change()` when target order is a trailing stop. Full spec: GAP-001 doc. | P1 | B10 | OPEN |
| DW-B9-GAP-001c | **Tighten Stop button (one-shot)**: new button `[Tighten N]` on Panel + Window. Move all stops to currentPrice - N ticks. Configurable `TightenTicks` per rule, default 4 ticks (MES = 1 point). XML-persisted. Same trailing-stop caveat as GAP-001a applies. Full spec: GAP-001 doc. | P2 | B10 | OPEN |
| DW-B9-01 | ATR box visualization on chart (draw stop/target zone around click-placed order) | P2 | B10 | OPEN |
| DW-B9-02 | IMPL-NOTE-1: document verified chart attachment API for AtrSizingEngine in B9 ticket completion report | P1 | B9-T1 | OPEN |
| DW-B9-03 | Bid+1 / Ask-1 offset for click trader (auto-adjust limit price to inside market) | P3 | B10 | OPEN |

| DW-B10-GAP-002a | **Pending BE price watcher**: `ArmPendingBe()` + `OnPendingBePriceTick()` + `Instrument.MarketData` subscription. BE button becomes toggle: immediate fire if price already at level, else arm (amber `[BE ●]`) until level reached, click again to disarm. Full spec: docs/brain/PTT-COPIER-B9/GAP-002-pending-be-and-trailing-stop-compatibility.md | P1 | B10 | OPEN |
| DW-B10-GAP-002b | **`MoveStopToBreakEven` trailing stop fix**: when stop order has `TrailPrice > 0` and is already at/above BE level → skip. When below BE level → cancel + replace with fixed StopMarket at BE price (not `acc.Change()`). Full spec: GAP-002 doc. | P1 | B10 (after GAP-001d) | OPEN |
| DW-B10-GAP-002c | **Sim101 test for pending BE**: verify `NinjaTrader.Data.Instrument.GetInstrument(name).MarketData.MarketDataUpdate` fires correctly in AddOn context. Prerequisite for DW-B10-GAP-002a. | P1 | B10 | OPEN |

### Gap Documents
- Trailing stop / `acc.Change()` interaction + Tighten Stop spec:
  `docs/brain/PTT-COPIER-B9/GAP-001-trailing-stop-order-type-preservation.md`
- Pending BE state machine + trailing stop compatibility full spec:
  `docs/brain/PTT-COPIER-B9/GAP-002-pending-be-and-trailing-stop-compatibility.md`

### Open Items Summary (as of B9 -- feeding into B10)

| ID | Item | Priority | Target Block |
|----|------|----------|--------------|
| DW-B9-GAP-001d | **[FIRST]** Sim101 verify: `acc.Change(StopPrice)` on trailing stop -- does trail survive? | P1 | B10 |
| DW-B10-GAP-002c | **[FIRST]** Sim101 verify: `Instrument.MarketData.MarketDataUpdate` fires in AddOn context? | P1 | B10 |
| DW-B9-GAP-001a | Mode 2: trailing stop relay policy (recommend: skip trailing stops) | P1 | B10 |
| DW-B9-GAP-001b | BE button: cancel+replace path for trailing stop orders | P1 | B10 |
| DW-B10-GAP-002a | Pending BE price watcher + toggle UI | P1 | B10 |
| DW-B10-GAP-002b | `MoveStopToBreakEven` trailing stop skip/replace logic | P1 | B10 |
| DW-B9-GAP-001c | Tighten Stop one-shot button (4 ticks default, configurable) | P2 | B10 |
| DW-B9-01 | ATR box visualization on chart | P2 | B10 |
| DW-B9-03 | Click trader bid+1/ask-1 price offset | P3 | B10 |
