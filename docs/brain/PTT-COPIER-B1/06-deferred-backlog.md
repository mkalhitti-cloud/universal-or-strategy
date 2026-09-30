# PTT-COPIER — Deferred Work Backlog (through B1)

**Block:** PTT-COPIER-B1
**Authored:** 2026-07-06
**Canonical source:** `docs/brain/PTT-COPIER-B1/06-deferred-backlog.md`
**Status:** B1 FINAL_PASS — items below are deferred to B2+

---

## Ledger

| ID | Priority | Status | Block Deferred | Item | File | Notes |
|----|----------|--------|---------------|------|------|-------|
| DW-B1-01 | P1 | OPEN | B2 | `PassesDailyCapCheck` real implementation | `CopyEngine.cs` | Stub returning `true` in B1. Needs real P&L floor check: sum net realized P&L for all follower accounts on given instrument today; block copy if floor breached. |
| DW-B1-02 | P2 | OPEN | B2 | Per-rule ON/OFF toggle wiring | `TradeCopierWindow.cs` | `OnRuleToggle` updates button text only; does not call `CopyEngine.SetEnabled`. B2 should implement `SetRuleEnabled(instrument, bool)` on the engine. |
| DW-B1-03 | P2 | OPEN | B2 | `+ Add Rule` button | `TradeCopierWindow.cs` | `IsEnabled = false` in B1. Dynamic rule row creation for multi-rule support. |
| DW-B1-04 | P2 | OPEN | B2 | Follower multi-select | `TradeCopierWindow.cs` | B1 uses single ComboBox. B2 to replace with checklist (`ListBox` + `SelectionMode.Extended`). |
| DW-B1-05 | P3 | OPEN | B2 | xUnit test file for `CopyEngine.cs` | `CopyEngineTests.cs` (new) | 17 `[Fact]` test methods from plan §11 not submitted with T1. Block 2 task. |

---

## Resolved Items

*(none at B1 close — this is the initial ledger)*

---

## Forward: Known B2 Scope

B2 is the first additive block on top of B1. It should address all OPEN items above
(DW-B1-01 through DW-B1-05) and document any new deferred items in
`docs/brain/PTT-COPIER-B2/06-deferred-backlog.md`.

The ptt-architect Phase 1 for B2 MUST read this file in Thought 1 before planning.
