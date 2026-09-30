# B129 LaneB Architecture Plan — DW-B134

**Block**: B129 LaneB  
**Defect**: DW-B134 — ATM Bracket Drag Not Synced to Followers  
**Phase**: 1 (Architecture)  
**Status**: REVIEW_PASS pending  
**Author**: ptt-architect  
**Date**: 2026-08-21  

---

## A. Problem Statement

When a trader drags an ATM stop bracket (named "Buy STP" or "Sell STP") in NinjaTrader 8 Chart Trader,
the price change is NOT propagated to follower accounts by the PTT CopyEngine.

Follower bracket orders remain at the original stop price, leaving followers exposed to different risk
than the leader. This is a silent failure — no error is logged, no StatusUpdate fires.

**Symptom**: Leader drags "Buy STP" from 4990 to 4985. All follower "Buy STP" brackets remain at 4990.

---

## B. Root Cause (Layer 1 + Layer 2 + Layer 3)

### Layer 1 — `IsBracketLegStatic` missing STP suffix clause

**File**: [`src/PropTraderTools/CopyEngine.cs`](../../src/PropTraderTools/CopyEngine.cs:3532)  
**Method**: `IsBracketLegStatic` (L3532)

```csharp
// CURRENT — missing STP EndsWith clause:
private static bool IsBracketLegStatic(Order order)
{
    return order.FromEntrySignal != null
        || (order.Name != null
            && (order.Name.StartsWith("Stop")
                || order.Name.StartsWith("Target")
                || order.Name.StartsWith("PTT-")));
}
```

NT8 ATM bracket stop orders are named `"Buy STP"` and `"Sell STP"` — they have neither a "Stop" prefix,
nor a "Target" prefix, nor "PTT-" prefix. `FromEntrySignal` is `null` for ATM-owned orders.

**Result**: `IsBracketLegStatic("Buy STP")` returns `false`.

**Cascade**:
- `IsWorkingBracket` (L2006) = `(Working||Accepted) && IsBracketLegStatic(order)` → returns `false`
- `TryHandleBracketDrag` (L1647): `if (!IsWorkingBracket(order)) return false;` → skips the drag
- `HandleBracketChange` and `SyncFollowerBracket` are **never called** for ATM STP drags

**Note**: `IsStopLeg` (L3521) **already has** the STP `EndsWith` clause (added in B25). `IsBracketLegStatic`
was not updated at that time. `IsBracketLeg` (L3550, instance version) does NOT need STP — it is used
exclusively by `CancelOneAccount`, not the drag path.

### Layer 2 — `SyncFollowerBracket` uses `acc.Change()` which is silently ignored for ATM brackets

**File**: [`src/PropTraderTools/CopyEngine.cs`](../../src/PropTraderTools/CopyEngine.cs:2062)  
**Method**: `SyncFollowerBracket` (L2040)

Even if Layer 1 were fixed in isolation, `SyncFollowerBracket` would still fail to move the follower's
ATM bracket. The current implementation calls `acc.Change(new Order[] { fo })`, which is **silently
ignored** by the NT8 ATM engine for brackets it owns.

**Authority**: `CopyEngine.cs` L3598-3601 comment confirms this exact root cause for `MoveStopToBreakEven`:
> "NT8 ATM engine owns Stop1/Stop2 brackets and ignores acc.Change() from AddOn context — no exception,
> no effect."

The fix established for `MoveStopToBreakEven` is the **cancel+resubmit** pattern, and the same fix
applies here.

### Layer 3 — `IsTrailingStop` guard fires first, preventing the ATM STP path from executing

**File**: [`src/PropTraderTools/CopyEngine.cs`](../../src/PropTraderTools/CopyEngine.cs:2056)  
**Guard**: `if (isStop && IsTrailingStop(fo)) return;` in `SyncFollowerBracket`

`IsTrailingStop` (L2018) is defined as `order.OrderType == OrderType.StopMarket`. ATM STP brackets are
`StopMarket` orders. If the new ATM STP detection branch is placed AFTER this guard, it is never reached.

**Fix**: The new `if (isStop && IsAtmSTPOrder(fo))` branch **must be placed before** the
`IsTrailingStop` guard in `SyncFollowerBracket`.

---

## C. OQ-03 Answer — Cascade Safety Analysis (HARD GATE)

**STATUS: SAFE**

**Question**: After `acc.Cancel(followerOrder["Buy STP"])` is issued for a follower account's ATM STP
bracket, does the resulting `Cancelled` `OrderUpdate` event cascade into `TryCancelFollowerEntries`
and trigger `CancelOneAccount`, wiping the follower's position entry?

**Analysis**:

1. `acc.Cancel(...)` fires `OrderUpdate` for the follower account order (State=Cancelled, Account="Sim102").
2. `OnOrderUpdate` (L1274) receives the event.
3. Pre-gate checks (L1276-1338): "Buy STP" matches none of the PTT-named guards (`PTT-BE-*`, `PTT-QX-*`,
   `PTT-Copy`, `Entry`). All pre-gate helpers return without action.
4. **Gate 1** (L1341-1342): `_isCopyEnabled` — passes (assume enabled).
5. **Gate 2 — `FindMatchingRule`** (L1603-1614, L1346):

```csharp
// FindMatchingRule: CYC=3.
private CopyRule? FindMatchingRule(Order order)
{
    foreach (var rule in _rules)
    {
        if (order.Instrument.FullName == rule.Instrument
            && order.Account.Name == rule.MasterAccount?.Name)  // L1609
            return rule;
    }
    return null;
}
```

   - `order.Account.Name` = follower account name (e.g. "Sim102")
   - `rule.MasterAccount?.Name` = leader account name (e.g. "Sim101")
   - "Sim102" != "Sim101" → **no rule matches** → `FindMatchingRule` returns `null`

6. **Gate 2+2.5** (L1348-1350): `if (matchedRule == null || ...) return;`
   - `matchedRule == null` → **IMMEDIATE RETURN** at L1350.
   - Execution stops here. Lines 1353–1641 are never reached.

7. `TryCancelFollowerEntries` (L1361) is **NEVER reached** for follower account orders.
8. `CancelOneAccount` is **NEVER called** as a result of the cancel+resubmit.

**Secondary confirmation**: Even if Gate 2 somehow produced a match (impossible for a correctly configured
rule), `IsAtmBracketName("Buy STP")` would also fail (L1625 guard in `TryCancelFollowerEntries`):
- `IsAtmBracketName` (L760) checks for "Stop" prefix + digit at index 4, or "Target" prefix + digit
  at index 6. "Buy STP" fails both patterns.
- However, **this secondary guard is irrelevant** — Gate 2's null-return at L1349-1350 is the primary,
  unconditional block.

**Conclusion**: The cancel+resubmit of the follower's "Buy STP" bracket is **SAFE**. No cascade into
`TryCancelFollowerEntries`. The follower's position entry order is NOT cancelled.

---

## D. Fix Design

### D.1 — `IsBracketLegStatic` — STP suffix clause

**Location**: [`src/PropTraderTools/CopyEngine.cs:3532`](../../src/PropTraderTools/CopyEngine.cs:3532)

**Change**: Add one new `||` clause to the existing compound return expression.

**Before**:
```csharp
private static bool IsBracketLegStatic(Order order)
{
    return order.FromEntrySignal != null
        || (order.Name != null
            && (order.Name.StartsWith("Stop")
                || order.Name.StartsWith("Target")
                || order.Name.StartsWith("PTT-")));
}
```

**After**:
```csharp
// DW-B134: added STP EndsWith clause -- NT8 ATM stop brackets are named "Buy STP"/"Sell STP".
// Mirrors IsStopLeg (L3521) which already has this clause. CYC: 3 -> 4.
private static bool IsBracketLegStatic(Order order)
{
    return order.FromEntrySignal != null
        || (order.Name != null
            && (order.Name.StartsWith("Stop")
                || order.Name.StartsWith("Target")
                || order.Name.StartsWith("PTT-")
                || order.Name.EndsWith("STP", StringComparison.OrdinalIgnoreCase)));
}
```

**CYC impact**: 3 → 4 (one new `||` branch). Below 8.

---

### D.2 — `SyncFollowerBracket` — ATM STP detection branch

**Location**: [`src/PropTraderTools/CopyEngine.cs:2040`](../../src/PropTraderTools/CopyEngine.cs:2040)

The new branch must be inserted **after** the price-delta guard (2) and **before** the `IsTrailingStop`
guard (3), because ATM STP orders are `StopMarket` type and the `IsTrailingStop` guard would otherwise
fire first and return early.

**Change**: Insert a new branch at position (3) of the method, shift existing branch (3) to (4), update
the method CYC comment.

**Revised method structure**:
```csharp
// DW-B134: CYC=6: fo null(1), price delta(2), ATM STP(3), IsTrailingStop(4), isStop branch(5).
// JS-001: try/catch around acc.Change() -- no throw in hot path.
// DW-B9-GAP-001a: trailing stop follower orders are skipped (Option B: skip is safer).
// DW-B134: ATM STP brackets (EndsWith "STP") require cancel+resubmit -- acc.Change() is no-op.
private void SyncFollowerBracket(
    Account acc,
    Order leaderOrder,
    bool isStop,
    double newPrice,
    double tickSize
)
{
    var fo = FindFollowerBracketOrder(acc, leaderOrder.FromEntrySignal, isStop);
    if (fo == null)                                   // (1)
        return;

    double currentPrice = isStop ? fo.StopPrice : fo.LimitPrice;
    if (Math.Abs(newPrice - currentPrice) < tickSize) // (2)
        return;

    // DW-B134: ATM STP path -- cancel+resubmit before IsTrailingStop guard.
    if (isStop && IsAtmSTPOrder(fo))                  // (3) NEW
    {
        SyncAtmFollowerBracket(acc, fo, newPrice);
        return;
    }

    if (isStop && IsTrailingStop(fo))                 // (4) existing
    {
        StatusUpdate?.Invoke("HandleBracketChange: skip trailing stop " + fo.Name);
        return;
    }

    try
    {
        if (isStop)                                   // (5)
            fo.StopPrice = newPrice;
        else
            fo.LimitPrice = newPrice;
        acc.Change(new Order[] { fo });
        StatusUpdate?.Invoke(
            acc.Name
                + ": bracket synced "
                + (isStop ? "stop" : "target")
                + " -> "
                + newPrice
        );
    }
    catch (Exception ex)
    {
        StatusUpdate?.Invoke(acc.Name + ": bracket sync error: " + ex.Message);
    }
}
```

**CYC impact**: 5 → 6. Below 8.

---

### D.3 — New predicate `IsAtmSTPOrder`

**Location**: Add near `IsTrailingStop` (L2018) in [`src/PropTraderTools/CopyEngine.cs`](../../src/PropTraderTools/CopyEngine.cs:2018)

```csharp
// DW-B134: true if order name has STP suffix (NT8 ATM bracket stops: "Buy STP", "Sell STP").
// Mirrors IsBracketLegStatic STP clause. Made internal static for test access.
// CYC=1: expression body. JS-021: no lock. JS-001: no throw. ASCII-only.
internal static bool IsAtmSTPOrder(Order order) =>
    order.Name != null
    && order.Name.EndsWith("STP", StringComparison.OrdinalIgnoreCase);
```

**CYC**: 1. Below 8.

---

### D.4 — New helper `SyncAtmFollowerBracket`

**Location**: Add after `SyncFollowerBracket` in [`src/PropTraderTools/CopyEngine.cs`](../../src/PropTraderTools/CopyEngine.cs:2081)

```csharp
// DW-B134: cancel+resubmit for ATM-owned STP brackets.
// acc.Change() is a no-op on ATM-engine brackets (confirmed L3598-3601).
// Pattern mirrors MoveStopToBreakEven cancel+resubmit (L3598+).
// CYC=3: (1) acc null guard, (2) fo null guard, (3) try block = 0 McCabe.
// JS-021: no lock. JS-001: try/catch -- no throw in hot path.
// NT8-049: StopMarket arg6=0 (limitPrice), arg7=newPrice (stopPrice).
// NT8-013: Core.Globals.MaxDate for gtd. NT8-007: (CustomOrder)null.
// NT8-014: order name starts with "PTT-".
// OQ-03: cancel of follower ATM bracket is SAFE -- Gate 2 (FindMatchingRule L1609)
//        returns null for follower account orders, blocking TryCancelFollowerEntries.
private void SyncAtmFollowerBracket(Account acc, Order fo, double newPrice)
{
    if (acc == null)   // (1)
        return;
    if (fo == null)    // (2)
        return;
    try
    {
        acc.Cancel(new Order[] { fo });
        var newStop = acc.CreateOrder(
            fo.Instrument,
            fo.OrderAction,
            OrderType.StopMarket,
            OrderEntry.Automated,
            TimeInForce.Day,
            fo.Quantity,
            0,
            newPrice,
            "",
            "PTT-STP-Drag",
            NinjaTrader.Core.Globals.MaxDate,
            (NinjaTrader.Cbi.CustomOrder)null
        );
        acc.Submit(new Order[] { newStop });
        StatusUpdate?.Invoke(
            acc.Name + ": ATM STP resubmit -> " + newPrice
        );
    }
    catch (Exception ex)
    {
        StatusUpdate?.Invoke(acc.Name + ": ATM STP sync error: " + ex.Message);
    }
}
```

**CYC**: 3. Below 8.

---

## E. CYC Budget

| Method | Old CYC | New CYC | Budget (≤8) | Notes |
|--------|---------|---------|-------------|-------|
| `IsBracketLegStatic` | 3 | 4 | PASS | +1 EndsWith STP clause |
| `IsAtmSTPOrder` | — | 1 | PASS | New predicate, expression body |
| `SyncFollowerBracket` | 5 | 6 | PASS | +1 ATM STP branch before IsTrailingStop |
| `SyncAtmFollowerBracket` | — | 3 | PASS | New helper: 2 null guards + try block |

All methods remain ≤ 8. No CYC extraction required.

---

## F. Spec Requirements (DW-B134)

| Requirement | Addressed By |
|-------------|-------------|
| ATM bracket stop drags must be detected by the bracket drag gate | Layer 1: `IsBracketLegStatic` STP clause (D.1) |
| `IsWorkingBracket` must return true for "Buy STP"/"Sell STP" Working orders | Layer 1: via `IsBracketLegStatic` fix |
| `TryHandleBracketDrag` must dispatch "Buy STP" drags to `HandleBracketChange` | Layer 1: prerequisite for dispatch path |
| Follower ATM STP brackets must be updated to the new leader stop price | Layer 2+3: `SyncFollowerBracket` cancel+resubmit path (D.2+D.4) |
| `acc.Change()` must NOT be called on ATM-owned brackets (silent no-op) | Layer 2: cancel+resubmit pattern in `SyncAtmFollowerBracket` (D.4) |
| IsTrailingStop guard must NOT skip ATM STP orders | Layer 3: new branch placed before `IsTrailingStop` guard (D.2) |
| OQ-03: cancel+resubmit must not cascade to `TryCancelFollowerEntries` | Gate 2 null-return: SAFE (Section C) |
| New order name must start with "PTT-" (NT8-014) | `SyncAtmFollowerBracket`: name = "PTT-STP-Drag" |

---

## G. xUnit Test Stubs

**File**: `src/PropTraderTools/Tests/B129Tests.cs` (new file — append 3 `[Fact]` tests)

**Test seam**: `[assembly: InternalsVisibleTo("PropTraderTools.Tests")]` already declared at
[`CopyEngine.cs:46`](../../src/PropTraderTools/CopyEngine.cs:46). Private static methods accessed via
`System.Reflection` with `BindingFlags.NonPublic | BindingFlags.Static` (established pattern in
`CopyEngineTests.cs:L18-22`).

---

### Test 1 — `B129_DW134_STPSuffixDetectedByIsBracketLegStatic`

**Purpose**: Verify Layer 1 fix — IsBracketLegStatic correctly returns true for STP-suffix names.

**Test-seam**: `IsBracketLegStatic` is `private static`. Access via:
```csharp
var mi = typeof(CopyEngine).GetMethod(
    "IsBracketLegStatic",
    BindingFlags.NonPublic | BindingFlags.Static
);
```

**Assertions**:
```
// Get MethodInfo via reflection (BindingFlags.NonPublic | BindingFlags.Static)
// Create stub orders with Name set and FromEntrySignal=null.
// Note: NinjaTrader.Cbi.Order cannot be directly constructed in test context.
// Use a proxy/stub Order object OR verify via IsAtmSTPOrder (internal static) instead.

// Alternative: test via the public-accessible gate IsWorkingBracket (internal static, L2006).
// IsWorkingBracket(order) = (Working||Accepted) && IsBracketLegStatic(order)
// Create stub with OrderState=Working, Name="Buy STP" -- assert IsWorkingBracket returns true.

Case 1: Name="Buy STP", OrderState=Working, FromEntrySignal=null
  → Assert IsAtmSTPOrder(stub) == true   (via internal static, direct call)
  
Case 2: Name="Sell STP", OrderState=Working, FromEntrySignal=null
  → Assert IsAtmSTPOrder(stub) == true

Case 3: Name="Stop1", OrderState=Working, FromEntrySignal=null
  → Assert IsBracketLegStatic via reflection: returns true (existing StartsWith("Stop"))

Case 4: Name="Entry", OrderState=Working, FromEntrySignal=null
  → Assert IsBracketLegStatic via reflection: returns false (not a bracket leg)
```

**Test-seam note**: If `NinjaTrader.Cbi.Order` cannot be instantiated in test context, the test for
`IsBracketLegStatic` uses `IsAtmSTPOrder` directly (which is `internal static`) and a separate
reflection call with a null-or-stub order to confirm the false case. Cases 3 and 4 require a stub `Order`
object with settable `Name` and `FromEntrySignal`. If stub creation is blocked by NT8 sealed classes,
use `IsAtmSTPOrder` as the primary test target and add a separate `[Fact]` for the `StartsWith` cases.

---

### Test 2 — `B129_DW134_SyncFollowerBracketCancelResubmitFiredForAtmBracket`

**Purpose**: Verify Layer 2+3 fix — when the follower bracket is an ATM STP order, `acc.Cancel()` and
`acc.CreateOrder()`+`acc.Submit()` are called instead of `acc.Change()`.

**Test-seam requirement**: `SyncFollowerBracket` is a `private void` instance method. It calls
`FindFollowerBracketOrder` (which iterates `acc.Orders`) and then `SyncAtmFollowerBracket` (which calls
`acc.Cancel`, `acc.CreateOrder`, `acc.Submit`). These calls require a mock/stub `Account`.

Since NT8 `Account` is a sealed NT8 type, a **test wrapper** is needed:

- Option A (preferred): Add an `internal` overload `SyncAtmFollowerBracket(Account acc, Order fo, double newPrice)` that is called directly in tests with a mock-able interface.
- Option B: Test `IsAtmSTPOrder` separately (verifying the predicate) and test `SyncAtmFollowerBracket` via integration test with a stub account.

**Assertions** (specification-level, test-seam permitting):
```
1. Given: isStop=true, fo.Name="Buy STP", fo.OrderType=StopMarket
2. Invoke SyncAtmFollowerBracket(acc, fo, newPrice=4985.0)
3. Assert: acc.Cancel was called with fo in the order array
4. Assert: acc.CreateOrder was called with OrderType.StopMarket, stopPrice=4985.0, name="PTT-STP-Drag"
5. Assert: acc.Submit was called with the new order
6. Assert: acc.Change was NOT called (distinguishes fix from old path)
```

**Fallback**: If Account cannot be mocked, the test verifies via `IsAtmSTPOrder` that the predicate
correctly identifies "Buy STP" as an ATM STP order (confirming the branch would be taken), and documents
that `SyncAtmFollowerBracket` path requires integration test coverage via simulator replay.

---

### Test 3 — `B129_DW134_OQ03_CancelledBracketDoesNotTriggerFollowerEntryCancel`

**Purpose**: Verify OQ-03 confirmation — Gate 2 (`FindMatchingRule`) returns null for follower account
orders, ensuring `TryCancelFollowerEntries` is never reached when the follower's ATM bracket is cancelled.

**Test-seam**: `FindMatchingRule` is `private` instance method. Access via:
```csharp
var mi = typeof(CopyEngine).GetMethod(
    "FindMatchingRule",
    BindingFlags.NonPublic | BindingFlags.Instance
);
```

**Setup**:
```
- Add a CopyRule to the engine: master="Sim101", instrument="NQ 09-25", follower=["Sim102"]
- Create a stub Order where:
    order.Account.Name = "Sim102"  (follower account)
    order.Instrument.FullName = "NQ 09-25"
    order.OrderState = Cancelled
    order.Name = "Buy STP"
```

**Assertions**:
```
1. Invoke FindMatchingRule(followerOrder) via reflection
2. Assert: result == null
   (Reason: "Sim102" != "Sim101" -- follower account never matches master account in rule)
3. Assert: TryCancelFollowerEntries was NOT invoked
   (Verified by absence of CancelOneAccount calls -- confirm via StatusUpdate or reflection on _rules)
```

**Supplementary assertion**:
```
4. Create a stub Order where order.Account.Name = "Sim101" (leader account), same instrument
5. Invoke FindMatchingRule(leaderOrder)
6. Assert: result != null (confirms the rule IS found for leader account)
   (Proves the test setup is valid and the follower result=null is meaningful, not a setup bug)
```

---

## H. 7-Scan Checklist (for ticket to carry forward)

The implementing engineer (ptt-engineer) MUST verify all 7 scans pass before marking the ticket done.

| # | Scan | Check | Expected Result |
|---|------|-------|-----------------|
| SCAN-01 | `lock()` grep | `grep -r "lock(" src/PropTraderTools/CopyEngine.cs` | 0 new matches in modified methods |
| SCAN-02 | `async void` grep | `grep -rn "async void " src/PropTraderTools/CopyEngine.cs` | 0 results |
| SCAN-03 | `DateTime.Now` grep | `grep -rn "DateTime\.Now" src/PropTraderTools/CopyEngine.cs` | 0 results |
| SCAN-04 | Non-ASCII grep | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` | 0 results |
| SCAN-05 | CYC verification | `python scripts/complexity_audit.py src/PropTraderTools/CopyEngine.cs` | All modified methods ≤ 8 |
| SCAN-06 | PTT- prefix check | `grep -n "CreateOrder" src/PropTraderTools/CopyEngine.cs` \| grep "PTT-STP-Drag" | 1 match at SyncAtmFollowerBracket |
| SCAN-07 | Build clean | `powershell -File scripts\build_readiness.ps1` | 0 errors, 0 warnings (new) |

---

## I. Files Touched

| File | Operation | Change Description |
|------|-----------|-------------------|
| [`src/PropTraderTools/CopyEngine.cs`](../../src/PropTraderTools/CopyEngine.cs) | Edit | (1) `IsBracketLegStatic`: add STP EndsWith clause. (2) `IsAtmSTPOrder`: new internal static predicate near L2018. (3) `SyncFollowerBracket`: insert ATM STP branch before IsTrailingStop guard; update CYC comment. (4) `SyncAtmFollowerBracket`: new private void helper after L2081. |
| [`src/PropTraderTools/Tests/B129Tests.cs`](../../src/PropTraderTools/Tests/B129Tests.cs) | New | 3 `[Fact]` tests: `B129_DW134_STPSuffixDetectedByIsBracketLegStatic`, `B129_DW134_SyncFollowerBracketCancelResubmitFiredForAtmBracket`, `B129_DW134_OQ03_CancelledBracketDoesNotTriggerFollowerEntryCancel` |
| [`src/PropTraderTools/PropTraderTools.csproj`](../../src/PropTraderTools/PropTraderTools.csproj) | Edit | Add `<Compile Include="Tests\B129Tests.cs" />` to the ItemGroup |

---

## J. Open Risk: Orphaned Stop After Target Fill

When `SyncAtmFollowerBracket` replaces the follower's "Buy STP" with a new "PTT-STP-Drag" order, the
new stop is **not part of the original ATM OCO pair**. If the original ATM target fills, the ATM engine
cancels the OCO partner — but the OCO partner is now the cancelled original "Buy STP", not the new
"PTT-STP-Drag". The new stop remains working after target fill.

**Risk**: Orphaned "PTT-STP-Drag" stop order on follower after target fill.

**Mitigation** (deferred): This requires a separate DW item (`DW-B134-OCO`) to handle stop cleanup
on target fill. The primary fix (drag sync) is correct and valuable. The orphaned stop risk exists
**only if the leader already had a Working target at the time of the drag** and the target fills before
the next PTT sync cycle.

**In-scope for B129 LaneB**: The drag sync fix (Layers 1-3). The OCO orphan problem is deferred.

---

*Plan written by ptt-architect. All 8 sequential thoughts completed. OQ-03 answered: SAFE.*
*NT8 API facts sourced from `docs/standards/NT8_FULL_REFERENCE.md` lines 338, 2098-2106, 2143-2167.*
*CYC verified against source comments at CopyEngine.cs L2037, L3531.*
