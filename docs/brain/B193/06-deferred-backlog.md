# B193 Deferred Backlog

**Epic**: B193 -- Formalize B193-DIRECT-1/2/3: per-panel Quick ALL + button rename
**Author**: ptt-plan-reviewer (Phase 5)
**Date**: 2026-10-06
**Preceding backlog**: docs/brain/B191/06-deferred-backlog.md
**Status**: Authoritative deferred backlog for the NEXT epic block (B194+)

---

## Purpose

This document records all deferred work items that remain open after B193 completion.
It supersedes docs/brain/B191/06-deferred-backlog.md as the authoritative carry-forward list.
Items closed by B193 are listed with CLOSED status for audit trail completeness.

**P0 = Compile-blocking or P0 DNA violation. Must be resolved before shipping.**
**P1 = Architecture risk. Must be resolved before next major feature wave.**
**P2 = Quality / testing gap. Track and address in near-term pipeline.**
**P3 = Low-risk cleanup / informational. Address when convenient.**

---

## B193 Block -- 2026-10-06

### Items Closed by B193

| DW ID | Description | Priority | Closed By | Closing Artifact |
|-------|-------------|----------|-----------|-----------------|
| DW-B192-QALL-RESET-01 | Cross-panel _quickAllT1 reset concern (pre-B193 design risk). Closed by B193-DIRECT-2: per-panel _quickAllT1 design eliminates the singleton state reset problem. Each panel maintains its own Quick ALL tick value independently. Card was not previously present in spec (pre-B193); added and closed in T3. | P2 | B193-T3 | ticket-3-completion.md + ticket-3-verification.md (VERIFY_PASS); spec line 42218-42232 |
| DW-B194-STOP-01 | SnapshotStopPrice returned 0 for all NT8 SIM ATM bracket stops -- no stop submitted by any QX button. Root cause: NT8 SIM ATM stops appear as OrderType.Market + OrderState.TriggerPending (not StopMarket/Working). Fixed by B194-DIRECT (commit a434679a): added isTriggerPendingStop branch to accept Market+TriggerPending orders with StopPrice>0. Live path (StopMarket/Working) preserved unchanged. Confirmed working post-fix. | P0 | B194-DIRECT (commit a434679a) | Direct fix -- Director approval granted; verified working in NT8 SIM |

### New Items Opened by B193

Two items were identified during architecture planning and are carried forward to B194+.

---

## Items Carried Forward (OPEN)

---

### DW-B183-03: IsLaneEEligible CYC=8 Frozen

```
Priority: P1
Description: IsLaneEEligible in CopyEngine.cs is at CYC=8 (JS-066 ceiling). Any branch
  addition to IsLaneEEligible without prior helper extraction is an automatic P1 JS-066
  violation. Not modified in B183-B193.
  Future engineers modifying Lane E eligibility MUST extract a helper method first
  (using the same pattern as DW-B192-SINST-01 guidance below).
Blocking: No (current CYC is at limit, not over it; blocks only future branch additions
  to IsLaneEEligible)
Target Block: When Lane E is next modified
Status: OPEN (CARRIED FORWARD from B183 via B184, B185, B186, B187, B188, B189, B190, B191, B192, B193)
Owner: Director
Source: docs/brain/B191/06-deferred-backlog.md
```

---

### DW-B192-SINST-01: SetInstrument() CYC at Ceiling 8

```
Priority: P2
Description: SetInstrument() in TradeCopierPanel.cs is at CYC=8 (JS-066 ceiling). Any branch
  addition to SetInstrument() without prior helper extraction is an automatic P1 JS-066
  violation. Engineers modifying SetInstrument() MUST extract a helper method first.
  Pattern identical to DW-B183-03 (IsLaneEEligible). Not blocking; CYC is at limit, not over it.
  Documented in spec: specs/002-trade-copier-spec.html section-b193 at line 42199.
Blocking: No (current CYC is at limit, not over it; blocks only future branch additions)
Target Block: When SetInstrument() is next modified
Status: OPEN (opened B193)
Owner: Director
Source: docs/brain/B193/02-architecture-plan.md (Deferred Work Register)
        specs/002-trade-copier-spec.html (DW-B192-SINST-01 card, line 42199)
```

---

### DW-B193-01: OnQuickAllBufferChanged Old Label Strings

```
Priority: P3
Description: OnQuickAllBufferChanged in TradeCopierPanel.cs (~line 1027-1029) still uses
  old label strings "Quick ALL" and "QAll2t" in the singleton-sink callback path.
  The per-panel Up/Down path (lines 1764-1778) correctly uses "3x All" / "2x All" after
  B193-DIRECT-3. OnQuickAllBufferChanged is a singleton-sink callback -- since B193-DIRECT-2
  moved display state to per-panel, this method may be vestigial or rarely triggered.
  Low severity: visual label shows old name only on the singleton path when singleton fires.
  Not a DNA violation (no JS rule on label string consistency). Informational only.
Blocking: No
Target Block: B194+ when OnQuickAllBufferChanged is next touched
Status: OPEN (opened B193)
Owner: Director
Source: docs/brain/B193/02-architecture-plan.md (Deferred Work Register)
```

---

## Summary by Priority

| Priority | Count | Items |
|----------|-------|-------|
| P0 | 0 | (DW-B194-STOP-01 CLOSED by B194-DIRECT) |
| P1 | 1 | DW-B183-03 |
| P2 | 1 | DW-B192-SINST-01 |
| P3 | 1 | DW-B193-01 |

**Total open items**: 3
**Items closed by B193**: 1 (DW-B192-QALL-RESET-01)
**Items closed by B194-DIRECT**: 1 (DW-B194-STOP-01)
**New items opened by B193**: 2 (DW-B192-SINST-01, DW-B193-01)

---

## Recommended Next Block Priorities

1. **DW-B183-03** (P1): IsLaneEEligible CYC freeze -- address when Lane E eligibility
   logic is next modified. No action required unless a branch addition is planned.

2. **DW-B192-SINST-01** (P2): SetInstrument() CYC freeze -- address when SetInstrument()
   is next modified. No action required unless a branch addition is planned.

3. **DW-B193-01** (P3): OnQuickAllBufferChanged old label strings -- low priority cleanup.
   Address when touching OnQuickAllBufferChanged or when singleton path is revisited.

---

*Written by: ptt-plan-reviewer*
*Epic: B193*
*Phase: 5 (Final Review)*
*Required for FINAL_PASS gate (role definition mandate).*
*Supersedes: docs/brain/B191/06-deferred-backlog.md (as authoritative carry-forward list)*
