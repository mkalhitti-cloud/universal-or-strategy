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

### Prior B1 Item Status After B2

| ID | Item | Status After B2 |
|----|------|-----------------|
| DW-B1-01 | PassesDailyCapCheck stub | Still OPEN -- addressed B3 |
| DW-B1-02 | Per-rule ON/OFF | Still OPEN -- addressed B3 |
| DW-B1-03 | + Add Rule button | Still OPEN -- addressed B3 |
| DW-B1-04 | Follower multi-select | Still OPEN -- carried to B5 |
| DW-B1-05 | xUnit test file | Still OPEN -- addressed B3 |

---

## PTT-COPIER-B3 -- Deferred Items

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B3-01 | Break Even button -- move stop to entry+N ticks on both surfaces | P1 | B4 | CLOSED (B4) |
| DW-B3-02 | Follower multi-select checklist ComboBox (carried from B1) | P1 | B5 | CLOSED (B5) |
| DW-B3-03 | xUnit tests for BreakEven() | P1 | B5 | CLOSED (B5) |

### Prior Item Status After B3

| ID | Item | Status After B3 |
|----|------|-----------------|
| DW-B1-04 | Follower multi-select | OPEN -- carried to B5 |
| DW-B2-01 | CopyEngineTests StatusUpdate unsubscribe hygiene | OPEN -- P3, carried to B5 |

---

## PTT-COPIER-B4 -- Deferred Items

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B5-01 | Follower multi-select ComboBox (both Panel and Window surfaces) | P2 | B5 | CLOSED (B5) |
| DW-B5-02 | BE keyboard shortcut in TradeCopierWindow (Shift+B per rule row) | P2 | B5 | CLOSED (B5) |
| DW-B5-03 | Rule persistence across sessions (serialize/deserialize CopyRule list) | P3 | future | CLOSED (B6) |
| DW-B5-04 | Spec HTML update for B3+B4 changes | P3 | future | CLOSED (B6) |

### Prior Item Status After B4

| ID | Item | Status After B4 |
|----|------|-----------------|
| DW-B3-01 | Break Even button | CLOSED -- implemented B4 |
| DW-B3-02 | Follower multi-select | CLOSED (B5) |
| DW-B3-03 | xUnit tests for BreakEven | CLOSED (B5) |
| DW-B2-01 | CopyEngineTests StatusUpdate unsubscribe hygiene | CLOSED (B5) |

---

## PTT-COPIER-B5 -- Deferred Items

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B5-03 | Rule persistence across sessions (serialize/deserialize CopyRule list on NT shutdown/startup) | P3 | B6/future | CLOSED (B6) |
| DW-B5-04 | Spec HTML update for B3/B4/B5 changes (002-trade-copier-spec.html -- BE, Shift+B, ListBox, buffer documented) | P3 | B6/future | CLOSED (B6) |

### Prior Item Status After B5

| ID | Item | Status After B5 |
|----|------|-----------------|
| DW-B5-01 | Follower ListBox multi-select (Panel + Window) | CLOSED -- ListBox+ScrollViewer on both surfaces |
| DW-B5-02 | Shift+B in TradeCopierWindow | CLOSED -- KeyBinding + OnWindowBreakEven + MouseEnter tracking |
| DW-B3-03 | xUnit BreakEven tests | CLOSED -- 2 new [Fact] methods (total 19) |
| DW-B2-01 | StatusUpdate unsubscribe hygiene | CLOSED -- IDisposable + Dispose() on CopyEngineTests |

---

## Open Items Summary (as of B5 -- feeding into B6)

| ID | Item | Priority | Target Block |
|----|------|----------|--------------|
| DW-B5-03 | Rule persistence across sessions | P3 | B6/future |
| DW-B5-04 | Spec HTML update for B3+B4+B5 changes | P3 | B6/future |

---

## PTT-COPIER-B6 -- Deferred Items

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B5-03 | Rule persistence across sessions (SaveRules/LoadRules via XmlSerializer, copy_rules.xml) | P3 | B6 | CLOSED (B6) |
| DW-B5-04 | Spec HTML update for B3/B4/B5 changes (BE button, Shift+B, ListBox, buffer, B6 persistence) | P3 | B6 | CLOSED (B6) |
| DW-B6-01 | No new deferred items introduced in B6. Deferred backlog is empty after B6. | -- | -- | N/A |

### Prior Item Status After B6

| ID | Item | Status After B6 |
|----|------|-----------------|
| DW-B5-03 | Rule persistence across sessions | CLOSED -- SaveRules/LoadRules implemented (CopyEngine.cs lines 458-604), lifecycle hooks in TradeCopierWindow.cs |
| DW-B5-04 | Spec HTML update for B3/B4/B5/B6 changes | CLOSED -- 5 sections added to 002-trade-copier-spec.html; JSON->XML correction applied |

### Open Items Summary (as of B6)

**NONE. All deferred items are CLOSED. Backlog is empty.**
