# B161-B166 Ghost-Entry Repair Block — Architecture Plan

**Epic:** B161-B166  
**Phase:** Formal Record (repairs already in `src/PropTraderTools/CopyEngine.cs`)  
**Architect:** ptt-architect  
**Date:** 2026-09-15  
**Status:** PLAN_READY (see Violations section for VIOLATION-01 follow-on ticket requirement)

---

## 1. Executive Summary

This block documents 7 surgical repairs to `CopyEngine.cs` that collectively close ghost-entry race windows triggered during a BE-All event burst. A *ghost entry* is a spurious `ReplaceFollowerCopyOnAtmCancel` or `DispatchCopy` fire that places a fresh ATM bracket (Stop1 + Target1) on a follower account whose real position is already flat or closing.

The root cause shared across all repairs is a timing gap in the NT8 event queue: leader order events (PTT-BE-Stop fill, PTT-BE-Target OCO cancel) and follower order events (ATM bracket cancel) interleave on concurrent NT8 callback threads. Prior guards were incomplete — some were missing entirely (B161), some used non-consuming reads under concurrency (B162-A), some matched the wrong name prefix (B162-B), some could not distinguish lifecycle cancels from re-place triggers (B163), and some left coverage gaps in the BE-Target state machine (B164, B165, B166).

All 7 repairs are confined to a single file. No new fields, no new ConcurrentDictionary keys, no lock() anywhere.

---

## 2. LANE-SPLIT GATE RESULT: SINGLE-PIPELINE

| Question | Answer | Rationale |
|---|---|---|
| Q1: Fixes within same method or within 50 lines? | YES (for some pairs) | B162-A and B162-B both touch the TryFireBePriceRetry / HasActivePttBeExits / StillAtNonBePrice region. B165 and B166 both touch IsFollowerPositionBlocked. |
| Q2: Fix B design depends on Fix A final design? | YES | B164 adds Signal 4 on top of B163's infrastructure. B166 depends on B162-A's atomic-claim pattern (sentinel only meaningful when TryRemove is the gate). |
| Q3: Each fix has standalone value if the other is blocked? | MIXED | B162-A alone blocks double-fire; B162-B alone fixes Signal 2 name. B163 is fully independent. B164/B165/B166 each add independent signal coverage. Combined SIM gate was used. |
| Q4: Each fix has independent SIM verification path? | NO | Combined SIM gate: PASSED 2026-09-15 4:33 PM. |

**GATE RESULT: SINGLE-PIPELINE**

---

## 3. Repair Details

### B161 — `TryReplaceOnAtmCancel` (`~L947`)

**File:** `src/PropTraderTools/CopyEngine.cs`  
**Verified lines:** L947–L960

#### Root Cause

`TryReplaceOnAtmCancel` was not guarded by `IsFollowerPositionBlocked`. During a BE-All burst, follower ATM bracket cancel events arrived before the leader's `PTT-BE-Stop` fill cleared the leader position (leader `Qty > 0`, follower flat). B160 wired `IsFollowerPositionBlocked` into `DispatchCopy` via `ShouldSkipFollower` but this separate code path (`TryReplaceOnAtmCancel`) was missed.

#### Code Change

```csharp
// Added at guard position (4), after IsPttEntryOrderCancelTrigger check:
if (order.Account != null
    && order.Instrument != null
    && IsFollowerPositionBlocked(order.Account, order.Instrument))
    return; // BE-All race -- skip re-place (4)
```

Verified at L955–L958. The guard is placed after the cheap name/state filter (`IsPttEntryOrderCancelTrigger`) to avoid the O(n) `acc.Orders.ToList()` scan on non-PTT-Copy cancels.

#### CYC

| Before | After | Budget |
|--------|-------|--------|
| 4 | 5 | ≤ 8 ✓ |

#### JS Rule Compliance

| Rule | Status | Evidence |
|------|--------|---------|
| JS-001 | PASS | `void` method; no throw |
| JS-002 | PASS | `void`; not applicable |
| JS-021 | PASS | No `lock()`. `IsFollowerPositionBlocked` uses `ToList()` snapshot |
| JS-042 | PASS | Comment string `"BE-All race -- skip re-place (4)"` is ASCII-only |
| JS-CYC | PASS | CYC=5 ≤ 8 |

#### Correctness Argument

`IsFollowerPositionBlocked` is a read-only predicate over `acc.Orders` (ToList snapshot) and three ConcurrentDictionary reads. It cannot mutate state. Placing it at position (4) preserves the existing drain-check order. The guard fires only when follower account and instrument are non-null (explicit null checks at L955–L956). If `IsFollowerPositionBlocked` returns `true`, the follower is in a BE-race window and re-placement is deferred — the correct behavior, mirroring what `DispatchCopy` already does. No new state introduced. Zero risk of false positive: `IsFollowerPositionBlocked` only returns `true` when at least one of 6 signals indicates BE-in-progress.

---

### B162-A — `TryFireBePriceRetry` (`~L7218`)

**File:** `src/PropTraderTools/CopyEngine.cs`  
**Verified lines:** L7218–L7261

#### Root Cause

`TryGetValue` at guard (4) was non-consuming. Two concurrent `PTT-BE-Stop-N` cancel callbacks on separate NT8 threads both passed `TryGetValue` before either reached `TryRemove`. Both fired `SubmitBeStop`. First fire = correct exit (flat). Second fire = entry into reverse 7-share position on Sim101. `TryFireFollowerBeRetry` (L1876) already used `TryRemove` as the atomic gate — `TryFireBePriceRetry` was not written to that standard.

#### Code Change

```csharp
// BEFORE:
if (!_pendingBePriceRetry.TryGetValue(o.Account.Name, out double bePrice)) return;
// ... later ...
_bePriceRetryAttempts.TryRemove(o.Account.Name, out _); // post-fire cleanup (now removed)

// AFTER:
if (!_pendingBePriceRetry.TryRemove(o.Account.Name, out double bePrice)) return; // (4) atomic claim
// B166: counter NOT cleared here -- preserved as Signal 6 sentinel until path-specific clear below.
```

Verified at L7224: `TryRemove` is in place. `_bePriceRetryAttempts` is NOT cleared at guard (4) per B166 constraint. Post-fire `TryRemove` calls at guard are removed. The Working-path clear (L7231) is retained (stop placed = ghost impossible). The live-cancel fire path (L7253) clears before `SubmitBeStop` (retry will re-arm). The flat-cancel path (L7249) does NOT clear — sentinel preserved for Signal 6.

#### CYC

| Before | After | Budget |
|--------|-------|--------|
| 7 | 8 | ≤ 8 ✓ |

(The `||` in the flat-cancel guard at L7246–7247 adds one branch.)

#### JS Rule Compliance

| Rule | Status | Evidence |
|------|--------|---------|
| JS-001 | PASS | `void` method; no throw |
| JS-002 | PASS | `void`; not applicable |
| JS-021 | PASS | `ConcurrentDictionary.TryRemove` is lock-free atomic; no `lock()` |
| JS-042 | PASS | All string literals ASCII-only |
| JS-CYC | PASS | CYC=8 ≤ 8 |

#### Correctness Argument

`ConcurrentDictionary.TryRemove` is guaranteed to return `true` for exactly one concurrent caller. After the fix, whichever of the two concurrent threads wins the `TryRemove` proceeds; the other returns immediately at guard (4). This is the proven atomic-claim pattern already used in `TryFireFollowerBeRetry` (L1885). No double-`SubmitBeStop` is possible. The B166 constraint (not clearing `_bePriceRetryAttempts` here) is essential: the sentinel must survive until `IsBeRetryArmedFlat` can block any Entry cancel that arrives in the same event-queue gap.

---

### B162-B — `HasActivePttBeExits` (`L3104`) + `StillAtNonBePrice` (`L7181`)

**File:** `src/PropTraderTools/CopyEngine.cs`  
**Verified lines:** L3110, L7187–L7188

#### Root Cause

`SubmitBeStopOrder` submits bare `"PTT-BE-Stop"` (no suffix). `RelayBe` uses this path for all accounts during BE-All. `HasActivePttBeExits` (Signal 2 of `IsFollowerPositionBlocked`) previously matched only `"PTT-BE-Stop-"` (with trailing dash). While the bare `PTT-BE-Stop` order was Working during the BE burst, Signal 2 returned `false` → `DispatchCopy` fired → new ATM brackets on followers mid-burst.

`StillAtNonBePrice` had the same truncated prefix and missed bare `PTT-BE-Stop` orders.

#### Code Change

```csharp
// HasActivePttBeExits L3110 BEFORE:
if (o.Name == null || !o.Name.StartsWith("PTT-BE-Stop-", StringComparison.Ordinal)) continue;
// AFTER:
if (o.Name == null || !o.Name.StartsWith("PTT-BE-Stop", StringComparison.Ordinal)) continue;

// StillAtNonBePrice L7187 BEFORE:
if (o.Name == null || !o.Name.StartsWith("PTT-BE-Stop-", StringComparison.Ordinal)) ...
// AFTER:
if (o.Name == null || !o.Name.StartsWith("PTT-BE-Stop", StringComparison.Ordinal)) ...
```

Both verified in source. `"PTT-BE-Stop"` (no trailing dash) matches both bare `PTT-BE-Stop` and all suffixed variants (`PTT-BE-Stop-1`, `PTT-BE-Stop-2`, etc.).

> **INTENTIONAL ASYMMETRY:** `TryTriggerBeRecovery` at L5193 retains `"PTT-BE-Stop-"` WITH dash. This is correct and deliberate — that guard controls OCO-pair bracket recovery for followers only. Bare `"PTT-BE-Stop"` must NOT trigger bracket recovery (it is the leader's atomic stop, not part of a follower OCO pair). This is NOT a discrepancy.

#### CYC

| Method | Before | After | Budget |
|--------|--------|-------|--------|
| `HasActivePttBeExits` | 8 | 8 | ≤ 8 ✓ |
| `StillAtNonBePrice` | 7 | 7 | ≤ 8 ✓ |

CYC unchanged — only a string literal changed, no branch added or removed.

#### JS Rule Compliance

| Rule | Status | Evidence |
|------|--------|---------|
| JS-001 | PASS | Both are `bool`-returning; no throw |
| JS-002 | PASS | No null returns; `false` on guard exit |
| JS-021 | PASS | `acc.Orders.ToList()` snapshot; no `lock()` |
| JS-042 | PASS | `"PTT-BE-Stop"` is ASCII-only |
| JS-CYC | PASS | CYC unchanged at 8 and 7 |

#### Correctness Argument

`"PTT-BE-Stop"` is a strict prefix of all BE-Stop order names (bare: `"PTT-BE-Stop"`, suffixed: `"PTT-BE-Stop-1"`, `"PTT-BE-Stop-2"`, etc.). `StartsWith` with `StringComparison.Ordinal` is deterministic. The fix adds no false positives: no non-BE-Stop order starts with this prefix (all PTT order names are distinct by the naming standard). Signal 2 now correctly fires for any Working/Accepted/Submitted BE-Stop regardless of whether it was placed as bare or suffixed.

---

### B163 — `IsPttEntryOrderCancelTrigger` (`L924`)

**File:** `src/PropTraderTools/CopyEngine.cs`  
**Verified lines:** L924–L935

#### Root Cause

NT8 cancels a follower's filled `"Entry"` ATM bracket order as normal lifecycle teardown after `PTT-BE-Stop` fills. At that moment: follower is flat, leader still has position → `IsReplaceDispatchEligible` passes → ghost ATM bracket (Stop1 + Target1) placed. `IsPttEntryOrderCancelTrigger` did not distinguish lifecycle cancel of a filled Entry (`AverageFillPrice > 0`) from genuine ATM-sweep cancel of never-filled Entry (`AverageFillPrice == 0`).

#### Code Change

```csharp
// Added at position (4):
if (order.AverageFillPrice > 0)
    return false; // (4) B163: lifecycle cancel of a previously-filled Entry -- not a re-place trigger
```

Verified at L932–L933.

#### CYC

| Before | After | Budget |
|--------|-------|--------|
| 3 | 4 | ≤ 8 ✓ |

(Task-stated values. Base + 4 `if` guards = CYC 5 in standard counting; task reports guard-branch delta: added one new guard, task shorthand "3→4" reflects one new guard added. Either way well within budget.)

#### JS Rule Compliance

| Rule | Status | Evidence |
|------|--------|---------|
| JS-001 | PASS | `bool`-returning `static` method; no throw |
| JS-002 | PASS | No null return; `false` on all guard exits |
| JS-021 | PASS | `static` method; no instance state; no `lock()` |
| JS-042 | PASS | Comment `"(4) B163: lifecycle cancel..."` is ASCII-only |
| JS-CYC | PASS | Within budget |

#### Correctness Argument

`order.AverageFillPrice` is set by NT8 when an order receives a fill. It is:
- **Immutable** once set — NT8 does not reset it after the fill event.
- **Session-stable** — survives for the lifetime of the Order object.
- **Zero for never-filled orders** — guaranteed by NT8 order lifecycle.

An Entry cancelled by genuine ATM sweep (user cancel / BE-All sweep) was never filled → `AverageFillPrice == 0` → guard passes → `TryReplaceOnAtmCancel` proceeds normally. An Entry cancelled as NT8 lifecycle teardown after it was filled → `AverageFillPrice > 0` → returns `false` → replacement blocked. This is a stateless, zero-new-field, deterministic predicate. No false positives possible.

---

### B164 — `IsFollowerPositionBlocked` + `HasActivePttBeTargets` (`L3147`)

**File:** `src/PropTraderTools/CopyEngine.cs`  
**Verified lines:** L3147–L3158 (helper), L3213–L3220 (signal call)

#### Root Cause

After `PTT-BE-Stop` fills:
- Signal 2 (`HasActivePttBeExits`) = `false` (stop is now Filled, not Working/Accepted/Submitted)
- Signal 3 (`HasSettlingBeTargets`) = `false` (target is still Working, not yet CancelPending)

In this 1–3 event-loop tick gap, any `Entry:Cancelled(AverageFillPrice==0)` passed all 3 prior signals → ghost ATM bracket (Stop1 + Target1) placed on Sim102/103/104 (confirmed in output.md 2026-09-14 L436–453).

#### Code Change

New helper `HasActivePttBeTargets`:

```csharp
internal bool HasActivePttBeTargets(Account acc, Instrument instr)
{
    if (acc == null)   return false;
    if (instr == null) return false;
    foreach (var o in acc.Orders.ToList())
    {
        if (o.Name == null || !o.Name.StartsWith("PTT-BE-Target-", StringComparison.Ordinal)) continue;
        if (o.Instrument?.FullName != instr.FullName) continue;
        if (IsPttBeStopActiveState(o.OrderState)) return true; // reuse Active-state predicate
    }
    return false;
}
```

Signal 4 in `IsFollowerPositionBlocked` (L3213–L3220):

```csharp
if (HasActivePttBeTargets(acc, instr))  // (7) Signal 4 -- post-Stop-fill race (B164)
{
    NinjaTrader.Code.Output.Process(
        "[PTT-COPY-GUARD] ghost-entry blocked (active-be-targets): "
            + acc.Name + "\n" + instr.FullName,
        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
    return true;
}
```

Verified in source.

#### CYC

| Method | Before | After | Budget |
|--------|--------|-------|--------|
| `IsFollowerPositionBlocked` | 6 | 7 | ≤ 8 ✓ (at this stage) |
| `HasActivePttBeTargets` (new) | — | 8 | ≤ 8 ✓ |

#### JS Rule Compliance

| Rule | Status | Evidence |
|------|--------|---------|
| JS-001 | PASS | Both `bool`-returning; no throw |
| JS-002 | PASS | `false` on all null/empty-loop exits; never null |
| JS-021 | PASS | `ToList()` snapshot; no `lock()` |
| JS-042 | PASS | `"[PTT-COPY-GUARD] ghost-entry blocked (active-be-targets): "` ASCII-only |
| JS-CYC | PASS | Parent 7 ≤ 8; new helper 8 ≤ 8 |

#### Correctness Argument

`HasActivePttBeTargets` uses the same structure as `HasActivePttBeExits` and `HasSettlingBeTargets` — three identical helper bodies differing only in name prefix and state predicate. Reusing `IsPttBeStopActiveState` (Working/Accepted/Submitted) for PTT-BE-Target orders is semantically correct: the NT8 order state machine is shared across order types. Signal 4 is active in the exact window where Signal 2 transitions from `true` → `false` (stop fills) and before Signal 3 activates (target receives OCO CancelPending). Signals 3 and 4 together cover the complete PTT-BE-Target lifecycle: Signal 4 = pre-OCO (Working), Signal 3 = post-OCO (CancelPending/CancelSubmitted).

> **CONCERN-02 (naming):** `IsPttBeStopActiveState` has "Stop" in its name but is correctly reused for Target orders. This is a cosmetic naming issue only — the predicate tests `Working || Accepted || Submitted`, which is valid for any order type. Flag for future rename to `IsPttBeOrderActiveState` but not a functional defect.

---

### B165 — `IsFollowerPositionBlocked` Signal 5 (`~L3222`)

**File:** `src/PropTraderTools/CopyEngine.cs`  
**Verified lines:** L3222–L3229, L9382–L9386

#### Root Cause

When `MoveStopToBreakEven` (500ms fallback timer) fires for a follower and the `PTT-BE-Stop` slot is consumed, `_pendingFollowerBeSlots` still holds the slot until `RunFollowerEvictPath` clears it (requires `PTT-BE-Stop:Filled` on that specific follower). Signals 1–4 were all `false` for follower Sim104 after its targets were cancelled, but the slot was still live in `_pendingFollowerBeSlots`. `DispatchCopy` fired a full 3-lot ATM on Sim104 via this gap.

#### Code Change

```csharp
if (IsBeSlotNonTerminal(acc.Name))  // (8) Signal 5 -- BE settle window (B165)
{
    NinjaTrader.Code.Output.Process(
        "[PTT-COPY-GUARD] ghost-entry blocked (be-settle-pending): "
            + acc.Name + "\n" + instr.FullName,
        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
    return true;
}
```

`IsBeSlotNonTerminal` (L9382–L9386):

```csharp
private bool IsBeSlotNonTerminal(string accName)
{
    // CYC=1
    return _pendingFollowerBeSlots.ContainsKey(accName);
}
```

Both verified in source.

#### CYC

| Method | Before | After | Budget |
|--------|--------|-------|--------|
| `IsFollowerPositionBlocked` | 7 | 8 | ≤ 8 ✓ (at this stage, before B166) |
| `IsBeSlotNonTerminal` | pre-existing | CYC=1 | ≤ 8 ✓ |

#### JS Rule Compliance

| Rule | Status | Evidence |
|------|--------|---------|
| JS-001 | PASS | `bool`-returning; no throw |
| JS-002 | PASS | `false` on empty-slot case; never null |
| JS-021 | PASS | `_pendingFollowerBeSlots.ContainsKey` is O(1) lock-free read |
| JS-042 | PASS | `"[PTT-COPY-GUARD] ghost-entry blocked (be-settle-pending): "` ASCII-only |
| JS-CYC | PASS | Parent 8 ≤ 8 (at B165 stage) |

#### Correctness Argument

`_pendingFollowerBeSlots` is a `ConcurrentDictionary<string, PendingFollowerBeSlot>`. `ContainsKey` is an O(1) lock-free read. The slot is present from the moment `MoveStopToBreakEven` registers it (via `RegisterPendingBeSlot`) until `RunFollowerEvictPath` removes it on `PTT-BE-Stop:Filled`. Signal 5 therefore covers the entire post-fallback-BE window, regardless of individual signal states 1–4. No new fields, no new state — Signal 5 observes existing state maintained by the BE engine.

---

### B166 — `TryFireBePriceRetry` (`~L7242`) + `RunFollowerEvictPath` (`L2013`)

**File:** `src/PropTraderTools/CopyEngine.cs`  
**Verified lines:** L7242–L7249 (flat-cancel path), L2022 (RunFollowerEvictPath)

#### Root Cause

3-step race:

1. 500ms fallback timer fires `MoveStopToBreakEven`, re-arming `_bePriceRetryAttempts["Sim103"] = 1` while `IsFlat = false` (NT8 position state lag).
2. New `PTT-BE-Stop` immediately Cancelled (position closing) → `TryFireBePriceRetry` flat-cancel path previously called `_bePriceRetryAttempts.TryRemove(...)` → Signal 6 sentinel cleared BEFORE `DispatchCopy` evaluated it.
3. `DispatchCopy` fired; all 6 signals false (sentinel gone) → ghost ATM bracket placed.

Fix: sentinel stays alive through the flat-cancel path; `RunFollowerEvictPath` clears it on confirmed position close (`Close:Filled` or `PTT-BE-Stop:Filled` triggers `RunFollowerEvictPath`).

#### Code Change A — Flat-cancel path sentinel preservation

```csharp
// BEFORE (bug):
if (!StillAtNonBePrice(...) || IsFlat(FindPosition(...)))
{
    _bePriceRetryAttempts.TryRemove(o.Account.Name, out _); // BUG: dropped Signal 6 sentinel
    return;
}

// AFTER (B166):
if (!StillAtNonBePrice(o.Account, o.Instrument, bePrice)   // (7)
    || IsFlat(FindPosition(o.Account, o.Instrument)))       // (8) B166: flat = no position to protect
{
    return; // B166: sentinel stays alive for Signal 6 (IsBeRetryArmedFlat)
}
```

Verified at L7246–L7249: no `TryRemove` in the flat-cancel path.

#### Code Change B — `RunFollowerEvictPath` sentinel clear

```csharp
// Added after _filledBeTargetCount.TryRemove:
_bePriceRetryAttempts.TryRemove(accName, out _);
// B166: clear Signal 6 sentinel on confirmed position close
```

Verified at L2022.

#### CYC

| Method | Before | After | Budget |
|--------|--------|-------|--------|
| `TryFireBePriceRetry` | 8 | 8 | ≤ 8 ✓ |
| `RunFollowerEvictPath` | 4 | 4 | ≤ 8 ✓ |

(CYC unchanged in both methods — removing the `TryRemove` call from inside the flat-cancel `if` body does not add or remove branches. `RunFollowerEvictPath` adds a `TryRemove` statement, not an `if` branch.)

#### JS Rule Compliance

| Rule | Status | Evidence |
|------|--------|---------|
| JS-001 | PASS | Both `void`; no throw |
| JS-002 | PASS | `void`; not applicable |
| JS-021 | PASS | `ConcurrentDictionary.TryRemove` is lock-free; no `lock()` |
| JS-042 | PASS | Comment `"B166: sentinel stays alive..."` is ASCII-only |
| JS-CYC | PASS | Both methods CYC unchanged at 8 and 4 |

#### Correctness Argument

**Liveness of the sentinel:**  
The sentinel (`_bePriceRetryAttempts[accName]`) is written by `TryArmBePriceRetry` (called from `MoveStopToBreakEven`). After B166, it is only cleared in three specific paths:
1. **Working path** (L7231): stop is placed and Working — position is still live, ghost is impossible, safe to clear.
2. **Live-cancel fire path** (L7253): retry fires `SubmitBeStop` — new stop will arm a new slot; old counter cleared as part of retry.
3. **RunFollowerEvictPath** (L2022): confirmed position close — position is gone, ghost window is over.

The flat-cancel path (`IsFlat || !StillAtNonBePrice`) no longer clears the sentinel. `IsBeRetryArmedFlat = IsFlat(pos) && ContainsKey(accName)` remains `true` until `RunFollowerEvictPath` fires.

**NT8 event ordering:**  
NT8 fires `PTT-BE-Stop:Cancelled` before `Entry:Cancelled` for the same account within the same event burst (stop cancel arrives before the entry cancel propagates). This means Signal 6 is live when `IsFollowerPositionBlocked` is evaluated in `TryReplaceOnAtmCancel`. The sentinel is visible to any thread reading `_bePriceRetryAttempts.ContainsKey` due to ConcurrentDictionary's visibility guarantees.

**No false positives:**  
`IsBeRetryArmedFlat` requires BOTH `IsFlat(pos)` AND `ContainsKey(accName)`. A follower with an open position has `IsFlat = false` → Signal 6 = `false`. Only a flat follower with a live sentinel is blocked.

---

## 4. IsFollowerPositionBlocked Signal Map

**Method:** `IsFollowerPositionBlocked(Account acc, Instrument instr)` — `src/PropTraderTools/CopyEngine.cs` L3182–L3241

| # | Signal | Method | State Covered | Added By | Source Line |
|---|--------|--------|---------------|----------|-------------|
| 1 | `HasOpenPosition` | Checks `Quantity > 0` | Normal position open | B159 | L3186 |
| 2 | `HasActivePttBeExits` | `PTT-BE-Stop*` in Working/Accepted/Submitted | Pre-fill race window | B160 (B162-B fixed prefix) | L3195 |
| 3 | `HasSettlingBeTargets` | `PTT-BE-Target-*` in CancelPending/CancelSubmitted | Confirmed OCO-settle window | B160 | L3204 |
| 4 | `HasActivePttBeTargets` | `PTT-BE-Target-*` in Working/Accepted/Submitted | Post-Stop-fill gap (Stop filled, Target still Working) | B164 | L3213 |
| 5 | `IsBeSlotNonTerminal` | `_pendingFollowerBeSlots.ContainsKey` | BE slot still live (post-OCO, pre-eviction) | B165 | L3222 |
| 6 | `IsBeRetryArmedFlat` | `IsFlat && _bePriceRetryAttempts.ContainsKey` | Post-fallback-BE window (timer fired, flat, sentinel live) | B166 | L3231 |

### Time-Ordered Coverage (BE burst lifecycle)

```
T0: PTT-BE-Stop Working
    Signal 2 = true  ──────────────────────────────── BLOCKED

T1: PTT-BE-Stop Filling
    Signal 2 = true  ──────────────────────────────── BLOCKED

T2: PTT-BE-Stop Filled   <── B164 gap window opens
    Signal 2 = false
    Signal 4 = true (PTT-BE-Target still Working)  ── BLOCKED

T3: PTT-BE-Target CancelPending (OCO reaction)
    Signal 4 = false
    Signal 3 = true  ──────────────────────────────── BLOCKED

T4: PTT-BE-Target Cancelled
    Signal 3 = false
    Signal 5 = true (BE slot in _pendingFollowerBeSlots)  BLOCKED

T5: RunFollowerEvictPath fires (PTT-BE-Stop:Filled)
    Signal 5 = false
    (If fallback timer had fired and re-armed _bePriceRetryAttempts
     while flat) Signal 6 = true  ─────────────────── BLOCKED

T6: RunFollowerEvictPath clears _bePriceRetryAttempts
    Signal 6 = false
    ALL signals false --> SAFE to dispatch copies
```

No coverage gaps. Signals are ordered by temporal occurrence of their guard condition.

---

## 5. SIM Gate Results

**Status:** PASSED  
**Date/Time:** 2026-09-15 4:33 PM  
**Scope:** Signal 6 (`IsBeRetryArmedFlat`) blocked ghost brackets on all 3 follower accounts (Sim102, Sim103, Sim104) in combined test session.  
**Prior gates:** B161–B165 each individually validated during their respective repair sessions. Combined SIM gate used for B166 as the culminating fix.

---

## 6. Violations and Concerns

### VIOLATION-01: `IsFollowerPositionBlocked` CYC=9 — Exceeds JS-CYC Budget

**Severity:** P1 (JS-CYC rule violation)  
**Source:** L3178 contains stale comment `CYC=8 ... Signal 5 at budget`. B166 added Signal 6 (`if (IsBeRetryArmedFlat(...))` at L3231) as a ninth `if`-branch, pushing actual CYC to **9**.

**Calculation:**

| Branch | Line | +CYC |
|--------|------|------|
| base | — | 1 |
| `if (acc == null)` | L3184 | +1 |
| `if (instr == null)` | L3185 | +1 |
| `if (HasOpenPosition)` | L3186 | +1 |
| `if (HasActivePttBeExits)` | L3195 | +1 |
| `if (HasSettlingBeTargets)` | L3204 | +1 |
| `if (HasActivePttBeTargets)` | L3213 | +1 |
| `if (IsBeSlotNonTerminal)` | L3222 | +1 |
| `if (IsBeRetryArmedFlat)` | L3231 | +1 |
| **Total** | | **9** |

**Required remediation (ptt-engineer):**  
Extract Signals 5 and 6 into a single delegated helper that handles the check, log, and return — keeping the parent to one `if`-branch for both signals:

```csharp
// Proposed helper (ptt-engineer to implement):
// private bool IsBeSettleOrRetrySignalActive(Account acc, Instrument instr) { ... }
// Covers: IsBeSlotNonTerminal(acc.Name) || IsBeRetryArmedFlat(acc, instr)
// Each true path emits its own reason-tagged log before returning true.
// CYC = 1 + 1 (if-slot) + 1 (if-retry) + 1 (|| short-circuit) = 4
// Parent IsFollowerPositionBlocked CYC reduces from 9 to 8.
```

This is a standalone fix ticket. It does not change any functional logic — only restructures the branch to comply with JS-CYC.

### CONCERN-01: `TryTriggerBeRecovery` Retains `"PTT-BE-Stop-"` With Dash (Intentional)

`TryTriggerBeRecovery` at L5193 uses `"PTT-BE-Stop-"` (with dash). This is **intentional** and correct: that guard controls OCO-pair bracket recovery for followers only, which requires a suffixed Stop order (part of an OCO pair). Bare `"PTT-BE-Stop"` (leader's atomic stop) must NOT trigger bracket recovery. This asymmetry from B162-B is documented and is not a defect.

### CONCERN-02: `IsPttBeStopActiveState` Naming (Minor — Non-Functional)

`HasActivePttBeTargets` (B164) reuses `IsPttBeStopActiveState` as its state predicate. The predicate tests `Working || Accepted || Submitted`, which is correct for Target orders. The "Stop" in the helper name is misleading. No functional impact. Recommend renaming to `IsPttBeOrderActiveState` in a future cleanup pass.

---

## 7. Component Summary

| Component | File | Change Type | New Fields | New Helpers |
|-----------|------|-------------|------------|-------------|
| `TryReplaceOnAtmCancel` | `CopyEngine.cs` | Guard added | 0 | 0 |
| `TryFireBePriceRetry` | `CopyEngine.cs` | Atomic claim + sentinel preservation | 0 | 0 |
| `HasActivePttBeExits` | `CopyEngine.cs` | Name prefix fix | 0 | 0 |
| `StillAtNonBePrice` | `CopyEngine.cs` | Name prefix fix | 0 | 0 |
| `IsPttEntryOrderCancelTrigger` | `CopyEngine.cs` | AFP guard added | 0 | 0 |
| `HasActivePttBeTargets` | `CopyEngine.cs` | New helper | 0 | 1 |
| `IsFollowerPositionBlocked` | `CopyEngine.cs` | Signals 4, 5, 6 added | 0 | 0 (reuses existing) |
| `RunFollowerEvictPath` | `CopyEngine.cs` | Sentinel clear added | 0 | 0 |

**Total new fields:** 0  
**Total new helpers:** 1 (`HasActivePttBeTargets`)  
**Files modified:** 1 (`src/PropTraderTools/CopyEngine.cs`)  
**Lock() instances added:** 0  
**DateTime.Now usages added:** 0  

---

## 8. Follow-On Tickets Required

| Ticket | Priority | Description |
|--------|----------|-------------|
| VIOLATION-01-FIX | P1 | Extract Signals 5+6 from `IsFollowerPositionBlocked` into `IsBeSettleOrRetrySignalActive` helper. Brings parent CYC from 9 to 8. Update in-code CYC comment at L3178. |
| CONCERN-02-RENAME | P3 | Rename `IsPttBeStopActiveState` → `IsPttBeOrderActiveState`. Update all 3 call sites (HasActivePttBeExits, HasActivePttBeTargets, plus any others). |

---

PLAN_READY
