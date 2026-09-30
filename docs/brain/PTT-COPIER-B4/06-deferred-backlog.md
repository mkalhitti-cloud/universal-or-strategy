# PTT Trade Copier -- Deferred Work Ledger
# Append-only. Each block adds one section. Never delete rows -- update Status only.

---

## PTT-COPIER-B1 -- Deferred Items

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B1-01 | PassesDailyCapCheck -- real P&L floor implementation (was stub) | P0 | B3 | CLOSED (B3) |
| DW-B1-02 | Per-rule ON/OFF toggle | P1 | B3 | CLOSED (B3) |
| DW-B1-03 | + Add Rule button (multi-rule support) | P1 | B3 | CLOSED (B3) |
| DW-B1-04 | Follower multi-select checklist ComboBox | P1 | B3 | OPEN -> B5 |
| DW-B1-05 | xUnit test file for CopyEngine | P1 | B3 | CLOSED (B3) |

---

## PTT-COPIER-B2 -- Deferred Items

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B2-01 | StatusUpdate handlers in CopyEngineTests.cs never unsubscribed | P3 | B5 | OPEN |

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
| DW-B3-02 | Follower multi-select checklist ComboBox (carried from B1) | P1 | B5 | OPEN |
| DW-B3-03 | xUnit tests for BreakEven() | P1 | B5 | OPEN |

### Prior Item Status After B3

| ID | Item | Status After B3 |
|----|------|-----------------|
| DW-B1-04 | Follower multi-select | OPEN -- carried to B5 |
| DW-B2-01 | CopyEngineTests StatusUpdate unsubscribe hygiene | OPEN -- P3, carried to B5 |

---

## PTT-COPIER-B4 -- Deferred Items

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B5-01 | Follower multi-select ComboBox (both Panel and Window surfaces -- currently single-follower only) | P2 | B5 | OPEN |
| DW-B5-02 | BE keyboard shortcut in TradeCopierWindow (Shift+B per rule row, not just Panel) | P2 | B5 | OPEN |
| DW-B5-03 | Rule persistence across sessions (serialize/deserialize CopyRule list on NT shutdown/startup) | P3 | future | OPEN |
| DW-B5-04 | Spec HTML update for B3+B4 changes (002-trade-copier-spec.html -- BE, Shift+B, buffer documented) | P3 | future | OPEN |

### Prior Item Status After B4

| ID | Item | Status After B4 |
|----|------|-----------------|
| DW-B3-01 | Break Even button | CLOSED -- implemented B4 |
| DW-B3-02 | Follower multi-select | OPEN -- carried as DW-B5-01 |
| DW-B3-03 | xUnit tests for BreakEven | OPEN -- not addressed B4, merged into B5 backlog |
| DW-B2-01 | CopyEngineTests StatusUpdate unsubscribe hygiene | OPEN -- P3, merged into future backlog |

---

## Open Items Summary (as of B4 -- feeding into B5)

| ID | Item | Priority | Target Block |
|----|------|----------|--------------|
| DW-B5-01 | Follower multi-select ComboBox (both surfaces) | P2 | B5 |
| DW-B5-02 | BE keyboard shortcut in TradeCopierWindow per rule row | P2 | B5 |
| DW-B5-03 | Rule persistence across sessions | P3 | future |
| DW-B5-04 | Spec HTML update for B3+B4 | P3 | future |
