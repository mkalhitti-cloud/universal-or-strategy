# B113 Defect Brief — DW-B117

**Block**: B113
**Date created**: 2026-08-26
**Director approved**: 2026-08-26
**Status**: APPROVED — pipeline not yet started

---

## Defect

**ID**: DW-B117
**Priority**: P0 (escalated from P1 after Combo C 2026-08-26)
**Title**: QX-ALL PTT-QX-T2/T3 Missing on Followers Due to NT8 ATM Re-Arm After Pre-Cancel

---

## Root Cause (confirmed by DW-B117-DIAG diagnostic probe)

`ExecuteOne` in `PttGlobalQuickExit.cs` calls `CancelQxBrackets` on the follower BEFORE
submitting PTT-QX orders (the "pre-cancel" step introduced by DW-B79-03).

This pre-cancel successfully moves the follower's native ATM brackets (Target1/2/3) to
`CancelSubmitted`. However, cancelling NT8 ATM bracket orders triggers NT8's internal ATM
engine to **re-arm**: NT8 detects that its strategy's brackets were cancelled and automatically
creates a new set of bracket orders. This re-arm produces Target1 and Target2 Working but
**NOT Target3** (the ATM engine only partially re-arms after a forced cancel — the third
tranche is delayed or suppressed by internal ATM state).

The PTT-QX submit loop then places PTT-QX-T1, PTT-QX-T2, PTT-QX-T3. The re-armed Target2
and/or Target3 arrive Working during the submit loop and conflict with PTT-QX-T2/T3. NT8's
OCO management cancels PTT-QX-T2 and/or PTT-QX-T3.

**Result**: follower accounts have incomplete PTT-QX coverage. The un-covered tranches have
neither a PTT-QX stop nor a PTT-BE stop (both swept). Position partially unprotected.

---

## Observed Evidence

| Test | Account | Missing | Contracts naked |
|------|---------|---------|-----------------|
| Combo D (2026-08-26, run 1) | Sim102 | T3 only | 1 |
| Combo C (2026-08-26) | Sim103 | T2 + T3 | 3 (Director manually closed) |

Timing-dependent: any follower can be affected. Missing bracket count varies per run.

---

## Why Post-Submit Cancel is UNSAFE

Cancelling the re-armed ATM brackets after PTT-QX submission would trigger another NT8 ATM
re-arm. That re-arm would produce yet more ATM brackets. Cancelling those would trigger another
re-arm. This is a re-arm loop — the same structural failure as DW-B111.

---

## Fix Direction: Cancel-After Pattern

**Core principle**: Do NOT batch-cancel all follower ATM brackets before submitting PTT-QX.
Instead, submit PTT-QX first (ATM brackets coexist momentarily), then cancel each native ATM
bracket one-for-one in `OnOrderUpdate` as each corresponding PTT-QX-T* order confirms Working.

### Change 1 — PttGlobalQuickExit.cs `ExecuteOne` (remove pre-cancel)
Remove the `CancelQxBrackets` call from the follower path (`!skipIfFollower` branch).
Keep `[PTT-QX-GUARD]` log line for diagnostics. Keep `_qxCancelInProgress.TryAdd/TryRemove`
(still used by `TryReplacePttBeBrackets` as first-layer guard). Remove the `try/finally` block
that wraps `CancelQxBrackets`. Remove the `CancelQxBrackets` call itself.

### Change 2 — CopyEngine.cs: new field `_qxPendingFollowerCleanup`
Add a `ConcurrentDictionary<string, (Instrument Instr, DateTime Expiry)>` field. Keyed by
`acc.Name`. Set by `ExecuteOne` (PttGlobalQuickExit) immediately after `executor.Execute()`
returns for a follower account. Expiry = `DateTime.UtcNow.AddSeconds(2)` (2-second TTL).

### Change 3 — CopyEngine.cs `OnOrderUpdate`: cancel-after logic
In the `Working` state handler, after the existing DW-B117-DIAG block (L1230–1250), add a
new guard:
```
if PTT-QX-T* order just went Working on a follower account
AND _qxPendingFollowerCleanup contains that account
AND Expiry not elapsed
THEN: find the corresponding native ATM bracket on that account+instrument
      (same tranche index: PTT-QX-T1 → Target1, T2 → Target2, T3 → Target3)
      and cancel it via acc.CancelOrder(nativeBracket)
ALSO: if all 3 native brackets cancelled (or TTL elapsed), TryRemove from dict
```

**Why this avoids re-arm**: NT8's ATM engine does not re-arm when individual brackets are
cancelled one-at-a-time in response to a replacement order already being Working. The ATM
sees the position as already managed by the new orders. The re-arm only fires when all
brackets are batch-cancelled with no replacement in place.

### Remove DW-B117-DIAG probe (same ticket)
The `[DW-B117-DIAG]` block in `OnOrderUpdate` (L1230–1250, `CopyEngine.cs`) was a temporary
diagnostic. It MUST be removed in B113-T1. Root cause is confirmed — the probe is no longer
needed.

---

## Files Modified

| File | Change |
|------|--------|
| `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | Change 1: remove `CancelQxBrackets` from `ExecuteOne` follower path; set `_qxPendingFollowerCleanup` after submit |
| `src/PropTraderTools/CopyEngine.cs` | Change 2: add `_qxPendingFollowerCleanup` field; Change 3: cancel-after logic in `OnOrderUpdate`; Remove DW-B117-DIAG probe |

## Files NOT Modified

| File | Reason |
|------|--------|
| `PttQuickExit.cs` | No change — per-account submit loop unchanged |
| `PttGlobalBreakEven.cs` | No change — BE path unchanged |
| `PttBreakEvenSwap.cs` | No change |
| `TradeCopierPanel.cs` | No change — UI unchanged |

---

## Tests Required

Minimum 4 new xUnit `[Fact]` tests in `src/PropTraderTools/Tests/B113Tests.cs`:

| ID | Name | Assertion |
|----|------|-----------|
| T_B113_01 | QxPendingFollowerCleanup_SetAfterExecuteOne_ForFollower | `_qxPendingFollowerCleanup` contains follower acc.Name after ExecuteOne on follower |
| T_B113_02 | QxPendingFollowerCleanup_NotSet_ForLeader | `_qxPendingFollowerCleanup` empty after ExecuteOne on leader (skipIfFollower=true) |
| T_B113_03 | QxPendingFollowerCleanup_ClearedAfterTtl | Entry removed after 2s expiry (mock UtcNow) |
| T_B113_04 | CancelAfter_TargetIndexMapping | PTT-QX-T1 maps to Target1, T2→Target2, T3→Target3 (name-index parse) |

---

## CYC Targets

| Method | Current CYC | Expected after B113 |
|--------|-------------|----------------------|
| `ExecuteOne` | 2 | 2 (remove try/finally block — net CYC same or lower) |
| `OnOrderUpdate` (relevant region) | Counted within larger method | +1 for new cleanup branch (must stay ≤ 8) |

---

## Jane Street Constraints

- JS-021: no `lock()` — `ConcurrentDictionary` for `_qxPendingFollowerCleanup` ✓
- JS-033: no `async void` — all paths synchronous ✓
- JS-001: no `throw` in hot path ✓
- JS-002: `_qxPendingFollowerCleanup` never null (initialized at declaration) ✓
- ASCII-only string literals ✓
- NT8-006: no LINQ in hot path — use `foreach` or indexer ✓
- NT8-007: `CancelOrder` (not `CreateOrder`) — no arg count issue ✓

---

## Sync Gate

After implementation, engineer must run:
```powershell
powershell -File scripts\ptt-sync-and-verify.ps1
```
Expected: `N/N OK, 0 MISMATCH` (N = total file count).
Then press F5 in NinjaTrader 8 — must produce "Compilation succeeded" 0 errors.

---

## Live Re-Test Required (after pipeline)

Run Combo D (QX-ALL then BE-ALL) and Combo C (BE-ALL then QX-ALL) on fresh NT8 session.

**Combo D pass criterion**:
- Zero `[DW-B117-DIAG]` lines (probe removed)
- All 3 followers show PTT-QX-T1/T2/T3 all Working after QX-ALL
- No competing native ATM Target1/2/3 remaining Working
- `[PTT-QX-CLEANUP]` log lines confirm one-for-one native bracket cancel per PTT-QX-T* Working event

**Combo C pass criterion**:
- DW-B112 guard still fires correctly (structural PTT-QX check unchanged)
- All 3 followers show PTT-QX-T1/T2/T3 all Working after QX-ALL
- No unprotected position on any account
