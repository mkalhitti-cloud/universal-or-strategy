# BWAVE-NEXT LaneBRepair-R3 — Tickets
**Status**: TICKETS_COMPLETE  
**Plan source**: `02-architecture-plan.md` (REVIEW_PASS, branch `bwave-next-lane-b`, baseline 340b778a)  
**Author**: ptt-architect  
**Date**: 2026-08-22

---

## T1 — R3-F1 + R3-F2 + R3-V1 (single ticket, all R3 items)

**Title**: R3-F1 + R3-F2 + R3-V1 repair on bwave-next-lane-b  
**Spec req IDs**: R3-F1 (P1), R3-F2 (P1), R3-V1 (VERIFY ONLY — DISMISSED)  
**Branch**: `bwave-next-lane-b`

---

### Files Touched

| File | Action |
|------|--------|
| `src/PropTraderTools/Tests/BwaveNextLaneBTests.cs` | R3-F1: single-line BindingFlags fix at ~line 172 |
| `src/PropTraderTools/CopyEngine.cs` | R3-F2: statement reorder in `SubmitDrainedEntry` ~lines 6631-6651 |

---

### Execution Order Within T1

1. **Verify R3-F1** — read `CopyEngine.cs` line 3703, confirm `private static`
2. **Apply R3-F1** — change `BwaveNextLaneBTests.cs` line ~172 (details below)
3. **Verify R3-F2** — read `CopyEngine.cs` lines 6627-6651, confirm cleanup precedes submit
4. **Apply R3-F2** — reorder statements in `SubmitDrainedEntry` (details below)
5. **Document R3-V1** — record DISMISSED verdict in `ticket-1-completion.md`
6. **Run 7-scan checklist** — all 7 scans must pass before reporting done
7. **Write `ticket-1-completion.md`** — completion artifact (required)

---

## R3-F1 — BindingFlags Fix (BwaveNextLaneBTests.cs)

### Problem

`FindFollowerEntryOrder` is declared `private static` at `CopyEngine.cs` line 3703:

```csharp
private static Order? FindFollowerEntryOrder(Account follower, Instrument instrument)
```

The test at `BwaveNextLaneBTests.cs` line ~172 reflects it via:

```csharp
var method = EngineType.GetMethod("FindFollowerEntryOrder", Priv);
```

Where `Priv` (line 15) is:

```csharp
private static readonly BindingFlags Priv =
    BindingFlags.NonPublic | BindingFlags.Instance;
```

`BindingFlags.Instance` does not match a `static` method. `GetMethod` returns `null`.  
`Assert.NotNull(method)` then fails — the test is broken by incorrect reflection flags.

### Fix

**File**: `src/PropTraderTools/Tests/BwaveNextLaneBTests.cs`  
**Line**: ~172 (the single `GetMethod("FindFollowerEntryOrder", ...)` call)  
**Constraint**: Do NOT modify the `Priv` constant at line 15 — it is shared by all other `GetMethod` calls that correctly target instance methods.

**Before** (WRONG):
```csharp
var method = EngineType.GetMethod("FindFollowerEntryOrder", Priv);
```

**After** (CORRECT):
```csharp
var method = EngineType.GetMethod(
    "FindFollowerEntryOrder",
    BindingFlags.NonPublic | BindingFlags.Static);
```

### Method Signature Affected

No production method signature changes. Test scaffolding only.  
`FindFollowerEntryOrder` in `CopyEngine.cs` is **not modified**.

### CYC Impact

Zero. No branches added. Test file; CYC is not tracked.

### JS Rules That Apply

None (test file). No hot-path, no lock, no async void, no null return.

### xUnit Tests

| Test | Filter | Assert | Must Pass |
|------|--------|--------|-----------|
| `FindFollowerEntryOrder_AcceptsEntryName_ForCloneMode` | `--filter "FindFollowerEntryOrder"` | `Assert.NotNull(method)` does not throw | YES |

**Verify-first condition**: If `CopyEngine.cs` line 3703 reads `private static` — apply fix. If `private` (instance) — document STALE, no fix.  
**Result already confirmed**: STATIC CONFIRMED. Fix applies.

---

## R3-F2 — SubmitDrainedEntry Cleanup Reorder (CopyEngine.cs)

### Problem

In `SubmitDrainedEntry` (~lines 6631-6651), drain-owned order IDs are removed from `_drainOwnedOrderIds` **before** `SubmitEntryDirect` is called. If `SubmitEntryDirect` fails silently (NT8 rejects the replacement order), those IDs are permanently gone from drain tracking and cannot be recovered.

**As-found order** (WRONG):
1. `_pendingDispatchDrains.TryRemove(acctKey, out var payload)` — early-exit gate (correct)
2. `follower == null` guard — early-exit null check (correct)
3. `foreach (var id in payload.DrainedOrderIds) _drainOwnedOrderIds.TryRemove(id, out _)` — **PREMATURE**
4. `SubmitEntryDirect(...)` — submit happens **after** IDs already cleared

### Fix

**File**: `src/PropTraderTools/CopyEngine.cs`  
**Method**: `SubmitDrainedEntry`  
**Lines**: ~6631-6651  
**Action**: Move the `foreach` cleanup block to **after** `SubmitEntryDirect`. Update the header comment numbering and add R3-F2 rationale comment. No other changes.

**Verify-first condition**: Read lines 6627-6651. If `foreach ... _drainOwnedOrderIds.TryRemove` appears **before** `SubmitEntryDirect` — apply fix. If already correct (submit before cleanup) — document STALE, no fix.  
**Result already confirmed**: CONFIRMED buggy. Fix applies.

**Before** (WRONG — as-found):
```csharp
// CYC=4: (1) TryRemove fails early return, (2) FollowerAccount null early return,
//        (3) F3 cleanup foreach, (4) delegated to SubmitEntryDirect.
private void SubmitDrainedEntry(string acctKey)
{
    if (!_pendingDispatchDrains.TryRemove(acctKey, out var payload)) // (1)
        return;

    var follower = payload.FollowerAccount;
    if (follower == null) // (2)
        return;

    // F3-repair: clear drain-owned IDs now that drain is complete.
    foreach (var id in payload.DrainedOrderIds) // (3)
        _drainOwnedOrderIds.TryRemove(id, out _);

    SubmitEntryDirect( // (4) delegated
        follower,
        payload.Instrument,
        payload.Qty,
        payload.Price,
        payload.Action,
        payload.OrderType);
}
```

**After** (CORRECT — fixed):
```csharp
// CYC=4: (1) TryRemove fails early return, (2) FollowerAccount null early return,
//        (3) delegated to SubmitEntryDirect, (4) F3 cleanup foreach (after submit).
// R3-F2: cleanup moved after SubmitEntryDirect -- drain IDs preserved until submit completes.
private void SubmitDrainedEntry(string acctKey)
{
    if (!_pendingDispatchDrains.TryRemove(acctKey, out var payload)) // (1)
        return;

    var follower = payload.FollowerAccount;
    if (follower == null) // (2)
        return;

    SubmitEntryDirect( // (3) submit first -- drain IDs still in dict here
        follower,
        payload.Instrument,
        payload.Qty,
        payload.Price,
        payload.Action,
        payload.OrderType);

    // R3-F2: clear drain-owned IDs AFTER submit so IDs are preserved on submit failure.
    foreach (var id in payload.DrainedOrderIds) // (4)
        _drainOwnedOrderIds.TryRemove(id, out _);
}
```

### Method Signature

```csharp
private void SubmitDrainedEntry(string acctKey)
```

Signature unchanged. No new parameters, no return-type change.

### CYC

`SubmitDrainedEntry` CYC = 4 before and after. Decision points are unchanged:
1. `TryRemove` early return
2. `follower == null` early return
3. `SubmitEntryDirect` (delegate — counted as 1 branch in project convention)
4. `foreach` loop body

### JS Rules That Apply

| Rule | Constraint | Status |
|------|-----------|--------|
| JS-021 | No `lock()` | Not present — satisfied |
| JS-001 | No `throw` in hot path | Not added — satisfied |
| JS-002 | No `return null` | Not added — satisfied |
| JS-033 | No `async void` (non-event-handler) | Not added — satisfied |

### NT8 Hard Constraints

| Constraint | Status |
|-----------|--------|
| No `try/catch` added | Satisfied — statement reorder only |
| No `CreateOrder` without `PTT-` prefix | Not introduced |
| No `DateTime.Now` | Not introduced |
| No `lock()` | Not present |

### xUnit Tests

The existing drain integration tests serve as structural regression verification:

| Test | Filter key | What it asserts |
|------|-----------|-----------------|
| `DrainThenDispatch` | `DrainThenDispatch` | Full drain flow reaches SubmitEntryDirect |
| `OnDrainCancelAck` | `OnDrainCancelAck` | Drain cancel-ack transitions correctly |
| `DrainWatchdog` | `DrainWatchdog` | Watchdog timeout path is correct |
| `ActiveOrders` | `ActiveOrders` | ActiveOrders list correctness preserved |
| `NakedDetector` | `NakedDetector` | Naked-entry detection unaffected |
| `AbortDrainOnFill` | `AbortDrainOnFill` | Drain aborts correctly on fill event |

All must pass after the reorder.

---

## R3-V1 — Order.Name Null Guard (DISMISSED — no source change)

### Verdict

**DISMISSED**. No code change required.

### Evidence

NT8 documentation confirms `Order.Name` is non-null for all live orders:

| Source | Line | Evidence |
|--------|------|---------|
| `NT8_FULL_REFERENCE.md` | 2106 | `CreateOrder(... string name ...)` — `name` is a required `string` at every creation path |
| `NT8_ADDON_KNOWLEDGE.md` | 229 | `order.Name` accessed without null guard in all project code |
| `NT8_FULL_REFERENCE.md` | 770 | "properties will always reflect the current state of an order" |
| `NT8_FULL_REFERENCE.md` | 3023, 3468 | NT8 warns about null *Order objects*, not null Name on a live Order |
| `ActiveOrders` filter | — | Only returns live, non-null Order objects |

### Engineer Documentation Requirement

In `ticket-1-completion.md`, include the following verbatim text:

```
R3-V1 DISMISSED: NT8 docs confirm Order.Name non-null for live orders.
StartsWith is safe. No fix applied.
```

---

## 7-Scan Checklist (Engineer Contract)

The engineer MUST run all 7 scans before reporting T1 complete. Partial scan completion = ticket not done.

| Scan | Command | Required Result |
|------|---------|----------------|
| **SCAN-01** — lock() | `grep -r "lock(" src/ --include="*.cs"` | 0 results |
| **SCAN-02** — async void | `grep -rn "async void " src/ --include="*.cs"` | 0 results (non-event-handler) |
| **SCAN-03** — return null | `grep -rn "return null;" src/ --include="*.cs"` | Review for hot paths; none new in modified files |
| **SCAN-04** — CYC | `python scripts/complexity_audit.py` | `SubmitDrainedEntry` CYC <= 4 |
| **SCAN-05** — Build | `dotnet build` | 0 errors, 0 warnings |
| **SCAN-06** — Tests | `dotnet test --filter "DrainThenDispatch\|OnDrainCancelAck\|DrainWatchdog\|ActiveOrders\|NakedDetector\|AbortDrainOnFill\|FindFollowerEntryOrder"` | All pass |
| **SCAN-07** — NT8 Sync | `powershell -File scripts\ptt-sync-and-verify.ps1` | All files OK, 0 MISMATCH lines |

---

## Acceptance Criteria Checklist

The engineer must confirm each item before writing `ticket-1-completion.md`:

```
[ ] R3-F1: FindFollowerEntryOrder_AcceptsEntryName_ForCloneMode test passes
[ ] R3-F1: Priv constant at BwaveNextLaneBTests.cs line 15 is UNCHANGED
[ ] R3-F1: Only line ~172 modified in BwaveNextLaneBTests.cs
[ ] R3-F2: SubmitEntryDirect appears BEFORE foreach cleanup in SubmitDrainedEntry
[ ] R3-F2: _pendingDispatchDrains.TryRemove remains the FIRST statement in SubmitDrainedEntry
[ ] R3-F2: SubmitDrainedEntry CYC = 4 (complexity_audit.py confirms)
[ ] R3-F2: No try/catch added anywhere in the method
[ ] R3-F2: No new branches added
[ ] R3-V1: "R3-V1 DISMISSED: NT8 docs confirm Order.Name non-null for live orders. StartsWith is safe. No fix applied." documented in ticket-1-completion.md
[ ] SCAN-01: 0 lock() results
[ ] SCAN-02: 0 async void results (non-handler)
[ ] SCAN-03: no new return null in modified files
[ ] SCAN-04: SubmitDrainedEntry CYC <= 4
[ ] SCAN-05: dotnet build exits 0, 0 errors
[ ] SCAN-06: all 7 test-filter names pass
[ ] SCAN-07: ptt-sync-and-verify.ps1 exits 0, 0 MISMATCH
[ ] (long)(int)Environment.TickCount preserved (not changed to TickCount64)
[ ] .ToList() on ActiveOrders preserved (not removed)
[ ] No new try/catch in hot paths
```

---

## Completion Artifact

The engineer MUST write `docs/brain/BWAVE-NEXT/LaneBRepair-R3/ticket-1-completion.md` containing:

1. Per-item status: R3-F1 DONE / STALE, R3-F2 DONE / STALE, R3-V1 DISMISSED (verbatim text above)
2. Scan results (SCAN-01 through SCAN-07) with actual output snippet or "PASS"
3. Build output line count (0 errors, 0 warnings)
4. Test filter output (pass/fail per test name)
5. NT8 sync output (0 MISMATCH)
6. NT8 F5 result (PASS/FAIL)

---

## Deferred Backlog Carry-Forward

All open items from R2 carry forward unchanged. No new items from R3.

| ID | Description | Status |
|----|-------------|--------|
| DW-NEXT-B-01 | Drain key: extend from `acct` to `acct+instrument` | OPEN — future |
| DW-NEXT-B-02 | Preserve GTC/TIF in `PendingDispatchDrain` payload | OPEN — future |
| DW-NEXT-B-03 | Watchdog: option to resubmit on timeout rather than drop | OPEN — future |
| DW-NEXT-B-04 | `OnOrderUpdate` helper extraction — CYC reduction target | OPEN — future |

---

**TICKETS_COMPLETE**
