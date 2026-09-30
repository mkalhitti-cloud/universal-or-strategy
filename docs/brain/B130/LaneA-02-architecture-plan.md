# B130 LaneA Architecture Plan — DW-B137

**Block**: B130 LaneA
**Defect**: DW-B137 — IsAtmSTPOrder Wrong Name Format
**Phase**: 1 (Architecture)
**Status**: REVIEW_PASS pending
**Author**: ptt-architect
**Date**: 2026-09-01

---

## A. Problem Statement

When the Director drags an ATM stop bracket or target bracket in NinjaTrader 8 Chart Trader using
the `MES $200 SL 6` ATM template, the price change is NOT propagated to follower accounts.

The B129 LaneB fix (DW-B134) correctly established the cancel+resubmit pattern for ATM-owned stop
brackets named `"Buy STP"` / `"Sell STP"`. However, the `MES $200 SL 6` template generates brackets
with entirely different order names:
- Stop brackets: `Stop1`, `Stop2`, `Stop3`
- Target brackets: `Target1`, `Target2`, `Target3`
- Entry bracket: `Entry`

The predicate `IsAtmSTPOrder` (L2028) only matches `EndsWith("STP")`. It returns **false** for
`Stop1`, `Stop2`, `Stop3`. As a result, `SyncFollowerBracket` falls through to the `acc.Change()` path,
which is a confirmed silent no-op on ATM-owned brackets.

**Symptom**: Director drags Stop1 bracket on leader (Sim101). Follower accounts (Sim102/103/104) do
not update. No drag log lines appear in Output Tab. B129 SIM gate (2026-08-31) confirmed:
"No drag at all for both stops AND targets."

---

## B. Root Cause (Single Layer — L2028 Only)

### Layer 1 Verification: IsBracketLegStatic — PASSES for Stop1/Target1

`IsBracketLegStatic` ([`CopyEngine.cs:3639`](../../src/PropTraderTools/CopyEngine.cs:3639)) was
updated in B129 LaneB (DW-B134). It already contains:
```csharp
order.Name.StartsWith("Stop")    // matches Stop1, Stop2, Stop3 ✅
order.Name.StartsWith("Target")  // matches Target1, Target2, Target3 ✅
```

`IsWorkingBracket("Stop1")` → `IsBracketLegStatic` returns `true` → `TryHandleBracketDrag`
dispatches to `HandleBracketChange`. **Layer 1 PASSES.**

### IsStopLeg Verification: PASSES for Stop1

`IsStopLeg` ([`CopyEngine.cs:3626`](../../src/PropTraderTools/CopyEngine.cs:3626)) already contains:
```csharp
order.Name.StartsWith("Stop")  // matches Stop1, Stop2, Stop3 ✅
```
`HandleBracketChange` calls `IsStopLeg(leaderOrder)` → `isStop = true` for Stop1/Stop2/Stop3.
**isStop flag is CORRECT.**

For `Target1`/`Target2`/`Target3`: `IsStopLeg` returns `false` → `isStop = false`. CORRECT.

### Layer 2: IsAtmSTPOrder — THE ONLY MISSING PREDICATE

**File**: [`src/PropTraderTools/CopyEngine.cs:2028`](../../src/PropTraderTools/CopyEngine.cs:2028)

```csharp
// CURRENT — only matches "Buy STP" / "Sell STP":
internal static bool IsAtmSTPOrder(Order order) =>
    order.Name != null
    && order.Name.EndsWith("STP", StringComparison.OrdinalIgnoreCase);
```

`IsAtmSTPOrder("Stop1")` returns **false**. `SyncFollowerBracket` falls to `acc.Change()`.
`acc.Change()` is a silent no-op on ATM-owned brackets (confirmed B129 SIM gate 2026-08-31).

**This is the sole root cause. No other code change is required.**

### OQ-03 Safety Pre-Confirmation for Target Cancel+Resubmit

Gate 2 (`FindMatchingRule`) returns `null` for any follower account order (follower account name
never matches `rule.MasterAccount.Name`). This blocks `TryCancelFollowerEntries` unconditionally.
The same safety guarantee that protects Stop1/Buy STP cancel+resubmit applies equally to
Target1/Target2/Target3 cancel+resubmit. **SAFE.**

---

## C. NT8 API Validation

> All facts below are **confirmed** from prior SIM gate and NT8 reference docs.
> This section embeds the confirmed facts directly — no re-derivation.

### C.1 — acc.Change() Behavior for ATM Brackets

- `acc.Change()` is available in AddOn context
  (NT8_FULL_REFERENCE.md L2365: `Account.Change()` available in AddOn context).
- `acc.Change()` is a **silent no-op** on ATM-owned brackets — both stop brackets
  (StopMarket type) AND target brackets (Limit type). Empirically confirmed by B129 SIM gate
  2026-08-31: "No drag at all for both stops AND targets."
  (NT8_ADDON_KNOWLEDGE.md: `Account.Change()` silent no-op on ATM-owned brackets.)
- The cancel+resubmit pattern (`acc.Cancel()` + `acc.CreateOrder()` + `acc.Submit()`) is the
  **only AddOn-context pattern** that successfully moves ATM-owned bracket orders.

### C.2 — Target Bracket Treatment (Architect Decision)

**Question**: Do `Target1`/`Target2`/`Target3` drag events need cancel+resubmit, or does
`acc.Change()` work for ATM Limit-type target brackets?

**Evidence**:
- B129 SIM gate 2026-08-31 reported: "No drag at all for both stops AND targets."
- DW-B137 defect brief states: "B129 SIM showed no drag at all for both stops AND targets."
- NT8's ATM engine owns the bracket pair (stop + target) as an OCO group.
- If `acc.Change()` is a no-op for ATM-owned stop brackets, the ATM engine's ownership of
  the bracket pair means target brackets in the same ATM are subject to the same restriction.

**Architect Decision**: Target1/Target2/Target3 also require cancel+resubmit treatment.
`acc.Change()` confirmed no-op for ATM Limit target brackets by SIM observation.

**Implementation**: New `SyncAtmFollowerTarget` method. Mirrors `SyncAtmFollowerBracket` but
uses `OrderType.Limit` with `limitPrice=newPrice, stopPrice=0`. Order name `"PTT-TGT-Drag"`.

### C.3 — CreateOrder Signature for Limit Orders

```
acc.CreateOrder(
    instrument,   // Order.Instrument
    action,       // Order.OrderAction
    OrderType.Limit,
    OrderEntry.Automated,
    TimeInForce.Day,
    quantity,     // Order.Quantity
    limitPrice,   // arg6: limitPrice = newPrice
    0,            // arg7: stopPrice = 0 for Limit
    "",           // fromEntrySignal: empty
    "PTT-TGT-Drag",  // NT8-014: PTT- prefix required
    NinjaTrader.Core.Globals.MaxDate,
    (NinjaTrader.Cbi.CustomOrder)null
);
```

Source: NT8_FULL_REFERENCE.md CreateOrder parameter reference. Limit order: arg6=limitPrice,
arg7=0. Mirrors `SyncAtmFollowerBracket` arg6=0, arg7=stopPrice (StopMarket order).

### C.4 — Confirmed NT8 Facts (embed directly)

- `AtmStrategyChangeStopTarget()` — StrategyBase-only. NOT available in AddOn context.
- `AtmStrategyCreate()` — StrategyBase-only. NOT available in AddOn context.
- `Account.Cancel()` + `Account.CreateOrder()` + `Submit()` — AddOn-available. Correct pattern.
- `Account.Change()` — AddOn-available but silent no-op on ATM-owned brackets (confirmed).

---

## D. Fix Design

### D.1 — IsAtmSTPOrder Extension (Core Fix)

**File**: [`src/PropTraderTools/CopyEngine.cs:2028`](../../src/PropTraderTools/CopyEngine.cs:2028)

**BEFORE**:
```csharp
// DW-B134: true if order name has STP suffix (NT8 ATM bracket stops: "Buy STP", "Sell STP").
// Mirrors IsBracketLegStatic STP clause. Made internal static for test access.
// CYC=1: expression body. JS-021: no lock. JS-001: no throw. ASCII-only.
internal static bool IsAtmSTPOrder(Order order) =>
    order.Name != null
    && order.Name.EndsWith("STP", StringComparison.OrdinalIgnoreCase);
```

**AFTER**:
```csharp
// DW-B134: true if order name has STP suffix (NT8 ATM bracket stops: "Buy STP", "Sell STP").
// DW-B137: extended to cover Stop1/Stop2/Stop3 and Target1/Target2/Target3 ATM formats.
// MES $200 SL 6 ATM template uses StartsWith("Stop") and StartsWith("Target") naming.
// "Buy STP"/"Sell STP" EndsWith("STP") preserved for backward compatibility.
// Safety confirmed: grep of CopyEngine.cs shows 0 CreateOrder calls with "Stop*"/"Target*" names.
// CYC=1: expression body. JS-021: no lock. JS-001: no throw. ASCII-only.
internal static bool IsAtmSTPOrder(Order order) =>
    order.Name != null
    && (order.Name.EndsWith("STP", StringComparison.OrdinalIgnoreCase)
        || order.Name.StartsWith("Stop", StringComparison.OrdinalIgnoreCase)
        || order.Name.StartsWith("Target", StringComparison.OrdinalIgnoreCase));
```

**CYC**: 1 (expression body; compound boolean OR clauses do not add McCabe branches).
**Grep confirmation**: 0 matches for `CreateOrder.*"Stop` or `CreateOrder.*"Target` in CopyEngine.cs.
Option A is safe — no PTT orders use `Stop*` or `Target*` name prefixes.

---

### D.2 — SyncFollowerBracket Extension for Target Drag

**File**: [`src/PropTraderTools/CopyEngine.cs:2048`](../../src/PropTraderTools/CopyEngine.cs:2048)

Current code at lines 2067-2071 (branch 3):
```csharp
if (isStop && IsAtmSTPOrder(fo)) // (3) DW-B134
{
    SyncAtmFollowerBracket(acc, fo, newPrice);
    return;
}
```

**Add branch (3b) immediately after branch (3)**:
```csharp
if (!isStop && IsAtmSTPOrder(fo)) // (3b) DW-B137: ATM target cancel+resubmit
{
    SyncAtmFollowerTarget(acc, fo, newPrice);
    return;
}
```

**Placement**: After existing branch (3) `isStop && IsAtmSTPOrder`, before the `IsTrailingStop`
guard. The isStop=false path has no trailing-stop guard (trailing stops are StopMarket type which
has isStop=true). Placement is safe.

**Updated CYC comment on SyncFollowerBracket**:
```csharp
// DW-B134: CYC=6: fo null(1), price delta(2), ATM STP(3), IsTrailingStop(4), isStop branch(5).
// DW-B137: CYC=7: adds ATM target branch(3b). Still <=8.
```

**CYC**: 6 → 7. PASS (≤ 8).

---

### D.3 — New SyncAtmFollowerTarget Method

**File**: [`src/PropTraderTools/CopyEngine.cs`](../../src/PropTraderTools/CopyEngine.cs)
**Location**: Add immediately after `SyncAtmFollowerBracket` (after L2159).

```csharp
// DW-B137: cancel+resubmit for ATM-owned target brackets (Limit type).
// acc.Change() is a no-op on ATM-engine brackets (confirmed B129 SIM gate 2026-08-31).
// Pattern mirrors SyncAtmFollowerBracket (DW-B134). Uses OrderType.Limit.
// CYC=4: (1) acc null, (2) fo null, (3) Block A try-body, (4) newTarget null in Block B.
// Two independent try/catch blocks -- Block B runs regardless of Block A outcome.
// JS-021: no lock. JS-001: try/catch -- no throw in hot path.
// NT8-049: Limit order arg6=limitPrice=newPrice, arg7=0 (stopPrice).
// NT8-013: Core.Globals.MaxDate for gtd. NT8-007: (CustomOrder)null.
// NT8-014: order name starts with "PTT-".
// OQ-03: cancel of follower ATM bracket is SAFE -- Gate 2 (FindMatchingRule L1609)
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

**CYC**: 4. PASS (≤ 8).

---

## E. CYC Budget

| Method | Old CYC | New CYC | Budget (≤8) | Notes |
|--------|---------|---------|-------------|-------|
| `IsAtmSTPOrder` | 1 | 1 | **PASS** | Expression body; 3 OR clauses = 1 McCabe node |
| `SyncFollowerBracket` | 6 | 7 | **PASS** | +1 ATM target branch (3b) |
| `SyncAtmFollowerTarget` | — | 4 | **PASS** | New method: 2 null guards + null check in Block B |

All methods remain ≤ 8. No CYC extraction required.

---

## F. Spec Requirements Traceability

| Requirement | Addressed By |
|-------------|-------------|
| Stop1/Stop2/Stop3 drag → cancel+resubmit on follower | IsAtmSTPOrder extension (D.1) + existing `SyncAtmFollowerBracket` (unchanged) |
| Target1/Target2/Target3 drag → cancel+resubmit on follower | IsAtmSTPOrder extension (D.1) + new `SyncAtmFollowerTarget` (D.3) + branch (3b) in D.2 |
| "Buy STP"/"Sell STP" existing behavior preserved | `EndsWith("STP")` clause retained as-is in IsAtmSTPOrder |
| No PTT orders named Stop*/Target* (Option A safety) | Grep confirmed: 0 `CreateOrder` calls with "Stop" or "Target" prefixes in CopyEngine.cs |
| OQ-03 safety applies to target cancel+resubmit | Gate 2 `FindMatchingRule` null-return blocks `TryCancelFollowerEntries` for all follower account orders |
| PTT- prefix on new order name | `"PTT-TGT-Drag"` in `SyncAtmFollowerTarget` |
| `IsTrailingStop` guard not hit for ATM stop orders | Stop path: branch (3) fires before branch (4) — unchanged from B129 LaneB |
| Layer 1 IsBracketLegStatic passes for Stop1/Target1 | Already correct from B129 LaneB — no change required |

---

## G. xUnit Test Stubs (2 new [Fact] in Tests/B130Tests.cs)

**File**: `src/PropTraderTools/Tests/B130Tests.cs` (new file)

**Test seam**: `IsAtmSTPOrder` is `internal static` (accessible via `InternalsVisibleTo` at
[`CopyEngine.cs:46`](../../src/PropTraderTools/CopyEngine.cs:46)). Order stub with settable `Name`
follows the pattern established in `B129Tests.cs`.

---

### Test 1 — `B130_DW137_Stop1NameRoutesToCancelResubmit`

**Purpose**: Verify `IsAtmSTPOrder` returns `true` for Stop1/Stop2/Stop3 names, and `true` for
legacy "Buy STP" format (backward compatibility). Confirms the stop drag route is taken.

**Assertions**:
```
CopyEngine.IsAtmSTPOrder(stubOrderWithName("Stop1"))    == true   // MES ATM stop bracket
CopyEngine.IsAtmSTPOrder(stubOrderWithName("Stop2"))    == true
CopyEngine.IsAtmSTPOrder(stubOrderWithName("Stop3"))    == true
CopyEngine.IsAtmSTPOrder(stubOrderWithName("Buy STP"))  == true   // backward compat: B129 format
CopyEngine.IsAtmSTPOrder(stubOrderWithName("Sell STP")) == true   // backward compat: B129 format
CopyEngine.IsAtmSTPOrder(stubOrderWithName("Entry"))    == false  // non-bracket: must stay false
CopyEngine.IsAtmSTPOrder(stubOrderWithName("PTT-Copy")) == false  // PTT orders: must stay false
```

---

### Test 2 — `B130_DW137_Target1NameRoutesCorrectly`

**Purpose**: Verify `IsAtmSTPOrder` returns `true` for Target1/Target2/Target3 names (confirms
the !isStop ATM target path would be taken, routing to `SyncAtmFollowerTarget`).

**Assertions**:
```
CopyEngine.IsAtmSTPOrder(stubOrderWithName("Target1"))     == true   // MES ATM target bracket
CopyEngine.IsAtmSTPOrder(stubOrderWithName("Target2"))     == true
CopyEngine.IsAtmSTPOrder(stubOrderWithName("Target3"))     == true
CopyEngine.IsAtmSTPOrder(stubOrderWithName("PTT-TGT-Drag"))== false  // PTT orders excluded
CopyEngine.IsAtmSTPOrder(stubOrderWithName("PTT-STP-Drag"))== false  // PTT orders excluded
```

**Note on "PTT-TGT-Drag" exclusion**: "PTT-TGT-Drag" starts with "PTT-" not "Target" or "Stop".
`IsAtmSTPOrder` returns `false` for it. This is correct — PTT-resubmitted orders should never
re-enter the ATM cancel+resubmit path (OQ-03 safety applies separately via Gate 2).

**Test-seam note**: If `NinjaTrader.Cbi.Order` cannot be instantiated in test context, follow the
stub pattern from `B129Tests.cs` (minimal fake with settable `Name` property). `IsAtmSTPOrder` is
`internal static` — call directly without reflection.

---

## H. 7-Scan Checklist (Ticket Carries This Forward)

| # | Scan | Command | Expected |
|---|------|---------|---------|
| SCAN-01 | `lock()` | `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "lock\("` | 0 new matches in modified methods |
| SCAN-02 | `async void` | `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "async void "` | 0 results |
| SCAN-03 | `DateTime.Now` | `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "DateTime\.Now"` | 0 results |
| SCAN-04 | Non-ASCII | `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "[^\x00-\x7F]"` | 0 results |
| SCAN-05 | CYC | `python scripts/complexity_audit.py src/PropTraderTools/CopyEngine.cs` | All modified methods ≤ 8 |
| SCAN-06 | PTT- prefix | `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "PTT-TGT-Drag\|PTT-STP-Drag"` | Matches in `SyncAtmFollowerBracket` + `SyncAtmFollowerTarget` |
| SCAN-07 | Build | `powershell -File scripts\build_readiness.ps1` | 0 errors |

---

## I. Files Touched

| File | Operation | Description |
|------|-----------|-------------|
| `src/PropTraderTools/CopyEngine.cs` | Edit | (1) `IsAtmSTPOrder` L2028: extend to `StartsWith("Stop")` + `StartsWith("Target")` + update comment. (2) `SyncFollowerBracket` L2067: insert branch (3b) `!isStop && IsAtmSTPOrder(fo)` → `SyncAtmFollowerTarget`. (3) `SyncAtmFollowerTarget`: new `private void` method after `SyncAtmFollowerBracket` (after L2159). |
| `src/PropTraderTools/Tests/B130Tests.cs` | New | 2 `[Fact]` tests: `B130_DW137_Stop1NameRoutesToCancelResubmit`, `B130_DW137_Target1NameRoutesCorrectly` |
| `src/PropTraderTools/PropTraderTools.csproj` | Edit | Add `<Compile Include="Tests\B130Tests.cs" />` |

---

## J. Backward Compatibility

- `"Buy STP"` / `"Sell STP"`: `EndsWith("STP")` clause retained → **PRESERVED**
- `B129Tests.cs` tests must still pass:
  - `B129_DW134_STPSuffixDetectedByIsBracketLegStatic` — unaffected (IsBracketLegStatic unchanged)
  - `B129_DW134_SyncFollowerBracketCancelResubmitFiredForAtmBracket` — unaffected (SyncAtmFollowerBracket unchanged)
  - `B129_DW134_OQ03_CancelledBracketDoesNotTriggerFollowerEntryCancel` — unaffected (FindMatchingRule unchanged)
- `Stop1`/`Stop2`/`Stop3` without "STP" suffix: now matched by `StartsWith("Stop")` → **FIXED**
- `Target1`/`Target2`/`Target3`: now matched by `StartsWith("Target")` → **FIXED**
- `SyncFollowerBracket` existing branches (1)(2)(3)(4)(5) unchanged in position and semantics

---

## K. Open Items / Deferred

| Item | Description | Priority | Status |
|------|-------------|----------|--------|
| DW-B134-OCO | OCO orphan risk after STP cancel+resubmit (carry-forward from B129 LaneB). Same risk applies to `SyncAtmFollowerTarget`: after cancel+resubmit of ATM target, the OCO partner (the stop) may be affected by NT8 ATM engine OCO behavior. | P2 | OPEN — carry-forward |
| DW-B130-SIM-01 | Director SIM gate: verify Stop1/Stop2/Stop3 drag sync works in live NT8 session after B130 sync. Confirm `"PTT-STP-Drag"` orders appear on followers when leader Stop1 is dragged. | P1 | OPEN — Director action |
| DW-B130-SIM-02 | Director SIM gate: verify Target1/Target2/Target3 drag sync produces `"PTT-TGT-Drag"` Limit orders on followers. Confirm price is correctly set. | P1 | OPEN — Director action |

---

*Plan written by ptt-architect. All 8 sequential thoughts completed.*
*NT8 API facts sourced from NT8_FULL_REFERENCE.md, NT8_ADDON_KNOWLEDGE.md, and B129 SIM gate notes.*
*Grep confirmations: 0 `CreateOrder` calls with Stop*/Target* prefixes (Option A safety verified).*
*IsStopLeg (L3626) and IsBracketLegStatic (L3639) verified correct — no changes needed to those methods.*
