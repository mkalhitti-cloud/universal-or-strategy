# DW-B82-01 Repair Completion

## Status: REPAIR_COMPLETE -- SIM Gate Pending

**Commit**: 460bedce
**Branch**: main
**[Fact] count**: 306 (+2 added: T_DW_B82_01_01, T_DW_B82_01_02)
**Date**: 2026-08-21

---

## Root Cause Summary

`_beReplaceAttempts` counter incremented in `TryReplacePttBeBrackets` on every
PTT-BE-Stop cancel/re-place attempt (max 3 allowed). Counter was ONLY reset in
`TryEvictFollowerBeSlot` (position-close path).

**Failure mode**: QX sweep cancels all 3 PTT-BE-Stop-* brackets in one trade.
Counter goes 0 -> 3 in a single BE -> QX cycle. On next entry, every account
hits `prevAttempts >= 3` guard at TryReplacePttBeBrackets L1817 and returns
immediately -- no slot registered, no brackets placed -- follower gets bare
PTT-BE-Stop only (stops-only, no targets, no OCO).

This affected ALL scenarios from trade 2 onward in any session with at least
one prior BE-QX cycle.

---

## Fix Applied

**File**: `src/PropTraderTools/CopyEngine.cs`

**Location 1 -- TryFireFollowerBeRetry L1067**:
After `_pendingFollowerBeSlots.TryRemove` atomic claim succeeds, immediately
reset the attempt counter:
```csharp
_beReplaceAttempts.TryRemove(o.Account.Name, out _);   // DW-B82-01: reset on slot consumption
```

**Location 2 -- QueueBeRetryFallback timer Tick lambda L1156**:
Inside the `if (_pendingFollowerBeSlots.TryRemove(...))` success branch,
immediately reset the attempt counter:
```csharp
_beReplaceAttempts.TryRemove(capturedAcc.Name, out _); // DW-B82-01: reset on slot consumption
```

**Rationale**: Both paths represent successful slot consumption -- the slot
is about to be used to fire `MoveStopToBreakEven`. At that point the attempt
counter becomes stale (it counted prior attempts, not this fresh slot use).
Resetting it here ensures TryReplacePttBeBrackets starts from 0 for the
next BE cycle on this account.

---

## Tests Added (B82Tests.cs)

| Test | Description |
|------|-------------|
| T_DW_B82_01_01 | TryFireFollowerBeRetry IL contains exactly 2 TryRemove callvirt instructions (slot + counter reset) |
| T_DW_B82_01_02 | QueueBeRetryFallback timer Tick lambda IL contains >= 2 TryRemove callvirt instructions (slot + counter reset) |

Both use IL opcode scan (callvirt 0x6F resolving to method named "TryRemove") --
same pattern as B81Tests, NT8-safe (no NT8 sealed type instantiation).

---

## SIM Gate Protocol

**Gate**: Director runs 4+ consecutive `entry -> BE-ALL -> QX` cycles in live SIM.

**Pass criteria**:
- Cycles 2, 3, 4+: followers get PTT-BE-Stop-1/2/3 WITH PTT-BE-Target-1/2/3 (full OCO)
- No "stops-only" (PTT-BE-Stop without matching PTT-BE-Target) on any cycle after the first
- Log shows `[BE-RETRY]` with "event-driven BE retry firing" (not "fallback TryRemove=false")
- Log shows `_beReplaceAttempts reset` (not `"prevAttempts >= 3"` gate hit after cycle 1)

**Fail criteria**:
- Any cycle 2+ produces bare PTT-BE-Stop without targets
- Open DW-B82-02 with fresh NT8 output log

---

## Related Items

| Item | Status | Notes |
|------|--------|-------|
| DW-B81-01 | CLOSED | TryEvictFollowerBeSlot Rejected guard -- commit e609cfe3 |
| HOTFIX-B80-01 | PENDING SIM | QueueBeRetryFallback delayMs:500 -- commit 04b3acfc |
| HOTFIX-B80-02 | PENDING SIM | TryReplacePttBeBrackets TryAdd dedup guard |
| DW-B81-02 | PENDING SIM | 5-cycle consecutive BE-ALL check |
| DW-B81-03 | PENDING SIM | After B81-02 SIM gate |