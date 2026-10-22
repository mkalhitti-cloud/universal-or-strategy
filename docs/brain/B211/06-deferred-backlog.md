# B211 Deferred Backlog

## Block: B211 — Move Target to Ask / Move Target to Bid (PTT-MA)
## Date: 2026-07-08
## Status: PIPELINE_COMPLETE pending SIM validation

---

### Completed Items (closed by B211 pipeline)

- CLOSED: DW-B211-03 — Canonical target order identification.
  Decision: "LAST Working PTT-prefixed Limit exit in acc.Orders iteration (most recently submitted)."
  PTT-prefix filter excludes ATM-managed brackets (Target1, Target2, Stop1, Stop2).
  Implemented: PttMoveTargetOne.cs L124-136 (`target = o;` overwrites on each loop match — takes last).
  Commit: Ticket-1 (2026-07-07).

- CLOSED: DW-B211-05 — Shared vs independent buffers.
  Decision: CONFIRM SHARED. `_askBuffer` serves Ask + Ask-ALL; `_bidBuffer` serves Bid + Bid-ALL.
  Natural UX: user sets desired offset once, applies to single or all accounts.
  Implemented: TradeCopierPanel.cs L244-245 (volatile int fields); L1481-1484 (OnAskAll* delegates to OnAsk*);
  L1531-1534 (OnBidAll* delegates to OnBid*); UpdateAskLabels/UpdateBidLabels refresh both buttons.
  Commit: Ticket-2 (2026-07-08).

---

### Deferred Items

#### DW-B211-01: Direction-aware validation (warn/block for direction-inappropriate move)
- **Issue**: V1 fires unconditionally — pressing Ask when long (Ask is below entry, moving target to a
  potentially unprofitable price) produces no warning. Direction-aware validation would warn the user
  (or optionally block) when the selected direction appears incorrect for their open position.
- **Root cause**: Design requires position direction tracking (long vs short) and market context (entry price).
  Also required for the planned Increase/Decrease target buttons (DW-B211-02).
- **Recommended fix**: In a future block, after DW-B211-01 direction decision is resolved, add a guard or
  visual warning in OnAskClick/OnBidClick that checks `pos.MarketPosition` and warns if Ask move is
  being used on a long position where Ask < entry or similar logic.
- **Priority**: P2 — Low. V1 fires unconditionally; experienced user knows their trade direction.
- **Blocks**: DW-B211-02 (Increase/Decrease buttons depend on this decision).
- **Target Block**: Future block

---

#### DW-B211-02: Increase/Decrease target nudge buttons
- **Issue**: B211 V1 moves the target to the live Ask or Bid price. A natural follow-on feature is
  "nudge" buttons that move the target +N or -N ticks from its current price (without referencing
  the live market). This allows fine-tuning of an already-moved target.
- **Root cause**: Design depends on DW-B211-01 direction-handling decision (how to determine "up" vs "down"
  semantically for long vs short positions).
- **Recommended fix**: After DW-B211-01 is resolved, design a PttNudgeTargetOne/All pair following the
  same pattern as PttMoveTargetOne/All. Requires reading the current target order's limit price as the
  base, not the market price.
- **Priority**: P2 — Low. Separate feature with standalone SIM verification path.
- **Blocks**: Nothing. Independent feature.
- **Target Block**: Future block (after DW-B211-01 resolved)

---

#### DW-B211-04: GTC sweep race protection for PTT-MA-* orders
- **Issue**: If a new entry fill arrives within ~50ms of an Ask/Bid click, `TryCancelStaleGtcExitOnEntry`
  could sweep the just-submitted `PTT-MA-Ask-N` order before it reaches Working state. V1 accepts this
  race condition: the stop order is always intact (no naked risk), but the newly moved target would be
  cancelled and the previous target is already gone (also cancelled). Net effect: no open target exit.
- **Root cause**: `TryCancelStaleGtcExitOnEntry` sweeps stale GTC exits on new entry fill. PTT-MA-* orders
  are NOT protected by FIX-G-2 (confirmed by T8). In a rapid re-entry scenario, the new PTT-MA-* order
  could be classified as a stale exit from the prior position.
- **Recommended fix**: If SIM Round confirms the race is a practical problem, add a `_maInProgress` flag
  mirroring the FIX-G-2 pattern to guard PTT-MA-* orders during the cancel+submit window (~50-100ms).
  Set to true before `acc.Cancel(target)`, false in the catch or after submit.
- **Priority**: P1 — SIM-conditional. Only implement if SIM confirms the race.
- **Blocks**: Nothing until SIM confirms.
- **Target Block**: During SIM Round (DW-B211-SIM-01/02)

---

#### DW-B211-SIM-01: SIM Round for B211 — combined with B209 SIM R7
- **Issue**: B211 code changes are complete and committed (ptt-sync: 21/0). SIM validation has not yet
  been run with the new code in a live NT8 SIM environment.
- **Action**: Run SIM Round combining B211 and B209 scenarios:
  1. **Ask button (B211)**: Open a long position. Press Ask button. Verify:
     - Existing Working Limit exit (PTT-prefixed) is cancelled.
     - New Limit order `PTT-MA-Ask-NNNNN` submitted at Ask price + buffer ticks.
     - Label shows correct ask price.
  2. **Bid button (B211)**: Open a short position. Press Bid button. Verify:
     - Existing Working Limit exit cancelled.
     - New Limit order `PTT-MA-Bid-NNNNN` submitted at Bid price - buffer ticks.
  3. **Ask-ALL / Bid-ALL (B211)**: With multiple follower accounts, press Ask-ALL. Verify all accounts
     with Working Limit exits on the instrument have their exits moved to Ask + buffer.
  4. **No position (B211-BEHAV-2)**: Press Ask with no open position. Verify no-op (log "[PTT-MA] no position").
  5. **No target (B211-BEHAV-1)**: Press Ask with position but no Working Limit exit. Verify no-op.
  6. **B209 OCO path (FIX-G-1)**: Execute BE swap, fill PTT-BE-Target-N. Verify IsBeSwapInProgress
     clears after Cancelled transition on PTT-BE-Stop-N.
  7. **B209 GTC guard (FIX-G-2)**: Execute BE swap, verify PTT-BE-Stop-N NOT swept by
     TryCancelSingleStaleGtcExit while beSwapInProgress.
  8. **DW-B211-04 race check**: Execute Ask click, immediately trigger new entry fill. Observe whether
     PTT-MA-* order survives or is swept. Document result for DW-B211-04 disposition.
- **Priority**: P0 — PROD-blocking (combined with B209 SIM R7).
- **Blocks**: PROD deployment of B211 + B209 commits.
- **Target Block**: Next SIM session

---

#### DW-B211-SIM-02: Evaluate DW-B211-04 GTC sweep race during SIM Round
- **Issue**: Needs empirical confirmation from SIM before deciding whether to implement _maInProgress guard.
- **Action**: During DW-B211-SIM-01 run: time the window between acc.Cancel and acc.Submit receipt
  confirmation. If a forced rapid re-entry during that window causes PTT-MA-* sweep: DW-B211-04
  implementation is required. If no practical race observed: mark DW-B211-04 as CLOSED (race not
  observed in SIM conditions).
- **Priority**: P1 — SIM-conditional.
- **Blocks**: DW-B211-04 disposition.
- **Target Block**: During DW-B211-SIM-01 SIM session

---

### Prior B209 Deferred Items (carried forward — not resolved by B211)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B209-01 | Guardian script single-hunk false positive fix | P2 | B212/future | OPEN |
| DW-B209-02 | TradeCopierPanel.cs BrushTeal pre-existing gap (_instr2tBtn.Background) | P2 | Future | OPEN |
| DW-B209-03 | SIM Round 7 — OCO path + GTC sweep guard (B209 FIX-G-1 + FIX-G-2) | P0 | Combined with B211 SIM | OPEN |
| DW-B209-04 | specs/002-trade-copier-spec.html B209 block updates | P1 | Before SIM R7 | OPEN |

Note: DW-B209-03 is now combined with DW-B211-SIM-01 (single SIM Round for both blocks).

---

### Summary Table

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B211-01 | Direction-aware validation (warn/block for direction-inappropriate Ask/Bid) | P2 | Future block | OPEN |
| DW-B211-02 | Increase/Decrease target nudge buttons (depends DW-B211-01) | P2 | Future block | OPEN |
| DW-B211-03 | Canonical target identification (LAST PTT-* Working Limit exit) | P1 | — | CLOSED (B211) |
| DW-B211-04 | GTC sweep race protection for PTT-MA-* (_maInProgress flag, SIM-conditional) | P1 | During SIM Round | OPEN |
| DW-B211-05 | Shared vs independent buffers | P2 | — | CLOSED (B211) |
| DW-B209-01 | Guardian script single-hunk false positive | P2 | B212/future | OPEN |
| DW-B209-02 | TradeCopierPanel.cs BrushTeal gap | P2 | Future | OPEN |
| DW-B209-03 | SIM R7 — B209 OCO + GTC guard (combined with B211 SIM) | P0 | Next SIM session | OPEN |
| DW-B209-04 | specs/002-trade-copier-spec.html B209 updates | P1 | Before SIM R7 | OPEN |
| DW-B211-SIM-01 | SIM validation for B211 + B209 combined round | P0 | Next SIM session | OPEN |
| DW-B211-SIM-02 | Evaluate DW-B211-04 GTC race empirically during SIM | P1 | During SIM Round | OPEN |
