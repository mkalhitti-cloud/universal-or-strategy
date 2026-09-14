# B155 Tickets
## Epic: Abort stale drain payload on position-flat (DW-B154-04)

**Status:** TICKETS_COMPLETE
**Author:** PTT Architect
**Block:** B155
**Plan source:** `docs/brain/B155/02-architecture-plan.md` (REVIEW_PASS)
**Ticket count:** 1

---

# T1 — Abort drain payload when account position goes flat

**Defect:** DW-B154-04 (P1 — directly causes unintended position re-entry)
**Spec requirement IDs:** DW-B154-04

## Files

| File | Role |
|------|------|
| `src/PropTraderTools/CopyEngine.cs` | Production fixes (a) (b) (c) (d) |
| `src/PropTraderTools/CopyEngineTests.cs` | Tests a, b, c |

---

## Atomic Changes (strictly ordered — implement in this sequence)

### (a) Fix 1 — Add `IsFlat` flat guard to `SubmitDrainedEntry`

**File:** `src/PropTraderTools/CopyEngine.cs`
**Location:** `private void SubmitDrainedEntry(string acctKey)` at ~line 8228

Find the existing method body (exact text, do NOT alter these lines):

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

Replace the entire method body with the following (lines (5) and (6) are the EXISTING code from (3) and (4) above — preserve them verbatim, only the inline comment numbers change):

```csharp
private void SubmitDrainedEntry(string acctKey)
{
    // B155 DW-B154-04: flat guard (3) aborts submit when account is flat.
    if (!_pendingDispatchDrains.TryRemove(acctKey, out var payload)) // (1)
        return;
    var follower = payload.FollowerAccount;
    if (follower == null) // (2)
        return;
    if (IsFlat(FindPosition(follower, payload.Instrument))) // (3) NEW — abort if flat
    {
        NinjaTrader.Code.Output.Process(
            "[DRAIN-ABORT-FLAT] acct=" + acctKey,
            NinjaTrader.NinjaScript.PrintTo.OutputTab1);
        foreach (var id in payload.DrainedOrderIds)  // (4) cleanup on abort path
            _drainOwnedOrderIds.TryRemove(id, out _);
        return;
    }
    SubmitEntryDirect(...)  // (5) — renumbered from (3), actual call preserved verbatim
    foreach (var id in payload.DrainedOrderIds) // (6) — renumbered from (4), actual code preserved verbatim
        _drainOwnedOrderIds.TryRemove(id, out _);
}
```

**Rules:**
- Lines (5) and (6) are the EXISTING code from original (3) and (4). Do not alter them. Only the inline comment numbers change.
- `DrainedOrderIds` cleanup MUST run in BOTH the abort path (line 4) and the submit path (line 6).
- `"[DRAIN-ABORT-FLAT]"` is ASCII-only — do not introduce any non-ASCII characters.
- `FindPosition(follower, payload.Instrument)` — use the existing `FindPosition` helper. Do not access `acc.Positions` directly.

**CYC delta:** +1. Old CYC = 4, new CYC = 5. Within ceiling of 8.

**JS constraints:**
- JS-001: no `throw` — `IsFlat` and `FindPosition` are pure reads.
- JS-002: method is `void`.
- JS-003: no `lock()` — uses `ConcurrentDictionary.TryRemove`.
- JS-021: `FindPosition` reads `acc.Positions` from `OnOrderUpdate` context — NT8-safe.

---

### (b) Fix 2 — Add `AbortDrainOnFill` call in `TryEvictFollowerBeSlot`

**File:** `src/PropTraderTools/CopyEngine.cs`
**Location:** `private void TryEvictFollowerBeSlot(OrderEventArgs)` at ~line 1892

Find this exact line in the method's flat path (do NOT alter or move it):

```csharp
    _filledBeTargetCount.TryRemove(accName, out _);
```

Add the following block IMMEDIATELY AFTER that line (no blank line between them is acceptable; one blank line is also acceptable — match surrounding style):

```csharp
    // B155 DW-B154-04: abort any pending drain payload when position goes flat.
    // Prevents SubmitDrainedEntry from firing for a now-flat account.
    // AbortDrainOnFill handles TryRemove + DrainedOrderIds cleanup; no-op if no drain exists.
    AbortDrainOnFill(accName);
```

**Rules:**
- Do not move or modify `_filledBeTargetCount.TryRemove(accName, out _);`.
- `AbortDrainOnFill` is an existing private instance method — do NOT add it or modify it.
- This is a void call with no conditional branch.

**CYC delta:** 0 (void call, no branch). Method CYC stays at 6.

**JS constraints:** no `throw`, void call, no `lock()`.

---

### (c) Add `SubmitDrainedEntryTestable` internal seam

**File:** `src/PropTraderTools/CopyEngine.cs`
**Location:** Near existing drain test seams (~line 8250–8320, look for other `internal ...Testable` methods in the drain region).

Add the following one-liner (access modifier MUST be `internal`, not `private`):

```csharp
// B155 test seam — exposes private SubmitDrainedEntry for unit tests.
internal void SubmitDrainedEntryTestable(string acctKey) => SubmitDrainedEntry(acctKey);
```

**Rules:**
- `internal` is required — `CopyEngineTests.cs` uses `InternalsVisibleTo` to access it.
- Do not expose this method publicly.
- Place it near other `internal ...Testable` methods for consistency.

---

### (d) Confirm comment header on `SubmitDrainedEntry`

Verify that the comment `"B155 DW-B154-04: flat guard (3) aborts submit when account is flat."` appears at the top of the method body. This is already included in the replacement body from change (a) — confirm it is present after (a) is applied. No additional edit required if (a) was applied correctly.

---

## Tests (new — `src/PropTraderTools/CopyEngineTests.cs`)

### Test a — `SubmitDrainedEntry_ShouldAbort_WhenAccountIsFlat`

**Tag:** `[Fact]`

**Approach — choose one based on NT8 stub availability:**

**Option 1 (preferred — if NT8 Account/Position stubs are available):**
- Arrange: seed `_pendingDispatchDrains` with a payload where the follower account has a flat position (position == null or Quantity == 0); seed `_drainOwnedOrderIds` with the payload's order IDs.
- Act: call `engine.SubmitDrainedEntryTestable(acctKey)`.
- Assert:
  - `_pendingDispatchDrains` does NOT contain `acctKey` after the call (`TryRemove` consumed it).
  - `_drainOwnedOrderIds` does NOT contain any of the seeded drain order IDs (cleanup ran on abort path).
  - `SubmitEntryDirect` was NOT called (use a spy flag or mock if available).

**Option 2 (structural — if NT8 stubs are unavailable):**
- Use `System.Reflection` to verify `SubmitDrainedEntryTestable` exists on `CopyEngine` as a non-null `internal` instance method.
- Use source/IL inspection to confirm that the string literal `"DRAIN-ABORT-FLAT"` appears within `SubmitDrainedEntry` (confirming the abort log path exists in the compiled method body).

**What this test guards:** The flat-guard abort path in `SubmitDrainedEntry` — ensures no phantom entry fires after position goes flat, and cleanup runs on the abort path.

---

### Test b — `TryEvictFollowerBeSlot_ShouldCallAbortDrain_WhenPositionFlat`

**Tag:** `[Fact]`

**Approach — structural reflection:**
- Use `System.Reflection.BindingFlags.NonPublic | BindingFlags.Instance` to locate `TryEvictFollowerBeSlot` on `CopyEngine`.
- Read the method body source (or use IL inspection) to confirm `"AbortDrainOnFill"` appears in the method.
- Assert: the string `"AbortDrainOnFill"` is present in the method's IL or compiled body (confirming the call was not removed by a future refactor).

**What this test guards:** The proactive abort call in `TryEvictFollowerBeSlot` — ensures Fix 2 remains in place after future refactors.

---

### Test c — `AbortDrainOnFill_ShouldExist_AsPrivateInstanceHelper`

**Tag:** `[Fact]`

**Approach — existence contract via reflection:**
- `var m = typeof(CopyEngine).GetMethod("AbortDrainOnFill", BindingFlags.NonPublic | BindingFlags.Instance);`
- Assert: `m != null` (method exists).
- Assert: `m.ReturnType == typeof(void)`.
- Assert: `m.GetParameters().Length == 1` and the first parameter is of type `string`.

**What this test guards:** The existence of `AbortDrainOnFill` as a private instance string-keyed helper — the NT8-stub-free contract that ensures Fix 2's dependency does not silently disappear in future refactors.

---

## 7-Scan Checklist (engineer contract — ALL must pass before BUILD_PASS)

| Scan | Command | Required Result |
|------|---------|-----------------|
| SCAN-1 | `grep -rn "lock\s*(" src/PropTraderTools/` | 0 matches |
| SCAN-2 | `grep -rn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` | 0 matches |
| SCAN-3 | `grep -rn "[^\x00-\x7F]" src/PropTraderTools/CopyEngineTests.cs` | 0 matches |
| SCAN-4 | `lizard --CCN 8 src/PropTraderTools/CopyEngine.cs` | 0 warnings |
| SCAN-5 | `lizard --CCN 8 src/PropTraderTools/CopyEngineTests.cs` | 0 warnings |
| SCAN-6 | `dotnet build` | 0 errors, 0 warnings |
| SCAN-7 | `lizard` output for `SubmitDrainedEntry` function specifically | CYC = 5 |
| SCAN-8 | `lizard` output for `TryEvictFollowerBeSlot` function specifically | CYC = 6 |

**All 8 scans are required. A single failure blocks BUILD_PASS.**

---

## Verify (SIM gate — manual, post-build)

1. Enter a trade on leader — confirm follower copies are live with drain active.
2. Press BE ALL — PTT-BE-Stop orders go Working on all accounts.
3. Wait for BE stops to trigger and close the position (account goes flat).
4. Confirm NO new entry fires on any follower account in the next 3 seconds.
5. Check output tab — `"[DRAIN-ABORT-FLAT] acct=<acctName>"` MUST appear for the flattened account (confirms Fix 1 safety net fired, or Fix 2 proactive abort cleared it before Fix 1 reached it).
6. Confirm output does NOT show `"[DRAIN-TIMEOUT-SUBMIT]"` for the now-flat account within the 3-second window.

---

## Deferred (carry forward — do NOT implement in this ticket)

| ID | Description | Status |
|----|-------------|--------|
| DW-B154-05 | `IsPttCopyEntryFilled` CYC improvement | OPEN |
| DW-B154-06 | `TryTriggerBeRecovery` monitor | OPEN |
| DW-B153-05 | Stale CYC annotation at line 6675 | OPEN |
| DW-B153-06 | Orphaned dead code `UpdateLegTargetPrice` | OPEN |

---

## NT8 Key Facts (embedded per protocol)

- `AtmStrategyChangeStopTarget()` — StrategyBase-only. NOT AddOnBase. Cancel+resubmit is the AddOn pattern.
- `AtmStrategyCreate()` — StrategyBase-only. NOT AddOnBase.
- `Account.Change()` — AddOnBase available but silent no-op on ATM-owned brackets.
- `Account.Cancel()` + `Account.CreateOrder()` + `Submit()` — AddOnBase available. Correct bracket-change pattern.
- **No new NT8 API introduced by B155.** All calls are to existing local helpers (`IsFlat`, `FindPosition`, `AbortDrainOnFill`) or standard .NET concurrent collections.
