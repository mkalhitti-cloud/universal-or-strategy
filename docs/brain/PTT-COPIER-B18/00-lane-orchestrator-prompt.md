# PTT-COPIER-B18 — Lane Orchestrator Prompt
# Paste this into a NEW ptt-orchestrator session to launch the B18 lane.
# Date: 2026-07-15
# Status: READY TO LAUNCH (parallel with B17)

---

You are the **ptt-orchestrator** for block **PTT-COPIER-B18**.

## Context

B17 is running in a parallel lane fixing click trader pixel-price accuracy (`TradeCopierPanel.cs` only).
B18 runs in parallel with B17 — **zero file overlap** — and fixes the two P1 blockers
preventing copy trading from functioning at all.

**WAVE WORKSPACE**: `c:\WSGTA\universal-or-strategy`
**NT8 ADDONS FOLDER**: `C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools\`
**SPEC**: `c:\WSGTA\universal-or-strategy-director\specs\002-trade-copier-spec.html`
**BRAIN**: `c:\WSGTA\universal-or-strategy-director\docs\brain\PTT-COPIER-B18\`
**KNOWLEDGE BASE**: `c:\WSGTA\universal-or-strategy-director\docs\standards\NT8_ADDON_KNOWLEDGE.md`
**NT8 COMPILER RULES**: `c:\WSGTA\universal-or-strategy\docs\standards\NT8_COMPILER_RULES.md`

## SRC CODE AUTHORIZATION

B18 is authorized to edit ONLY these files:
- `src/PropTraderTools/TradeCopierAddOn.cs` (T1 only)
- `src/PropTraderTools/TradeCopierWindow.cs` (T2 only)

**BANNED FILES (do NOT touch under any circumstances)**:
- `TradeCopierPanel.cs` — B17 active, merge conflict risk
- `CopyEngine.cs` — deferred to B19
- `AtrSizingEngine.cs` — unrelated

## Mission

Fix two P1 blockers confirmed from Sim101 live testing session (2026-07-15):

---

### TICKET 1 — `TradeCopierAddOn.cs` — DW-B17-LEADER-01 (PRIORITY: fix this first)

**Root cause**: `WireLeaderAccount` calls `FindVisualChild<ComboBox>(chartTrader)`.
DFS first-match returns the **Instrument ComboBox** (`MES SEP26`), not the Account ComboBox.
`SelectedItem as Account` = null. `_leaderAccount` stays null forever.
Every "Apply Rule" click shows "No leader -- select account in ChartTrader." even with account selected.

**Fix**: Add `FindAccountComboBox` private static helper that walks all ComboBoxes and returns
the first one whose `SelectedItem is NinjaTrader.Cbi.Account`. Add `FindVisualChildByIndex<T>`
as a fallback (for the case where no account is yet selected when wiring fires).
Replace the `FindVisualChild<ComboBox>` call in `WireLeaderAccount` with `FindAccountComboBox`.

Full implementation details: `docs/brain/PTT-COPIER-B18/04-tickets.md` — Ticket 1.

**Success gate**: Click "Apply Rule" in ChartTrader panel with PA-APEX account selected.
Status bar shows `"Rule: MES SEP26 leader=PA-APEX-422136-01..."` (not "No leader").

---

### TICKET 2 — `TradeCopierWindow.cs` — DW-B18-ACCOUNTS-01 (run after T1 or in parallel)

**Root cause**: `BuildRuleRow` and `BuildDynamicRuleRow` wrap `followerLb` (ListBox) in a
`ScrollViewer { MaxHeight=80 }`. WPF `VirtualizingStackPanel` sees infinite height from the
outer ScrollViewer, renders only 4 items (80px / ~22px = 4 rows). All 20+ accounts are bound
but not rendered. Scroll does not work. Leader ComboBox in same row shows all accounts correctly
because ComboBox renders via Popup (unconstrained).

**Fix**: Remove the outer `ScrollViewer` wrapper in both `BuildRuleRow` and `BuildDynamicRuleRow`.
Set `followerLb.Height = 100` (fixed height). Place `followerLb` directly in the Grid column.
The ListBox's own internal ScrollViewer handles scrolling correctly without the outer wrapper.

Full implementation details: `docs/brain/PTT-COPIER-B18/04-tickets.md` — Ticket 2.

**Success gate**: TradeCopierWindow follower area shows all 20+ accounts. Scroll works.
Multi-select (Ctrl+click) works. Dynamic rows (+ Add Rule) show same full list.

---

## Workflow

Follow the standard PTT orchestrator-worker-validator model:

1. **ptt-architect** (skip — architecture already complete, confirmed from live testing)
   - Artifacts: `02-architecture-plan.md` ✅ already written

2. **ptt-engineer** (T1): Edit `TradeCopierAddOn.cs`
   - Follow `04-tickets.md` Ticket 1 step by step
   - Build, deploy, F5 verify
   - Write `ticket-1-completion.md`

3. **ptt-verifier** (T1): Verify T1
   - Read `ticket-1-completion.md`
   - Confirm `FindAccountComboBox` method present, `WireLeaderAccount` updated
   - Confirm build passes, no regressions
   - Write `ticket-1-verification.md`

4. **Director live test** (T1): Apply Rule in ChartTrader → confirm "No leader" gone

5. **ptt-engineer** (T2): Edit `TradeCopierWindow.cs`
   - Follow `04-tickets.md` Ticket 2 step by step
   - Build, deploy, F5 verify
   - Write `ticket-2-completion.md`

6. **ptt-verifier** (T2): Verify T2
   - Confirm ScrollViewer removed from both BuildRuleRow and BuildDynamicRuleRow
   - Confirm followerLb.Height = 100 in both methods
   - Write `ticket-2-verification.md`

7. **Director live test** (T2): TradeCopierWindow → confirm all accounts visible

8. **ptt-final-review**: Write `05-final-review.md`, update manifest to FINAL_PASS

---

## Key References

- Architecture plan: `docs/brain/PTT-COPIER-B18/02-architecture-plan.md`
- Tickets: `docs/brain/PTT-COPIER-B18/04-tickets.md`
- Manifest: `docs/brain/PTT-COPIER-B18/manifest.json`
- All defect evidence: `docs/standards/NT8_ADDON_KNOWLEDGE.md` — Testing Session (2026-07-15)
- NT8 compiler rules: `docs/standards/NT8_COMPILER_RULES.md` — mandatory read before any .cs edit
- B17 brain (DO NOT CONFLICT): `docs/brain/PTT-COPIER-B17/`

## Deferred to B19 (out of B18 scope)

- DW-B17-SYNC-01: Copy ON/OFF sync — touches TradeCopierPanel.cs, wait for B17 close
- DW-B17-ACCOUNT-NAME-01: Strip !Apex!Apex display suffix — nice-to-have

---
