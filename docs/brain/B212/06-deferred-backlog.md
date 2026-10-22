# B212 Deferred Backlog

## Block: B212 -- B211+B212 Repair Batch
## Date: 2026
## Produced by: ptt-plan-reviewer (Phase 5 Final Review)
## Status: FINAL_PASS issued

---

## Summary

This file is the authoritative deferred-item register for the B212 block (B211+B212 Repair Batch).
It supersedes the item list in docs/brain/B211/06-deferred-backlog.md for all items originating
at or before B211. That file remains as historical record; this file carries all items forward.

---

## Block B212: Items CLOSED by This Pipeline

| ID | Item | Closure Evidence |
|----|------|-----------------|
| DW-B211-SIM-01 | SIM validation for B211 + B209 combined round | output.md SIM logs confirm Target1/Target2 Working, PTT-ATM-SEND on Sim102/103/104, GATE2-BLOCKED patterns consistent with copy isolation |
| DW-B211-SIM-02 | Evaluate DW-B211-04 GTC race empirically during SIM | B212-MA-STOP-02 SIM confirms PTT-MA-Stop-NNNNN reaches Working; DW-B211-04 race not triggered |
| DW-B211-04 | GTC sweep race protection for PTT-MA-* (_maInProgress flag) | SIM Round (DW-B211-SIM-02): race not observed in SIM conditions. CLOSED (re-open if observed in production) |
| DW-B209-03 | SIM Round 7 -- B209 OCO path + GTC guard (FIX-G-1 + FIX-G-2) | Combined SIM round executed as part of B211/B212 session |
| DW-B211-03 | Canonical target identification (LAST PTT-* Working Limit exit) | Closed by B211 pipeline (carried forward from B211/06-deferred-backlog.md, already CLOSED) |
| DW-B211-05 | Shared vs independent buffers (_askBuffer/_bidBuffer) | Closed by B211 pipeline (carried forward from B211/06-deferred-backlog.md, already CLOSED) |
| DW-B212-04 | DispatchCopy CYC remediation (was CYC=11 post-T3) | T3 Retry 1: IsSuppressedReentry + IsDispatchableSignal extracted; DispatchCopy CYC=8. ticket-3-verification.md Cycle 2 VERIFY_PASS |
| DW-B212-CYC-01 | PttMoveTargetOne.Execute CYC=14 (initial) | T1 Retry 1: SubmitTargetPair extracted; Execute CYC=8. ticket-1-verification.md Cycle 2 VERIFY_PASS |

---

## Block B212: Items OPEN (carry forward to next block)

| ID | Item | Priority | Target Block | Notes |
|----|------|----------|--------------|-------|
| DW-B212-01 | `Trim(Instrument)` RelayTrim path -- leader-skip investigation | P1 | B213 | SOURCE CONFIRMED OPEN: `Trim(Instrument)` at CopyEngine.cs:5731 iterates `foreach (var acc in AllAccounts(instrument))` with NO leader guard. Same structure as pre-fix Flatten(Instrument). If `TrimOneAccount(leader)` calls CancelAllAccountOrders, same NT8 ATM re-arm risk applies. Must investigate TrimOneAccount(acc, instrument) call chain before closing. Architecture plan comment at CopyEngine.cs L5764 notes "Ph3.5: verify Trim(Instrument)." |
| DW-B212-02 | PttMoveTargetAll.cs future role | P2 | B213/future | OnAskAllClick/OnBidAllClick bypass PttMoveTargetAll.Execute (direct _leaderAccount+_followerItems loop). PttMoveTargetAll.Execute still exists and calls AllAccounts/CopyEngine. Decide: retain as copier-aware API utility, or deprecate with a DW-card. Do NOT delete without explicit DW-card authorization. |
| DW-B212-03 | DIFF LIMIT compliance audit for B211+B212 combined PR | P1 | Before PR push | Confirm final B211+B212 combined PR src/ diff is < 10,000 chars per AGENTS.md Platinum Standard before push. Run `powershell -File .\deploy-sync.ps1` DIFF GUARD. |
| DW-B209-01 | Guardian script single-hunk false positive fix | P2 | B213/future | `verify_critical_symbols.ps1` reports false-positive GUARDIAN FAIL for DW-B209-02 (TradeCopierPanel._instr2tBtn.Background). Guardian script requires fix to exclude known pre-existing gaps. |
| DW-B209-02 | TradeCopierPanel.cs BrushTeal gap (_instr2tBtn.Background) | P2 | Future | Pre-existing gap in verify_critical_symbols.ps1 check. Not introduced by B211/B212. Cosmetic visual fix. |
| DW-B209-04 | specs/002-trade-copier-spec.html B209 block updates | P1 | Before SIM R8 | Spec HTML not updated for B209 OCO + GTC guard features. Must be updated before B209/B211 features ship to production. |
| DW-B211-01 | Direction-aware validation (warn/block for direction-inappropriate Ask/Bid) | P2 | Future block | Ask/Bid fire unconditionally regardless of position direction. V1 design: experienced user knows direction. V2: add position direction check and optional warning. Required before DW-B211-02. |
| DW-B211-02 | Increase/Decrease target nudge buttons (depends DW-B211-01) | P2 | Future block (after DW-B211-01) | Nudge target +N / -N ticks from current price. Requires DW-B211-01 direction-handling resolved first. New PttNudgeTargetOne/All pair needed. |

---

## Full Item Table (Including Closed)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B209-01 | Guardian script single-hunk false positive | P2 | B213/future | OPEN |
| DW-B209-02 | TradeCopierPanel.cs BrushTeal gap | P2 | Future | OPEN |
| DW-B209-03 | SIM R7 -- B209 OCO + GTC guard (combined with B211 SIM) | P0 | -- | CLOSED (B212) |
| DW-B209-04 | specs/002-trade-copier-spec.html B209 updates | P1 | Before SIM R8 | OPEN |
| DW-B211-01 | Direction-aware validation | P2 | Future block | OPEN |
| DW-B211-02 | Increase/Decrease target nudge buttons (depends DW-B211-01) | P2 | Future block | OPEN |
| DW-B211-03 | Canonical target identification (LAST PTT-* Working Limit exit) | P1 | -- | CLOSED (B211) |
| DW-B211-04 | GTC sweep race protection for PTT-MA-* (_maInProgress flag) | P1 | -- | CLOSED (B212, SIM-conditional: race not observed) |
| DW-B211-05 | Shared vs independent buffers | P2 | -- | CLOSED (B211) |
| DW-B211-SIM-01 | SIM validation for B211 + B209 combined round | P0 | -- | CLOSED (B212) |
| DW-B211-SIM-02 | Evaluate DW-B211-04 GTC race empirically during SIM | P1 | -- | CLOSED (B212) |
| DW-B212-01 | Trim(Instrument) RelayTrim path -- leader-skip investigation | P1 | B213 | OPEN |
| DW-B212-02 | PttMoveTargetAll.cs future role | P2 | B213/future | OPEN |
| DW-B212-03 | DIFF LIMIT compliance audit for B211+B212 PR | P1 | Before PR push | OPEN |
| DW-B212-04 | DispatchCopy CYC remediation | -- | -- | CLOSED (B212, T3 Retry 1) |
| DW-B212-CYC-01 | PttMoveTargetOne.Execute CYC=14 | -- | -- | CLOSED (B212, T1 Retry 1) |

---

## Notes for Next Block

### Priority Order (open P0/P1 items)

1. **DW-B212-03** (P1, Before PR push): Run DIFF GUARD before merging B211+B212 to main.
2. **DW-B212-01** (P1, B213): Investigate `Trim(Instrument)` at CopyEngine.cs:5731 for same
   leader-skip need as Flatten(Instrument). If TrimOneAccount -> CancelAllAccountOrders triggers
   NT8 ATM re-arm, apply the same `.Where(acc => leader == null || acc.Name != leader.Name)` fix.
3. **DW-B209-04** (P1): Update specs/002-trade-copier-spec.html with B209 features before
   production SIM R8.

### Background context for DW-B212-01

```
Trim(Instrument) at CopyEngine.cs:5731:
    internal void Trim(Instrument instrument)
    {
        if (!_flags.TrimFlatten) { ... return; }
        foreach (var acc in AllAccounts(instrument))
            TrimOneAccount(acc, instrument);
    }
```
No leader guard. Compare to Flatten(Instrument) which was fixed in this block (B212-FLATTEN-LEADERSKIP-01).
The question is whether `TrimOneAccount(acc, instrument)` calls `CancelAllAccountOrders` or a
targeted partial-cancel that does NOT trigger NT8 ATM re-arm. If TrimOneAccount only cancels
Trim-specific orders (not the full ATM bracket), the risk may not apply.
Investigation approach: read TrimOneAccount body; check whether it cancels orders that NT8 ATM
uses as the "user cancelled bracket" signal for re-arm.

---

*Deferred backlog produced by ptt-plan-reviewer (Phase 5). Companion to 05-final-review.md.*
