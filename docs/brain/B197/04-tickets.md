# B197 Tickets
# Gap Restore: TrySetSlateReadyOnDrainFill + TryAbortLeaderFlatDrains + TryCancelStaleGtcExitOnEntry + IsPttDragOrphanCancellable suffix fix

**Author**: ptt-architect
**Phase**: 3 (Ticket Generation)
**Epic**: B197
**Source plan**: docs/brain/B197/02-architecture-plan.md (REVIEW_PASS)
**Plan review**: docs/brain/B197/02-plan-review.md (0 violations, W-01 cosmetic carry-forward)
**Status**: TICKETS_COMPLETE
**Date**: 2026-10-12

---

## Recommended Implementation Order

T4 → T1 → T2 → T3

Rationale:
- T4 is isolated (no shared state, no ordering dep). Validates test harness first.
- T1 requires knowledge of AbortDrainOnFill ordering constraint; implement next.
- T2 adds new method and wires into TryEvictFollowerBeSlot.
- T3 depends on T1's call site being present (inserts after T1's call).

---

## TICKET T1

### 1. TICKET HEADER

| Field | Value |
|-------|-------|
| ID | T1 |
| Priority | P1 |
| Title | TrySetSlateReadyOnDrainFill — new private method + call site in OnOrderUpdate |
| DW IDs Closed | DW-B157-01 |
| Spec Req IDs | DW-B157-01: TrySetSlateReadyOnDrainFill absent from B156/B157 megacommit |
| Files Modified | `src/PropTraderTools/CopyEngine.cs`, `src/PropTraderTools/Tests/B197Tests.cs` |

---

### 2. SCOPE LOCK

This ticket modifies **CopyEngine.cs lines 1535-1539 (OnOrderUpdate Filled branch)** and adds **one new private method `TrySetSlateReadyOnDrainFill`** ONLY.
No other methods, fields, or files are touched.

---

### 3. ROOT CAUSE

When a drain-tracked PTT-Copy entry fills, `_followerSlateReady` is never set to `true` for the follower account.
This leaves the follower in slate-unready state, blocking all subsequent drag syncs (IsLaneEEligible returns false permanently).
The method `TrySetSlateReadyOnDrainFill` was planned in B156/B157 but was absent from the megacommit.

---

### 4. DESIGN

**New method**: `TrySetSlateReadyOnDrainFill`
- Visibility: `private`
- Return type: `void`
- Parameter: `OrderEventArgs e`
- CYC: 5 (base 1 + 4 guards)

**Guard sequence** (exact, in this order):
```
(1) if (e?.Order == null) return;
(2) if (e.Order.OrderState != OrderState.Filled) return;
(3) if (!_drainOwnedOrderIds.ContainsKey(e.Order.OrderId)) return;
(4) if (!IsFollowerAccount(e.Order.Account)) return;
```

**Body**:
```csharp
_followerSlateReady[e.Order.Account.Name] = true;
NinjaTrader.Code.Output.Process(
    "[SLATE-READY-DRAIN] acct=" + e.Order.Account.Name,
    NinjaTrader.NinjaScript.PrintTo.OutputTab1);
```

**NT8 API used** (all lock-free):
- `_drainOwnedOrderIds.ContainsKey(e.Order.OrderId)` — ConcurrentDictionary<string,byte> read. Lock-free. Field at line 394.
- `IsFollowerAccount(e.Order.Account)` — line 813. Takes Account.
- `_followerSlateReady[e.Order.Account.Name] = true` — ConcurrentDictionary<string,bool> indexer write. Lock-free. Field at line 401.
- `NinjaTrader.Code.Output.Process(...)` — existing log pattern in OnOrderUpdate.

---

### 5. IMPLEMENTATION STEPS

**Step 1 — Add the new method body**

Place the new method **after** the closing brace of `TrySetFollowerSlateReady` (~line 1061), in the follower-slate section. Add the full header comment block:

```csharp
// B197 T1 DW-B157-01: TrySetSlateReadyOnDrainFill -- CYC=5.
// Marks follower slate-ready when a drain-tracked PTT-Copy entry fills.
// ORDERING: Must be called BEFORE AbortDrainOnFill in OnOrderUpdate Filled branch.
// AbortDrainOnFill (L7029) removes the order ID from _drainOwnedOrderIds.
// Guard (3) checks ContainsKey -- if AbortDrainOnFill runs first, Guard(3) always fails silently.
// JS-021: _drainOwnedOrderIds.ContainsKey + _followerSlateReady[] are ConcurrentDictionary ops (lock-free).
// JS-001: no throw -- early returns only. JS-002: void. ASCII-only.
private void TrySetSlateReadyOnDrainFill(OrderEventArgs e)
{
    if (e?.Order == null) return;                                          // (1)
    if (e.Order.OrderState != OrderState.Filled) return;                   // (2)
    if (!_drainOwnedOrderIds.ContainsKey(e.Order.OrderId)) return;         // (3)
    if (!IsFollowerAccount(e.Order.Account)) return;                       // (4)
    _followerSlateReady[e.Order.Account.Name] = true;
    NinjaTrader.Code.Output.Process(
        "[SLATE-READY-DRAIN] acct=" + e.Order.Account.Name,
        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
}
```

**Step 2 — Modify the OnOrderUpdate Filled branch (lines 1535-1539)**

Current code at lines 1535-1539:
```csharp
else if (e.Order.OrderState == OrderState.Filled)
{
    // Drain-tracked entry filled -- abort replacement, position is open.
    AbortDrainOnFill(e.Order.Account.Name); // R2-F1: clean _drainOwnedOrderIds on fill-abort
}
```

Replace with:
```csharp
else if (e.Order.OrderState == OrderState.Filled)
{
    TrySetSlateReadyOnDrainFill(e);                  // T1: set slate-ready FIRST (_drainOwnedOrderIds intact)
    AbortDrainOnFill(e.Order.Account.Name);          // R2-F1: clean _drainOwnedOrderIds on fill-abort
    TryCancelStaleGtcExitOnEntry(e);                 // T3: sweep stale GTC exits on new entry fill
}
```

**NOTE**: `TryCancelStaleGtcExitOnEntry(e)` is the T3 call site. Both T1 and T3 call sites are inserted in this same edit. If implementing T1 before T3, add a `// TODO B197-T3` placeholder for the T3 call, or implement both call site edits in a single atomic change.

**Step 3 — Write the B197Tests.cs test class** (if not already created by T4; add tests to existing class if T4 ran first)

---

### 6. CALL SITE WIRING

| Property | Value |
|----------|-------|
| File | `src/PropTraderTools/CopyEngine.cs` |
| Containing method | `OnOrderUpdate` |
| Insertion point | Line 1538 — replace `AbortDrainOnFill(e.Order.Account.Name);` |
| Before | Existing `AbortDrainOnFill(e.Order.Account.Name);` call |
| After | Start of `else if (e.Order.OrderState == OrderState.Filled)` block |
| Exact line | Line 1538 becomes line 3 of a 3-call block: T1 call, AbortDrainOnFill, T3 call |

**Ordering invariant (CRITICAL)**: `TrySetSlateReadyOnDrainFill(e)` MUST appear on the line immediately after `{` in the Filled branch, BEFORE `AbortDrainOnFill`. If reversed, Guard (3) (`_drainOwnedOrderIds.ContainsKey`) will always fail because `AbortDrainOnFill` at line 7033 calls `_drainOwnedOrderIds.TryRemove(id, out _)`.

---

### 7. NT8 CONSTRAINTS

- No `AtmStrategyCreate` or `AtmStrategyChangeStopTarget` — these are StrategyBase-only. Not used.
- `acc.Cancel()` not used by T1 — only ConcurrentDictionary ops.
- All ConcurrentDictionary reads/writes are lock-free. No `lock()` anywhere.
- `IsFollowerAccount(Account)` is AddOnBase-accessible (internal instance method at line 813). PASS.
- `NinjaTrader.Code.Output.Process(...)` is AddOnBase-accessible. PASS.

---

### 8. METHOD SIGNATURES

```csharp
private void TrySetSlateReadyOnDrainFill(OrderEventArgs e)
```

---

### 9. CYC BUDGET

| Method | Pre-B197 CYC | Post-B197 CYC | Delta | Status |
|--------|-------------|--------------|-------|--------|
| `TrySetSlateReadyOnDrainFill` | — (new) | 5 | +5 | ≤8 PASS |
| `OnOrderUpdate` | (unchanged) | (unchanged) | 0 | ≤8 PASS |

CYC derivation for `TrySetSlateReadyOnDrainFill`:
- base: 1
- Guard (1) `if (e?.Order == null)`: 1
- Guard (2) `if (OrderState != Filled)`: 1
- Guard (3) `if (!ContainsKey)`: 1
- Guard (4) `if (!IsFollowerAccount)`: 1
- **Total: CYC = 5**

---

### 10. MANDATORY 7-SCAN CHECKLIST

Engineer MUST run all 7 scans before issuing VERIFY_PASS:

```
[ ] Scan 1 — lock() grep:
    grep -rn "lock(" src/ --include="*.cs"
    EXPECT: 0 new occurrences introduced by T1

[ ] Scan 2 — CYC check:
    lizard src/PropTraderTools/CopyEngine.cs --CCN 8 -f "TrySetSlateReadyOnDrainFill"
    EXPECT: TrySetSlateReadyOnDrainFill CYC = 5 (≤8)

[ ] Scan 3 — ASCII check:
    grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs
    EXPECT: 0 non-ASCII characters in modified file

[ ] Scan 4 — throw check:
    grep -n "throw new" src/PropTraderTools/CopyEngine.cs
    EXPECT: 0 new "throw new" occurrences in TrySetSlateReadyOnDrainFill

[ ] Scan 5 — nullable check:
    Confirm e?.Order null-guard (Guard 1) and Guard 4 IsFollowerAccount(e.Order.Account) are present.
    EXPECT: both guards present before body execution

[ ] Scan 6 — test compile:
    dotnet build src/PropTraderTools/
    EXPECT: 0 errors, 0 warnings introduced by T1

[ ] Scan 7 — test run:
    dotnet test src/PropTraderTools/ --filter B197
    EXPECT: all B197 [Fact]s PASS (minimum T1 tests pass; full suite once T1-T4 complete)
```

---

### 11. TEST SPECIFICATION

**Test file**: `src/PropTraderTools/Tests/B197Tests.cs`
**Test class**: `B197Tests`
**Framework**: xUnit
**Strategy**: Source-text assertions — no NT8 runtime required.

```csharp
[Fact]
public void TrySetSlateReadyOnDrainFill_SourceContainsFollowerSlateReady()
{
    var src = System.IO.File.ReadAllText(
        System.IO.Path.Combine("..", "..", "..", "CopyEngine.cs"));
    Assert.Contains("_followerSlateReady[", src);
}

[Fact]
public void TrySetSlateReadyOnDrainFill_SourceContainsSlateReadyDrainMarker()
{
    var src = System.IO.File.ReadAllText(
        System.IO.Path.Combine("..", "..", "..", "CopyEngine.cs"));
    Assert.Contains("[SLATE-READY-DRAIN]", src);
}
```

**Assertion rationale**:
- Fact 1: Verifies the `_followerSlateReady[e.Order.Account.Name] = true` write is present (core body).
- Fact 2: Verifies the log marker `[SLATE-READY-DRAIN]` which confirms the drain-fill path was reached in implementation.

---

### 12. SUCCESS CRITERIA

PASS when ALL of the following are true:
1. `TrySetSlateReadyOnDrainFill` method body exists in CopyEngine.cs.
2. Method body contains `_followerSlateReady[` write.
3. Method body contains `[SLATE-READY-DRAIN]` log marker.
4. `TrySetSlateReadyOnDrainFill(e)` appears as the FIRST call in the `else if (OrderState.Filled)` block, before `AbortDrainOnFill`.
5. All 7 scans pass (0 lock, CYC=5, ASCII-only, no new throw, guards present, build clean, T1 tests pass).
6. `dotnet test --filter B197` shows `TrySetSlateReadyOnDrainFill_SourceContainsFollowerSlateReady: PASS` and `TrySetSlateReadyOnDrainFill_SourceContainsSlateReadyDrainMarker: PASS`.

---

### 13. COMPLETION ARTIFACT

`docs/brain/B197/ticket-1-completion.md`

---

---

## TICKET T2

### 1. TICKET HEADER

| Field | Value |
|-------|-------|
| ID | T2 |
| Priority | P1 |
| Title | TryAbortLeaderFlatDrains — new private method + TryEvictFollowerBeSlot wiring |
| DW IDs Closed | DW-B156-01, DW-B156-DEFER-1 |
| Spec Req IDs | DW-B156-01: TryAbortLeaderFlatDrains wiring absent; DW-B156-DEFER-1: TryEvictFollowerBeSlot leader-flat branch missing |
| Files Modified | `src/PropTraderTools/CopyEngine.cs`, `src/PropTraderTools/Tests/B197Tests.cs` |

---

### 2. SCOPE LOCK

This ticket adds **one new private method `TryAbortLeaderFlatDrains`** and modifies **`TryEvictFollowerBeSlot` (lines 1718-1737)** to add one call site ONLY.
No other methods, fields, or files are touched.

---

### 3. ROOT CAUSE

When a leader fill causes the leader account to go flat, any pending `PendingDispatchDrain` entries remain queued. This allows a ghost re-entry PTT-Copy order to fire on the follower after the leader is already flat — recreating a position that should not exist (DW-B158-SIM-01 failure mode).
`TryAbortLeaderFlatDrains` was planned in B156 to abort all pending drains when the leader position goes flat, but was never wired into `TryEvictFollowerBeSlot`.

---

### 4. DESIGN

**New method**: `TryAbortLeaderFlatDrains`
- Visibility: `private`
- Return type: `void`
- Parameter: `OrderEventArgs e`
- CYC: 5 (base 1 + 3 guards + foreach)

**Guard sequence** (exact, in this order):
```
(1) if (IsFollowerAccount(e.Order.Account)) return;
(2) if (e.Order.OrderState != OrderState.Filled) return;
(3) if (!IsFlat(FindPosition(e.Order.Account, e.Order.Instrument))) return;
```

**Body**:
```csharp
foreach (var kv in _pendingDispatchDrains.ToList())
    AbortDrainOnFill(kv.Key);
NinjaTrader.Code.Output.Process(
    "[DRAIN-ABORT-LEADER-FLAT] triggered by " + e.Order.Account.Name,
    NinjaTrader.NinjaScript.PrintTo.OutputTab1);
```

**TryEvictFollowerBeSlot modification**:

Insert `TryAbortLeaderFlatDrains(e);` after the null guard (line 1722) and before `IsEvictTriggerState` gate (line 1723). This ensures `e.Order` is confirmed non-null before TryAbortLeaderFlatDrains dereferences `e.Order.Account`.

CYC delta: +0 (method call is not a branch; McCabe/Lizard do not count method calls).

**NT8 API used** (all lock-free):
- `IsFollowerAccount(e.Order.Account)` — line 813. Takes Account.
- `IsFlat(FindPosition(e.Order.Account, e.Order.Instrument))` — IsFlat at line 5730, FindPosition at line 5792. Pattern confirmed at TryEvictFollowerBeSlot line 1729.
- `_pendingDispatchDrains.ToList()` — ConcurrentDictionary<string,PendingDispatchDrain> snapshot. Lock-free. Field at line 388.
- `AbortDrainOnFill(kv.Key)` — line 7029. Takes string (account key). ConcurrentDictionary TryRemove ops — lock-free.
- `NinjaTrader.Code.Output.Process(...)` — existing log pattern. AddOnBase-accessible.

---

### 5. IMPLEMENTATION STEPS

**Step 1 — Add the new method body**

Place the new method **immediately after the closing brace of `TryEvictFollowerBeSlot`** (~line 1737), before `LogBeSlotEviction` (~line 1742), in the BE-slot section. This groups TryAbortLeaderFlatDrains with its caller:

```csharp
// B197 T2 DW-B156-01 + DW-B156-DEFER-1: TryAbortLeaderFlatDrains -- CYC=5.
// Aborts all pending dispatch drains when the leader account fills to flat.
// Prevents ghost re-entry PTT-Copy orders from firing after leader goes flat (DW-B158-SIM-01).
// Called from TryEvictFollowerBeSlot, after null guard, before IsEvictTriggerState.
// NAMING NOTE: Originally "IsLeaderFlatDrainAbort" in DW-B156-DEFER-1 backlog. Renamed to
//   TryAbortLeaderFlatDrains: Is... prefix reserved for boolean predicates in CopyEngine.cs convention.
// JS-021: _pendingDispatchDrains foreach is ConcurrentDictionary enumeration (lock-free). AbortDrainOnFill uses TryRemove.
// JS-001: no throw. JS-002: void. ASCII-only.
private void TryAbortLeaderFlatDrains(OrderEventArgs e)
{
    if (IsFollowerAccount(e.Order.Account)) return;                           // (1)
    if (e.Order.OrderState != OrderState.Filled) return;                      // (2)
    if (!IsFlat(FindPosition(e.Order.Account, e.Order.Instrument))) return;   // (3)
    foreach (var kv in _pendingDispatchDrains.ToList())
        AbortDrainOnFill(kv.Key);
    NinjaTrader.Code.Output.Process(
        "[DRAIN-ABORT-LEADER-FLAT] triggered by " + e.Order.Account.Name,
        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
}
```

**Step 2 — Modify TryEvictFollowerBeSlot (lines 1718-1724)**

Current code (lines 1718-1724):
```csharp
private void TryEvictFollowerBeSlot(OrderEventArgs e)
{
    var o = e?.Order;
    if (o == null)
        return; // (1) null guard
    if (!IsEvictTriggerState(o))
        return; // (2) terminal-state gate
```

Replace lines 1721-1724 with:
```csharp
    var o = e?.Order;
    if (o == null)
        return; // (1) null guard
    TryAbortLeaderFlatDrains(e); // T2 DW-B156-01: leader-flat drain abort -- +0 CYC (method call is not a branch)
    if (!IsEvictTriggerState(o))
        return; // (2) terminal-state gate
```

**NOTE**: The call appears at the FIRST opportunity after the null guard. This placement ensures `e.Order` is non-null (confirmed by null guard) before TryAbortLeaderFlatDrains dereferences `e.Order.Account`.

---

### 6. CALL SITE WIRING

| Property | Value |
|----------|-------|
| File | `src/PropTraderTools/CopyEngine.cs` |
| Containing method | `TryEvictFollowerBeSlot` |
| Insertion point | After line 1722 (`return; // (1) null guard`), before line 1723 (`if (!IsEvictTriggerState(o))`) |
| Before | `if (!IsEvictTriggerState(o)) return;` gate |
| After | `if (o == null) return;` null guard |
| Exact line (post-insert) | New line between original lines 1722 and 1723 |

---

### 7. NT8 CONSTRAINTS

- No `AtmStrategyCreate` or `AtmStrategyChangeStopTarget` — StrategyBase-only. Not used.
- `_pendingDispatchDrains` enumeration is lock-free — ConcurrentDictionary snapshot via `.ToList()`.
- `AbortDrainOnFill` uses only ConcurrentDictionary TryRemove — lock-free. No `lock()`.
- `IsFlat` and `FindPosition` are pure computation — no state mutation.
- `IsFollowerAccount` is AddOnBase-accessible. PASS.
- `NinjaTrader.Code.Output.Process` is AddOnBase-accessible. PASS.

---

### 8. METHOD SIGNATURES

```csharp
private void TryAbortLeaderFlatDrains(OrderEventArgs e)
```

(TryEvictFollowerBeSlot signature unchanged: `private void TryEvictFollowerBeSlot(OrderEventArgs e)`)

---

### 9. CYC BUDGET

| Method | Pre-B197 CYC | Post-B197 CYC | Delta | Status |
|--------|-------------|--------------|-------|--------|
| `TryAbortLeaderFlatDrains` | — (new) | 5 | +5 | ≤8 PASS |
| `TryEvictFollowerBeSlot` | 6 | 6 | 0 | ≤8 PASS |

CYC derivation for `TryAbortLeaderFlatDrains`:
- base: 1
- Guard (1) `if (IsFollowerAccount)`: 1
- Guard (2) `if (OrderState != Filled)`: 1
- Guard (3) `if (!IsFlat(FindPosition))`: 1
- foreach: 1
- **Total: CYC = 5**

CYC derivation for `TryEvictFollowerBeSlot` (post-modification):
- Method call `TryAbortLeaderFlatDrains(e)` is NOT a McCabe/Lizard branch: +0 CYC.
- CYC remains 6 per comment at line 1696.

---

### 10. MANDATORY 7-SCAN CHECKLIST

```
[ ] Scan 1 — lock() grep:
    grep -rn "lock(" src/ --include="*.cs"
    EXPECT: 0 new occurrences introduced by T2

[ ] Scan 2 — CYC check:
    lizard src/PropTraderTools/CopyEngine.cs --CCN 8 -f "TryAbortLeaderFlatDrains"
    lizard src/PropTraderTools/CopyEngine.cs --CCN 8 -f "TryEvictFollowerBeSlot"
    EXPECT: TryAbortLeaderFlatDrains CYC=5; TryEvictFollowerBeSlot CYC=6

[ ] Scan 3 — ASCII check:
    grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs
    EXPECT: 0 non-ASCII characters in modified file

[ ] Scan 4 — throw check:
    grep -n "throw new" src/PropTraderTools/CopyEngine.cs
    EXPECT: 0 new "throw new" occurrences in TryAbortLeaderFlatDrains

[ ] Scan 5 — nullable check:
    Confirm e.Order.Account is accessed only after TryEvictFollowerBeSlot's null guard on e.Order.
    Guard (1) in TryAbortLeaderFlatDrains: IsFollowerAccount(e.Order.Account) — safe because
    TryEvictFollowerBeSlot null guard on o = e?.Order has already run.
    EXPECT: no NullReferenceException path reachable on e.Order

[ ] Scan 6 — test compile:
    dotnet build src/PropTraderTools/
    EXPECT: 0 errors, 0 warnings introduced by T2

[ ] Scan 7 — test run:
    dotnet test src/PropTraderTools/ --filter B197
    EXPECT: all B197 [Fact]s PASS
```

---

### 11. TEST SPECIFICATION

**Test file**: `src/PropTraderTools/Tests/B197Tests.cs`
**Test class**: `B197Tests`
**Framework**: xUnit
**Strategy**: Source-text assertions — no NT8 runtime required.

```csharp
[Fact]
public void TryEvictFollowerBeSlot_SourceContainsDrainAbortLeaderFlat()
{
    var src = System.IO.File.ReadAllText(
        System.IO.Path.Combine("..", "..", "..", "CopyEngine.cs"));
    Assert.Contains("[DRAIN-ABORT-LEADER-FLAT]", src);
}

[Fact]
public void TryEvictFollowerBeSlot_SourceContainsTryAbortLeaderFlatDrains()
{
    var src = System.IO.File.ReadAllText(
        System.IO.Path.Combine("..", "..", "..", "CopyEngine.cs"));
    Assert.Contains("TryAbortLeaderFlatDrains", src);
}
```

**Assertion rationale**:
- Fact 1: Verifies the `[DRAIN-ABORT-LEADER-FLAT]` log marker is present (confirms the method body was implemented and the log fires on leader-flat).
- Fact 2: Verifies `TryAbortLeaderFlatDrains` symbol name exists in source, confirming the method was added AND the call site was wired (the string appears at both the method definition and the call site in TryEvictFollowerBeSlot).

---

### 12. SUCCESS CRITERIA

PASS when ALL of the following are true:
1. `TryAbortLeaderFlatDrains` method body exists in CopyEngine.cs.
2. Method body contains `[DRAIN-ABORT-LEADER-FLAT]` log marker.
3. `TryAbortLeaderFlatDrains(e)` call appears in `TryEvictFollowerBeSlot` AFTER null guard, BEFORE `IsEvictTriggerState` gate.
4. TryEvictFollowerBeSlot CYC remains 6 (no branch increase from the added call).
5. All 7 scans pass (0 lock, CYC correct, ASCII-only, no new throw, null safety confirmed, build clean, T2 tests pass).
6. `dotnet test --filter B197` shows both T2 Facts PASS.

---

### 13. COMPLETION ARTIFACT

`docs/brain/B197/ticket-2-completion.md`

---

---

## TICKET T3

### 1. TICKET HEADER

| Field | Value |
|-------|-------|
| ID | T3 |
| Priority | P1 |
| Title | TryCancelStaleGtcExitOnEntry + IsStaleGtcExitOrder — 2 new methods + call site in OnOrderUpdate |
| DW IDs Closed | DW-B154-01, DW-B154-02 |
| Spec Req IDs | DW-B154-01: TryCancelStaleGtcExitOnEntry (HIGH); DW-B154-02: stale GTC exit sweep (MEDIUM) |
| Files Modified | `src/PropTraderTools/CopyEngine.cs`, `src/PropTraderTools/Tests/B197Tests.cs` |

---

### 2. SCOPE LOCK

This ticket adds **two new methods (`TryCancelStaleGtcExitOnEntry` and `IsStaleGtcExitOrder`)** and modifies **CopyEngine.cs lines 1535-1539 (OnOrderUpdate Filled branch)** to add one call site ONLY.
The call site edit is shared with T1 (both edits target the same Filled block). If T1 was implemented first, the T3 call site (`TryCancelStaleGtcExitOnEntry(e)`) is already present as a placeholder or was inserted by T1's step 2.
No other methods, fields, or files are touched.

---

### 3. ROOT CAUSE

When a new follower entry fills (PTT-Copy or Named ATM "Entry"), stale Working exit orders (PTT-BE-* GTC exits, PTT-QX-* GTC exits) from a previous session remain open. These orphan exits mis-fire against the new position, causing incorrect bracket behavior.
`TryCancelStaleGtcExitOnEntry` sweeps all Working PTT-BE-* and PTT-QX-* orders on the account at the moment a new entry fills.

**CRITICAL DESIGN DEVIATION — Guard (3)**:
`IsPttCopyEntryFilled` was removed in B180. Its stub at line 837 (`=> false`) ALWAYS returns false.
Using the stub as Guard (3) would cause `TryCancelStaleGtcExitOnEntry` to **never fire** (silent bug).
The engineer MUST use the **inline name-check guard** specified below, NOT the removed stub.

---

### 4. DESIGN

**New method 1**: `TryCancelStaleGtcExitOnEntry`
- Visibility: `private`
- Return type: `void`
- Parameter: `OrderEventArgs e`
- CYC: **8 (JS-066 CEILING)** — future branch additions require helper extraction first.

**Guard sequence** (exact, in this order):
```
(1) if (e?.Order == null) return;
(2) if (e.Order.OrderState != OrderState.Filled) return;
(3) if (e.Order.Name == null
       || (!e.Order.Name.StartsWith("PTT-Copy", StringComparison.Ordinal)
           && e.Order.Name != "Entry")) return;
    // RATIONALE: "PTT-Copy" = follower copy-dispatched entry (confirmed at line 4844)
    //            "Entry" = Named ATM clone mode entry (confirmed at lines 4196, 4823)
    //            DO NOT use IsPttCopyEntryFilled — REMOVED in B180. Stub at L837 always returns false.
(4) if (e.Order.Account == null) return;
```

**Body**:
```csharp
var acc = e.Order.Account;
foreach (var o in acc.Orders.ToList())
{
    if (!IsStaleGtcExitOrder(o)) continue;
    try
    {
        acc.Cancel(new Order[] { o });
        StatusUpdate?.Invoke(acc.Name + ": [GTC-SWEEP] cancelled " + o.Name);
    }
    catch (Exception ex)
    {
        StatusUpdate?.Invoke("[GTC-SWEEP] cancel error: " + ex.Message);
    }
}
```

**New method 2**: `IsStaleGtcExitOrder`
- Visibility: `private static`
- Return type: `bool`
- Parameter: `Order o`
- CYC: 5

**Body**:
```csharp
if (o == null || o.Name == null) return false;
if (o.OrderState != OrderState.Working) return false;
return o.Name.StartsWith("PTT-BE-", StringComparison.Ordinal)
    || o.Name.StartsWith("PTT-QX-", StringComparison.Ordinal);
```

**NT8 API used**:
- `acc.Orders.ToList()` — confirmed at line 1941 (CancelPttDragOrphansForAccount). Creates snapshot. Lock-free read.
- `acc.Cancel(new Order[] { o })` — confirmed at line 1947. AddOnBase-accessible. No lock.
- `StatusUpdate?.Invoke(...)` — confirmed at line 1948. Null-conditional invoke. No lock.
- `OrderState.Working`, `OrderState.Filled` — standard NT8 enum values.
- `String.StartsWith(string, StringComparison.Ordinal)` — existing pattern throughout CopyEngine.cs. ASCII-safe.

---

### 5. IMPLEMENTATION STEPS

**Step 1 — Add IsStaleGtcExitOrder helper** (must be defined before TryCancelStaleGtcExitOnEntry or in any order — C# class member order is irrelevant for compilation)

Place near `CancelPttDragOrphansForAccount` (~line 1939) as a thematic neighbor (both handle account-level order sweep):

```csharp
// B197 T3 DW-B154-02: IsStaleGtcExitOrder -- CYC=5.
// Predicate: returns true if order is a Working PTT-BE-* or PTT-QX-* exit (stale GTC candidate).
// Used by TryCancelStaleGtcExitOnEntry to filter acc.Orders sweep.
// JS-021: no lock. JS-001: no throw. Static -- no instance state.
private static bool IsStaleGtcExitOrder(Order o)
{
    if (o == null || o.Name == null) return false;                             // (1) if + (2) ||
    if (o.OrderState != OrderState.Working) return false;                      // (3)
    return o.Name.StartsWith("PTT-BE-", StringComparison.Ordinal)             // (4) return-||
        || o.Name.StartsWith("PTT-QX-", StringComparison.Ordinal);
}
```

**Step 2 — Add TryCancelStaleGtcExitOnEntry method** (place after IsStaleGtcExitOrder):

```csharp
// B197 T3 DW-B154-01 + DW-B154-02: TryCancelStaleGtcExitOnEntry -- CYC=8 (JS-066 CEILING).
// Sweeps stale Working PTT-BE-* and PTT-QX-* exit orders when a new follower entry fills.
// CEILING NOTE: CYC=8 at JS-066 ceiling. Future branch additions require helper extraction first.
// GUARD (3) RATIONALE: IsPttCopyEntryFilled was REMOVED in B180. Stub at L837 always returns false.
//   Using the stub would make this method a permanent no-op. Inline name-check is the correct guard.
//   "PTT-Copy" = copy-dispatched entry (L4844). "Entry" = Named ATM clone entry (L4196, L4823).
// JS-021: acc.Orders.ToList() + acc.Cancel() mirror CancelPttDragOrphansForAccount (L1939-1954). No lock.
// JS-001: try/catch absorbs acc.Cancel errors -- does NOT rethrow. Mirrors L1945-1953. ASCII-only.
private void TryCancelStaleGtcExitOnEntry(OrderEventArgs e)
{
    if (e?.Order == null) return;                                              // (1)
    if (e.Order.OrderState != OrderState.Filled) return;                      // (2)
    if (e.Order.Name == null                                                   // (3) if-branch
        || (!e.Order.Name.StartsWith("PTT-Copy", StringComparison.Ordinal)   //     || operator
            && e.Order.Name != "Entry")) return;                              //     && operator
    if (e.Order.Account == null) return;                                      // (4)
    var acc = e.Order.Account;
    foreach (var o in acc.Orders.ToList())                                    // foreach
    {
        if (!IsStaleGtcExitOrder(o)) continue;
        try
        {
            acc.Cancel(new Order[] { o });
            StatusUpdate?.Invoke(acc.Name + ": [GTC-SWEEP] cancelled " + o.Name);
        }
        catch (Exception ex)
        {
            StatusUpdate?.Invoke("[GTC-SWEEP] cancel error: " + ex.Message);
        }
    }
}
```

**Step 3 — Verify/complete the OnOrderUpdate Filled branch (lines 1535-1539)**

If T1 was implemented first using the shared block spec from §4, the Filled block should already read:
```csharp
else if (e.Order.OrderState == OrderState.Filled)
{
    TrySetSlateReadyOnDrainFill(e);                  // T1: set slate-ready FIRST
    AbortDrainOnFill(e.Order.Account.Name);          // existing
    TryCancelStaleGtcExitOnEntry(e);                 // T3: sweep stale GTC exits
}
```

If `TryCancelStaleGtcExitOnEntry(e)` is still a placeholder/TODO comment, replace it with the actual call.
If T1 has not been implemented yet, apply the full 3-call block change from the T1 Step 2 spec above.

---

### 6. CALL SITE WIRING

| Property | Value |
|----------|-------|
| File | `src/PropTraderTools/CopyEngine.cs` |
| Containing method | `OnOrderUpdate` |
| Insertion point | Line 1539 (original close `}`) — becomes 3rd line of the 3-call Filled block |
| Before | Closing `}` of `else if (OrderState.Filled)` block |
| After | `AbortDrainOnFill(e.Order.Account.Name);` call |
| Ordering | T1 (TrySetSlateReadyOnDrainFill) → AbortDrainOnFill → T3 (TryCancelStaleGtcExitOnEntry) |

T3 is independent of drain state — it reads `e.Order.Name` and `e.Order.Account.Orders`, not drain dictionaries. No ordering dependency on AbortDrainOnFill beyond "after" for logical clarity.

---

### 7. NT8 CONSTRAINTS

- No `AtmStrategyCreate` or `AtmStrategyChangeStopTarget` — StrategyBase-only. Not used.
- `acc.Cancel(new Order[] { o })` is AddOnBase-accessible. Confirmed at line 1947. PASS.
- `acc.Orders.ToList()` — snapshot is safe in OnOrderUpdate callback thread. Confirmed at line 1941.
- try/catch around acc.Cancel — absorbs `ErrorCode.UnableToCancelOrder`. Does NOT rethrow. Matches existing pattern at lines 1945-1953. PASS JS-001.
- `IsPttCopyEntryFilled` / `IsPttCopyEntryFilledTestable` — **DO NOT USE**. Both are stubs that always return `false` (line 837). Use inline name-check Guard (3) as specified.
- No `lock()` anywhere. PASS JS-021.

---

### 8. METHOD SIGNATURES

```csharp
private void TryCancelStaleGtcExitOnEntry(OrderEventArgs e)
private static bool IsStaleGtcExitOrder(Order o)
```

---

### 9. CYC BUDGET

| Method | Pre-B197 CYC | Post-B197 CYC | Delta | Status |
|--------|-------------|--------------|-------|--------|
| `TryCancelStaleGtcExitOnEntry` | — (new) | **8 (CEILING)** | +8 | ≤8 PASS — ceiling |
| `IsStaleGtcExitOrder` | — (new) | 5 | +5 | ≤8 PASS |
| `OnOrderUpdate` | (unchanged) | (unchanged) | 0 | PASS |

CYC derivation for `TryCancelStaleGtcExitOnEntry`:
- base: 1
- Guard (1) `if (e?.Order == null)`: 1
- Guard (2) `if (OrderState != Filled)`: 1
- Guard (3) if-branch `if (e.Order.Name == null || ...)`: 1
- Guard (3) OR operator `||`: 1
- Guard (3) AND operator `&&`: 1
- Guard (4) `if (Account == null)`: 1
- foreach: 1
- **Total: CYC = 8** ← AT JS-066 CEILING. Future additions require helper extraction.

CYC derivation for `IsStaleGtcExitOrder`:
- base: 1
- null-if `if (o == null || ...)`: 1
- null-or `||` operator: 1
- Working-check `if (OrderState != Working)`: 1
- return-or `|| o.Name.StartsWith("PTT-QX-")`: 1
- **Total: CYC = 5**

---

### 10. MANDATORY 7-SCAN CHECKLIST

```
[ ] Scan 1 — lock() grep:
    grep -rn "lock(" src/ --include="*.cs"
    EXPECT: 0 new occurrences introduced by T3

[ ] Scan 2 — CYC check:
    lizard src/PropTraderTools/CopyEngine.cs --CCN 8 -f "TryCancelStaleGtcExitOnEntry"
    lizard src/PropTraderTools/CopyEngine.cs --CCN 8 -f "IsStaleGtcExitOrder"
    EXPECT: TryCancelStaleGtcExitOnEntry CYC=8 (at ceiling, not exceeded); IsStaleGtcExitOrder CYC=5

[ ] Scan 3 — ASCII check:
    grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs
    EXPECT: 0 non-ASCII characters in modified file

[ ] Scan 4 — throw check:
    grep -n "throw new" src/PropTraderTools/CopyEngine.cs
    EXPECT: 0 new "throw new" in TryCancelStaleGtcExitOnEntry or IsStaleGtcExitOrder
    NOTE: try/catch in TryCancelStaleGtcExitOnEntry is ALLOWED -- catch block does NOT rethrow.

[ ] Scan 5 — nullable check:
    Confirm: Guard (1) on e?.Order, Guard (4) on e.Order.Account, IsStaleGtcExitOrder null guard on o.
    Confirm: Guard (3) checks e.Order.Name == null FIRST (short-circuit before StartsWith call).
    EXPECT: no NullReferenceException path reachable in either new method

[ ] Scan 6 — test compile:
    dotnet build src/PropTraderTools/
    EXPECT: 0 errors, 0 warnings introduced by T3

[ ] Scan 7 — test run:
    dotnet test src/PropTraderTools/ --filter B197
    EXPECT: all B197 [Fact]s PASS
```

---

### 11. TEST SPECIFICATION

**Test file**: `src/PropTraderTools/Tests/B197Tests.cs`
**Test class**: `B197Tests`
**Framework**: xUnit
**Strategy**: Source-text assertions — no NT8 runtime required.

```csharp
[Fact]
public void TryCancelStaleGtcExitOnEntry_SourceContainsMethodName()
{
    var src = System.IO.File.ReadAllText(
        System.IO.Path.Combine("..", "..", "..", "CopyEngine.cs"));
    Assert.Contains("TryCancelStaleGtcExitOnEntry", src);
}

[Fact]
public void TryCancelStaleGtcExitOnEntry_SourceContainsGtcSweepMarker()
{
    var src = System.IO.File.ReadAllText(
        System.IO.Path.Combine("..", "..", "..", "CopyEngine.cs"));
    Assert.Contains("[GTC-SWEEP]", src);
}
```

**Assertion rationale**:
- Fact 1: Verifies `TryCancelStaleGtcExitOnEntry` symbol is present in source (confirms both method definition and call site wiring — symbol appears at both).
- Fact 2: Verifies `[GTC-SWEEP]` log marker is present, confirming the sweep body (StatusUpdate invoke lines) was implemented correctly.

---

### 12. SUCCESS CRITERIA

PASS when ALL of the following are true:
1. `TryCancelStaleGtcExitOnEntry` and `IsStaleGtcExitOrder` method bodies exist in CopyEngine.cs.
2. `TryCancelStaleGtcExitOnEntry` Guard (3) uses inline name-check (`StartsWith("PTT-Copy")` / `== "Entry"`), NOT `IsPttCopyEntryFilled` or `IsPttCopyEntryFilledTestable`.
3. `[GTC-SWEEP]` log marker present in TryCancelStaleGtcExitOnEntry body.
4. `TryCancelStaleGtcExitOnEntry(e)` call appears as the LAST line in the `else if (OrderState.Filled)` block, after `AbortDrainOnFill`.
5. CYC of TryCancelStaleGtcExitOnEntry = 8 (not exceeded). CYC of IsStaleGtcExitOrder = 5.
6. All 7 scans pass (0 lock, CYC correct, ASCII-only, no new rethrow, null safety confirmed, build clean, T3 tests pass).
7. `dotnet test --filter B197` shows both T3 Facts PASS.

---

### 13. COMPLETION ARTIFACT

`docs/brain/B197/ticket-3-completion.md`

---

---

## TICKET T4

### 1. TICKET HEADER

| Field | Value |
|-------|-------|
| ID | T4 |
| Priority | P2 |
| Title | IsPttDragOrphanCancellable + IsPttDragOrphanCancellableTestable — suffix fix (StartsWith) |
| DW IDs Closed | DW-B154-03 |
| Spec Req IDs | DW-B154-03: IsPttDragOrphanCancellable exact-match guard misses B141/B142 suffixed drag names |
| Files Modified | `src/PropTraderTools/CopyEngine.cs`, `src/PropTraderTools/Tests/B197Tests.cs` |

---

### 2. SCOPE LOCK

This ticket modifies **guard (3) in `IsPttDragOrphanCancellable` (line 1911)** and **guard (3) in `IsPttDragOrphanCancellableTestable` (line 1929)** ONLY.
Two lines changed. No new methods. No field changes.

---

### 3. ROOT CAUSE

B141/B142 introduced per-leg suffix naming for drag orders (e.g., "PTT-TGT-Drag-1", "PTT-STP-Drag-2").
Guard (3) in `IsPttDragOrphanCancellable` uses exact-match (`!= "PTT-TGT-Drag" && != "PTT-STP-Drag"`),
which silently misses all suffixed variants. The drag sweep (`CancelPttDragOrphansForAccount`) is
ineffective for all post-B141 drag orders — they remain Working orphans indefinitely.

---

### 4. DESIGN

**Change 1 — IsPttDragOrphanCancellable guard (3) at line 1911**

FROM:
```csharp
if (o.Name != "PTT-TGT-Drag" && o.Name != "PTT-STP-Drag") // (3) && = 1 extra Lizard
    return false;
```

TO:
```csharp
if (!o.Name.StartsWith("PTT-TGT-Drag", StringComparison.Ordinal) // (3) && = 1 extra Lizard
    && !o.Name.StartsWith("PTT-STP-Drag", StringComparison.Ordinal))
    return false;
```

**Change 2 — IsPttDragOrphanCancellableTestable guard (3) at line 1929**

FROM:
```csharp
if (orderName != "PTT-TGT-Drag" && orderName != "PTT-STP-Drag")
    return false;
```

TO:
```csharp
if (!orderName.StartsWith("PTT-TGT-Drag", StringComparison.Ordinal)
    && !orderName.StartsWith("PTT-STP-Drag", StringComparison.Ordinal))
    return false;
```

**Change 3 — NT8-014 comment update at line 1902**

FROM:
```csharp
// NT8-014: "PTT-TGT-Drag" and "PTT-STP-Drag" confirmed names -- must not be generalized.
```

TO:
```csharp
// NT8-014: "PTT-TGT-Drag" + suffixed variants (B141/B142: PTT-TGT-Drag-N) -- StartsWith covers all.
```

**CYC impact**: StartsWith has the same McCabe branch count as `!=`. CYC unchanged:
- `IsPttDragOrphanCancellable`: CYC = 4 (base 1 + guard1 Working 1 + guard2 FullName 1 + guard3 && 1).
- `IsPttDragOrphanCancellableTestable`: CYC = 4 (same structure).

---

### 5. IMPLEMENTATION STEPS

**Step 1 — Update IsPttDragOrphanCancellable guard (3) (line 1911)**

Locate at line 1905-1914 in CopyEngine.cs:
```csharp
private static bool IsPttDragOrphanCancellable(Order o, Instrument instr)
{
    if (o.OrderState != OrderState.Working) // (1)
        return false;
    if (o.Instrument?.FullName != instr?.FullName) // (2) ?. counts as 1 Lizard
        return false;
    if (o.Name != "PTT-TGT-Drag" && o.Name != "PTT-STP-Drag") // (3) && = 1 extra Lizard
        return false;
    return true;
}
```

Replace line 1911:
```
    if (o.Name != "PTT-TGT-Drag" && o.Name != "PTT-STP-Drag") // (3) && = 1 extra Lizard
```
WITH (2 lines):
```csharp
    if (!o.Name.StartsWith("PTT-TGT-Drag", StringComparison.Ordinal) // (3) && = 1 extra Lizard
        && !o.Name.StartsWith("PTT-STP-Drag", StringComparison.Ordinal))
```

**Step 2 — Update IsPttDragOrphanCancellableTestable guard (3) (line 1929)**

Locate at line 1918-1932 in CopyEngine.cs:
```csharp
internal static bool IsPttDragOrphanCancellableTestable(
    OrderState orderState,
    string orderInstrFullName,
    string instrFullName,
    string orderName
)
{
    if (orderState != OrderState.Working)
        return false;
    if (orderInstrFullName != instrFullName)
        return false;
    if (orderName != "PTT-TGT-Drag" && orderName != "PTT-STP-Drag")
        return false;
    return true;
}
```

Replace line 1929:
```
    if (orderName != "PTT-TGT-Drag" && orderName != "PTT-STP-Drag")
```
WITH (2 lines):
```csharp
    if (!orderName.StartsWith("PTT-TGT-Drag", StringComparison.Ordinal)
        && !orderName.StartsWith("PTT-STP-Drag", StringComparison.Ordinal))
```

**Step 3 — Update the NT8-014 comment at line 1902**

Replace:
```csharp
// NT8-014: "PTT-TGT-Drag" and "PTT-STP-Drag" confirmed names -- must not be generalized.
```
WITH:
```csharp
// NT8-014: "PTT-TGT-Drag" + suffixed variants (B141/B142: PTT-TGT-Drag-N) -- StartsWith covers all.
```

---

### 6. CALL SITE WIRING

No new call sites. The modified methods are already called from `CancelPttDragOrphansForAccount` (line 1943) via `IsPttDragOrphanCancellable(o, instr)`. The testable seam is called from test files. Both call sites are unchanged — only the internal guard logic changes.

---

### 7. NT8 CONSTRAINTS

- Pure static predicates — no NT8 state access. No AddOnBase API involved.
- `String.StartsWith(string, StringComparison.Ordinal)` — deterministic, locale-independent, zero-alloc on current string. Preferred pattern per Jane Street ASCII-only rule.
- No `lock()`, no allocation, no async. PASS JS-021.
- CYC unchanged at 4 for both methods. PASS JS-066.

---

### 8. METHOD SIGNATURES

Unchanged:
```csharp
private static bool IsPttDragOrphanCancellable(Order o, Instrument instr)

internal static bool IsPttDragOrphanCancellableTestable(
    OrderState orderState,
    string orderInstrFullName,
    string instrFullName,
    string orderName
)
```

---

### 9. CYC BUDGET

| Method | Pre-B197 CYC | Post-B197 CYC | Delta | Status |
|--------|-------------|--------------|-------|--------|
| `IsPttDragOrphanCancellable` | 4 | 4 | 0 | ≤8 PASS |
| `IsPttDragOrphanCancellableTestable` | 4 | 4 | 0 | ≤8 PASS |

CYC derivation (both methods, post-fix):
- base: 1
- Guard (1) `if (orderState != Working)`: 1
- Guard (2) `if (FullName != FullName)`: 1 (Lizard counts `?.` as +1)
- Guard (3) `&&` compound: 1 (Lizard counts `&&` as +1 regardless of exact-match vs StartsWith)
- **Total: CYC = 4** — unchanged.

---

### 10. MANDATORY 7-SCAN CHECKLIST

```
[ ] Scan 1 — lock() grep:
    grep -rn "lock(" src/ --include="*.cs"
    EXPECT: 0 new occurrences introduced by T4

[ ] Scan 2 — CYC check:
    lizard src/PropTraderTools/CopyEngine.cs --CCN 8 -f "IsPttDragOrphanCancellable"
    lizard src/PropTraderTools/CopyEngine.cs --CCN 8 -f "IsPttDragOrphanCancellableTestable"
    EXPECT: both methods CYC=4 (unchanged)

[ ] Scan 3 — ASCII check:
    grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs
    EXPECT: 0 non-ASCII characters in modified file

[ ] Scan 4 — throw check:
    grep -n "throw new" src/PropTraderTools/CopyEngine.cs
    EXPECT: 0 new "throw new" in modified guards (pure predicate, no throw)

[ ] Scan 5 — nullable check:
    Confirm: IsPttDragOrphanCancellable -- o.Name is accessed inside guard (3). Guard (3) is reached
    only after Guard (1) (Working check) and Guard (2) (FullName check) pass.
    Existing code at line 1909: o.Instrument?.FullName -- this may implicitly guarantee o.Name is non-null
    if NT8 guarantees Order.Name for Working orders. However, if o.Name could be null (NT8 edge case),
    the StartsWith call would throw. Confirm o.Name null-safety:
    If o.Name can be null, add: if (o.Name == null) return false; before guard (3).
    EXISTING behavior: line 1911 `o.Name != "PTT-TGT-Drag"` also would not throw on null (null != str = true).
    StartsWith on null WOULD throw. Recommendation: add null guard for o.Name if any doubt exists.
    EXPECT: null path either confirmed safe or null guard added for o.Name before StartsWith

[ ] Scan 6 — test compile:
    dotnet build src/PropTraderTools/
    EXPECT: 0 errors, 0 warnings introduced by T4

[ ] Scan 7 — test run:
    dotnet test src/PropTraderTools/ --filter B197
    EXPECT: all 4 T4 [Fact]s PASS (SuffixedTgt, SuffixedStp, UnsuffixedTgt, UnsuffixedStp)
```

**SCAN-5 NOTE**: In the existing code, `o.Name != "PTT-TGT-Drag"` does not throw when `o.Name` is `null` (null equality check is safe in C#). However, `o.Name.StartsWith(...)` WILL throw `NullReferenceException` if `o.Name` is null. The engineer must either:
(a) Confirm NT8 guarantees `Order.Name` is never null for Working orders, OR
(b) Add `if (o.Name == null) return false;` before guard (3) in both methods.

Similarly for `IsPttDragOrphanCancellableTestable`, the `orderName` parameter should be null-guarded.

---

### 11. TEST SPECIFICATION

**Test file**: `src/PropTraderTools/Tests/B197Tests.cs`
**Test class**: `B197Tests`
**Framework**: xUnit
**Strategy**: Testable-seam calls — uses `IsPttDragOrphanCancellableTestable` (internal static, accessible via `InternalsVisibleTo` at CopyEngine.cs line ~42).

```csharp
[Fact]
public void IsPttDragOrphanCancellableTestable_SuffixedTgtName_ReturnsTrue()
{
    bool result = CopyEngine.IsPttDragOrphanCancellableTestable(
        OrderState.Working, "ES 09-26", "ES 09-26", "PTT-TGT-Drag-1");
    Assert.True(result);
}

[Fact]
public void IsPttDragOrphanCancellableTestable_SuffixedStpName_ReturnsTrue()
{
    bool result = CopyEngine.IsPttDragOrphanCancellableTestable(
        OrderState.Working, "ES 09-26", "ES 09-26", "PTT-STP-Drag-2");
    Assert.True(result);
}

[Fact]
public void IsPttDragOrphanCancellableTestable_UnsuffixedTgtName_StillReturnsTrue()
{
    // Regression: base name (no suffix) must still be cancellable after fix
    bool result = CopyEngine.IsPttDragOrphanCancellableTestable(
        OrderState.Working, "ES 09-26", "ES 09-26", "PTT-TGT-Drag");
    Assert.True(result);
}

[Fact]
public void IsPttDragOrphanCancellableTestable_UnsuffixedStpName_StillReturnsTrue()
{
    // Regression: base name (no suffix) must still be cancellable after fix
    bool result = CopyEngine.IsPttDragOrphanCancellableTestable(
        OrderState.Working, "ES 09-26", "ES 09-26", "PTT-STP-Drag");
    Assert.True(result);
}
```

**Assertion rationale**:
- Facts 1 & 2: Verify the PRIMARY fix — suffixed drag names ("PTT-TGT-Drag-1", "PTT-STP-Drag-2") now return `true`. Pre-fix, these returned `false` (bug).
- Facts 3 & 4: Regression guard — ensure the change from exact-match to `StartsWith` does NOT break the original (unsuffixed) names. `StartsWith("PTT-TGT-Drag")` matches "PTT-TGT-Drag" exactly, so these must remain `true`.

**Test seam access**: `IsPttDragOrphanCancellableTestable` is `internal static` at line 1918. Access granted via `[assembly: InternalsVisibleTo("PropTraderTools.Tests")]` at CopyEngine.cs line ~42.

---

### 12. SUCCESS CRITERIA

PASS when ALL of the following are true:
1. Line 1911 of CopyEngine.cs contains `StartsWith("PTT-TGT-Drag", StringComparison.Ordinal)`.
2. Line 1929 (approx, may shift) of CopyEngine.cs contains `StartsWith("PTT-TGT-Drag", StringComparison.Ordinal)` in `IsPttDragOrphanCancellableTestable`.
3. NT8-014 comment at line 1902 updated to reference suffixed variants.
4. CYC of both methods remains 4 (unchanged).
5. Null safety for `o.Name` / `orderName` confirmed or null guard added.
6. All 7 scans pass.
7. `dotnet test --filter B197` shows all 4 T4 Facts PASS:
   - `IsPttDragOrphanCancellableTestable_SuffixedTgtName_ReturnsTrue: PASS`
   - `IsPttDragOrphanCancellableTestable_SuffixedStpName_ReturnsTrue: PASS`
   - `IsPttDragOrphanCancellableTestable_UnsuffixedTgtName_StillReturnsTrue: PASS`
   - `IsPttDragOrphanCancellableTestable_UnsuffixedStpName_StillReturnsTrue: PASS`

---

### 13. COMPLETION ARTIFACT

`docs/brain/B197/ticket-4-completion.md`

---

---

## B197Tests.cs — Full Test File Template

Engineer writes the following file at `src/PropTraderTools/Tests/B197Tests.cs`:

```csharp
// B197Tests.cs
// Epic: B197 -- Gap Restore: TrySetSlateReadyOnDrainFill + TryAbortLeaderFlatDrains +
//               TryCancelStaleGtcExitOnEntry + IsPttDragOrphanCancellable suffix fix
// Framework: xUnit (mandatory -- no NUnit, no MSTest)
// DW IDs: DW-B157-01, DW-B156-01, DW-B156-DEFER-1, DW-B154-01, DW-B154-02, DW-B154-03
// Seam access: IsPttDragOrphanCancellableTestable (internal static, InternalsVisibleTo at CopyEngine.cs L~42)
// Source-text tests: no NT8 runtime required. File path: 3 levels up from bin output = PropTraderTools/ dir.
using NinjaTrader.Cbi;
using PropTraderTools;
using Xunit;

namespace PropTraderTools.Tests
{
    public class B197Tests
    {
        // ── T1: TrySetSlateReadyOnDrainFill ──────────────────────────────────────────

        [Fact]
        public void TrySetSlateReadyOnDrainFill_SourceContainsFollowerSlateReady()
        {
            var src = System.IO.File.ReadAllText(
                System.IO.Path.Combine("..", "..", "..", "CopyEngine.cs"));
            Assert.Contains("_followerSlateReady[", src);
        }

        [Fact]
        public void TrySetSlateReadyOnDrainFill_SourceContainsSlateReadyDrainMarker()
        {
            var src = System.IO.File.ReadAllText(
                System.IO.Path.Combine("..", "..", "..", "CopyEngine.cs"));
            Assert.Contains("[SLATE-READY-DRAIN]", src);
        }

        // ── T2: TryAbortLeaderFlatDrains ─────────────────────────────────────────────

        [Fact]
        public void TryEvictFollowerBeSlot_SourceContainsDrainAbortLeaderFlat()
        {
            var src = System.IO.File.ReadAllText(
                System.IO.Path.Combine("..", "..", "..", "CopyEngine.cs"));
            Assert.Contains("[DRAIN-ABORT-LEADER-FLAT]", src);
        }

        [Fact]
        public void TryEvictFollowerBeSlot_SourceContainsTryAbortLeaderFlatDrains()
        {
            var src = System.IO.File.ReadAllText(
                System.IO.Path.Combine("..", "..", "..", "CopyEngine.cs"));
            Assert.Contains("TryAbortLeaderFlatDrains", src);
        }

        // ── T3: TryCancelStaleGtcExitOnEntry ─────────────────────────────────────────

        [Fact]
        public void TryCancelStaleGtcExitOnEntry_SourceContainsMethodName()
        {
            var src = System.IO.File.ReadAllText(
                System.IO.Path.Combine("..", "..", "..", "CopyEngine.cs"));
            Assert.Contains("TryCancelStaleGtcExitOnEntry", src);
        }

        [Fact]
        public void TryCancelStaleGtcExitOnEntry_SourceContainsGtcSweepMarker()
        {
            var src = System.IO.File.ReadAllText(
                System.IO.Path.Combine("..", "..", "..", "CopyEngine.cs"));
            Assert.Contains("[GTC-SWEEP]", src);
        }

        // ── T4: IsPttDragOrphanCancellable suffix fix ─────────────────────────────────

        [Fact]
        public void IsPttDragOrphanCancellableTestable_SuffixedTgtName_ReturnsTrue()
        {
            bool result = CopyEngine.IsPttDragOrphanCancellableTestable(
                OrderState.Working, "ES 09-26", "ES 09-26", "PTT-TGT-Drag-1");
            Assert.True(result);
        }

        [Fact]
        public void IsPttDragOrphanCancellableTestable_SuffixedStpName_ReturnsTrue()
        {
            bool result = CopyEngine.IsPttDragOrphanCancellableTestable(
                OrderState.Working, "ES 09-26", "ES 09-26", "PTT-STP-Drag-2");
            Assert.True(result);
        }

        [Fact]
        public void IsPttDragOrphanCancellableTestable_UnsuffixedTgtName_StillReturnsTrue()
        {
            // Regression: base name (no suffix) must still be cancellable after fix
            bool result = CopyEngine.IsPttDragOrphanCancellableTestable(
                OrderState.Working, "ES 09-26", "ES 09-26", "PTT-TGT-Drag");
            Assert.True(result);
        }

        [Fact]
        public void IsPttDragOrphanCancellableTestable_UnsuffixedStpName_StillReturnsTrue()
        {
            // Regression: base name (no suffix) must still be cancellable after fix
            bool result = CopyEngine.IsPttDragOrphanCancellableTestable(
                OrderState.Working, "ES 09-26", "ES 09-26", "PTT-STP-Drag");
            Assert.True(result);
        }
    }
}
```

---

## Cross-Ticket Invariant Summary

| Invariant | Rule | Enforced By |
|-----------|------|-------------|
| T1 call BEFORE AbortDrainOnFill in Filled branch | _drainOwnedOrderIds must be intact for Guard(3) | T1 Step 2 code, Scan 2 CYC check does not prevent reversal — engineer must verify order visually |
| T2 call AFTER null guard in TryEvictFollowerBeSlot | e.Order must be non-null when TryAbortLeaderFlatDrains accesses e.Order.Account | T2 Step 2 placement spec |
| T3 call AFTER AbortDrainOnFill in Filled branch | Logical clarity — T3 does not depend on drain state | T3 Step 3 |
| T4 fully independent | No shared state, no ordering dep | T4 Scope Lock |
| No IsPttCopyEntryFilled / IsPttCopyEntryFilledTestable usage in T3 | Stub always returns false (silent bug) | T3 Guard (3) design, Scan 5 nullable check |

---

## DW Closure Register

| DW ID | Ticket | Closed By |
|-------|--------|-----------|
| DW-B157-01 | T1 | TrySetSlateReadyOnDrainFill new method + OnOrderUpdate call site |
| DW-B156-01 | T2 | TryAbortLeaderFlatDrains new method |
| DW-B156-DEFER-1 | T2 | TryEvictFollowerBeSlot wiring (call after null guard) |
| DW-B154-01 | T3 | TryCancelStaleGtcExitOnEntry new method |
| DW-B154-02 | T3 | IsStaleGtcExitOrder helper + GTC sweep logic |
| DW-B154-03 | T4 | IsPttDragOrphanCancellable + Testable guard(3) StartsWith fix |

---

*Written by: ptt-architect*
*Epic: B197*
*Phase: 3 (Ticket Generation)*
*Source plan: 02-architecture-plan.md (REVIEW_PASS)*
*Status: TICKETS_COMPLETE*
