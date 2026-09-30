# B130-LaneA Tickets

**Epic**: B130-LaneA
**Defect**: DW-B137 — IsAtmSTPOrder Wrong Name Format
**Phase**: 3 (Ticket Generation)
**Status**: TICKETS_COMPLETE
**Author**: ptt-architect
**Plan**: docs/brain/B130/LaneA-02-architecture-plan.md (REVIEW_PASS)
**Date**: 2026-09-01

---

## Ticket T1 — IsAtmSTPOrder Predicate Extension + SyncAtmFollowerTarget + 2 Tests

### T1.1 Spec Requirements

- **DW-B137**: `IsAtmSTPOrder` must match `Stop1`/`Stop2`/`Stop3` and `Target1`/`Target2`/`Target3` ATM names (MES $200 SL 6 template naming format).
- **DW-B137**: Follower stop AND target brackets must be updated via cancel+resubmit when leader drags (confirmed: `acc.Change()` is a silent no-op on ATM-owned brackets — B129 SIM gate 2026-08-31).
- **Backward compat**: `"Buy STP"` / `"Sell STP"` `EndsWith("STP")` path MUST still work (B129 LaneB tests depend on it).
- **OQ-03 safety**: cancel+resubmit of target brackets is SAFE — Gate 2 `FindMatchingRule` returns `null` for all follower account orders, unconditionally blocking `TryCancelFollowerEntries`.
- **Plan sections**: D.1 (IsAtmSTPOrder), D.2 (SyncFollowerBracket branch 3b), D.3 (SyncAtmFollowerTarget), G (tests), H (7-scan), I (files).

---

### T1.2 Method Signatures (exact — engineer must implement exactly as specified)

#### Change 1 — `IsAtmSTPOrder` (`CopyEngine.cs` ~L2028)

**BEFORE** (exact current code):
```csharp
// DW-B134: true if order name has STP suffix (NT8 ATM bracket stops: "Buy STP", "Sell STP").
// Mirrors IsBracketLegStatic STP clause. Made internal static for test access.
// CYC=1: expression body. JS-021: no lock. JS-001: no throw. ASCII-only.
internal static bool IsAtmSTPOrder(Order order) =>
    order.Name != null
    && order.Name.EndsWith("STP", StringComparison.OrdinalIgnoreCase);
```

**AFTER** (exact replacement):
```csharp
// DW-B134: true if order name has STP suffix (NT8 ATM bracket stops: "Buy STP", "Sell STP").
// DW-B137: extended to cover Stop1/Stop2/Stop3 and Target1/Target2/Target3 (MES $200 SL 6 ATM).
// Mirrors IsBracketLegStatic STP+Stop+Target clauses. Made internal static for test access.
// Option A safety: grep confirms 0 CreateOrder calls use "Stop*"/"Target*" prefixed names.
// CYC=1: expression body. JS-021: no lock. JS-001: no throw. ASCII-only.
internal static bool IsAtmSTPOrder(Order order) =>
    order.Name != null
    && (order.Name.EndsWith("STP", StringComparison.OrdinalIgnoreCase)
        || order.Name.StartsWith("Stop", StringComparison.OrdinalIgnoreCase)
        || order.Name.StartsWith("Target", StringComparison.OrdinalIgnoreCase));
```

**CYC**: 1 (expression body; compound boolean OR clauses are not McCabe decision nodes).

---

#### Change 2 — `SyncFollowerBracket` (`CopyEngine.cs` ~L2067)

**CURRENT code** after DW-B134 fix (branch 3 block, lines ~2064–2071):
```csharp
            // DW-B134: ATM STP path -- cancel+resubmit before IsTrailingStop guard.
            // IsTrailingStop fires on StopMarket orders; ATM STP brackets ARE StopMarket.
            // Without this branch, IsTrailingStop would return early and skip the sync.
            if (isStop && IsAtmSTPOrder(fo)) // (3) DW-B134
            {
                SyncAtmFollowerBracket(acc, fo, newPrice);
                return;
            }
```

**REPLACE THAT BLOCK WITH** (exact):
```csharp
            // DW-B134: ATM STP path -- cancel+resubmit before IsTrailingStop guard.
            // DW-B137: ATM TGT path -- cancel+resubmit for target brackets (acc.Change() no-op).
            // IsTrailingStop fires on StopMarket orders; ATM STP brackets ARE StopMarket.
            // Without branch (3), IsTrailingStop would return early and skip stop sync.
            if (isStop && IsAtmSTPOrder(fo)) // (3) DW-B134 + DW-B137
            {
                SyncAtmFollowerBracket(acc, fo, newPrice);
                return;
            }
            if (!isStop && IsAtmSTPOrder(fo)) // (3b) DW-B137: ATM target cancel+resubmit
            {
                SyncAtmFollowerTarget(acc, fo, newPrice);
                return;
            }
```

**ALSO UPDATE** the CYC comment at line ~2044:

- OLD: `// DW-B134: CYC=6: fo null(1), price delta(2), ATM STP(3), IsTrailingStop(4), isStop branch(5).`
- NEW: `// DW-B134/DW-B137: CYC=7: fo null(1), price delta(2), ATM STP(3), ATM TGT(3b), IsTrailingStop(4), isStop branch(5), [CYC from branching=7].`

**NOTE**: The CYC comment update IS required. Engineer must update it.

**CYC**: 6 → 7 (PASS ≤ 8).

---

#### Change 3 — New method `SyncAtmFollowerTarget` (`CopyEngine.cs` — add AFTER `SyncAtmFollowerBracket`)

Add the following complete method after the closing brace of `SyncAtmFollowerBracket` (~L2159):

```csharp
        // DW-B137: cancel+resubmit for ATM-owned target brackets (Limit type).
        // acc.Change() is a no-op on ATM-engine brackets (confirmed B129 SIM gate 2026-08-31).
        // Pattern mirrors SyncAtmFollowerBracket (DW-B134/B129 LaneB).
        // CYC=4: (1) acc null, (2) fo null, (3) Block A -- exception handler 0 McCabe.
        //        (4) newTarget null in Block B.
        // Two independent try/catch blocks -- Block A isolates Cancel; Block B isolates CreateOrder+Submit.
        // JS-021: no lock. JS-001: two independent try/catch -- no throw in hot path.
        // NT8-049: Limit order arg5=limitPrice (newPrice), arg6=0 (stopPrice unused for Limit).
        // NT8-013: Core.Globals.MaxDate for GTC. NT8-007: (CustomOrder)null.
        // NT8-014: order name starts with "PTT-" ("PTT-TGT-Drag").
        // OQ-03: cancel of follower ATM target bracket SAFE -- Gate 2 (FindMatchingRule L1609)
        //        returns null for follower account orders, blocking TryCancelFollowerEntries.
        private void SyncAtmFollowerTarget(Account acc, Order fo, double newPrice)
        {
            if (acc == null) // (1)
                return;
            if (fo == null) // (2)
                return;

            // Block A -- Cancel only. Independent: if Cancel throws, Block B still runs.
            try
            {
                acc.Cancel(new Order[] { fo });
            }
            catch (Exception ex)
            {
                StatusUpdate?.Invoke(acc.Name + ": TGT cancel error: " + ex.Message);
            }

            // Block B -- CreateOrder + Submit only. Runs regardless of Block A outcome.
            try
            {
                var newTarget = acc.CreateOrder(
                    fo.Instrument,
                    fo.OrderAction,
                    OrderType.Limit,
                    OrderEntry.Automated,
                    TimeInForce.Day,
                    fo.Quantity,
                    newPrice,
                    0,
                    "",
                    "PTT-TGT-Drag",
                    NinjaTrader.Core.Globals.MaxDate,
                    (NinjaTrader.Cbi.CustomOrder)null
                );
                if (newTarget == null) // (3)
                {
                    StatusUpdate?.Invoke(acc.Name + ": ATM TGT CreateOrder returned null");
                    return;
                }
                acc.Submit(new[] { newTarget });
                StatusUpdate?.Invoke(acc.Name + ": ATM TGT resubmit -> " + newPrice);
            }
            catch (Exception ex)
            {
                StatusUpdate?.Invoke(acc.Name + ": TGT create error: " + ex.Message);
            }
        }
```

**CYC**: 4 (PASS ≤ 8).

---

#### Change 4 — `PropTraderTools.csproj`

Add one line to the existing `ItemGroup` that contains `Compile` entries:
```xml
    <Compile Include="Tests\B130Tests.cs" />
```

---

#### Change 5 — New file `Tests/B130Tests.cs`

Create file with exactly this content:

```csharp
// B130 Tests -- DW-B137: IsAtmSTPOrder name format extension
// Verifies Stop1/Stop2/Stop3 and Target1/Target2/Target3 ATM names are routed to cancel+resubmit.
// Test-seam: IsAtmSTPOrder is internal static -- accessible via InternalsVisibleTo (CopyEngine.cs L46).
// [assembly: InternalsVisibleTo("PropTraderTools.Tests")] at CopyEngine.cs L46 enables direct call.
// Stub pattern: minimal fake Order class with settable Name (consistent with B129Tests.cs pattern).
using Xunit;
using NinjaTrader.Cbi;
using PropTraderTools;

namespace PropTraderTools.Tests
{
    public class B130Tests
    {
        // Minimal stub: creates a fake Order-like object for name-predicate tests.
        // IsAtmSTPOrder only reads order.Name -- no other NT8 fields needed.
        // Uses the existing FakeOrder test-stub pattern from B129Tests.cs.
        private static Order StubOrder(string name)
        {
            // FakeOrder must be defined in test infrastructure or use the existing stub.
            // If FakeOrder does not exist in test project, use direct internal access pattern:
            // CopyEngine.IsAtmSTPOrder is tested by passing a stub that exposes .Name property.
            // The engineer must resolve stub availability from existing B129Tests.cs pattern.
            throw new System.NotImplementedException("Engineer: replace with actual stub from B129Tests.cs pattern");
        }

        [Fact]
        public void B130_DW137_Stop1NameRoutesToCancelResubmit()
        {
            // Stop1/Stop2/Stop3 must match IsAtmSTPOrder (routes to cancel+resubmit via SyncAtmFollowerBracket)
            // "Buy STP" must still match (backward compat)
            // "Entry" must NOT match (non-bracket name)
            Assert.True(CopyEngine.IsAtmSTPOrder(StubOrder("Stop1")));
            Assert.True(CopyEngine.IsAtmSTPOrder(StubOrder("Stop2")));
            Assert.True(CopyEngine.IsAtmSTPOrder(StubOrder("Stop3")));
            Assert.True(CopyEngine.IsAtmSTPOrder(StubOrder("Buy STP")));  // backward compat
            Assert.True(CopyEngine.IsAtmSTPOrder(StubOrder("Sell STP"))); // backward compat
            Assert.False(CopyEngine.IsAtmSTPOrder(StubOrder("Entry")));   // entry order not affected
            Assert.False(CopyEngine.IsAtmSTPOrder(StubOrder("PTT-Copy")));// PTT orders not affected
        }

        [Fact]
        public void B130_DW137_Target1NameRoutesCorrectly()
        {
            // Target1/Target2/Target3 must match IsAtmSTPOrder (routes to SyncAtmFollowerTarget)
            // acc.Change() on ATM-owned Limit target brackets is a no-op (B129 SIM confirmed)
            // PTT- named orders must NOT match (PTT-TGT-Drag, PTT-Copy are not ATM-owned brackets)
            Assert.True(CopyEngine.IsAtmSTPOrder(StubOrder("Target1")));
            Assert.True(CopyEngine.IsAtmSTPOrder(StubOrder("Target2")));
            Assert.True(CopyEngine.IsAtmSTPOrder(StubOrder("Target3")));
            Assert.False(CopyEngine.IsAtmSTPOrder(StubOrder("PTT-Copy")));
            Assert.False(CopyEngine.IsAtmSTPOrder(StubOrder("PTT-TGT-Drag"))); // PTT order excluded
        }
    }
}
```

**ENGINEER INSTRUCTION**: The `StubOrder` helper above uses a placeholder that MUST be replaced with the actual fake/stub `Order` pattern from `B129Tests.cs`. Read [`src/PropTraderTools/Tests/B129Tests.cs`](../../src/PropTraderTools/Tests/B129Tests.cs) to find the existing stub pattern and reuse it. If `B129Tests.cs` uses reflection to call methods or uses `FakeOrder`, replicate that pattern exactly. `IsAtmSTPOrder` is `internal static` — call directly without reflection.

---

### T1.3 JS Rule Constraints

| Rule | Constraint | Applies To |
|------|-----------|-----------|
| **JS-021** | NO `lock()` anywhere | `IsAtmSTPOrder`, `SyncFollowerBracket` (modified), `SyncAtmFollowerTarget` (new) |
| **JS-001** | All NT8 calls (`acc.Cancel`, `acc.CreateOrder`, `acc.Submit`) MUST be in `try/catch` blocks. No rethrow. | `SyncAtmFollowerTarget` Block A and Block B |
| **JS-002** | No `return null` from value-expected methods. Null guards use `return;` (void — compliant). | `SyncAtmFollowerTarget` null guards at (1)(2)(3) |
| **JS-033** | No `async void`. All methods are synchronous. | All three methods |
| **JS-036** | No `new byte[]` heap alloc in hot path. `new Order[] { fo }` and `new[] { newTarget }` are the pre-existing NT8 array pattern (identical to `SyncAtmFollowerBracket` L2123/L2152) — accepted. | `SyncAtmFollowerTarget` Block A + Block B |
| **JS-066** | CYC ≤ 8 for ALL modified/new methods. Verify with `complexity_audit.py`. | `IsAtmSTPOrder`=1, `SyncFollowerBracket`=7, `SyncAtmFollowerTarget`=4 |

---

### T1.4 xUnit `[Fact]` Names

1. `B130_DW137_Stop1NameRoutesToCancelResubmit`
2. `B130_DW137_Target1NameRoutesCorrectly`

---

### T1.5 7-Scan Checklist (ENGINEER MUST VERIFY ALL 7 BEFORE BUILD_PASS)

| # | Scan | Command | Expected |
|---|------|---------|---------|
| **SCAN-01** | `lock()` | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` | 0 new matches in modified methods |
| **SCAN-02** | `async void` | `grep -n "async void " src/PropTraderTools/CopyEngine.cs` | 0 results |
| **SCAN-03** | `DateTime.Now` | `grep -n "DateTime\.Now" src/PropTraderTools/CopyEngine.cs` | 0 results |
| **SCAN-04** | Non-ASCII | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` | 0 results |
| **SCAN-05** | CYC | `python scripts/complexity_audit.py src/PropTraderTools/CopyEngine.cs` | All modified methods ≤ 8 |
| **SCAN-06** | PTT- prefix | `grep -n "PTT-TGT-Drag\|PTT-STP-Drag" src/PropTraderTools/CopyEngine.cs` | 2 matches (one per method: `SyncAtmFollowerBracket` + `SyncAtmFollowerTarget`) |
| **SCAN-07** | Build | `powershell -File scripts\build_readiness.ps1` | 0 errors, 0 new warnings |

---

### T1.6 Files Touched

| File | Operation | Description |
|------|-----------|-------------|
| `src/PropTraderTools/CopyEngine.cs` | Edit | **Change 1**: `IsAtmSTPOrder` (~L2028) extended with `StartsWith("Stop")` + `StartsWith("Target")` clauses + comment updated. **Change 2**: `SyncFollowerBracket` — insert branch (3b) `!isStop && IsAtmSTPOrder(fo)` → `SyncAtmFollowerTarget` after existing branch (3) + CYC comment updated (~L2044). **Change 3**: `SyncAtmFollowerTarget` new `private void` method added after closing brace of `SyncAtmFollowerBracket` (~after L2159). |
| `src/PropTraderTools/Tests/B130Tests.cs` | New file | 2 `[Fact]` tests as specified in T1.2 Change 5. Engineer must replace `StubOrder` placeholder with actual stub from `B129Tests.cs`. |
| `src/PropTraderTools/PropTraderTools.csproj` | Edit | **Change 4**: Add `<Compile Include="Tests\B130Tests.cs" />` to existing `ItemGroup`. |

---

### T1.7 Existing Tests That Must Still Pass

All tests in [`src/PropTraderTools/Tests/B129Tests.cs`](../../src/PropTraderTools/Tests/B129Tests.cs):

**DW-B134 group (3 tests)**:
- `B129_DW134_STPSuffixDetectedByIsBracketLegStatic`
- `B129_DW134_SyncFollowerBracketCancelResubmitFiredForAtmBracket`
- `B129_DW134_OQ03_CancelledBracketDoesNotTriggerFollowerEntryCancel`

**DW-B135 group (3 tests)**:
- `B129_DW135_GuardClearedAfterLeaderFlat`
- `B129_DW135_DW128ProtectionPreservedDuringRaceWindow`
- `B129_DW135_FirstEntryAfterRestartNotBlocked`

None of these reference `IsAtmSTPOrder` directly (they test via reflection or stub). The predicate extension adds new OR clauses only — existing `true`/`false` results for `"Buy STP"` / `"Sell STP"` are PRESERVED. The DW-B135 group tests the entry-guard/race-window logic path — no B130 changes touch that code path.

---

### T1.8 NT8 API Facts (Engineer Reference — Do Not Deviate)

| Fact | Source |
|------|--------|
| `acc.Change()` is a silent no-op on ATM-owned brackets (both stop and target) | B129 SIM gate 2026-08-31 + NT8_ADDON_KNOWLEDGE.md |
| `acc.Cancel()` + `acc.CreateOrder()` + `acc.Submit()` = only working AddOn pattern for ATM brackets | NT8_FULL_REFERENCE.md + plan section C.1 |
| `AtmStrategyChangeStopTarget()` — StrategyBase-only, NOT AddOn | NT8_FULL_REFERENCE.md + plan section C.4 |
| `AtmStrategyCreate()` — StrategyBase-only, NOT AddOn | NT8_FULL_REFERENCE.md + plan section C.4 |
| `OrderType.Limit` CreateOrder: arg6=`limitPrice`=`newPrice`, arg7=`stopPrice`=0 | NT8_FULL_REFERENCE.md CreateOrder signature + plan section C.3 |
| `NinjaTrader.Core.Globals.MaxDate` for GTC | NT8_FULL_REFERENCE.md NT8-013 |
| `(NinjaTrader.Cbi.CustomOrder)null` for last arg | NT8_FULL_REFERENCE.md NT8-007 |
| Order name must start with `"PTT-"` | NT8-014 + plan section C.3 — use `"PTT-TGT-Drag"` |

---

*Tickets written by ptt-architect from REVIEW_PASS plan.*
*Plan author: ptt-architect. Plan review: REVIEW_PASS (ptt-plan-reviewer, 2026-09-01).*
*Engineer: implement exactly as specified. Deviations require Director approval.*
