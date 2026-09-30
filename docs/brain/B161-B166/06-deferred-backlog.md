# B161-B166 Ghost-Entry Repair Block — Deferred Backlog

**Block:** B161-B166 Ghost-Entry Repair
**Date:** 2026-09-15
**Reviewer:** ptt-plan-reviewer (Phase 5 Final Review)
**Source file:** `src/PropTraderTools/CopyEngine.cs`

---

## Block Header

This file records deferred work items discovered and documented during the B161-B166
Ghost-Entry Repair Block pipeline. Each item is either a known violation requiring a
concrete follow-on ticket, a cosmetic concern deferred to a future pass, or a housekeeping
closure of superseded requirements.

All items in this file must be reviewed at the start of any future engineer pass that
touches `IsFollowerPositionBlocked` or adjacent BE-path methods.

---

## Deferred Items

---

### DW-B161B166-01 — VIOLATION-01-FIX

**ID:** DW-B161B166-01
**Priority:** P1 — mandatory before next engineer pass on `IsFollowerPositionBlocked`
**Status:** CLOSED
**Closed date:** 2026-09-15
**Closed by:** Resolved by DW-B161B166-01 pipeline (ptt-plan-reviewer Phase 5 Final Review)
**Target block:** B5 / next block to touch `IsFollowerPositionBlocked`

**Description:**
`IsFollowerPositionBlocked` (`src/PropTraderTools/CopyEngine.cs` L3182-L3241) has a
McCabe cyclomatic complexity of 9, exceeding the JS-CYC budget of 8 by 1.

Root cause: B165 brought parent CYC to exactly 8 (within budget). B166 subsequently added
Signal 6 (`if (IsBeRetryArmedFlat(...))` at L3231) as a ninth `if`-branch directly in the
parent method, raising CYC to 9.

The stale inline comment at L3178 reads `CYC=8` — it predates B166 and must be updated
as part of this fix.

**Action required:**
Extract Signals 5 and 6 from `IsFollowerPositionBlocked` into a new helper:

```csharp
// Proposed helper (ptt-engineer to implement):
private bool IsBeSettleOrRetrySignalActive(Account acc, Instrument instr)
{
    if (IsBeSlotNonTerminal(acc.Name))
    {
        NinjaTrader.Code.Output.Process(
            "[PTT-COPY-GUARD] ghost-entry blocked (be-settle-pending): "
                + acc.Name + "\n" + instr.FullName,
            NinjaTrader.NinjaScript.PrintTo.OutputTab1);
        return true;
    }
    if (IsBeRetryArmedFlat(acc, instr))
    {
        NinjaTrader.Code.Output.Process(
            "[PTT-COPY-GUARD] ghost-entry blocked (be-retry-armed-flat): "
                + acc.Name + "\n" + instr.FullName,
            NinjaTrader.NinjaScript.PrintTo.OutputTab1);
        return true;
    }
    return false;
}
```

Replace Signals 5 and 6 in the parent with a single call:
```csharp
if (IsBeSettleOrRetrySignalActive(acc, instr))
    return true;
```

Effect: parent `IsFollowerPositionBlocked` CYC reduces from 9 to 8. New helper CYC = 4.
Update L3178 inline comment to reflect actual CYC = 8.

**Trigger condition:** Any engineer pass that modifies `IsFollowerPositionBlocked` or adds
further signals to it. This fix is a prerequisite — engineer must apply it first or as part
of the same commit.

**Rule violated:** JS-CYC (cyclomatic complexity ≤ 8 per method)
**Violation confirmed by:** Plan §6, plan review §4, ticket-reviewer §3, T5/T6/T7 completion
and verification reports (independent Layer 2 and Layer 3 counts all agree CYC=9).

---

### DW-B161B166-02 — CONCERN-02 (IsPttBeStopActiveState rename)

**ID:** DW-B161B166-02
**Priority:** P3 — cosmetic, no functional impact
**Status:** CLOSED
**Closed date:** 2026-09-15
**Closed by:** Resolved by DW-B161B166-02-03 pipeline (ptt-plan-reviewer Phase 5 Final Review)
**Target block:** B161-B166 (DW-B161B166-02-03)

**Description:**
`IsPttBeStopActiveState` is used as the active-state predicate in `HasActivePttBeExits`
(Signal 2) and `HasActivePttBeTargets` (Signal 4, added B164). The method name contains
"Stop" but the predicate is semantically valid for any order type — it tests
`Working || Accepted || Submitted`, which applies equally to Stop orders and Target orders.

The "Stop" in the name is misleading for readers who encounter it in the context of Target
order state checks.

**Action required:**
Rename `IsPttBeStopActiveState` to `IsPttBeOrderActiveState`. Update all call sites:
- `HasActivePttBeExits` (Signal 2, L3112)
- `HasActivePttBeTargets` (Signal 4, L3155)
- Any other call sites found by grep of `IsPttBeStopActiveState`

**No functional logic changes.** Rename only.

**Trigger condition:** Any cleanup or refactor pass. No urgency. Can be batched with
VIOLATION-01-FIX if convenient.

**Note from plan:** First documented in architecture plan §6 CONCERN-02 (B164 section).
Confirmed by plan review §2 B164 as a cosmetic non-blocking concern.

---

### DW-B161B166-03 — B162-B doc note (TryTriggerBeRecovery intentional asymmetry)

**ID:** DW-B161B166-03
**Priority:** P3 — documentation only, no functional impact
**Status:** CLOSED
**Closed date:** 2026-09-15
**Closed by:** Resolved by DW-B161B166-02-03 pipeline (ptt-plan-reviewer Phase 5 Final Review)
**Target block:** B161-B166 (DW-B161B166-02-03)

**Description:**
`TryTriggerBeRecovery` at L5193 uses `"PTT-BE-Stop-"` WITH trailing dash, while
`HasActivePttBeExits` (L3110) and `StillAtNonBePrice` (L7187-L7188) use `"PTT-BE-Stop"`
WITHOUT trailing dash (B162-B fix).

This asymmetry is intentional and correct: `TryTriggerBeRecovery` controls OCO-pair bracket
recovery for followers only, which must match suffixed Stop orders exclusively.

However, a future maintainer who sees the three patterns side-by-side may incorrectly flag
L5193 as a missed B162-B fix. The current comment at L5194 (`// (5) DW-B90: only Stop-*
triggers recovery`) provides some context but does not explicitly call out the B162-B
asymmetry.

**Action required:**
Add or update the comment at L5193 in `TryTriggerBeRecovery` to explicitly state:

```csharp
// B162-B: intentional -- "PTT-BE-Stop-" WITH dash is correct here.
// Only suffixed follower Stop orders (PTT-BE-Stop-1, PTT-BE-Stop-2) trigger
// bracket recovery. Bare "PTT-BE-Stop" (leader's atomic stop) must NOT.
// HasActivePttBeExits and StillAtNonBePrice use the bare prefix intentionally.
```

**No logic changes.** Comment only.

**Trigger condition:** Any pass that touches `TryTriggerBeRecovery` or nearby code.

---

### DW-B161B166-04 — B161 original SIM gate (CLOSED — superseded)

**ID:** DW-B161B166-04
**Priority:** P3 — informational
**Status:** CLOSED (superseded)

**Description:**
At the start of the B161-B166 epic, a separate B161 SIM gate was anticipated to verify
that the `IsFollowerPositionBlocked` guard in `TryReplaceOnAtmCancel` blocks ghost re-place
on all follower accounts.

**Disposition:**
The combined B166 SIM gate (PASSED 2026-09-15 4:33 PM) tested Signal 6 (`IsBeRetryArmedFlat`)
blocking ghost brackets on all 3 follower accounts (Sim102, Sim103, Sim104) across the
complete BE-All burst path. This test exercises `IsFollowerPositionBlocked` from
`TryReplaceOnAtmCancel` as well as from `DispatchCopy`, because the guard is the same
predicate in both paths.

The B161 SIM gate requirement is satisfied by the combined B166 SIM gate.
No separate B161 SIM gate is outstanding.

**No further action required.**

---

## Item Summary

| ID | Priority | Description | Status |
|----|----------|-------------|--------|
| DW-B161B166-01 | P1 | VIOLATION-01-FIX: Extract Signals 5+6 into `IsBeSettleOrRetrySignalActive` helper | CLOSED (2026-09-15) |
| DW-B161B166-02 | P3 | CONCERN-02: Rename `IsPttBeStopActiveState` to `IsPttBeOrderActiveState` | CLOSED (2026-09-15) |
| DW-B161B166-03 | P3 | B162-B doc note: Add asymmetry comment to `TryTriggerBeRecovery` at L5193 | CLOSED (2026-09-15) |
| DW-B161B166-04 | P3 | B161 original SIM gate superseded by combined B166 SIM gate | CLOSED |

---

*Written by ptt-plan-reviewer — Phase 5 Final Cross-Repair Review — 2026-09-15*
*Updated by ptt-plan-reviewer — Phase 5 DW-B161B166-02-03 Final Review — 2026-09-15: DW-B161B166-02 and DW-B161B166-03 closed. All 4 items now CLOSED. Zero open deferred items remain in docs/brain/B161-B166/.*
