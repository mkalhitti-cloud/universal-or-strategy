# B118 Implementation Tickets — DW-B126 BE/QX Race Condition Fix

**Block**: B118
**Phase**: 3 (Ticket Generation)
**Status**: TICKETS_COMPLETE
**Architect**: ptt-architect
**Date**: 2026-08-28
**Input**: `docs/brain/B118/02-architecture-plan.md` (REVIEW_PASS — 2026-08-28)
**Rules Gate**: PASS (JS-001, JS-002, JS-021, JS-033 verified in plan review)

---

## Ticket B118-T1 — Cancel PTT-BE-* orders before QX submit — DW-B126 race fix

### Spec Requirement IDs

- **DW-B126** (P1): BE/QX race condition — PTT-BE bracket orders still Working/Accepted when
  QX-ALL fires causes oversell. Fix: cancel all PTT-BE-* orders and await terminal confirmation
  BEFORE calling SnapshotTargetOrders on leader and follower paths.
- **DW-B127** (P2): Stale QX window (rapid double-press). Structurally eliminated by this fix:
  second press finds zero PTT-BE-* orders in non-terminal states; CancelPttBeOrders returns 0;
  WaitForPttBeCancelled fast-paths immediately.
- **Plan Section B**: Fix design — cancel-first on both leader and follower paths in Execute().
- **Plan Section C**: CYC budget table (all methods <= 8).
- **Plan Section E**: NT8 API grounding (Account.Cancel, OrderState enum, acc.Orders.ToList()).
- **Plan Section F**: Test plan (8 xUnit [Fact] tests).
- **Plan Section G**: File scope — one modified file, one new test file.
- **Plan Section H**: Closure criteria (Output tab evidence, no oversell, DW-B127 structural check).

---

### Files Modified

| File | Change Type |
|------|-------------|
| `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | MODIFIED — 4 new methods + 4 lines inserted in Execute() |
| `src/PropTraderTools/Tests/B118Tests.cs` | NEW — 8 xUnit [Fact] tests |

**All other files**: NO CHANGE. CopyEngine.cs, PttBreakEven.cs, PttBreakEvenSwap.cs,
PttQuickExit.cs, TradeCopierPanel.cs, PttCancel.cs, and all existing test files
must not be touched.

---

### Method Signatures

All four methods are added to `PttGlobalQuickExit` class (after existing methods, before the
closing brace). Access modifiers are exact — do not change internal to private or vice versa.

```csharp
// NEW — CYC=7 — cancel all PTT-BE-* orders in non-terminal states on acc for instr.
// Returns count of orders submitted for cancel (0 if none found or guards fail).
// JS-021 PASS (no lock), JS-001 PASS (no throw), JS-033 PASS (synchronous int).
// NT8: Account.Cancel(IEnumerable<Order>) — AddOn pattern, NT8_FULL_REFERENCE.md lines 2408-2451.
internal static int CancelPttBeOrders(
    NinjaTrader.Cbi.Account acc,
    NinjaTrader.Cbi.Instrument instr)

// NEW — CYC=7 — synchronous poll until all PTT-BE-* orders on acc+instr are terminal or timeout.
// Fast path: returns immediately when expectedCount <= 0 (no sleep, no iteration).
// Fail-safe: timeout logs warning and returns normally — does NOT throw.
// JS-021 PASS (no lock), JS-001 PASS (no throw), JS-033 PASS (synchronous void).
// DateTime.UtcNow used (not DateTime.Now) — SCAN-06 compliant.
internal static void WaitForPttBeCancelled(
    NinjaTrader.Cbi.Account acc,
    NinjaTrader.Cbi.Instrument instr,
    int expectedCount,
    int maxWaitMs)

// NEW — CYC=1 — name predicate: true iff name starts with "PTT-BE-Target-" or "PTT-BE-Stop-".
// Extracted from callers to keep CancelPttBeOrders and WaitForPttBeCancelled within CYC=8 budget.
// StringComparison.Ordinal (not InvariantCulture, not CurrentCulture).
private static bool IsPttBeOrder(string name)

// NEW — CYC=1 — state predicate: true iff order is NOT in a terminal state.
// Terminal states: Cancelled, Filled, Rejected, PartFilled, Unknown.
// Source: NT8_FULL_REFERENCE.md lines 976-997.
// CancelPending and CancelSubmitted are NON-terminal (cancel not yet confirmed by exchange);
// polling continues through these intermediate states.
// Why not Order.IsTerminalState(): NT8 line 829 documents method but does not enumerate
// its exact terminal set. Explicit predicate is safer and version-stable.
private static bool IsNonTerminalPttBeState(NinjaTrader.Cbi.OrderState s)
```

**Modified method**: `Execute()` — 4 lines inserted, 0 lines deleted, CYC unchanged at 8.

---

### Implementation Steps

**Step 1: Add CancelPttBeOrders method**

Add as `internal static int CancelPttBeOrders(Account acc, Instrument instr)` after
the `ResolveFollowerTargets` method and before the closing brace of the class:

```csharp
// B118 DW-B126: cancel all PTT-BE-* orders in non-terminal states before QX snapshot.
// Returns: count of orders submitted for cancel (0 if none or guard fails).
// CYC=7: acc null(1), instr null(2), foreach(3), o null(4), instrOk(5), IsPttBeOrder(6), stateOk(7).
// JS-021: no lock. JS-001: no throw. JS-033: synchronous int. ASCII-only.
// NT8: Account.Cancel(IEnumerable<Order>) -- NT8_FULL_REFERENCE.md lines 2408-2451.
internal static int CancelPttBeOrders(
    NinjaTrader.Cbi.Account acc,
    NinjaTrader.Cbi.Instrument instr)
{
    if (acc == null || instr == null)
        return 0;
    var toCancel = new System.Collections.Generic.List<NinjaTrader.Cbi.Order>();
    foreach (NinjaTrader.Cbi.Order o in acc.Orders.ToList())
    {
        if (o == null)
            continue;
        if (o.Instrument == null || o.Instrument.FullName != instr.FullName)
            continue;
        if (!IsPttBeOrder(o.Name))
            continue;
        if (!IsNonTerminalPttBeState(o.OrderState))
            continue;
        toCancel.Add(o);
    }
    if (toCancel.Count == 0)
    {
        NinjaTrader.Code.Output.Process(
            "[PTT-QX-ALL] CancelPttBeOrders: acc=" + acc.Name + " count=0 (no active PTT-BE orders)",
            NinjaTrader.NinjaScript.PrintTo.OutputTab1
        );
        return 0;
    }
    acc.Cancel(toCancel);
    NinjaTrader.Code.Output.Process(
        "[PTT-QX-ALL] CancelPttBeOrders: acc=" + acc.Name + " count=" + toCancel.Count,
        NinjaTrader.NinjaScript.PrintTo.OutputTab1
    );
    return toCancel.Count;
}
```

**Step 2: Add WaitForPttBeCancelled method**

Add immediately after `CancelPttBeOrders`:

```csharp
// B118 DW-B126: synchronous poll until PTT-BE-* orders are terminal or maxWaitMs elapses.
// Fast path: expectedCount <= 0 returns immediately (no sleep).
// Fail-safe: timeout logs warning, does NOT throw. Execution proceeds to QX logic.
// CYC=7: acc/count guard(1), while(2), foreach(3), o null(4), instrOk(5), IsPttBeOrder(6), nonTerminal(7).
// JS-021: no lock. JS-001: no throw. JS-033: synchronous void.
// DateTime.UtcNow used (not DateTime.Now).
// maxWaitMs=1000 -> 50 iterations x 20ms = 1000ms bounded.
internal static void WaitForPttBeCancelled(
    NinjaTrader.Cbi.Account acc,
    NinjaTrader.Cbi.Instrument instr,
    int expectedCount,
    int maxWaitMs)
{
    if (acc == null || expectedCount <= 0)
        return;
    NinjaTrader.Code.Output.Process(
        "[PTT-QX-ALL] WaitForPttBeCancelled: acc=" + acc.Name + " waiting count=" + expectedCount,
        NinjaTrader.NinjaScript.PrintTo.OutputTab1
    );
    var deadline = DateTime.UtcNow.AddMilliseconds(maxWaitMs);
    while (DateTime.UtcNow < deadline)
    {
        int nonTerminal = 0;
        foreach (NinjaTrader.Cbi.Order o in acc.Orders.ToList())
        {
            if (o == null)
                continue;
            if (o.Instrument == null || o.Instrument.FullName != instr.FullName)
                continue;
            if (!IsPttBeOrder(o.Name))
                continue;
            if (IsNonTerminalPttBeState(o.OrderState))
                nonTerminal++;
        }
        if (nonTerminal == 0)
        {
            NinjaTrader.Code.Output.Process(
                "[PTT-QX-ALL] WaitForPttBeCancelled: acc=" + acc.Name + " completed",
                NinjaTrader.NinjaScript.PrintTo.OutputTab1
            );
            return;
        }
        Thread.Sleep(20);
    }
    NinjaTrader.Code.Output.Process(
        "[PTT-QX-ALL] WaitForPttBeCancelled: acc=" + acc.Name + " TIMEOUT after " + maxWaitMs + "ms -- proceeding",
        NinjaTrader.NinjaScript.PrintTo.OutputTab1
    );
}
```

**Step 3: Add IsPttBeOrder helper**

Add immediately after `WaitForPttBeCancelled`:

```csharp
// B118 DW-B126: name predicate for PTT-BE-* bracket orders.
// CYC=1: single boolean expression.
// Extracted to keep CancelPttBeOrders and WaitForPttBeCancelled within CYC=8 budget.
// StringComparison.Ordinal: deterministic, locale-independent, fastest for ASCII prefix match.
private static bool IsPttBeOrder(string name)
{
    return !string.IsNullOrEmpty(name)
        && (name.StartsWith("PTT-BE-Target-", StringComparison.Ordinal)
            || name.StartsWith("PTT-BE-Stop-", StringComparison.Ordinal));
}
```

**Step 4: Add IsNonTerminalPttBeState helper**

Add immediately after `IsPttBeOrder`:

```csharp
// B118 DW-B126: state predicate -- returns true when order is NOT in a terminal state.
// Terminal states sourced from NT8_FULL_REFERENCE.md lines 976-997.
// CancelPending and CancelSubmitted are NON-terminal (cancel not confirmed by exchange).
// CYC=1: single boolean expression.
private static bool IsNonTerminalPttBeState(NinjaTrader.Cbi.OrderState s)
{
    return s != NinjaTrader.Cbi.OrderState.Cancelled
        && s != NinjaTrader.Cbi.OrderState.Filled
        && s != NinjaTrader.Cbi.OrderState.Rejected
        && s != NinjaTrader.Cbi.OrderState.PartFilled
        && s != NinjaTrader.Cbi.OrderState.Unknown;
}
```

**Step 5: Modify Execute() — leader path insertion**

In `Execute()`, locate the line at approximately line 47:
```csharp
var targets = SnapshotTargetOrders(acc, pos.Instrument);
```

Insert TWO lines IMMEDIATELY BEFORE that line (no other changes around it):
```csharp
// B118 DW-B126: cancel PTT-BE-* BEFORE snapshot to eliminate BE/QX race.
int _beCancelCount = CancelPttBeOrders(acc, pos.Instrument);
WaitForPttBeCancelled(acc, pos.Instrument, _beCancelCount, 1000);
// PTT-BE-* are now terminal -- snapshot sees clean order book.
var targets = SnapshotTargetOrders(acc, pos.Instrument);
```

**Exact insertion point**: After `continue; // (4)` (the pos null/flat check) and BEFORE
the existing `var targets =` line. The DW-B115-DIAG block that follows `var targets` is NOT
moved or touched.

**Step 6: Modify Execute() — follower path insertion**

In `Execute()`, locate the line at approximately line 89:
```csharp
var followerTargets = SnapshotTargetOrders(follower, pos.Instrument);
```

Insert TWO lines IMMEDIATELY BEFORE that line (inside the `foreach (var follower in ...)` loop,
after the `continue; // (7)` null check):
```csharp
// B118 DW-B126: cancel follower PTT-BE-* BEFORE snapshot (same race applies to followers).
int _fBeCancelCount = CancelPttBeOrders(follower, pos.Instrument);
WaitForPttBeCancelled(follower, pos.Instrument, _fBeCancelCount, 1000);
var followerTargets = SnapshotTargetOrders(follower, pos.Instrument);
```

**Exact insertion point**: After `continue; // (7)` (the follower null check) and BEFORE the
existing `var followerTargets =` line. The DW-B115-DIAG block that follows is NOT moved or touched.

**Step 7: Create src/PropTraderTools/Tests/B118Tests.cs**

Create the new test file with 8 xUnit [Fact] tests as defined in the xUnit Test Names section
below. The test file must:
- Use `using Xunit;` only (no NUnit, no MSTest).
- Access `CancelPttBeOrders`, `WaitForPttBeCancelled` via `internal static` access
  (existing `InternalsVisibleTo` project config covers this).
- Use stub/mock Account + Order objects. If the project stub pattern does not cover
  NT8 Account, use reflection or interface shims consistent with B112-B117 test patterns.
- Not import or reference any file outside `src/PropTraderTools/`.

**Step 8: Verify Execute() CYC is still 8**

After both insertions, count branches in Execute():
1. `foreach (Account acc in Account.All)` — loop (1)
2. `if (engine != null && engine.IsFollowerAccount(acc)) continue` — guard (2)
3. `foreach (Position pos in acc.Positions)` — loop (3)
4. `if (pos == null || pos.Quantity == 0) continue` — guard (4)
5. `var rule = engine?.FindRule(...)` / `if (rule != null)` — null guard (5)
6. `foreach (var follower in rule.Value.FollowerAccounts)` — loop (6)
7. `if (follower == null) continue` — null guard (7)
8. `ExecuteOne(...)` delegate — (8)

The 4 new lines (2x CancelPttBeOrders + 2x WaitForPttBeCancelled) add no branches.
CYC remains 8. If your count differs, re-check before submitting.

---

### 7-Scan Checklist (engineer contract)

Run ALL scans from the workspace root `C:\WSGTA\universal-or-strategy`. Every scan must pass
before the ticket is considered complete. Do not submit a PR until all 7 are green.

- [ ] **SCAN-01** — lock() ban (JS-021 P0):
  ```powershell
  grep -r "lock(" src/ --include="*.cs"
  ```
  Must return 0 results in new code added by this ticket.

- [ ] **SCAN-02** — async void ban (JS-033 P0):
  ```powershell
  grep -rn "async void " src/ --include="*.cs"
  ```
  Must return 0 results in new code added by this ticket.

- [ ] **SCAN-03** — return null ban (JS-002 P0):
  ```powershell
  grep -rn "return null;" src/ --include="*.cs"
  ```
  Must return 0 results in new code added by this ticket. (Note: `return 0;` and
  `return;` are fine. Only literal `return null;` is banned.)

- [ ] **SCAN-04** — CSharpier formatting (JS Code Review P1):
  ```powershell
  dotnet csharpier check src/
  ```
  Must report 0 formatting violations. Run `dotnet csharpier format src/` to fix, then re-check.

- [ ] **SCAN-05** — Cyclomatic complexity CYC <= 8 (JS-066 P0):
  ```powershell
  python scripts/complexity_audit.py
  ```
  All methods in `PttGlobalQuickExit.cs` must report CYC <= 8. Check specifically:
  - `Execute` <= 8
  - `CancelPttBeOrders` <= 7
  - `WaitForPttBeCancelled` <= 7
  - `IsPttBeOrder` <= 1
  - `IsNonTerminalPttBeState` <= 1

- [ ] **SCAN-06** — ASCII-only identifiers and string literals (JS DNA mandate):
  ```powershell
  grep -rP "[^\x00-\x7F]" src/ --include="*.cs"
  ```
  Must return 0 matches in new code. No Unicode, no emoji, no curly quotes.
  Verify `DateTime.UtcNow` used (not `DateTime.Now`) in `WaitForPttBeCancelled`.

- [ ] **SCAN-07** — Build clean (0 errors, 0 warnings):
  ```powershell
  dotnet build src/PropTraderTools/PropTraderTools.csproj
  ```
  Must exit with 0 errors and 0 warnings. Then run:
  ```powershell
  dotnet test src/PropTraderTools/PropTraderTools.csproj
  ```
  All tests (existing + 8 new B118 tests) must pass.

---

### xUnit [Fact] Test Names

All tests in `src/PropTraderTools/Tests/B118Tests.cs`. Framework: `using Xunit;` only.

```
[Fact] T_B118_CancelPttBe_WorkingTargetCancelled
  Arrange: StubAccount with PTT-BE-Target-1 in Working state.
  Act: CancelPttBeOrders(acc, instr).
  Assert: returns 1 (Working PTT-BE-Target order counted and submitted for cancel).

[Fact] T_B118_CancelPttBe_WorkingStopCancelled
  Arrange: StubAccount with PTT-BE-Stop-1 in Working state.
  Act: CancelPttBeOrders(acc, instr).
  Assert: returns 1 (Working PTT-BE-Stop order counted and submitted for cancel).

[Fact] T_B118_CancelPttBe_TerminalOrderSkipped
  Arrange: StubAccount with PTT-BE-Target-1 in Cancelled state + PTT-BE-Stop-1 in Working state.
  Act: CancelPttBeOrders(acc, instr).
  Assert: returns 1 (only the Working order counted; Cancelled is terminal and skipped).

[Fact] T_B118_CancelPttBe_NullAccountReturnsZero
  Arrange: acc = null. instr = valid stub.
  Act: CancelPttBeOrders(null, instr).
  Assert: returns 0. Does not throw.

[Fact] T_B118_CancelPttBe_NonPttBeOrderSkipped
  Arrange: StubAccount with Target1 Working, PTT-QX-T1 Working. No PTT-BE-* orders.
  Act: CancelPttBeOrders(acc, instr).
  Assert: returns 0 (no PTT-BE-* orders match name predicate).

[Fact] T_B118_WaitPttBe_ReturnsFastWhenNoOrders
  Arrange: StubAccount (no orders). expectedCount = 0.
  Act: var sw = Stopwatch.StartNew(); WaitForPttBeCancelled(acc, instr, 0, 1000); sw.Stop();
  Assert: sw.ElapsedMilliseconds < 5 (fast path -- no Thread.Sleep executed).

[Fact] T_B118_WaitPttBe_ReturnsAfterTimeout
  Arrange: StubAccount with PTT-BE-Stop-1 permanently in Working state. expectedCount = 1.
  Act: WaitForPttBeCancelled(acc, instr, 1, 100). (100ms timeout for test speed)
  Assert: method returns (does not hang). Does not throw. Returns within 200ms.
  Note: 100ms timeout + one 20ms sleep overshoot + measurement overhead <= 200ms.

[Fact] T_B118_DW127_StructuralElimination
  Documents DW-B127 closed by this fix.
  Arrange: StubAccount with all PTT-BE-* orders in Cancelled state (terminal). expectedCount = 0.
  Act: int result = CancelPttBeOrders(acc, instr).
  Assert: result == 0 (second-press fast path -- no active PTT-BE orders remain).
  Comment in test body: "// DW-B127: second QX press finds zero non-terminal PTT-BE orders. Structural elimination confirmed."
```

---

### Acceptance Criteria

The verifier (ptt-verifier) checks ALL of the following. A ticket is DONE only when every
criterion is observable as PASS.

**AC-1: New methods present in PttGlobalQuickExit.cs**
- `CancelPttBeOrders(Account, Instrument)` exists with `internal static int` access.
- `WaitForPttBeCancelled(Account, Instrument, int, int)` exists with `internal static void` access.
- `IsPttBeOrder(string)` exists with `private static bool` access.
- `IsNonTerminalPttBeState(OrderState)` exists with `private static bool` access.

**AC-2: Execute() leader path contains cancel-first calls**
- Lines `CancelPttBeOrders(acc, pos.Instrument)` and `WaitForPttBeCancelled(...)` appear
  BEFORE `SnapshotTargetOrders(acc, pos.Instrument)` in the leader path.
- No other existing lines in the leader path were moved, deleted, or reordered.

**AC-3: Execute() follower path contains cancel-first calls**
- Lines `CancelPttBeOrders(follower, pos.Instrument)` and `WaitForPttBeCancelled(...)` appear
  BEFORE `SnapshotTargetOrders(follower, pos.Instrument)` in the follower foreach loop.
- No other existing lines in the follower path were moved, deleted, or reordered.

**AC-4: All 7 scans pass (see 7-Scan Checklist above)**
- SCAN-01 through SCAN-07 all green with zero findings.

**AC-5: All 8 new xUnit tests pass**
- `dotnet test` reports 8 new passing tests in B118Tests.cs.
- No existing tests regressed.

**AC-6: Execute() CYC remains 8**
- `python scripts/complexity_audit.py` reports `Execute` at CYC=8 (not 9+).

**AC-7: DW-B115-DIAG blocks untouched**
- Leader DIAG block (original lines 66-80) and follower DIAG block (original lines 93-121)
  are bit-for-bit identical to the baseline. Any diff tool must show 0 changes to those lines.

**AC-8: ExecuteOne() untouched**
- `ExecuteOne()` method body is bit-for-bit identical to the baseline. CYC=2 unchanged.

**AC-9: No other files modified**
- `git diff --name-only` shows only:
  - `src/PropTraderTools/Features/PttGlobalQuickExit.cs`
  - `src/PropTraderTools/Tests/B118Tests.cs`

**AC-10: NT8 Output tab evidence (SIM gate — manual)**
- After pressing QX-ALL following a BE-ALL, NT8 Output tab shows log lines:
  ```
  [PTT-QX-ALL] CancelPttBeOrders: acc=Sim101 ...
  [PTT-QX-ALL] WaitForPttBeCancelled: acc=Sim101 ...
  ```
  appearing BEFORE any `[PTT-QX-ALL] leader:` lines.
- No position quantity overshoot after QX completes.

---

*End of B118-T1*
