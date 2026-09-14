# B155 Architecture Plan
## Epic: Abort stale drain payload on position-flat (DW-B154-04)

**Status:** REVIEW_PENDING
**Author:** PTT Architect
**Defect:** DW-B154-04
**Block:** B155

---

## LANE-SPLIT GATE RESULT: SINGLE-PIPELINE

Q1. Same method or within 50 lines? → NO — Fix (a) in `SubmitDrainedEntry` (~line 8228), Fix (b) in `TryEvictFollowerBeSlot` (~line 1892).
Q2. Fix B depends on Fix A final design? → NO — independent surgeries on independent methods.
Q3. Each fix has standalone value? → YES — but spec mandates both as one ticket (belt-and-suspenders, same defect DW-B154-04).
Q4. Each fix has an independent SIM verification path? → **NO** — single shared SIM scenario (BE stop flattens account, no ghost entry).

**Gate result: Q4 = NO → SINGLE-PIPELINE. No lane split.**

---

## 1. ROOT CAUSE

After a BE ALL exit closes a follower account position, drain-held follower entry copies parked in `_pendingDispatchDrains` continue their countdown via `OnDrainCancelAck`. The cancel-ack handler decrements `PendingCancelCount`; when it reaches 0, `SubmitDrainedEntry` fires unconditionally and submits a new entry for an account that is now flat — re-opening a position the user did not intend.

The same submission can occur via the 2-second `TryDrainWatchdog` timeout path, which also calls `SubmitDrainedEntry` without checking flat state.

**Root observed in production log:**
- 9:13:40 PM — BE stops flatten all accounts.
- 9:13:42 PM — drain-released Sell entries re-open Short 3 on all accounts.

**Gap chain:**
1. `TryEvictFollowerBeSlot` fires on BE-stop Fill → clears `_pendingFollowerBeSlots` and `_beReplaceAttempts` → does **not** abort `_pendingDispatchDrains`.
2. Cancel acks from drain-owned cancel orders arrive → `OnDrainCancelAck` decrements → `SubmitDrainedEntry` fires.
3. `SubmitDrainedEntry` has no flat-position guard → `SubmitEntryDirect` is called → ghost entry fires.

---

## 2. SOLUTION DESIGN — Two-Fix Approach

### Fix 1 of 2 — Add flat guard to `SubmitDrainedEntry`

**Location:** `src/PropTraderTools/CopyEngine.cs` `SubmitDrainedEntry(string acctKey)` ~line 8228
**Primary gate:** This guard catches ALL paths that reach `SubmitDrainedEntry` regardless of timing — the normal countdown path (`OnDrainCancelAck`) and the 2s timeout path (`TryDrainWatchdog`).

**Current structure (lines 8228–8249):**
```csharp
private void SubmitDrainedEntry(string acctKey)
{
    if (!_pendingDispatchDrains.TryRemove(acctKey, out var payload)) // (1)
        return;
    var follower = payload.FollowerAccount;
    if (follower == null) // (2)
        return;
    SubmitEntryDirect(...)  // (3)
    foreach (var id in payload.DrainedOrderIds) // (4)
        _drainOwnedOrderIds.TryRemove(id, out _);
}
```

**Replace with:**
```csharp
private void SubmitDrainedEntry(string acctKey)
{
    if (!_pendingDispatchDrains.TryRemove(acctKey, out var payload)) // (1)
        return;
    var follower = payload.FollowerAccount;
    if (follower == null) // (2)
        return;
    if (IsFlat(FindPosition(follower, payload.Instrument)))     // (3) NEW flat guard
    {
        NinjaTrader.Code.Output.Process(
            "[DRAIN-ABORT-FLAT] acct=" + acctKey,
            NinjaTrader.NinjaScript.PrintTo.OutputTab1);
        foreach (var id in payload.DrainedOrderIds)             // (4) cleanup — abort path
            _drainOwnedOrderIds.TryRemove(id, out _);
        return;
    }
    SubmitEntryDirect(...)                                       // (5) renumbered — was (3)
    foreach (var id in payload.DrainedOrderIds)                 // (6) renumbered — was (4)
        _drainOwnedOrderIds.TryRemove(id, out _);
}
```

**Correctness notes:**
- `TryRemove` at (1) is atomic — payload is owned exclusively once control passes (1).
- `FindPosition(follower, payload.Instrument)` uses the `Account` object and `Instrument` from the already-extracted payload — both present, verified in pre-validated facts.
- `DrainedOrderIds` cleanup runs in **both** paths: abort (line 4) and submit (line 6). This is intentional — drain-owned order IDs must be cleaned regardless of submission outcome.
- Log string `"[DRAIN-ABORT-FLAT] acct="` is ASCII-only.

**CYC delta:** +1 branch (new `IsFlat` guard). Old CYC = 4, new CYC = 5. Within ceiling of 8. ✅

**JS rule compliance:**
- JS-001: no throw — `IsFlat` and `FindPosition` are pure reads. ✅
- JS-002: method is `void`. ✅
- JS-003: no `lock()` — uses `ConcurrentDictionary.TryRemove`. ✅
- JS-021: `FindPosition` reads `acc.Positions` from `OnOrderUpdate` context — NT8-safe. ✅
- ASCII-only: `"[DRAIN-ABORT-FLAT]"` is pure ASCII. ✅

---

### Fix 2 of 2 — Abort drain payload in `TryEvictFollowerBeSlot` when position goes flat

**Location:** `src/PropTraderTools/CopyEngine.cs` `TryEvictFollowerBeSlot(OrderEventArgs)` ~line 1892
**Role:** Proactive early abort — fires before `HandleDrainTerminalState` in `OnOrderUpdate` (line 1686 < line 1722). Removes the drain payload from `_pendingDispatchDrains` before the countdown can complete.

**Existing flat-path (lines 1903–1910, condensed):**
```csharp
if (isFilled && !IsFlat(FindPosition(o.Account, o.Instrument))) return;
// ... flat path: clear _pendingFollowerBeSlots, _beReplaceAttempts, _filledBeTargetCount ...
_filledBeTargetCount.TryRemove(accName, out _);
// NEW insertion point:
```

**Add immediately after `_filledBeTargetCount.TryRemove(accName, out _);`:**
```csharp
// B155 DW-B154-04: abort any pending drain payload when position goes flat.
// Prevents SubmitDrainedEntry from firing for an account that is now flat.
// AbortDrainOnFill handles cleanup: TryRemove(_pendingDispatchDrains) + DrainedOrderIds cleanup.
AbortDrainOnFill(accName);
```

**CYC delta:** 0 — `AbortDrainOnFill` is a void method call, not a conditional branch. CYC remains 6. ✅

**JS rule compliance:**
- JS-003: no `lock()`. ✅
- JS-001: no throw. ✅
- `AbortDrainOnFill` signature: `private void AbortDrainOnFill(string acctKey)` — matches the call site. ✅

---

## 3. CALL ORDERING ANALYSIS

`TryEvictFollowerBeSlot` (line 1686) fires **before** `HandleDrainTerminalState` (line 1722) in `OnOrderUpdate`.

**On a BE-stop Fill that causes flat:**
1. `TryEvictFollowerBeSlot` fires → flat guard passes → **Fix 2**: `AbortDrainOnFill(accName)` called → `_pendingDispatchDrains.TryRemove` succeeds → payload removed → `_drainOwnedOrderIds` cleaned.
2. `HandleDrainTerminalState` fires → drain already removed → `AbortDrainOnFill` is a no-op (`TryRemove` on empty slot returns `false`). No double-cleanup concern.

**On late cancel acks arriving after flat (countdown path):**
3. `OnDrainCancelAck` fires → `_pendingDispatchDrains.TryGetValue` → **not found** (Fix 2 already removed it) → returns. No submission.

**Race window (Fix 2 not yet fired, cancel ack arrives first):**
4. `SubmitDrainedEntry` is called → `TryRemove` succeeds → `IsFlat` guard fires (**Fix 1**) → logs `[DRAIN-ABORT-FLAT]` → cleans `DrainedOrderIds` → returns. No submission.

**TryDrainWatchdog timeout (2s) path:**
5. `TryDrainWatchdog` → `SubmitDrainedEntry` → same Fix 1 safety net applies.

Fix 2 is the **proactive abort** (early removal). Fix 1 is the **safety net** (last-line guard before submission). Neither alone is sufficient; together they are belt-and-suspenders with no double-cleanup hazard.

---

## 4. TESTABLE SEAM

Add near existing drain test seams in `CopyEngine.cs`:

```csharp
// B155 test seam — exposes private SubmitDrainedEntry for unit tests.
internal void SubmitDrainedEntryTestable(string acctKey) => SubmitDrainedEntry(acctKey);
```

**Access modifier:** `internal` — visible to `CopyEngineTests.cs` within the assembly. Does not expose to external callers.

---

## 5. TEST PLAN

**File:** `src/PropTraderTools/CopyEngineTests.cs`

### Test a — `SubmitDrainedEntry_ShouldAbort_WhenAccountIsFlat`
**What it asserts:**
- Arrange: seed `_pendingDispatchDrains` with a payload where follower account is flat (position == null or Quantity == 0); seed `_drainOwnedOrderIds` with the payload's order IDs.
- Act: call `SubmitDrainedEntryTestable(acctKey)`.
- Assert:
  - `_pendingDispatchDrains` does NOT contain `acctKey` after the call (TryRemove consumed it).
  - `_drainOwnedOrderIds` does NOT contain any of the seeded drain order IDs (cleanup ran on abort path).
  - `SubmitEntryDirect` was NOT called (use a spy or flag if NT8 stubs unavailable; structural reflection otherwise).
- **Note:** If full NT8 stubs are unavailable, use structural reflection to verify the `IsFlat` guard exists as a conditional branch before `SubmitEntryDirect` in `SubmitDrainedEntry`.

### Test b — `TryEvictFollowerBeSlot_ShouldCallAbortDrain_WhenPositionFlat`
**What it asserts:**
- Structural test: verify that `AbortDrainOnFill` is invoked (or reachable) from `TryEvictFollowerBeSlot`'s flat path.
- If full NT8 event stubs are unavailable: use reflection to confirm that the body of `TryEvictFollowerBeSlot` contains a call to `AbortDrainOnFill` (method reference search on compiled IL or source text scan in test context).

### Test c — `AbortDrainOnFill_ShouldExist_AsPrivateInstanceHelper`
**What it asserts:**
- Existence contract: `AbortDrainOnFill` is a `private` instance method with signature `void AbortDrainOnFill(string acctKey)`.
- Verified via `typeof(CopyEngine).GetMethod("AbortDrainOnFill", BindingFlags.NonPublic | BindingFlags.Instance)` — not null.
- This is the NT8-stub-free guard that ensures the helper Fix 2 depends on continues to exist after future refactors.

---

## 6. FILES MODIFIED

| File | Change |
|------|--------|
| `src/PropTraderTools/CopyEngine.cs` | Fix 1: flat guard in `SubmitDrainedEntry` (~line 8228) |
| `src/PropTraderTools/CopyEngine.cs` | Fix 2: `AbortDrainOnFill` call in `TryEvictFollowerBeSlot` (~line 1892) |
| `src/PropTraderTools/CopyEngine.cs` | Testable seam: `SubmitDrainedEntryTestable` |
| `src/PropTraderTools/CopyEngineTests.cs` | Tests a, b, c |

**Not modified:** `PttContracts.cs` — `PendingDispatchDrain.Instrument` already exists. No new contract types required.

---

## 7. SCAN GATES

| Gate | Command | Required Result |
|------|---------|-----------------|
| SCAN-7 | `lizard --CCN 8 src/PropTraderTools/CopyEngine.cs` | 0 warnings |
| SCAN-8 | `grep -rn "lock\s*(" src/PropTraderTools/` | 0 matches |
| SCAN-9 | ASCII byte scan of `CopyEngine.cs` and `CopyEngineTests.cs` | 0 bytes > 127 |
| SCAN-10 | `dotnet build` | 0 errors, 0 warnings |

**CYC annotations (post-fix):**
- `SubmitDrainedEntry`: CYC = 5 (was 4, +1 for flat guard)
- `TryEvictFollowerBeSlot`: CYC = 6 (unchanged — void call adds 0 branches)
- `AbortDrainOnFill`: CYC unchanged (not modified)

---

## 8. SIM VERIFY GATE

Single verification scenario (SINGLE-PIPELINE):

1. Enter a trade on leader account — confirm follower copies are live with drain active.
2. Press BE ALL — PTT-BE-Stop orders go Working on all accounts.
3. Wait for BE stops to trigger and close the position — account goes flat.
4. **Assert:** No new entry fires in the next 3 seconds on any follower account.
5. **Assert:** Output tab shows `[DRAIN-ABORT-FLAT] acct=<acctName>` for the flattened account — confirms drain was aborted via Fix 1 safety net (or Fix 2 proactive abort cleared it without reaching Fix 1).
6. **Assert:** Output does NOT show `[DRAIN-TIMEOUT-SUBMIT]` or any `SubmitEntryDirect` log for the now-flat account within the 3-second window.

---

## 9. DEFERRED BACKLOG (carried forward — READ ONLY)

| ID | Description | Status |
|----|-------------|--------|
| DW-B154-04 | Abort stale drain payload on position-flat | **CLOSED — addressed by B155** |
| DW-B154-05 | `IsPttCopyEntryFilled` CYC improvement | OPEN |
| DW-B154-06 | `TryTriggerBeRecovery` monitor | OPEN |
| DW-B153-05 | Stale CYC annotation at line 6675 | OPEN |
| DW-B153-06 | Orphaned dead code `UpdateLegTargetPrice` | OPEN |

---

## 10. KEY NT8 FACTS (embedded per protocol)

- `AtmStrategyChangeStopTarget()` — StrategyBase-only. NOT AddOnBase. Cancel+resubmit is the AddOn pattern.
- `AtmStrategyCreate()` — StrategyBase-only. NOT AddOnBase.
- `Account.Change()` — AddOnBase available but silent no-op on ATM-owned brackets.
- `Account.Cancel()` + `Account.CreateOrder()` + `Submit()` — AddOnBase available. Correct bracket-change pattern.
- **No new NT8 API introduced by B155.** All calls are to existing local helpers (`IsFlat`, `FindPosition`, `AbortDrainOnFill`) or standard .NET concurrent collections.

---

## 11. COMPONENT SUMMARY

| Component | Class | Method | File | Line (approx) |
|-----------|-------|--------|------|---------------|
| Fix 1 — flat guard | `CopyEngine` | `SubmitDrainedEntry(string)` | `CopyEngine.cs` | ~8228 |
| Fix 2 — proactive abort | `CopyEngine` | `TryEvictFollowerBeSlot(OrderEventArgs)` | `CopyEngine.cs` | ~1892 |
| Existing helper (unchanged) | `CopyEngine` | `AbortDrainOnFill(string)` | `CopyEngine.cs` | ~8254 |
| Existing helper (unchanged) | `CopyEngine` | `IsFlat(Position?)` | `CopyEngine.cs` | ~6446 |
| Existing helper (unchanged) | `CopyEngine` | `FindPosition(Account, Instrument)` | `CopyEngine.cs` | ~5208 |
| Test seam (new) | `CopyEngine` | `SubmitDrainedEntryTestable(string)` | `CopyEngine.cs` | near drain seams |
| Test a | `CopyEngineTests` | `SubmitDrainedEntry_ShouldAbort_WhenAccountIsFlat` | `CopyEngineTests.cs` | new |
| Test b | `CopyEngineTests` | `TryEvictFollowerBeSlot_ShouldCallAbortDrain_WhenPositionFlat` | `CopyEngineTests.cs` | new |
| Test c | `CopyEngineTests` | `AbortDrainOnFill_ShouldExist_AsPrivateInstanceHelper` | `CopyEngineTests.cs` | new |
