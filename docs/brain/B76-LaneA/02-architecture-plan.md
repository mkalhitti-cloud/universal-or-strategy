# B76-LaneA -- Architecture Plan
# Ph1 ptt-architect output

**Block**: B76-LaneA
**Date**: 2026-08-18
**Author**: ptt-architect (Ph1)
**Files in scope**:
  - `src/PropTraderTools/CopyEngine.cs`
  - `src/PropTraderTools/TradeCopierPanel.cs`
  - `src/PropTraderTools/TradeCopierAddOn.cs`
**Test file target**: `src/PropTraderTools/Tests/B76Tests.cs` (new)
**Pipeline verdict**: PENDING Ph2 review

---

## A. Problem Statement

Live trading session 2026-08-18 07:12 AM, MES SEP26, 4 accounts. Three P1/P2 bugs confirmed
by direct-engineer pre-pipeline test. All 3 are live-applied and verified. Pipeline run
formalises tests, CYC audit, and final sign-off.

### Bug #1 — HOTFIX-B76-FLATTEN-RACE-01 (P1 DANGEROUS, VERIFIED FIX)

Follower account -08 inverted to 1 Short after ATM BE stop fill.

Root cause chain:
1. ATM BE stop fills on ALL accounts (leader + followers) — all now flat.
2. NT8 position state lags (NT8_FULL_REFERENCE.md line 1721):
   `acc.Positions` still shows "1 Long" in the same `OnOrderUpdate` cycle.
3. BE stop fill on LEADER triggers `TryDispatchLeaderFlat` (IsNativeExitName path).
4. `FlattenOneAccount` reads `FindPosition()` -> stale "1 Long".
5. Submits PTT-Flatten Sell Market on already-flat follower -> INVERTS to 1 Short.

Fix: Add `posAfterCancel = FindPosition(acc, instrument)` AFTER `CancelAllAccountOrders`.
After the cancel round-trip, `acc.Positions` reflects the ATM fill. If qty=0 -> skip.
CYC: 4 -> 5.

### Bug #2 — HOTFIX-B76-FLATTEN-GUARD-01/02 (P1, VERIFIED FIX)

N PTT-Flatten orders submitted simultaneously (N = number of active brackets on account).

Root cause: Each bracket-cancel callback from NT8 fires `OnOrderUpdate`. Each fires
`TryDispatchLeaderFlat` -> `FlattenOneAccount`. No re-entry guard existed. Result: if account
had 3 brackets (Stop1, Stop2, Target1), 3 simultaneous PTT-Flatten market orders were submitted.

Fix iteration:
- v1: `_flattenInFlight` ConcurrentDictionary TryAdd/TryRemove. Failed: flag was cleared in
  `finally` before NT8 delivered cancel-ack callbacks. All N threads re-entered with flag clear.
- v2 (current): Scan `acc.Orders.ToList()` at method entry for an existing PTT-Flatten in
  Submitted/Accepted/Working state. NT8 order book is the authoritative in-flight signal --
  it persists until Filled/Cancelled, surviving all cancel-ack callbacks. CYC: 4->6.

### Bug #3 — HOTFIX-B76-POSSTATE-DEDUP-01 + POSSTATE-LEAK-01/02 (P1, VERIFIED FIX)

PositionStateChanged fired 16+ times per position event (16 False per close, 16+ True per entry).

Two root causes:
- **LEAK**: Stale `TradeCopierPanel` objects retained their `PositionStateChanged` subscriptions
  across F5 reloads. After N reloads = N handlers per fire. Fix: `DoInject` calls
  `stalePanel.Detach()` before grid removal. Also: `TradeCopierWindow.OnLoaded` calls
  `_engine.Unsubscribe()` first to drain all prior subscriptions (idempotent).
- **DEDUP**: `TryFirePositionState` had no guard -- fired on every qualifying fill regardless
  of whether `hasPos` actually changed. Fix: `_lastHasPos ConcurrentDictionary<string, int[]>`
  with `Interlocked.Exchange` CAS. Sentinel 2=unknown, 0=False, 1=True. First thread to write
  a new value fires; all others with same value return immediately.

### Bug #2-panel — HOTFIX-B76-ATM-TPL-CLASSNAME (P2, VERIFIED FIX)

`GetLeaderAtmTemplateName` returned `"AtmStrategy"` (the NT8 class name) when no template
was staged on ChartTrader. The primary path `ct.AtmStrategy.Name` returns the class-name
string when `AtmStrategy` is not null but no template is actively selected.
Live log: `[PTT-CLONE] SetCloneAtmCache: 'AtmStrategy' (empty=False)`.
Fix: class-name guard -- if returned name equals `"AtmStrategy"`, fall through to
AtmStrategySelector fallback. CYC unchanged (5).

**STATUS**: This fix was VERIFIED by the direct-engineer session but the code change to
`TradeCopierPanel.cs` still needs to be applied in Ph4a. All other 3 bugs are already live-applied.

---

## B. Exact Changes Applied (live, pre-pipeline)

### B1. CopyEngine.cs — FlattenOneAccount (lines 1861-1932)

New method body with:
1. **In-flight order-book guard** (HOTFIX-B76-FLATTEN-GUARD-01 v2): scan `acc.Orders.ToList()`
   for existing PTT-Flatten in Submitted/Accepted/Working. If found -> "flat-guard: in-flight skip".
2. **Pre-cancel fast-exit** (existing logic, unchanged): if pos null or qty=0 -> "flat skip".
3. **Post-cancel re-read** (HOTFIX-B76-FLATTEN-RACE-01): `posAfterCancel` after `CancelAllAccountOrders`.
   If posAfterCancel null or qty=0 -> "flat-race skip (pos cleared by bracket fill)".
4. **posAfterCancel used for action/qty** in `CreateOrder` path.
Header comment updated: CYC=6, documents both hotfixes.

### B2. CopyEngine.cs — TryFirePositionState (lines 1418-1444) + new field _lastHasPos (lines 187-188)

- `_lastHasPos ConcurrentDictionary<string, int[]>` field added.
- `TryFirePositionState`: Interlocked.Exchange CAS on `box[0]`. Prior==newVal -> return without firing.
- CYC: unchanged (filter narrowed, dedup guard is straight-line CAS).

### B3. TradeCopierAddOn.cs — DoInject stale panel removal

- Cast stale grid child to `TradeCopierPanel`, call `Detach()` before grid removal.

### B4. TradeCopierWindow.cs — OnLoaded idempotency

- `_engine.Unsubscribe()` added as first call in try block, draining prior subscriptions.

### B5. TradeCopierPanel.cs — GetLeaderAtmTemplateName (PENDING — not yet applied)

- Add class-name guard: if `n == "AtmStrategy"` fall through to AtmStrategySelector fallback.
- CYC unchanged (5).

---

## C. CYC Budget

| Method | File | CYC before | CYC after | Limit |
|--------|------|-----------|-----------|-------|
| `FlattenOneAccount` | CopyEngine.cs | 4 | 6 | ≤8 ✅ |
| `TryFirePositionState` | CopyEngine.cs | 2 | 2 | ≤8 ✅ |
| `GetLeaderAtmTemplateName` | TradeCopierPanel.cs | 5 | 5 | ≤8 ✅ |
| `DoInject` | TradeCopierAddOn.cs | N/A | unchanged | ≤8 ✅ |
| `OnLoaded` | TradeCopierWindow.cs | N/A | unchanged | ≤8 ✅ |

---

## D. JS-DNA Compliance Pre-Check

| Rule | Check | Result |
|------|-------|--------|
| JS-021 | No `lock()` added | ✅ (Interlocked.Exchange, ConcurrentDictionary) |
| JS-001 | No `throw new` in hot path | ✅ |
| JS-002 | No `return null` added | ✅ |
| JS-033 | No `async void` | ✅ |
| ASCII-only | All new string literals are ASCII | ✅ |

---

## E. Out of Scope

| Item | Reason |
|------|--------|
| DW-B66-BE-01 | Director decision pending |
| DW-B66-C-02 | Separate investigation |
| DW-B63-01 | Root cause not isolated |
| DW-B75-01/02/03/04 | P2 housekeeping, defer |
| Issue #1: NT8 "Cancellation rejected" popup | NT8-internal, no fix possible |

---

## F. Test Plan

New test file: `src/PropTraderTools/Tests/B76Tests.cs`

### Group 1 — FlattenOneAccount (HOTFIX-B76-FLATTEN-RACE-01 + FLATTEN-GUARD-01 v2)

| ID | What | How |
|----|------|-----|
| T_B76_01 | Method exists and is non-public instance | Reflection |
| T_B76_02 | Body contains string "flat-guard: in-flight skip" | IL / method body |
| T_B76_03 | Body contains string "flat-race skip" | IL / method body |
| T_B76_04 | Body contains TWO FindPosition call sites | IL call-site count |
| T_B76_05 | CancelAllAccountOrders IL offset < second FindPosition IL offset | IL offset ordering |
| T_B76_06 | Header comment CYC=6 (updated from CYC=4) | Method IL / source |

### Group 2 — TryFirePositionState dedup (HOTFIX-B76-POSSTATE-DEDUP-01)

| ID | What | How |
|----|------|-----|
| T_B76_07 | `_lastHasPos` field exists as ConcurrentDictionary | Reflection field check |
| T_B76_08 | TryFirePositionState body references Interlocked.Exchange | IL call-site check |
| T_B76_09 | TryFirePositionState returns without invoking when hasPos unchanged | IL / logic check |

### Group 3 — GetLeaderAtmTemplateName class-name guard (HOTFIX-B76-ATM-TPL-CLASSNAME)

| ID | What | How |
|----|------|-----|
| T_B76_10 | null chart -> string.Empty (regression) | Direct call |
| T_B76_11 | Method body contains string literal "AtmStrategy" as comparison guard | Reflection body |
| T_B76_12 | Method does NOT return string literal "AtmStrategy" directly | IL check |

**Minimum bar**: 12 [Fact] tests, all passing.
