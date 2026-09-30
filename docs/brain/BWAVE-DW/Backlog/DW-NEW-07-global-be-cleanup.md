## DW-NEW-07 -- Global BE Cleanup Orphaned on Last Panel Close

**Source**: Cubic/CodeRabbit review of PR #40, `src/PropTraderTools/TradeCopierPanel.cs:608`
**Discovered**: BWAVE-DW validation session, 2026-09-04
**Status**: SUBSTANTIALLY RESOLVED -- code fix merged in PR #41 (Repair-LaneC R-LC-2). SIM gate pending (DW-RepairLC-02).

### Resolution History

| Date | Action | PR |
|------|--------|----|
| 2026-09-04 | DW-NEW-07 identified; DW-C38-03 sibling-isolation fix confirmed correct but global cleanup path accidentally removed | PR #40 |
| 2026-09-04 | R-LC-2 merged: `CopyEngine.ClearAllPendingBeSlots()` + `TradeCopierAddOn.IsPanelsEmpty()` + guard in `Detach()` | PR #41 |
| 2026-09-04 | Post-merge review: code fix confirmed by independent Layer 3 verification (VERIFY_PASS) | PR #41 |
| TBD | SIM gate AC-7: arm BE ALL two accounts, close last chart, verify `IsPendingSlotsEmpty()==true` | pending |
| TBD | Director live-trading observations to be recorded here | pending |

### Finding

DW-C38-03 removed `DisarmAllAccounts()` from `Detach()` because it was incorrectly
disarming sibling panels' BE state (the contracted bug fix). However, the removal creates
a new edge case:

When `BE ALL` arms multiple accounts and the **last registered panel** is closed, the
other accounts' pending BE watchers remain active. These watchers can still place
break-even orders on accounts that have no visible panel managing them.

The original `DisarmAllAccounts()` call served dual purpose:
1. (BUG) Disarm siblings on any panel close -- CORRECTLY removed
2. (VALID) Global cleanup on last-panel close -- ACCIDENTALLY removed

### Director Live-Trading Observations (recorded 2026-09-04)

The DW-NEW-07 symptom is related to the last repair done prior to BWAVE-DW: **B119
(DW-B128 Reversal Entry Guard)**. The Director's observation is that after live MGC DEC26
trades on 2026-09-03, there was uncertainty about whether:

1. The original diagnosis was correct (reversal entries were the problem), OR
2. The real problem was missing brackets (naked fill after fill/cancel race), OR
3. The B119 repair was correct but insufficient, OR
4. B119 created a new problem through incorrect repair.

**Resolution (confirmed by log analysis, 2026-09-04)**:

- B119 is working correctly. Every `[PTT-COPY-GUARD]` line in the 09-03 log fires in
  exactly the right situation (blocking flat followers on direction reversal). The
  reversal guard diagnosis and implementation were correct.

- The MGC issue on 09-03 was **NOT caused by B119**. It was a pre-existing race condition
  (entry fills while cancel is in-flight during ATM drag repositioning) that B119 made
  more statistically frequent by increasing the number of entry cancel/resubmit cycles
  before a fill.

- The concrete symptom: PA-APEX-422136-04 received an MGC DEC26 entry fill with **zero
  bracket orders** (fo=NULL on all TP4-SFB events). PA-APEX-422136-03 on the same trade
  found `fo=Stop1` correctly. The difference was exchange-level timing — same code path.

- PA-04 order history at fill time: `Entry:Filled`, `Entry:CancelSubmitted`, and nothing
  else. No Stop or Target orders. PTT-Flatten safety net caught this and closed the
  position flat (the floor held, but there was a naked window).

- This symptom is tracked as **DW-NEW-08** (see adjacent backlog file). It is a separate
  concern from DW-NEW-07's global-BE-cleanup path.

**Relevance to DW-NEW-07**: The Director observation confirms that the global BE cleanup
path (R-LC-2) is a separate concern from the naked-fill race. The BE ALL arming behavior
(fire on accounts with no visible panel managing them) is the DW-NEW-07 scenario and is
**not** the same as the MGC naked fill. R-LC-2 covers the DW-NEW-07 scenario correctly.

### Fix Implemented (R-LC-2 -- merged PR #41)

DW-C38-03 sibling isolation preserved. Global cleanup path added:

- `TradeCopierAddOn.IsPanelsEmpty()` -- `internal static bool` returning `_panels.IsEmpty` (CYC=1, no lock)
- `CopyEngine.ClearAllPendingBeSlots()` -- foreach unsubscribes `AccountItemUpdate` handlers, then `.Clear()` (CYC=3, ConcurrentDictionary, no lock)
- `TradeCopierPanel.Detach()` lines 594-595 -- guard: `if (TradeCopierAddOn.IsPanelsEmpty()) _engine.ClearAllPendingBeSlots();`

All 7 scans passed. Build 0 errors. NT8 sync 18/18 OK.

### Acceptance Criteria

- [x] DW-C38-03 sibling isolation preserved (no regression) -- confirmed R-LC-2
- [x] When last panel closes: all pending BE slots cleared -- confirmed R-LC-2
- [ ] Integration test: two-panel scenario -- close panel 1, watchers of panel 2 unaffected; close panel 2, all pending slots cleared -- **OPEN: Director decision 2026-09-04: realistic integration test required (BWAVE-NEXT Lane A)**
- [x] Jane Street: no lock(), ConcurrentDictionary used -- confirmed R-LC-2
- [ ] SIM gate AC-7: live NT8 verification (DW-RepairLC-02) -- **PENDING**

### Follow-Up Assessment

R-LC-2 covers the DW-NEW-07 scenario (last panel close → global BE slot cleanup).
The Director's 09-03 observations describe a **different problem** (DW-NEW-08 naked fill
race) that is tracked separately. No change required to R-LC-2 based on these observations.

SIM gate AC-7 remains the only open item: arm BE ALL on two accounts, close last chart,
verify `IsPendingSlotsEmpty() == true`. This is a behavioral confirmation of R-LC-2,
not related to the DW-NEW-08 naked fill issue.

### Blocking?
No. Code fix merged. SIM gate and integration test remain open.