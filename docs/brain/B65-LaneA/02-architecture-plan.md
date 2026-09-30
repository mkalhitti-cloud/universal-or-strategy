# B65-LaneA Architecture Plan

**Block**: B65-LaneA
**Phase**: 1 (Architecture)
**Written by**: ptt-architect
**Date**: 2026-08-12
**Status**: PLAN_COMPLETE (awaiting ptt-plan-reviewer)

---

## Section 1 — Defect Summary

### DW-B65-01: Post-Fill Close Propagation Race Condition

**Priority**: P0 (live trading correctness)
**Confirmed**: 2026-08-12 live testing (18-second gap between leader flat and follower manual close)
**Root cause**: NT8 position update lag documented in NT8_FULL_REFERENCE.md line 1721.

**Mechanism**:

When a leader's close order fills (OrderState.Filled), `OnOrderUpdate` fires immediately.
`TryDispatchLeaderFlat` guard (3) calls `hasOpenPosition(account, instrument)` to determine
whether the leader still has an open position. Because NT8 does not update position state until
the next `OnBarUpdate()` event after the fill, `hasOpenPosition` returns `true` at this moment
even though the position has just been closed. Guard (3) therefore fires, returns `false`, and
follower flatten is never dispatched.

**Fix strategy**: When the order that triggered the fill is a native NT8 exit name (e.g. "Close",
"Flatten", "ExitLong", "RevShort"), the position-race guard is irrelevant — we know the leader
intended to exit. The fix bypasses guard (3) for these names, dispatching the follower flatten
unconditionally.

---

## Section 2 — NT8 API Evidence

### NT8-VERIFY-01 — Position update lag (root cause proof)

**Citation**: `docs/standards/NT8_FULL_REFERENCE.md`, line 1721:
> "Changes to positions will not be reflected till at least the next OnBarUpdate() event
> after an order fill."

This is the canonical NT8 documentation for the race condition. Any code that relies on
`Position.MarketPosition` or `Account.Positions` immediately after a fill event in
`OnOrderUpdate` may read a stale (pre-fill) position. This is why `hasOpenPosition` returns
`true` for the leader even after their close order has filled.

### NT8-VERIFY-02 — Order.Name = "Close" semantics

**Citation**: `docs/standards/NT8_FULL_REFERENCE.md`, lines 844-845:
> "Name — A string representing the name of an order which can be provided by the entry or
> exit signal name"

NT8 Close button produces `Order.Name = "Close"`. This is the standard NT8 idiom already
confirmed in `IsExitSignalName` (CopyEngine.cs line 753) and in `NT8_FULL_REFERENCE.md`.
The value "Close" is assigned by NT8 when the user clicks the Close button on the Positions
tab or the Account grid.

### NT8-VERIFY-03 — IsNativeExitName name collision check

**jcodemunch search_text("IsNativeExitName", repo="universal-or-strategy")**:
Result count = 0. No existing symbol named `IsNativeExitName` anywhere in the indexed codebase.
Zero name collision. The new helper method name is safe to introduce.

### NT8-VERIFY-04 — IsNativeExitName NOT present in codebase (confirmed)

Confirmed by NT8-VERIFY-03 result: `IsNativeExitName` does not exist in `src/PropTraderTools/`
or any other indexed file. This is a net-new symbol; no overload ambiguity, no test conflict.

### NT8-VERIFY-05 — "Close" is idiomatic NT8 close-button usage

**jcodemunch search_text("Order.Name", repo="universal-or-strategy", max_results=5)**:
Results found only in `archive/v12-reference/` files (benchmark stubs). Production
`src/PropTraderTools/` files reference the name inline via `e.Order.Name` without storing it.
The literal "Close" appears in `IsExitSignalName` at line 753, confirming it is the established
canonical value for NT8 Close button orders.

---

## Section 3 — Architecture: IsNativeExitName Helper

### Purpose

`IsNativeExitName` identifies order names that represent native NT8 platform exit operations,
as distinct from PTT-prefixed synthetic orders. Only native exit names indicate a
filled-but-position-not-yet-updated race condition that warrants bypassing guard (3).

### Contrast with IsExitSignalName

| | `IsExitSignalName` (existing) | `IsNativeExitName` (new) |
|---|---|---|
| `"PTT-Flatten"` | `true` — blocks copy | `false` — PTT orders are not native exits |
| `"PTT-Copy"` | `true` | `false` |
| `"Close"` | `true` | `true` |
| `"Flatten"` | `true` | `true` |
| `"RevLong"` | `true` | `true` |
| `"ExitLong"` | `true` | `true` |

`IsExitSignalName` gates Gate 0.5 (blocks phantom re-copies). `IsNativeExitName` gates the
position-race bypass in `TryDispatchLeaderFlat`. The two methods serve different purposes and
must not be merged.

### Method Specification

```csharp
// B65 T1: IsNativeExitName -- CYC=6. Returns true for NT8 platform exit names only.
// Excludes PTT- prefixed names -- those are synthetic signals, not native NT8 exits.
// Used to bypass NT8 position-update lag (NT8_FULL_REFERENCE.md line 1721) in TryDispatchLeaderFlat.
// JS-001: no throw. JS-002: returns bool. ASCII-only string literals.
// TESTABILITY: internal static with string param -- directly testable without NT8 runtime.
internal static bool IsNativeExitName(string name)
{
    if (name == null)                                              return false;
    if (name == "Close")                                           return true;
    if (name == "Flatten")                                         return true;
    if (name.StartsWith("Rev",  StringComparison.Ordinal))         return true;
    if (name.StartsWith("Exit", StringComparison.Ordinal))         return true;
    return false;
}
```

**CYC analysis**: 1 (base) + 5 decision points (null, "Close", "Flatten", Rev-prefix,
Exit-prefix) = **CYC = 6**. Within JS CYC ≤ 8 limit.

**Insert position**: Immediately after `IsExitSignalName` in `CopyEngine.cs` (after line 758,
the closing brace of `IsExitSignalName`, before the blank line at line 760). No other code
displaced.

### DW-B59-02 Status: CLOSED

The B62 deferred backlog listed DW-B59-02 as OPEN ("IsExitSignalName uses exact Rev match
instead of prefix"). Inspection of live source (`CopyEngine.cs` line 755) confirms that
`IsExitSignalName` already uses `name.StartsWith("Rev", StringComparison.Ordinal)`. The fix
was applied in a prior block (B60) and the deferred item was not formally closed. `IsNativeExitName`
inherits the correct `StartsWith("Rev")` pattern from day 1. **DW-B59-02 is CLOSED.**

---

## Section 4 — Architecture: TryDispatchLeaderFlat Signature Change

### New Signature (8 parameters)

```csharp
// CYC=5 (spec-comment) / CYC=7 (strict McCabe, counting loop + null guard + new IsNativeExitName branch):
// (1) state guard, (2) follower guard, (3a) native-exit bypass, (3b) open-position guard, (4) foreach follower.
// Fires only on Filled or Cancelled. Skips if account is a follower.
// Skips if NOT a native exit AND leader still has an open position.
// For native exits (Close/Flatten/Rev*/Exit*): bypasses position-race (NT8_FULL_REFERENCE.md line 1721).
// Loops rule.FollowerAccounts directly -- does NOT touch the leader account.
// JS-021: no lock. JS-001: no throw. JS-002: no null return.
private static bool TryDispatchLeaderFlat(
    Account account, Instrument instrument, OrderState state, string orderName,
    CopyRule rule,
    Func<Account, bool> isFollower,
    Func<Account, Instrument, bool> hasOpenPosition,
    Action<Account, Instrument> flattenOne)
{
    if (state != OrderState.Filled && state != OrderState.Cancelled) return false; // (1)
    if (isFollower(account)) return false;                                           // (2)
    // (3) Bypass position-race for native NT8 exits (NT8_FULL_REFERENCE.md line 1721):
    //     position state is not updated until next OnBarUpdate after fill.
    if (!IsNativeExitName(orderName) && hasOpenPosition(account, instrument)) return false;
    foreach (var acc in rule.FollowerAccounts)                                       // (4)
    {
        if (acc == null) continue;
        flattenOne(acc, instrument);
    }
    return true;
}
```

### Guard (3) Change Analysis

**OLD** (current):
```csharp
if (hasOpenPosition(account, instrument)) return false;  // (3)
```

**NEW** (B65):
```csharp
if (!IsNativeExitName(orderName) && hasOpenPosition(account, instrument)) return false;  // (3)
```

**Semantics**:
- Non-native exit (e.g. "BuyLimit", "PTT-Copy"): `IsNativeExitName` = false → `!false && hasOpenPosition` = `hasOpenPosition`. Behavior unchanged.
- Native exit (e.g. "Close", "ExitLong"): `IsNativeExitName` = true → `!true && ...` = `false` (short-circuit). Guard skipped entirely. Flatten dispatched regardless of position state.

**CYC impact**: +1 branch (the `&&` short-circuit). Spec-comment CYC: 4 → 5. Strict McCabe: 6 → 7. Both within ≤ 8 limit.

### Parameter Ordering Rationale

`orderName` is inserted as the 4th parameter (after `state`, before `rule`) because:
1. It is logically part of the "order descriptor" group (account, instrument, state, orderName).
2. The call site passes `e.Order.Account`, `e.Order.Instrument`, `e.Order.OrderState`, `e.Order.Name` — a natural 4-tuple from a single `Order` object.
3. `rule`, `isFollower`, `hasOpenPosition`, `flattenOne` are the "policy/dependency injection" group and remain after.

---

## Section 5 — Architecture: Call-Site Update in OnOrderUpdate

### Current Call (line 651-653, 7 args)

```csharp
if (TryDispatchLeaderFlat(
        e.Order.Account, e.Order.Instrument, e.Order.OrderState, matchedRule.Value,
        IsFollowerAccount, HasOpenPosition, FlattenOneAccount)) return;
```

### New Call (8 args — add e.Order.Name as 4th arg)

```csharp
if (TryDispatchLeaderFlat(
        e.Order.Account, e.Order.Instrument, e.Order.OrderState, e.Order.Name,
        matchedRule.Value,
        IsFollowerAccount, HasOpenPosition, FlattenOneAccount)) return;
```

**Change**: Insert `e.Order.Name,` after `e.Order.OrderState,` on line 652.

`e.Order.Name` is the same `Order` object referenced on lines 640-660. No null risk:
NT8 `Order.Name` is never null for a filled/cancelled order — it is set by the platform at
order submission time (NT8_FULL_REFERENCE.md lines 844-845).

### Other Callers

`TryDispatchLeaderFlat` is `private static`. Only one call site exists in the codebase:
`OnOrderUpdate` at line 651. No other updates required.

---

## Section 6 — Existing B61 Tests: Impact Analysis

### Reflection Helper Compatibility

`GetTryDispatchLeaderFlat()` at CopyEngineTests.cs line 2856 uses `GetMethod` by name only
(no type array for parameter matching). After B65 changes the signature from 7 to 8 parameters,
the reflection call still resolves correctly because there is only one method named
`TryDispatchLeaderFlat` (no overloads). No change to the reflection helper is required.

### Object[] Invocation Updates Required

All existing B61 test invocations pass `object[]` with 7 elements. The new 8-parameter
signature requires 8 elements. Failing to update causes `TargetParameterCountException`
at runtime.

**5 invocations need updating** (4 primary tests + 1 Cancelled sub-assertion in T_B61_04):

| Test | Current position 4 (0-indexed) | Insert at position 3 | Behavior impact |
|---|---|---|---|
| T_B61_01 | `ruleVal` | `"BuyLimit"` (non-native) | Guard (3): `!IsNativeExitName("BuyLimit")=true` → still checks `hasOpenPosition=true` → still returns `false` |
| T_B61_02 | `ruleVal` | `"BuyLimit"` | State guard fires before orderName check → still returns `false` |
| T_B61_03 | `ruleVal` | `"BuyLimit"` | Follower guard fires before orderName check → still returns `false` |
| T_B61_04 (primary) | `ruleVal` | `"BuyLimit"` | Non-native exit, `hasOpenPosition=false` → guard passes → still returns `true` |
| T_B61_04 (Cancelled) | `ruleVal` | `"BuyLimit"` | Cancelled state, non-native, `hasOpenPosition=false` → still returns `true` |

All 5 existing assertions remain valid after the orderName addition. No test outcome changes.

---

## Section 7 — Tests Required (T_B65_01 through T_B65_09)

All tests are xUnit `[Fact]` only. No NUnit, no MSTest.

### T_B65_01 — IsNativeExitName_Null_ReturnsFalse

**Setup**: `IsNativeExitName(null)`
**Assert**: `false`
**Rationale**: Null guard must return false without NullReferenceException. JS-001 compliance.

---

### T_B65_02 — IsNativeExitName_Close_ReturnsTrue

**Setup**: `IsNativeExitName("Close")`
**Assert**: `true`
**Rationale**: "Close" is the NT8 Close button order name (NT8_FULL_REFERENCE.md line 845).
Must be classified as a native exit to bypass position-race guard.

---

### T_B65_03 — IsNativeExitName_Flatten_ReturnsTrue

**Setup**: `IsNativeExitName("Flatten")`
**Assert**: `true`
**Rationale**: "Flatten" is the NT8 Flatten-all-positions order name. Must be classified as
a native exit.

---

### T_B65_04 — IsNativeExitName_RevPrefix_ReturnsTrue

**Setup**: `IsNativeExitName("RevLong")`
**Assert**: `true`
**Rationale**: NT8 reversal orders use "Rev..." prefix (e.g. "RevLong", "RevShort").
`StartsWith("Rev", Ordinal)` captures all variants. Tests a non-trivial prefix case.

---

### T_B65_05 — IsNativeExitName_ExitPrefix_ReturnsTrue

**Setup**: `IsNativeExitName("ExitLong")`
**Assert**: `true`
**Rationale**: NinjaScript strategy exit signals use "Exit..." prefix (e.g. "ExitLong",
"ExitShort"). `StartsWith("Exit", Ordinal)` captures all variants.

---

### T_B65_06 — IsNativeExitName_PttFlatten_ReturnsFalse

**Setup**: `IsNativeExitName("PTT-Flatten")`
**Assert**: `false`
**Rationale**: "PTT-Flatten" is a PTT synthetic order, not a native NT8 exit. It must NOT
trigger the position-race bypass — PTT orders operate on follower accounts, not leader
positions. This is the critical boundary test separating native from synthetic exits.

---

### T_B65_07 — IsNativeExitName_ArbitrarySignal_ReturnsFalse

**Setup**: `IsNativeExitName("BuyLimit")`
**Assert**: `false`
**Rationale**: Arbitrary non-exit order names (entries, adjustments) must return false.
Ensures the helper does not over-match.

---

### T_B65_08 — TryDispatchLeaderFlat_NativeExitFilled_BypassesPositionRace

**Setup**:
- `orderName = "Close"`, `state = OrderState.Filled`
- `isFollower` = `(_ => false)` (leader account)
- `hasOpenPosition` = `((_, __) => true)` (position still shows open — race condition)
- `flattenOne` = counter delegate
- Rule with 0 followers (guards-only test)

**Assert**: `result == true` AND `flattenOne` was NOT called (0 followers in rule)
**Rationale**: This is the primary regression test for DW-B65-01. Despite `hasOpenPosition`
returning `true` (simulating the NT8 position-update race), the native exit name "Close"
causes guard (3) to be bypassed. The method returns `true` (flatten dispatched). With 0
followers in the rule, `flattenOne` call count = 0.

**Extended variant**: Add a sub-assertion with `orderName = "ExitLong"` to confirm the
same bypass for the Exit-prefix family.

---

### T_B65_09 — TryDispatchLeaderFlat_NonExitFilled_LeaderHasPosition_SkipsFlat

**Setup**:
- `orderName = "BuyLimit"`, `state = OrderState.Filled`
- `isFollower` = `(_ => false)` (leader account)
- `hasOpenPosition` = `((_, __) => true)` (leader has open position — legitimate state)
- `flattenOne` = counter delegate
- Rule with 0 followers

**Assert**: `result == false` AND `flattenOne` was not called
**Rationale**: A non-exit fill (e.g. an entry order) with the leader still holding a
position must NOT trigger follower flatten. Guard (3) must still block. Confirms that the
bypass is exclusive to native exit names.

---

## Section 8 — Jane Street Compliance

| Rule | Status | Evidence |
|---|---|---|
| JS-021: no lock() | PASS | `IsNativeExitName` and modified `TryDispatchLeaderFlat` are pure static methods with no shared mutable state. No `lock()` introduced. |
| JS-001: no throw | PASS | Both methods return `bool` at all code paths. No exceptions thrown. |
| JS-002: no return null | PASS | Both methods return `bool`. Null is impossible as a return value. |
| CYC ≤ 8 | PASS | `IsNativeExitName` CYC=6. `TryDispatchLeaderFlat` CYC=5 (spec) / 7 (strict McCabe). Both within limit. |
| ASCII-only strings | PASS | All string literals: "Close", "Flatten", "Rev", "Exit", "PTT-Flatten", "BuyLimit" — all ASCII. No Unicode. |
| xUnit [Fact] only | PASS | All 9 new tests use xUnit `[Fact]`. No NUnit, no MSTest. |
| DateTime.UtcNow (not .Now) | N/A | No DateTime usage in changed methods. |
| No FontFamily | N/A | No UI code touched. |
| Dispatcher.InvokeAsync | N/A | Pure static helpers; no WPF interaction. |

---

## Section 9 — Scan Checklist (7 Scans — Engineer and Verifier Contract)

All 7 scans must be run by both ptt-engineer (before commit) and ptt-verifier (during verification).
Expected results listed; any deviation is a blocking failure.

**SCAN-01 — lock() scan**
```powershell
grep -n "lock(" src/PropTraderTools/CopyEngine.cs
```
Expected: zero results (no lock() anywhere in the file).

**SCAN-02 — throw scan**
```powershell
grep -n "throw new" src/PropTraderTools/CopyEngine.cs
```
Expected: zero results in modified or newly-added code. Pre-existing throws (if any) are pre-existing and must not be increased.

**SCAN-03 — return null scan**
```powershell
grep -n "return null" src/PropTraderTools/CopyEngine.cs
```
Expected: zero results in `IsNativeExitName` and `TryDispatchLeaderFlat`. Pre-existing elsewhere are pre-existing.

**SCAN-04 — CYC scan**
```powershell
python scripts/complexity_audit.py
```
Expected: `IsNativeExitName` reports CYC ≤ 8. `TryDispatchLeaderFlat` reports CYC ≤ 8.
Any CYC > 8 is a blocking failure requiring extraction before commit.

**SCAN-05 — ASCII scan**
```powershell
grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs
```
Expected: results match the pre-existing non-ASCII lines (lines 398, 499, 1376, 1377 per
PRE-EXISTING-01/02 in deferred backlog). No new non-ASCII lines introduced by B65.

**SCAN-06 — Build scan**
```powershell
dotnet build src/PropTraderTools/PropTraderTools.csproj
```
Expected: zero errors, zero new warnings. Build must succeed with the 8-param signature.

**SCAN-07 — Test scan**
```powershell
dotnet test
```
Expected: All T_B65_01 through T_B65_09 PASS. All T_B61_01 through T_B61_04 still PASS
(after object[] updated to 8 elements). All pre-existing tests PASS. Zero test failures.

---

## Section 10 — Files Changed

| File | Change type | Description |
|---|---|---|
| `src/PropTraderTools/CopyEngine.cs` | Insert + Modify | (1) Insert `IsNativeExitName` after line 758 (after `IsExitSignalName` closing brace). (2) Modify `TryDispatchLeaderFlat` signature to add `string orderName` as 4th param. (3) Replace guard (3) with the `!IsNativeExitName(orderName) &&` compound form. (4) Update comment block for `TryDispatchLeaderFlat` to reflect new CYC and NT8 citation. |
| `src/PropTraderTools/CopyEngineTests.cs` | Insert + Modify | (1) Add T_B65_01 through T_B65_09 as new `[Fact]` methods (insert after the T_B61 region). (2) Update T_B61_01, T_B61_02, T_B61_03 object[] invocations to 8 elements (add `"BuyLimit"` as 4th element). (3) Update T_B61_04 primary invocation to 8 elements. (4) Update T_B61_04 Cancelled sub-invocation (lines 2993-2999) to 8 elements. Total: 5 object[] invocations updated. |

**No other files are touched.**

---

## Section 11 — Deferred Items

### Items CLOSED This Block

#### DW-B65-01 (= DW-B60-01) — Leader manual close does not close follower position

**Status**: CLOSED by B65-LaneA Ticket-1
**Resolution**: `IsNativeExitName` helper + `TryDispatchLeaderFlat` guard (3) bypass.
When leader closes via native NT8 exit (Close/Flatten/Rev*/Exit*), followers are
flattened unconditionally, bypassing the NT8 position-update race (line 1721).

#### DW-B59-02 — IsExitSignalName uses exact "Rev" match instead of prefix

**Status**: CLOSED (confirmed already fixed in B60/B62)
**Evidence**: `CopyEngine.cs` line 755 shows `name.StartsWith("Rev", StringComparison.Ordinal)`
already in production. The B62 backlog listed this as OPEN because the fix was applied
but not formally acknowledged in the deferred-backlog closure. `IsNativeExitName` inherits
the correct `StartsWith("Rev")` pattern from day 1 of B65. No further action required.

---

### Items OPEN — Carry Forward

#### DW-B58-01 — SnapshotTargetsPublic hardcoded order-name prefixes

**Priority**: P2 | **Status**: OPEN
Hardcoded prefixes `PTT-QX-T` and `PTT-TGT-` in `SnapshotTargetsPublic`. Future PTT
order name additions must update this method.

#### DW-B58-02 — GlobalBe non-atomic lazy init

**Priority**: P2 | **Status**: OPEN
`if (_globalBe == null) _globalBe = new ...` is non-atomic. Currently safe (UI-thread-only
callers). Requires `Interlocked.CompareExchange` if a non-UI-thread caller is introduced.

#### DW-B58-03 — RelayBe does not forward OcoGroup from BeEventArgs

**Priority**: P2 | **Status**: OPEN
`RelayBe` generates its own `OcoId` via `NextQxOcoId()`. Future correlated OcoId fan-out
across accounts requires a new `SubmitBeStop` overload.

#### DW-B54-01 — ATM auto-inject (blocked)

**Priority**: P1 | **Status**: OPEN — blocked
`AtmStrategyCreate()` is `StrategyBase`-only. `AddOnBase` (`TradeCopierAddOn`) cannot call
this API. Requires a companion `StrategyBase` add-in. Deferred indefinitely.

#### PRE-EXISTING-01 — Non-ASCII characters at CopyEngine.cs lines 398, 499

**Priority**: P2 | **Status**: OPEN — pre-existing
Em-dash Unicode in B56 BUILD-FIX stub markers. Not introduced by B65.

#### PRE-EXISTING-02 — Non-ASCII characters at CopyEngine.cs lines 1376, 1377

**Priority**: P2 | **Status**: OPEN — pre-existing
Unicode arrow characters in exit-order direction comments. Not introduced by B65.
(Note: line numbers may shift slightly after B65 inserts ~25 lines for `IsNativeExitName`.)

#### PRE-EXISTING-03 — deploy-sync.ps1 archived; PropTraderTools sync is manual

**Priority**: P2 | **Status**: OPEN — pre-existing
Manual SHA-256 copy + `verify_links.ps1 -Fix` is the current PropTraderTools deploy workflow.
No change in B65.

---

*End of B65-LaneA Architecture Plan*
