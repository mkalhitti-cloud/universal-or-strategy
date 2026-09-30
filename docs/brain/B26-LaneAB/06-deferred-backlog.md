# PTT Deferred Work Backlog

This file accumulates deferred work items across all blocks.
Each block appends an entry with:
- closed items (resolved this block)
- open/deferred items (carried forward)
- [Fact] delta for the block

---

## B26-LaneAB (B26 Lane A + Lane B)

**Block closed**: 2026-07-17
**Reviewer**: ptt-plan-reviewer (Phase 5)
**Verdict**: FINAL_PASS

### Closed This Block

| ID | Defect | Resolution | File(s) | Ticket |
|----|--------|------------|---------|--------|
| DW-B26-01 | Wrong BreakEven overload in `OnTrailBeAccountUpdate` — 2-arg routed through `AllAccounts`→`FindRule`→`yield break` when no copy rule. Stop never moved. | CLOSED — Change 2: `BreakEven(instr, newBuffer)` → `BreakEven(acc, instr, newBuffer)` at `CopyEngine.cs:1422`. | `CopyEngine.cs` | B26-AB-T1 |
| DW-B26-02 | `PendingBeFired` broadcast carried no account identity — both panels subscribed to same instrument both flipped to BE Live simultaneously. | CLOSED — Changes 1,3,4,5: event widened to `Action<string,string>`, invoke passes `acc?.Name`, `OnPendingBeFiredDispatch` and `OnBeConnected` updated with account guard. | `CopyEngine.cs`, `TradeCopierPanel.cs` | B26-AB-T1, B26-AB-T2 |

### Deferred to B26-LaneC

| ID | Defect | Priority | Target Block | Status |
|----|--------|----------|--------------|--------|
| DW-B26-03 | BE Armed state visually indistinguishable from Idle — `UpdateButtonColors` overwrites background on every position tick, making BorderBrush-only Armed highlight invisible. Fix: move background authority to `UpdateBeVisuals`; guard `UpdateButtonColors` to skip when `_beState != BeState.Idle`. ~5 line change in `TradeCopierPanel.cs`. | P1 | B26-LaneC | OPEN |
| DEAD-B26 | Dead fields and methods surviving from pre-B12 V1 panel layout. Delete: L121-125 (`_copyToggleBtn`, `_flattenBtn`, `_cancelBtn`, `_trimBtn`, `_beBtn`) and 2 dead methods (`OnToggle` L1270, `OnBreakEven` L1293) from `TradeCopierPanel.cs`. Retain `_beBufferBox` L128 — live via `DispatchShortcut` L1417. | P1 | B26-LaneC | OPEN |

### [Fact] Delta

| Metric | Value |
|--------|-------|
| Baseline entering B26-LaneAB | 131 |
| Tests added this block | +2 (`T_B26_01_TrailBe_WithNoRule_StillMovesStop`, `T_B26_02_PendingBeFired_CarriesAccountName`) |
| Closing count | **133** |

---
