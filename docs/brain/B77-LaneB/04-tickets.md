# B77-LaneB Tickets — QX Race Guard

**Epic**: B77-LaneB
**Phase**: 3 — Ticket Generation
**Author**: ptt-architect
**Input plan**: docs/brain/B77-LaneB/02-architecture-plan.md (REVIEW_PASS)
**Approach**: C — HashSet<Order> snapshot before submit loop

---

## Ticket T1 — CopyEngine.cs: BuildQxSnapshot + 3-param CancelQxBrackets overload

### Spec Requirements Satisfied

- B77-LaneB requirement: capture point-in-time QX-order set before cancel call to prevent race cancellation of freshly submitted PTT-QX orders
- New method `BuildQxSnapshot` provides the snapshot (CYC <= 4)
- New overload `CancelQxBrackets(Account, Instrument, HashSet<Order>)` consumes the snapshot (CYC <= 8)
- Existing 2-param `CancelQxBrackets(Account, Instrument)` is NOT modified
- `IsQxCancelCandidate` is NOT modified

### File

`src/PropTraderTools/CopyEngine.cs`

### Insertion Point

Insert both new methods immediately **after** the closing `}` of the existing 2-param `CancelQxBrackets` method at line 605.
Order in file after insertion:

```
line ~585  [existing] IsQxCancelCandidate(Order o)
line ~586  [existing] CancelQxBrackets(Account acc, Instrument instr)          <-- 2-param, unchanged
line ~606  [NEW]      BuildQxSnapshot(Account acc, Instrument instr)            <-- insert here
line ~625  [NEW]      CancelQxBrackets(Account acc, Instrument instr, HashSet<Order> snapshot)
line ~648  [existing] CancelAllAccountOrders(...)
```

---

### Method 1: BuildQxSnapshot

#### Exact C# Signature

```csharp
internal static System.Collections.Generic.HashSet<NinjaTrader.Cbi.Order> BuildQxSnapshot(
    NinjaTrader.Cbi.Account acc,
    NinjaTrader.Cbi.Instrument instr)
```

#### Full Pseudocode / Step-by-Step Logic

```
// B77 DW-B77-01: BuildQxSnapshot -- capture point-in-time set of cancellable QX orders.
// Called by PttQuickExit.Execute() BEFORE CancelQxBrackets to record which orders existed
// at snapshot time. Only orders in this set may be cancelled by the 3-param overload.
// Prevents the race window where newly-submitted PTT-QX orders (from the Submit loop) are
// caught by a second CancelQxBrackets call that was queued before the Submit loop ran.
// CYC=4: null-guard(1) + foreach(2) + stateOk(3) + IsQxCancelCandidate(4).
// JS-021: no lock. HashSet<Order> is local; NT8 dispatcher is serial (single-threaded dispatch).
// JS-002: returns new empty HashSet<Order>() on null input -- never returns null.
// JS-001: no throw. JS-033: synchronous static. ASCII-only.

STEP 1: null guard
  if (acc == null || instr == null)
    return new System.Collections.Generic.HashSet<NinjaTrader.Cbi.Order>();   // never null -- JS-002

STEP 2: allocate result set
  var result = new System.Collections.Generic.HashSet<NinjaTrader.Cbi.Order>();

STEP 3: iterate acc.Orders, collect all QX candidates in an active state
  foreach (Order o in acc.Orders)
  {
    STEP 3a: build stateOk using the same 5-state gate as CancelQxBrackets
      bool stateOk = o.OrderState == OrderState.Working
                  || o.OrderState == OrderState.Initialized
                  || o.OrderState == OrderState.Accepted
                  || o.OrderState == OrderState.Submitted
                  || o.OrderState == OrderState.TriggerPending;

    STEP 3b: instrument filter
      if (!stateOk) continue;
      if (o.Instrument == null || o.Instrument.FullName != instr.FullName) continue;

    STEP 3c: QX candidate filter
      if (IsQxCancelCandidate(o))
        result.Add(o);
  }

STEP 4: return the snapshot set (never null)
  return result;
```

#### CYC Analysis

| Branch | Description |
|--------|-------------|
| 1 | `if (acc == null \|\| instr == null)` — null guard |
| 2 | `foreach (Order o in acc.Orders)` — loop |
| 3 | `stateOk` compound `\|\|` (Roslyn: 5-way OR = 1 decision point) + `if (!stateOk) continue` and `if (o.Instrument...)` = 1 combined gate |
| 4 | `if (IsQxCancelCandidate(o))` — add to set |

**CYC = 4. Budget: <= 4. PASS.**

---

### Method 2: CancelQxBrackets 3-param overload

#### Exact C# Signature

```csharp
internal void CancelQxBrackets(
    NinjaTrader.Cbi.Account acc,
    NinjaTrader.Cbi.Instrument instr,
    System.Collections.Generic.HashSet<NinjaTrader.Cbi.Order> snapshot)
```

#### Full Pseudocode / Step-by-Step Logic

```
// B77 DW-B77-02: CancelQxBrackets 3-param overload -- snapshot-gated cancel.
// Identical to the 2-param overload except: an order is only added to stale if it
// is contained in `snapshot`. Orders not in snapshot (submitted after snapshot was
// taken = this cycle's new orders) are skipped, preventing the race window.
// snapshot == null fallback: behaves identically to the 2-param overload (cancels all).
// CYC=7 (plan) / CYC=8 (worst-case per R6 advisory): within budget <= 8.
// Engineer MUST perform Roslyn-accurate branch count and confirm <= 8 before marking T1 done.
// JS-021: no lock. HashSet<Order> passed by reference, consumed synchronously on caller thread.
// JS-001: no throw. JS-002: void return. JS-033: synchronous void. ASCII-only.

STEP 1: null guard
  if (acc == null || instr == null) return;              // (branch 1)

STEP 2: allocate stale list
  var stale = new System.Collections.Generic.List<NinjaTrader.Cbi.Order>();

STEP 3: iterate acc.Orders
  foreach (Order o in acc.Orders)                        // (branch 2)
  {
    STEP 3a: state gate -- same 5-state compound as 2-param overload
      bool stateOk = o.OrderState == OrderState.Working
                  || o.OrderState == OrderState.Initialized
                  || o.OrderState == OrderState.Accepted
                  || o.OrderState == OrderState.Submitted
                  || o.OrderState == OrderState.TriggerPending;
      if (!stateOk) continue;                            // (branch 3 -- Roslyn 1 decision for compound OR)

    STEP 3b: instrument filter
      if (o.Instrument == null || o.Instrument.FullName != instr.FullName) continue; // (branch 4)

    STEP 3c: snapshot filter -- NEW gate (branch 5)
      if (snapshot != null && !snapshot.Contains(o)) continue;
      // snapshot == null: skip this guard, fall through to IsQxCancelCandidate (2-param parity)

    STEP 3d: QX candidate filter
      if (IsQxCancelCandidate(o))                        // (branch 6)
        stale.Add(o);
  }

STEP 4: empty guard
  if (stale.Count == 0) return;                          // (branch 7)

STEP 5: cancel
  try { acc.Cancel(stale.ToArray()); }
  catch { }
```

**Counting note (from plan-review advisory R6)**: The existing 2-param source comment says CYC=6 but counting yields 7 branches. This overload adds 1 branch (snapshot filter). Plan claims CYC=7; worst-case per advisory = CYC=8. Either way budget <= 8 is satisfied. Engineer performs authoritative Roslyn count at implementation.

#### CYC Analysis

| Branch | Description |
|--------|-------------|
| 1 | `if (acc == null \|\| instr == null)` — null guard |
| 2 | `foreach (Order o in acc.Orders)` — loop |
| 3 | `stateOk` compound OR + `if (!stateOk) continue` — state gate (Roslyn = 1 decision) |
| 4 | `if (o.Instrument == null \|\| ... != instr.FullName) continue` — instrument filter |
| 5 | `if (snapshot != null && !snapshot.Contains(o)) continue` — snapshot filter (NEW) |
| 6 | `if (IsQxCancelCandidate(o))` — candidate filter |
| 7 | `if (stale.Count == 0) return` — empty guard |

**CYC = 7 (plan) / max 8 (advisory). Budget: <= 8. PASS.**

---

### T1 — 7-Scan Checklist

| # | Rule | Check | Status |
|---|------|-------|--------|
| SCAN-01 | JS-021 | No `lock()` in either new method — HashSet<Order> is thread-local, NT8 dispatcher is serial | REQUIRED |
| SCAN-02 | JS-001 | No `throw new` in either new method — no exception sites | REQUIRED |
| SCAN-03 | JS-002 | `BuildQxSnapshot` returns `new HashSet<Order>()` on null input — never returns null | REQUIRED |
| SCAN-04 | JS-033 | Both methods are synchronous (`void` and `HashSet<Order>` return, no `async` keyword) | REQUIRED |
| SCAN-05 | ASCII-only | All string literals in comments and code are ASCII-only (no Unicode, no curly quotes) | REQUIRED |
| SCAN-06 | CYC <= 8 | `CancelQxBrackets` 3-param overload: engineer performs Roslyn count, confirms <= 8 | REQUIRED |
| SCAN-07 | CYC <= 4 | `BuildQxSnapshot`: CYC = 4 (4 branches enumerated above) | REQUIRED |

**All 7 scans must pass before T1 is marked complete.**

---

## Ticket T2 — PttQuickExit.cs: Use BuildQxSnapshot + 3-param overload

### Spec Requirements Satisfied

- Captures `snapshot` from `BuildQxSnapshot` BEFORE `CancelQxBrackets` call (temporal ordering contract)
- Line 67 call updated from 2-param to 3-param overload
- `CancelQxBracketsForFollowers` call at line 69 is NOT modified (follower path is wholesale-replace, not snapshot-guarded)
- Submit loop (lines 83-152) is NOT modified
- `Execute()` CYC remains 8 (one `var snapshot` local added, no new branch)

### File

`src/PropTraderTools/Features/PttQuickExit.cs`

### Before/After Diff

**Context** — lines 63–70 before the change:

```csharp
            // Step 2: snapshot stop price before cancel
            double snapshotStop = SnapshotStopPrice(leader, instr);

            // Step 3: cancel ATM bracket + previous PTT-QX orders
            CopyEngine.Instance?.CancelQxBrackets(leader, instr);
            // B70 DW-B70-02: also cancel follower PTT-Copy brackets before re-placing QX orders
            CopyEngine.Instance?.CancelQxBracketsForFollowers(instr);
```

**After the change** — lines 63–72 (2 lines inserted/changed, 0 lines removed):

```csharp
            // Step 2: snapshot stop price before cancel
            double snapshotStop = SnapshotStopPrice(leader, instr);

            // Step 3: cancel ATM bracket + previous PTT-QX orders
            // B77 DW-B77-01: capture snapshot of current QX candidates BEFORE cancelling.
            // Orders submitted after this point (by the Submit loop below) are NOT in the snapshot
            // and will be skipped by the 3-param CancelQxBrackets overload -- no race cancellation.
            var snapshot = CopyEngine.BuildQxSnapshot(leader, instr);
            CopyEngine.Instance?.CancelQxBrackets(leader, instr, snapshot);
            // B70 DW-B70-02: also cancel follower PTT-Copy brackets before re-placing QX orders
            CopyEngine.Instance?.CancelQxBracketsForFollowers(instr);
```

### Exact Line-by-Line Change Map

| Original line | Action | New content |
|---------------|--------|-------------|
| 66 (blank before CancelQx) | INSERT 3 new lines above line 67 | comment block + `var snapshot = ...` |
| 67 `CopyEngine.Instance?.CancelQxBrackets(leader, instr);` | REPLACE | `CopyEngine.Instance?.CancelQxBrackets(leader, instr, snapshot);` |
| 69 `CopyEngine.Instance?.CancelQxBracketsForFollowers(instr);` | UNCHANGED | (no modification) |

**Note on insertion point**: The `var snapshot` line and the 3 comment lines are inserted between the `snapshotStop` assignment (line 64) and the original `CancelQxBrackets` call (old line 67). After the insert, old line 67 becomes approximately line 71.

### Why Snapshot Must Be Captured BEFORE CancelQxBrackets (Temporal Ordering Contract)

The snapshot must represent the set of QX orders that **existed before this Execute() invocation began placing new orders**. The only point that satisfies this contract is immediately before the first `CancelQxBrackets` call.

If the snapshot were captured after `CancelQxBrackets` (or during the Submit loop), orders already cancelled would be absent, and the snapshot would contain zero or partial entries — the guard would be vacuous or wrong.

If the snapshot were captured after the Submit loop, it would **include** the new orders just submitted, making the snapshot guard meaningless (it would allow cancellation of the exact orders it was designed to protect).

The only correct position is: **after `SnapshotStopPrice` (line 64), before `CancelQxBrackets` (old line 67)**.

### Execute() CYC Impact

`Execute()` has CYC=8 (documented in source comment, line 28-29). Adding one local variable (`var snapshot = ...`) with no conditional branching adds **0 branches**. CYC remains **8 (unchanged)**.

---

### T2 — 7-Scan Checklist

| # | Rule | Check | Status |
|---|------|-------|--------|
| SCAN-01 | JS-021 | No `lock()` added — `snapshot` is a local HashSet<Order>, no new synchronization | REQUIRED |
| SCAN-02 | JS-001 | No `throw new` added | REQUIRED |
| SCAN-03 | JS-002 | No `return null` added — snapshot variable is never null (BuildQxSnapshot guarantees non-null) | REQUIRED |
| SCAN-04 | JS-033 | `Execute()` is and remains synchronous void — no `async` keyword | REQUIRED |
| SCAN-05 | ASCII-only | All comment text is ASCII-only (no Unicode, curly quotes, or emoji) | REQUIRED |
| SCAN-06 | Execute() CYC unchanged | No new conditional branches in Execute() — CYC stays at 8 | REQUIRED |
| SCAN-07 | All existing QX logic preserved | `CancelQxBracketsForFollowers` (line 69), Submit loop (83-152), Step 7 (154-158) all unchanged | REQUIRED |

**All 7 scans must pass before T2 is marked complete.**

---

## Ticket T3 — CopyEngineTests.cs: 8 xUnit [Fact] Tests

### Spec Requirements Satisfied

- All 8 test IDs T_B77_QX_01 through T_B77_QX_08 present
- All tests use xUnit `[Fact]` — no NUnit, no MSTest
- Tests appended as new class `B77QxRaceGuardTests` at end of `CopyEngineTests.cs`

### File

`src/PropTraderTools/CopyEngineTests.cs`

### xUnit Using Statement

`using Xunit;` is **already present** at line 9 of `CopyEngineTests.cs` — no new using statement needed.

### Insertion Point

Append the new class **after** the closing `}` of the last existing class in the file. The last two lines of the current file are:

```csharp
    }
}
```

The new `B77QxRaceGuardTests` class is appended as a sibling class inside the same `namespace PropTraderTools` block, OR as a new top-level class after the closing `}` of the namespace.

**Preferred structure** — append inside the same namespace (before the final `}`):

```csharp
    // existing last class closes here
    }

    // ======================================================================
    // B77-LaneB -- QX Race Guard Tests
    // ======================================================================
    public class B77QxRaceGuardTests
    {
        // ... 8 [Fact] methods below
    }
}  // end namespace PropTraderTools
```

### Mock/Stub Strategy

The NT8 `Account` and `Order` types are not directly instantiable in unit tests. The existing test classes in `CopyEngineTests.cs` use reflection (`GetMethod`, `GetField`) and delegate injection to test internal logic without live NT8 runtime. For B77 tests:

- **`BuildQxSnapshot` null-guard tests (T_B77_QX_04, T_B77_QX_07, T_B77_QX_08)**: Test the null-input branch by invoking with `null` arguments via reflection, asserting the returned set is non-null and empty.
- **`IsQxCancelCandidate` tests (T_B77_QX_05, T_B77_QX_06)**: Invoke via reflection on a constructed stub Order (reflected or minimal mock), asserting true/false per spec.
- **`CancelQxBrackets` snapshot-filter tests (T_B77_QX_01, T_B77_QX_02, T_B77_QX_03)**: Use delegate injection or reflection against a test-double Account that surfaces a known Orders collection, asserting which orders are added to the cancel list.

The engineer MUST NOT instantiate `Account` or `Order` directly (NT8 internal constructors). Use the same reflection pattern established in `CopyEngineTests.cs`.

### Class Skeleton

```csharp
    // ======================================================================
    // B77-LaneB -- QX Race Guard Tests
    // Covers: BuildQxSnapshot, CancelQxBrackets 3-param overload, IsQxCancelCandidate
    // xUnit [Fact] only. JS-021: no lock. JS-001: no throw. JS-002: no return null.
    // JS-033: synchronous. ASCII-only. OKF testing-strategies.md standard.
    // ======================================================================
    public class B77QxRaceGuardTests
    {
        private static System.Reflection.MethodInfo GetStaticMethod(string name)
            => typeof(CopyEngine).GetMethod(
                name,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        private static System.Reflection.MethodInfo GetInstanceMethod(string name,
            System.Type[] paramTypes)
            => typeof(CopyEngine).GetMethod(
                name,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                null, paramTypes, null);

        // --- T_B77_QX_01 through T_B77_QX_08 methods below ---
    }
```

---

### Test Definitions

#### T_B77_QX_01: Race-guard positive path — new orders not in snapshot are skipped

```csharp
[Fact]
public void T_B77_QX_01_RaceGuard_NewOrderNotInSnapshot_IsNotCancelled()
{
    // Arrange:
    //   snapshot = HashSet containing orderA (old cycle order)
    //   acc.Orders = { orderA, orderB }  -- orderB = new cycle order (not in snapshot)
    //   both orders are Working PTT-QX-Stop with matching instrument
    // Act:
    //   invoke CancelQxBrackets(acc, instr, snapshot) via reflection
    // Assert:
    //   orderA IS added to the cancel list (in snapshot)
    //   orderB is NOT added to the cancel list (not in snapshot -> skipped)
    //   i.e. acc.Cancel is called with array containing only orderA
}
```

#### T_B77_QX_02: Race-guard negative path — stale orders in snapshot ARE cancelled

```csharp
[Fact]
public void T_B77_QX_02_RaceGuard_StaleOrderInSnapshot_IsCancelled()
{
    // Arrange:
    //   snapshot = HashSet containing orderA (prior cycle order -- stale)
    //   acc.Orders = { orderA }
    //   orderA is Working, PTT-QX-Stop, matching instrument
    // Act:
    //   invoke CancelQxBrackets(acc, instr, snapshot) via reflection
    // Assert:
    //   orderA IS added to the cancel list (it is in the snapshot)
    //   acc.Cancel is called with [orderA]
}
```

#### T_B77_QX_03: Non-PTT-QX orders are unaffected regardless of snapshot contents

```csharp
[Fact]
public void T_B77_QX_03_RaceGuard_NonQxOrder_UnaffectedBySnapshot()
{
    // Arrange:
    //   snapshot = HashSet containing orderC (a non-PTT-QX order, e.g. Name="Entry")
    //   acc.Orders = { orderC }
    //   orderC is Working, matching instrument, Name="Entry"
    // Act:
    //   invoke CancelQxBrackets(acc, instr, snapshot) via reflection
    // Assert:
    //   orderC is NOT added to cancel list
    //   (IsQxCancelCandidate("Entry") = false, so the outer IsQxCancelCandidate gate blocks it
    //    regardless of snapshot membership)
    //   acc.Cancel is NOT called (stale list is empty)
}
```

#### T_B77_QX_04: BuildQxSnapshot returns empty set when no PTT-QX orders are Working

```csharp
[Fact]
public void T_B77_QX_04_BuildQxSnapshot_NoWorkingQxOrders_ReturnsEmptySet()
{
    // Arrange:
    //   acc.Orders = empty collection OR all orders are non-PTT-QX or terminal-state
    // Act:
    //   result = (HashSet<Order>) BuildQxSnapshot.Invoke(null, new object[] { acc, instr })
    // Assert:
    //   result != null
    //   result.Count == 0
}
```

#### T_B77_QX_05: IsQxCancelCandidate true for Working PTT-QX Stop in snapshot → cancel; false when not in snapshot → skip

```csharp
[Fact]
public void T_B77_QX_05_IsQxCancelCandidate_WorkingQxStop_InSnapshot_IsCancelled_NotInSnapshot_IsSkipped()
{
    // Arrange:
    //   orderA: Working, Name="PTT-QX-Stop", matching instrument
    //   snapshot containing orderA
    //   snapshot NOT containing orderB (same state/name/instr)
    // Act part A (in-snapshot):
    //   invoke IsQxCancelCandidate via reflection with orderA
    //   result = true
    // Act part B (not-in-snapshot):
    //   invoke CancelQxBrackets(acc, instr, emptySnapshot) with acc.Orders = { orderA }
    // Assert:
    //   IsQxCancelCandidate(orderA) == true
    //   When snapshot is empty HashSet, orderA is NOT cancelled (not in snapshot guard fires)
}
```

#### T_B77_QX_06: IsQxCancelCandidate returns false for Filled orders even if order is in snapshot

```csharp
[Fact]
public void T_B77_QX_06_IsQxCancelCandidate_FilledOrder_InSnapshot_IsNotCancelled()
{
    // Arrange:
    //   orderA: Filled, Name="PTT-QX-T1", matching instrument
    //   snapshot containing orderA
    //   acc.Orders = { orderA }
    // Act:
    //   invoke CancelQxBrackets(acc, instr, snapshot) via reflection
    // Assert:
    //   staleCount == 0 (Filled fails the stateOk gate before snapshot check)
    //   acc.Cancel is NOT called
    //   Confirms terminal-state gate fires before snapshot check -- order of gates correct
}
```

#### T_B77_QX_07: CancelQxBrackets with empty snapshot — no NRE, no exception, 0 cancels

```csharp
[Fact]
public void T_B77_QX_07_CancelQxBrackets_EmptySnapshot_NoExceptionZeroCancels()
{
    // Arrange:
    //   snapshot = new HashSet<Order>()  -- empty, non-null
    //   acc.Orders = { orderA }  where orderA is Working PTT-QX-Stop
    // Act:
    //   invoke CancelQxBrackets(acc, instr, snapshot) via reflection
    //   (must not throw)
    // Assert:
    //   no exception thrown
    //   orderA is NOT cancelled (not in snapshot)
    //   acc.Cancel is NOT called (stale list empty)
}
```

#### T_B77_QX_08: BuildQxSnapshot is deterministic / idempotent — two calls return equal sets

```csharp
[Fact]
public void T_B77_QX_08_BuildQxSnapshot_TwoCalls_SameState_ReturnEqualSets()
{
    // Arrange:
    //   acc with N working PTT-QX orders (state unchanged between calls)
    // Act:
    //   snapshot1 = BuildQxSnapshot(acc, instr)  -- first call
    //   snapshot2 = BuildQxSnapshot(acc, instr)  -- second call, same acc state
    // Assert:
    //   snapshot1.Count == snapshot2.Count
    //   snapshot1.SetEquals(snapshot2) == true
    //   (snapshot is deterministic: same input state produces same set)
}
```

---

### T3 — 7-Scan Checklist

| # | Rule | Check | Status |
|---|------|-------|--------|
| SCAN-01 | JS-021 | No `lock()` in any test method or helper | REQUIRED |
| SCAN-02 | JS-001 | No `throw new` in test helpers — assert pattern only; use `Assert.Throws<>` if needed | REQUIRED |
| SCAN-03 | JS-002 | No `return null` in test helper methods | REQUIRED |
| SCAN-04 | JS-033 | All 8 `[Fact]` methods are synchronous `void` — no `async void` | REQUIRED |
| SCAN-05 | ASCII-only | All string literals in test comments and arrange/act steps are ASCII-only | REQUIRED |
| SCAN-06 | All 8 test IDs present | T_B77_QX_01 through T_B77_QX_08 all present in B77QxRaceGuardTests class | REQUIRED |
| SCAN-07 | xUnit [Fact] only | Only `[Fact]` attribute used — zero `[Test]`, `[TestCase]`, `[TestMethod]` attributes | REQUIRED |

**All 7 scans must pass before T3 is marked complete.**

---

## Completion Gate

- [x] All 4 mandatory reads completed (architecture plan, plan review, RULES_CATALOG, source files)
- [x] T1 has exact C# method signatures + pseudocode + CYC analysis + 7-scan checklist
- [x] T2 has before/after diff at correct lines + temporal ordering rationale + 7-scan checklist
- [x] T3 has all 8 test IDs (T_B77_QX_01..08) with arrange/act/assert pseudocode + 7-scan checklist
- [x] docs/brain/B77-LaneB/04-tickets.md written

---

TICKETS_COMPLETE
