# B120 Tickets — DW-B129 Leader Fallback Flatten

**Block**: B120
**Defect**: DW-B129 (P0)
**Plan**: `docs/brain/B120/02-architecture-plan.md` (REVIEW_PASS)
**Phase**: 3 — Ticket Generation
**Author**: ptt-architect
**Date**: 2026-08-28

---

## Ticket Count: 1

---

## Ticket B120-T1

**Ticket ID**: B120-T1
**Title**: DW-B129 — Leader Fallback Flatten After B118 PTT-BE Cancel (PttGlobalQuickExit.cs)
**Priority**: P0
**Spec Req IDs**: DW-B129
**File**: `src/PropTraderTools/Features/PttGlobalQuickExit.cs`
**Test file**: `src/PropTraderTools/Tests/B120Tests.cs`

---

### Description

After B118 DW-B126 `CancelPttBeOrders` + `WaitForPttBeCancelled` runs on the leader path
(`_beCancelCount > 0`), the order book is empty — PTT-BE replaced the original ATM bracket
and is now also cancelled. `SnapshotTargetOrders` returns 0. `ExecuteOne` resolves stop=0,
submits no QX order. Leader left with open position and no bracket protection.

**Root cause log evidence (2026-08-28 live gate)**:
```
[PTT-QX-ALL] CancelPttBeOrders: acc=Sim101 count=6
[PTT-QX-ALL] WaitForPttBeCancelled: acc=Sim101 completed
[DW-B115-DIAG] leader targets: Sim101 count=0 posQty=7
[PTT-QX] stop resolved: 0 on Sim101
[PTT-QX] snapshot: 0 cancellable orders for MES SEP26
[PTT-QX] cancel: 0 queued, 0 race-skipped on Sim101
<-- NO QX ORDER. LEADER LEFT OPEN. -->
```

**Source reference**: [`PttGlobalQuickExit.cs`](src/PropTraderTools/Features/PttGlobalQuickExit.cs)
- Line 49: `_beCancelCount = CancelPttBeOrders(acc, pos.Instrument)` — B118 cancel fires
- Line 50: `WaitForPttBeCancelled(...)` — waits for terminal state
- Line 52: `var targets = SnapshotTargetOrders(acc, pos.Instrument)` — returns count=0
- Line 54: `double leaderStop = PttQuickExit.SnapshotStopPrice(...)` — returns 0
- Line 90: `ExecuteOne(acc, ...)` — called with targets=[], leaderStop=0; no QX order submitted
- Lines 92–167: follower dispatch block — extraction boundary for CYC budget

---

### Fix Steps

1. **Extract the follower dispatch block (lines 92–167 in current `Execute()`) into a new
   private void method**:

   ```
   private void ExecuteFollowers(
       Account acc,
       Position pos,
       System.Collections.Generic.List<(double Price, int Qty)> targets,
       (int t1, int t2) ticks,
       double leaderStop)
   ```

   This method contains: the `rule != null` guard, the follower `foreach`, the follower
   `null` continue, `_fBeCancelCount`/`WaitForPttBeCancelled`/`SnapshotTargetOrders`,
   the follower DW-B115-DIAG block, `ResolveFollowerTargets`, the follower log, and the
   follower `ExecuteOne` call (lines 92–167). `CopyEngine.Instance` is captured internally
   (same pattern as `ResolveQuickTicks()`). The engine is **not** a parameter.

   This extraction frees 1 branch budget in `Execute()` (CYC: 9→8, then new guard added: 8→8).
   After extraction and the new guard: `Execute()` CYC = 7 (≤ 8 JS-066 satisfied).
   `ExecuteFollowers()` CYC = 7 (≤ 8 JS-066 satisfied).

2. **Add `NeedsLeaderFallbackFlatten` helper to the `PttGlobalQuickExit` class**:

   ```csharp
   // B120 DW-B129: true when B118 cancelled BE orders AND snapshot is empty AND
   //          leader still has open position. Account.Flatten is the only reliable exit.
   internal static bool NeedsLeaderFallbackFlatten(int beCancelCount, int snapshotCount, int posQty)
   {
       return beCancelCount > 0 && snapshotCount == 0 && posQty > 0;
   }
   ```

   - CYC = 2 (one `&&` chain counts as 2 decisions)
   - `internal static`, no lock, no throw, ASCII-only
   - Returns `bool` — no null-capable return type (JS-002 satisfied)

3. **Insert flatten guard in `Execute()` between `SnapshotTargetOrders` (line 52) and
   `ExecuteOne` (line 90)** — insert AFTER the DW-B115-DIAG block (after the closing `}` at
   line 89) and BEFORE the `ExecuteOne` call at line 90:

   ```csharp
   if (NeedsLeaderFallbackFlatten(_beCancelCount, targets.Count, pos.Quantity))
   {
       NinjaTrader.Code.Output.Process(
           "[PTT-QX-FLATTEN] leader fallback flatten: "
               + acc.Name + " " + pos.Instrument.FullName
               + " qty=" + pos.Quantity,
           NinjaTrader.NinjaScript.PrintTo.OutputTab1
       );
       acc.Flatten(pos.Instrument);
       continue;  // skip ExecuteOne -- flatten handles the exit
   }
   ```

   **Behaviour**:
   - `NeedsLeaderFallbackFlatten` returns `true` → log, call `acc.Flatten(pos.Instrument)`,
     then `continue` to the next `pos` in `foreach (Position pos in acc.Positions)`.
     `ExecuteOne` is NOT called. The leader position is closed at market by NT8 Flatten.
   - `NeedsLeaderFallbackFlatten` returns `false` → the `if` block is skipped. Execution
     falls through to `ExecuteOne` exactly as before. Normal QX bracket swap fires.
     No change to the happy path.

4. **Replace the follower block in `Execute()` (lines 92–167) with a call to
   `ExecuteFollowers`** immediately after the `ExecuteOne(acc, ...)` call (line 90):

   ```csharp
   ExecuteFollowers(acc, pos, targets, ticks, leaderStop);
   ```

---

### Method Signatures (Exact)

```csharp
private void ExecuteFollowers(
    Account acc,
    Position pos,
    System.Collections.Generic.List<(double Price, int Qty)> targets,
    (int t1, int t2) ticks,
    double leaderStop)

internal static bool NeedsLeaderFallbackFlatten(
    int beCancelCount,
    int snapshotCount,
    int posQty)
```

---

### xUnit Tests (B120Tests.cs — 3 [Fact] methods)

**Framework**: xUnit only. No NUnit. No MSTest. Per `docs/protocol/TEST_FRAMEWORK_PROTOCOL.md`.

#### Test 1 — True path

**Method name**: `Test_NeedsLeaderFallbackFlatten_True_WhenBECancelledAndSnapshotEmpty`

```csharp
[Fact]
public void Test_NeedsLeaderFallbackFlatten_True_WhenBECancelledAndSnapshotEmpty()
{
    // beCancelCount=1, snapshotCount=0, posQty=7 -- all three predicates pass
    Assert.True(PttGlobalQuickExit.NeedsLeaderFallbackFlatten(1, 0, 7));
}
```

**Asserts**: All three conditions satisfied (`beCancelCount>0`, `snapshotCount==0`, `posQty>0`)
→ method returns `true`. Flatten fallback fires.

#### Test 2 — False path: no BE orders

**Method name**: `Test_NeedsLeaderFallbackFlatten_False_WhenBECancelCountZero`

```csharp
[Fact]
public void Test_NeedsLeaderFallbackFlatten_False_WhenBECancelCountZero()
{
    // beCancelCount=0 -- normal path, ExecuteOne runs
    Assert.False(PttGlobalQuickExit.NeedsLeaderFallbackFlatten(0, 0, 7));
}
```

**Asserts**: `beCancelCount==0` (no PTT-BE-* orders existed) → returns `false`. `ExecuteOne`
runs as before. Flatten does NOT fire on the normal path.

#### Test 3 — False path: snapshot has targets

**Method name**: `Test_NeedsLeaderFallbackFlatten_False_WhenSnapshotHasTargets`

```csharp
[Fact]
public void Test_NeedsLeaderFallbackFlatten_False_WhenSnapshotHasTargets()
{
    // snapshotCount=3 -- targets present, normal QX runs
    Assert.False(PttGlobalQuickExit.NeedsLeaderFallbackFlatten(1, 3, 7));
}
```

**Asserts**: `snapshotCount==3` (order book has targets after B118 wait) → returns `false`.
`ExecuteOne` runs with the 3 targets. Flatten does NOT fire when orders are available.

---

### 7-Scan Checklist

The engineer MUST complete all 7 scans and mark each `[x]` before reporting implementation complete.
Any failing scan is a blocker — stop and fix before continuing.

- [ ] **Scan 1 — JS-021 no lock()**: `grep -r "lock(" src/PropTraderTools/Features/PttGlobalQuickExit.cs` → 0 results
- [ ] **Scan 2 — JS-033 no async void**: `grep -n "async void " src/PropTraderTools/Features/PttGlobalQuickExit.cs` → 0 results
- [ ] **Scan 3 — JS-066 CYC ≤ 8**: `Execute()` CYC = 7, `ExecuteFollowers()` CYC = 7, `NeedsLeaderFallbackFlatten` CYC = 2 — all ≤ 8
- [ ] **Scan 4 — JS-001 no throw**: `grep -n "throw new" src/PropTraderTools/Features/PttGlobalQuickExit.cs` → 0 results in new code
- [ ] **Scan 5 — JS-002 no null return**: `NeedsLeaderFallbackFlatten` returns `bool` (not null-capable); `ExecuteFollowers` returns `void`
- [ ] **Scan 6 — ASCII-only**: no Unicode, emoji, or curly-quotes in new string literals — `"[PTT-QX-FLATTEN] leader fallback flatten: "` and `" qty="` are ASCII-only
- [ ] **Scan 7 — NT8 API**: `Account.Flatten(Instrument)` confirmed valid in `NT8_FULL_REFERENCE.md` — no `Submit()` required; AddOn-valid (not StrategyBase-only)

---

### Acceptance Criteria

- **A.** `NeedsLeaderFallbackFlatten` present in `PttGlobalQuickExit.cs`, CYC=2, `internal static`
- **B.** `acc.Flatten(pos.Instrument)` call present on fallback path with `[PTT-QX-FLATTEN]` log prefix
- **C.** `continue` present immediately after `acc.Flatten(pos.Instrument)` — `ExecuteOne` skipped on fallback path
- **D.** `ExecuteFollowers()` extracted as `private void`; `Execute()` calls `ExecuteFollowers(acc, pos, targets, ticks, leaderStop)` in place of the former inline follower block
- **E.** `Execute()` CYC ≤ 8 after fix (target: CYC=7)
- **F.** Follower path unchanged — `_fBeCancelCount` is a separate local variable inside `ExecuteFollowers()`; `NeedsLeaderFallbackFlatten` is NOT called on the follower path
- **G.** Normal QX path unchanged — when `beCancelCount=0`, `NeedsLeaderFallbackFlatten` returns `false`, execution falls through to `ExecuteOne` exactly as before
- **H.** No `lock()` introduced anywhere in `PttGlobalQuickExit.cs` (JS-021)
- **I.** No `async void` introduced anywhere in `PttGlobalQuickExit.cs` (JS-033)
- **J.** `src/PropTraderTools/Tests/B120Tests.cs` present, xUnit framework, all 3 `[Fact]` tests compile and pass — `dotnet test` exits 0
- **K.** `powershell -File scripts\ptt-sync-and-verify.ps1` exits with 0 MISMATCH lines (MD5 verified)

---

### Post-Implementation Steps

1. Run sync and verify:
   ```powershell
   powershell -File scripts\ptt-sync-and-verify.ps1
   ```
   Must show: **0 MISMATCH lines**

2. Director presses **F5** in NinjaTrader 8 — must compile green with 0 errors.

3. Write `docs/brain/B120/ticket-1-completion.md` documenting:
   - All 7 scans with results
   - Acceptance criteria A–K with PASS/FAIL per item
   - `dotnet test` output (3/3 passing)
   - Sync verify output (0 MISMATCH)

---

### Spec Update (Ph5)

`specs/002-trade-copier-spec.html`: update `section-dw-b129` status → `CLOSED (B120 FINAL_PASS)`

---

*End of Ticket B120-T1*
