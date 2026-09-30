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
| DW-B7-01 | Per-account qty multiplier (1x/2x/3x) -- CopyRule DTO + serialization + UI TextBox per follower row | P2 | B8 | **CLOSED (B8)** |
| DW-B7-02 | ATR dynamic sizing engine (AtrSizingEngine.cs, MarketData subscription, rolling ATR calculation) | P1 | B8/B9 | **OPEN -- carries to B9** |
| DW-B7-03 | FollowerAtmMode behavioral wiring -- SendCopy switch on Inherit/Market/Named + Window UI dropdown | P2 | B8 | **CLOSED (B8)** |

### Prior Item Status After B7

| ID | Item | Status After B7 |
|----|------|-----------------|
| DW-B5-03 | Rule persistence across sessions | CLOSED (B6) -- no regression in B7 |
| DW-B5-04 | Spec HTML update for B3/B4/B5/B6 changes | CLOSED (B6) -- no regression in B7 |
| DW-B6-01 | (marker row -- no new items in B6) | N/A |

---

## PTT-COPIER-B8 -- Deferred Items

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B7-02 | ATR dynamic sizing engine (MarketData/AddOnBase incompatibility -- needs embedded Indicator host) | P1 | B9 | **OPEN** |
| DW-B8-01 | JS-002 cleanup: replace `return null` with `Option<T>` in CopyEngine.cs query helpers (FindRule:671/677, FindPosition:802, FindFollowerBracketOrder:437) | P2 | B9 | **OPEN** |
| DW-B8-02 | Gate hook path: `pre_task_rules_gate.py` scans universal-or-strategy-director/src/ -- update to detect PropTraderTools repo context | P2 | B9 | **OPEN** |
| DW-B8-03 | ATR dynamic sizing engine (carry from DW-B7-02) -- design AtrSizingEngine.cs as detached Indicator managed by AddOn | P1 | B9 | **OPEN** |
| DW-B8-04 | Click trader / chart-click entry -- ChartControl.MouseDown + armed state overlay (SPEC-B8-04) | P1 | B9 | **OPEN** |
| DW-B8-05 | ATR box visualization on chart -- depends on DW-B8-03 AtrSizingEngine (SPEC-B8-05) | P2 | B9 | **OPEN** |
| DW-B8-06 | Full mirror mode / Mode 2 -- OnOrderUpdate relay for order modifications (SPEC-B8-06) | P2 | B9 | **OPEN** |

### Prior B7 Item Status After B8

| ID | Item | Status After B8 |
|----|------|-----------------|
| DW-B7-01 | Per-account qty multiplier | CLOSED (B8) -- fully implemented: data model, engine, Panel UI, persistence, 6 tests |
| DW-B7-02 | ATR dynamic sizing engine | OPEN -- MarketData/AddOnBase incompatibility; carries to B9 |
| DW-B7-03 | FollowerAtmMode behavioral wiring | CLOSED (B8) -- fully implemented: sealed hierarchy, SendCopy dispatch, Panel ATM ComboBox, Window ATM ComboBox (static + dynamic rows), persistence, 6 tests |

### Non-Blocking Advisories (B8 -- not deferred items)

| Advisory | Description | Location |
|----------|-------------|----------|
| DEFECT-T2-002 | `ParseAtmModeNameWindow` helper absent; `CopyEngine.ParseAtmModeName` called directly. Functional equivalent. | `TradeCopierWindow.cs:525` |
| Comment drift | `OnRowApply` comment at line 521 references "3-element tag" for static rows -- stale after Cycle 2 fix. | `TradeCopierWindow.cs:521` |
| Non-ASCII comment | `§` (U+00A7) in comment at `CopyEngine.cs:866` -- not in executable code. | `CopyEngine.cs:866` |

### Open Items Summary (as of B8 -- feeding into B9)

| ID | Item | Priority | Target Block |
|----|------|----------|--------------|
| DW-B7-02 / DW-B8-03 | ATR dynamic sizing engine (AtrSizingEngine.cs as embedded Indicator) | P1 | B9 |
| DW-B8-04 | Click trader (ChartControl.MouseDown + armed overlay) | P1 | B9 |
| DW-B8-01 | JS-002 return null cleanup in CopyEngine.cs | P2 | B9 |
| DW-B8-02 | Gate hook path fix for PropTraderTools repo detection | P2 | B9 |
| DW-B8-05 | ATR box visualization (depends on DW-B8-03) | P2 | B9 |
| DW-B8-06 | Full mirror mode / Mode 2 (OnOrderUpdate modification relay) | P2 | B9 |
