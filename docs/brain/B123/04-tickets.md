# B123 Tickets

**Block**: B123
**Status**: TICKETS_COMPLETE
**Date**: 2026-08-27
**Author**: ptt-architect
**Input**: docs/brain/B123/02-architecture-plan.md (REVIEW_PASS)

---

## Ticket Count: 1

This block contains exactly one coherent change: a new `Execute(forcedTargets)` overload on
`PttGlobalQuickExit` plus the `OnInstrQAll2tClick` panel fix that calls it. These two edits
are inseparable — the overload without the caller fix is dead code, and the caller fix without
the overload is a compile error. One ticket. One PR.

---

## T1 — DW-B133 forced 2-target Execute overload + OnInstrQAll2tClick fix

### Spec Requirement IDs

- **DW-B133**: QAll2t button fires snapshot target count instead of forced 2-target split.

---

### Files to Edit / Create

| Action | File Path |
|--------|-----------|
| EDIT   | `src/PropTraderTools/Features/PttGlobalQuickExit.cs` |
| EDIT   | `src/PropTraderTools/TradeCopierPanel.cs` |
| CREATE | `src/PropTraderTools/Tests/B123Tests.cs` |

---

### Change 1 — `PttGlobalQuickExit.cs`: new `Execute(forcedTargets)` overload

**Placement**: Insert immediately after the closing brace of the existing no-arg `Execute()` method
(after line 118 in the current file). Do NOT modify the no-arg `Execute()` body in any way.

**Exact method signature**:
```csharp
internal void Execute(System.Collections.Generic.List<(double Price, int Qty)> forcedTargets)
```

**Full method XML doc + body** (implement exactly as written below):

```csharp
/// <summary>
/// Execute: forced-targets overload for QAll2t path.
/// Skips SnapshotTargetOrders -- uses forcedTargets directly.
/// CYC=8: null guard(0), flag guard(1), acc loop(2), follower skip(3),
///        pos loop(4), null/flat continue(5), diag loop(6), flatten guard(7).
/// JS-021: no lock. JS-001: no throw. JS-033: synchronous void. ASCII-only.
/// DW-B133: forcedTargets prevents live ATM snapshot from overriding the 2-target intent.
/// </summary>
internal void Execute(System.Collections.Generic.List<(double Price, int Qty)> forcedTargets)
{
    // Branch 0 -- precondition null/empty guard
    if (forcedTargets == null || forcedTargets.Count < 2)
    {
        NinjaTrader.Code.Output.Process(
            "[PTT-QX-2T-ALL] forcedTargets null or empty -- aborting",
            NinjaTrader.NinjaScript.PrintTo.OutputTab1
        );
        return;
    }

    // Branch 1 -- feature flag gate (same as no-arg)
    if (!CopyEngine.Instance.Flags.QxGlobalExit)
    {
        NinjaTrader.Code.Output.Process(
            "[PTT-QX-2T-ALL] Blocked: Global Quick Exit requires Elite tier",
            NinjaTrader.NinjaScript.PrintTo.OutputTab1
        );
        return;
    }

    NinjaTrader.Code.Output.Process(
        "[PTT-QX-2T-ALL] GlobalQuickExit fired (forced 2-target)",
        NinjaTrader.NinjaScript.PrintTo.OutputTab1
    );

    var engine = CopyEngine.Instance;

    // Branch 2 -- foreach acc in Account.All
    foreach (Account acc in Account.All)
    {
        // Branch 3 -- follower skip
        if (engine != null && engine.IsFollowerAccount(acc))
            continue;

        // Branch 4 -- foreach pos in acc.Positions
        foreach (Position pos in acc.Positions)
        {
            // Branch 5 -- null / flat guard
            if (pos == null || pos.Quantity == 0)
                continue;

            int _beCancelCount = CancelPttBeOrders(acc, pos.Instrument);
            WaitForPttBeCancelled(acc, pos.Instrument, _beCancelCount, 1000);

            double leaderStop = PttQuickExit.SnapshotStopPrice(acc, pos.Instrument);
            var ticks = ResolveQuickTicks(pos.Instrument);

            // Branch 6 -- DW-B115-DIAG for-loop (confirms count=2 per account in Output tab)
            NinjaTrader.Code.Output.Process(
                "[PTT-QX-2T-ALL] leader: " + acc.Name
                    + " " + pos.Instrument.FullName
                    + " qty=" + pos.Quantity
                    + " forcedTargetCount=" + forcedTargets.Count,
                NinjaTrader.NinjaScript.PrintTo.OutputTab1
            );
            for (int _d = 0; _d < forcedTargets.Count; _d++)
            {
                NinjaTrader.Code.Output.Process(
                    "[DW-B115-DIAG] target[" + _d + "] Price="
                        + forcedTargets[_d].Price
                        + " Qty=" + forcedTargets[_d].Qty,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
            }

            // Branch 7 -- flatten guard (structural parity with no-arg path; dead on forced path)
            if (NeedsLeaderFallbackFlatten(_beCancelCount, forcedTargets.Count, pos.Quantity))
            {
                NinjaTrader.Code.Output.Process(
                    "[PTT-QX-2T-FLATTEN] leader fallback flatten: "
                        + acc.Name + " " + pos.Instrument.FullName,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
                acc.Flatten(new[] { pos.Instrument });
                continue;
            }

            ExecuteOne(acc, pos.Instrument, ticks.t1, forcedTargets);
            ExecuteFollowers(acc, pos, forcedTargets, ticks, leaderStop);
        }
    }
}
```

**CYC budget**: 8 branches (0–7 listed above). Exactly at JS-066 limit. No extraction required.

**JS Rule constraints**:
- **JS-021 (P0)**: No `lock()` anywhere in this method or in `PttGlobalQuickExit.cs`.
- **JS-001 (P0)**: No `throw` statement. Early returns only.
- **JS-033 (P0)**: `internal void` — synchronous, not `async void`.
- **JS-066**: CYC = 8 <= 8. Compliant.
- ASCII-only string literals (no Unicode, no curly quotes, no emoji).

---

### Change 2 — `TradeCopierPanel.cs`: replace `OnInstrQAll2tClick` body

**Target method**: `OnInstrQAll2tClick` at approximately lines 1979–1982 (current body is 3 lines).

**Find** (exact text to replace):
```csharp
private void OnInstrQAll2tClick(object sender, RoutedEventArgs e)
{
    new PttGlobalQuickExit().Execute();
}
```

**Replace with**:
```csharp
// B123 DW-B133: fire global Quick Exit with forced 2-target split.
// Mirrors OnInstr2tClick (single-account) but delegates to PttGlobalQuickExit (all accounts).
// CYC=4: (1) _instrument null, (2) _leaderAccount null re-resolve, (3) null after resolve,
//        (4) FirstOrDefault lambda predicate.
// JS-021: no lock. JS-033: synchronous void event handler. ASCII-only.
private void OnInstrQAll2tClick(object sender, RoutedEventArgs e)
{
    if (_instrument == null)
        return;
    _leaderAccount = _leaderAccount ?? TryResolveLeaderAccount();
    if (_leaderAccount == null)
        return;
    var pos = _leaderAccount.Positions.FirstOrDefault(
        p => p.Instrument?.FullName == _instrument.FullName
    );
    int qty = pos?.Quantity ?? 1;
    var targets = Build2TargetList(qty);
    NinjaTrader.Code.Output.Process(
        "[PTT-QX-2T-ALL] button: "
            + _leaderAccount.Name
            + " " + _instrument.FullName
            + " qty=" + qty
            + " T1=" + targets[0].Qty
            + " T2=" + targets[1].Qty,
        NinjaTrader.NinjaScript.PrintTo.OutputTab1
    );
    new PttGlobalQuickExit().Execute(targets);
}
```

**CYC of updated method**: 3–4 (instrument null + leader null + pos null-coalesce). Within JS-066 limit.

**JS Rule constraints**:
- **JS-021 (P0)**: No `lock()`.
- **JS-033 (P0)**: Synchronous `void` event handler. Not `async void`.
- ASCII-only string literals.
- `Build2TargetList` is already `internal static` on `TradeCopierPanel` — no visibility change needed.

---

### Change 3 — `Tests/B123Tests.cs`: create xUnit test file

**File path**: `src/PropTraderTools/Tests/B123Tests.cs`

**Required using directives**:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;
using PropTraderTools;
```

**Namespace**: Match the project namespace (e.g. `PropTraderTools` or `PropTraderTools.Tests`).

**5 [Fact] methods** (implement all 5 exactly):

#### T_B123_01_Build2TargetList_7qty

```csharp
[Fact]
public void T_B123_01_Build2TargetList_7qty()
{
    // Arrange: 7-contract position
    // Act
    var targets = TradeCopierPanel.Build2TargetList(7);
    // Assert
    Assert.Equal(2, targets.Count);
    Assert.Equal(4, targets[0].Qty);  // ceiling: (7+1)/2 = 4
    Assert.Equal(3, targets[1].Qty);  // floor:   7-4 = 3
}
```

**What it asserts**: With qty=7, T1=4 (ceiling half), T2=3 (remainder), exactly 2 entries.

#### T_B123_02_Build2TargetList_6qty

```csharp
[Fact]
public void T_B123_02_Build2TargetList_6qty()
{
    // Arrange: 6-contract position (even split)
    // Act
    var targets = TradeCopierPanel.Build2TargetList(6);
    // Assert
    Assert.Equal(2, targets.Count);
    Assert.Equal(3, targets[0].Qty);  // (6+1)/2 = 3
    Assert.Equal(3, targets[1].Qty);  // 6-3 = 3
}
```

**What it asserts**: With qty=6, equal split T1=3 T2=3, exactly 2 entries.

#### T_B123_03_Build2TargetList_AlwaysCount2

```csharp
[Fact]
public void T_B123_03_Build2TargetList_AlwaysCount2()
{
    // Asserts: for qty 1..9, count is always 2, split sums to qty, T1 >= T2.
    for (int qty = 1; qty <= 9; qty++)
    {
        var targets = TradeCopierPanel.Build2TargetList(qty);
        Assert.Equal(2, targets.Count);
        Assert.Equal(qty, targets[0].Qty + targets[1].Qty);
        Assert.True(targets[0].Qty >= targets[1].Qty);
    }
}
```

**What it asserts**: No path through `Build2TargetList` returns 1 or 3 entries; split always sums to total qty; T1 is always the larger (or equal) half.

#### T_B123_04_ForcedOverload_Exists

```csharp
[Fact]
public void T_B123_04_ForcedOverload_Exists()
{
    // Confirms the new Execute(List<(double, int)>) overload was added.
    var type = typeof(PttGlobalQuickExit);
    var mi = type.GetMethod(
        "Execute",
        BindingFlags.NonPublic | BindingFlags.Instance,
        null,
        new[] { typeof(List<(double Price, int Qty)>) },
        null
    );
    Assert.NotNull(mi);
    Assert.Equal(typeof(void), mi.ReturnType);
}
```

**What it asserts**: The forced-targets overload exists and returns `void`.

#### T_B123_05_NoArgOverload_StillExists

```csharp
[Fact]
public void T_B123_05_NoArgOverload_StillExists()
{
    // Confirms the original no-arg Execute() was NOT removed or replaced.
    var type = typeof(PttGlobalQuickExit);
    var mi = type.GetMethod(
        "Execute",
        BindingFlags.NonPublic | BindingFlags.Instance,
        null,
        Type.EmptyTypes,
        null
    );
    Assert.NotNull(mi);
}
```

**What it asserts**: The zero-parameter `Execute()` still exists (regression guard for the QAll button path).

---

### 7-Scan Checklist (engineer contract — run in this order)

All scans must pass before this ticket may be marked complete.

```
SCAN-01  grep -rn "lock(" src/PropTraderTools/Features/PttGlobalQuickExit.cs
         EXPECTED: 0 matches

SCAN-02  grep -rn "async void " src/PropTraderTools/Features/PttGlobalQuickExit.cs
         EXPECTED: 0 matches

SCAN-03  grep -rn "return null" src/PropTraderTools/Features/PttGlobalQuickExit.cs
         EXPECTED: 0 matches (new overload is void; all early returns are bare "return;")

SCAN-04  grep -rn "lock(" src/PropTraderTools/TradeCopierPanel.cs
         EXPECTED: 0 matches (existing file must not have acquired any new lock() during this edit)

SCAN-05  grep -rn "async void " src/PropTraderTools/TradeCopierPanel.cs
         EXPECTED: 0 matches for non-event handlers
         (Note: async void is permitted for WPF event handlers; the new OnInstrQAll2tClick is
          synchronous void -- confirm it does NOT have the async keyword)

SCAN-06  python scripts/complexity_audit.py
         EXPECTED: Execute(forcedTargets) CYC <= 8
                   OnInstrQAll2tClick CYC <= 8
                   All 5 test methods CYC <= 8

SCAN-07  dotnet build src/PropTraderTools/PropTraderTools.csproj --no-incremental
         EXPECTED: Build succeeded. 0 Error(s). 0 Warning(s).
         (--no-incremental is mandatory: incremental build may return false green from stale DLL)
```

---

### Acceptance Criteria

| AC  | Description | Verify via |
|-----|-------------|------------|
| AC1 | QAll2t with 7-contract position fires T1=4 T2=3 per account | T_B123_01 + SIM DW-B133-SIM-01 |
| AC2 | QAll2t with 6-contract position fires T1=3 T2=3 per account | T_B123_02 + SIM DW-B133-SIM-01 |
| AC3 | 3-target ATM active: still fires 2 targets (forced split wins over snapshot) | T_B123_04 (overload exists) + SIM DW-B133-SIM-01 |
| AC4 | All follower accounts exit with 2-target brackets | SIM DW-B133-SIM-01 (Director-owned) |
| AC5 | Existing QAll button (no-arg path) still fires 3 targets — no regression | T_B123_05 (no-arg still exists) + SIM DW-B133-SIM-02 |
| AC6 | CYC(Execute(forcedTargets)) <= 8, zero P0 JS violations, build passes | SCAN-06 + SCAN-07 |
| AC7 | Log line "[PTT-QX-2T-ALL] GlobalQuickExit fired (forced 2-target)" in Output Tab | SCAN-07 (compile) + SIM DW-B133-SIM-01 |

---

### Post-Completion Required Steps (engineer)

After all 7 scans pass:

1. Run `powershell -File scripts\ptt-sync-and-verify.ps1` — copy to NT8 and MD5-verify every file.
2. Press **F5** in NinjaTrader 8 to recompile. Confirm 0 compile errors in NT8 Output.
3. Write `docs/brain/B123/ticket-1-completion.md` with: scan results, build output line, NT8 F5 result.

---

### Deferred Items (do NOT implement in this ticket)

| DW ID | Description | Priority |
|-------|-------------|----------|
| DW-B133-01 | Extract DIAG for-loop to `LogLeaderDiag()` helper (reduces Execute(forcedTargets) to CYC=7) | P3 |
| DW-B133-SIM-01 | Live SIM gate: QAll2t 7-contract position, verify 2 OCO pairs per account | P0 Director-owned |
| DW-B133-SIM-02 | Live SIM gate: QAll button still fires 3 targets after B123 deploy | P1 Director-owned |
