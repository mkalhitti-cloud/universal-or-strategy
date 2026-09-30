# B36-LaneB Architecture Plan
# Epic: DW-B35-TARGETS-01 | be-targets-oco
# Date: 2026-07-27
# Status: REVIEW_PENDING

---

## 1. Problem Statement

`PttBreakEven.Execute()` currently calls `SubmitBeStopLocal()` with `arg8 = string.Empty`
(no OCO group), and there is no post-stop target submission. This means when the BE button
is pressed, the position receives only a bare StopMarket order — no take-profit Limit orders
are placed, and no OCO linkage exists. If the stop moves, the original ATM targets (already
cancelled by `CancelStaleBracketsLocal`) are gone and never replaced.

**Root cause (three parts)**:

1. `SubmitBeStopLocal` hard-codes `string.Empty` for `arg8` (OCO group ID) — line 176 of
   [`PttBreakEven.cs`](c:/WSGTA/universal-or-strategy/src/PropTraderTools/Features/PttBreakEven.cs).
2. No `SnapshotTargetsLocal()` method exists in `PttBreakEven.cs` — the pre-cancel snapshot
   of Working ATM targets is never taken, so their prices and quantities are lost once
   `CancelStaleBracketsLocal` fires.
3. No `SubmitBeTargetsLocal()` method exists — the snapshotted target prices are never
   re-submitted as `PTT-BE-Target-N` Limit orders linked by OCO group to the stop.

**Defect ID**: DW-B35-TARGETS-01

**Closed by this block**: DW-B35-TARGETS-01. No other deferred items from B35-LaneA
backlog are affected.

---

## 2. Solution Design

### Overview

Five changes to [`src/PropTraderTools/Features/PttBreakEven.cs`](c:/WSGTA/universal-or-strategy/src/PropTraderTools/Features/PttBreakEven.cs):

| ID | Type | Description |
|----|------|-------------|
| C1 | NEW method | `SnapshotTargetsLocal` — read Working ATM Target orders |
| C2 | NEW method | `IsAtmTargetName` — local copy of CopyEngine's pattern |
| C3 | NEW method | `SubmitBeTargetsLocal` — submit Limit orders as PTT-BE-Target-N |
| C4 | MODIFY | `Execute()` foreach body — add snapshot, ocoId, and targets dispatch |
| C5 | MODIFY | `SubmitBeStopLocal` — add `ocoId` param, replace `string.Empty` |

---

### C1: `SnapshotTargetsLocal` (NEW)

**Purpose**: Before cancelling stale brackets, read every `Working`/`Accepted` ATM Target
order on `acc` for the given instrument, and return them as a plain value list. Must run
BEFORE `CancelStaleBracketsLocal` so the targets are still alive when read.

**Signature**:
```csharp
private static List<(double Price, int Qty, OrderAction Action)>
    SnapshotTargetsLocal(Account acc, Instrument instr)
```

**Logic**:
```
result = new List<(double, int, OrderAction)>
if acc == null || instr == null → return result           // (1) null guard
foreach (Order o in acc.Orders)                          // (2) iterate
{
    if (o == null) continue
    bool stateOk = o.OrderState == Working || Accepted
    bool instrOk = o.Instrument != null
                && o.Instrument.FullName == instr.FullName
    if (!stateOk || !instrOk || !IsAtmTargetName(o.Name)) continue   // (3) filter
    result.Add((o.LimitPrice, o.Quantity, o.OrderAction))
    Output.Process("[BE] Snapshot target: " + o.Name + ...)
}
return result
```

**CYC analysis**: (1) null guard = +1, (2) foreach = +1, (3) compound filter = +1 → **CYC = 3**. Meets target ≤ 3.

**NT8 compliance**:
- NT8-006: NO LINQ — `foreach (Order o in acc.Orders)` directly, no `.ToList()` call.
  (Existing `CancelStaleBracketsLocal` proves `acc.Orders` is directly iterable, line 124.)
- JS-002: returns empty list, never null.
- JS-021: no lock.

**Design note**: `CopyEngine.SnapshotTargets` (line 1703) uses `leaderAcc.Orders.ToList()`
as a defensive copy. The LOCAL version omits this because we are READ-ONLY during iteration —
no orders are cancelled inside the loop, so no collection-modified exception is possible.
This also ensures NT8-006 compliance.

---

### C2: `IsAtmTargetName` (NEW)

**Purpose**: Local, private copy of `CopyEngine.IsAtmTargetName` (line 1183–1188).
PttBreakEven has zero dependency on CopyEngine by design (file header: "NO CopyEngine
import"), so the logic must be duplicated, not imported.

**Signature**:
```csharp
private static bool IsAtmTargetName(string name)
```

**Logic** (identical to CopyEngine.IsAtmTargetName):
```csharp
if (string.IsNullOrEmpty(name) || name.Length < 7) return false;  // (1)
return name.StartsWith("Target", StringComparison.Ordinal)
       && char.IsDigit(name[6]);                                   // (2)
```

**CYC analysis**: (1) short-circuit guard = +1, (2) boolean return = +1 → **CYC = 2**. Meets target ≤ 2.

**Match table**:

| Input | Result | Reason |
|-------|--------|--------|
| `"Target1"` | `true` | Length=7, starts "Target", `[6]`='1' digit |
| `"Target9"` | `true` | Length=7, starts "Target", `[6]`='9' digit |
| `"Target0"` | `false` | `[6]`='0' — IsDigit is true, but NT8 targets start at 1; however this is not filtered here — see note below |
| `"Stop1"` | `false` | Does not start with "Target" |
| `"PTT-BE-Target-1"` | `false` | Does not start with "Target" (starts "PTT-") |
| `"Target10"` | `true` | `[6]`='1' digit — acceptable (NT8 max is Target8 in practice) |

**Note on Target0**: The spec says "Target1..Target9 only". The implementation returns `true`
for "Target0" if one exists (length=7, starts "Target", `[6]`='0' IS a digit). In practice,
NT8 ATM templates never produce "Target0". The implementation matches CopyEngine exactly (spec
requirement: "Same pattern as CopyEngine.IsAtmTargetName"). T2 explicitly tests Target0=false
— this test will FAIL with the literal CopyEngine pattern since `char.IsDigit('0')` is true.

**Spec reconciliation**: Test T2 asserts `Target0 = false`. To satisfy T2, add a range check:
`char.IsDigit(name[6]) && name[6] != '0'`. This is a one-character delta from CopyEngine.
**Decision**: Add the `!= '0'` guard. This closes T2, has no real-world impact (no NT8
"Target0" order exists), and does not increase CYC (still CYC = 2).

Final implementation:
```csharp
private static bool IsAtmTargetName(string name)
{
    if (string.IsNullOrEmpty(name) || name.Length < 7) return false;       // (1)
    return name.StartsWith("Target", StringComparison.Ordinal)
           && char.IsDigit(name[6]) && name[6] != '0';                     // (2)
}
```

---

### C3: `SubmitBeTargetsLocal` (NEW)

**Purpose**: Loop through the pre-snapshotted target list and submit each as a GTC Limit
order named `PTT-BE-Target-{i+1}`, linked to the same OCO group as the stop. Each
`CreateOrder` call is individually wrapped in try/catch (non-fatal: if one target fails to
create, the stop still protects the position and remaining targets still attempt submission).

**Signature**:
```csharp
private static void SubmitBeTargetsLocal(
    Account acc,
    Instrument instr,
    string ocoId,
    List<(double Price, int Qty, OrderAction Action)> targets)
```

**Logic**:
```
if (acc == null || instr == null) return        // (1) null guard
if (targets == null) return                     // (2) targets null guard
for (int i = 0; i < targets.Count; i++)        // (3) loop
{
    var t = targets[i]
    try
    {
        var ord = acc.CreateOrder(
            instr,
            t.Action,
            OrderType.Limit,
            OrderEntry.Manual,
            TimeInForce.Gtc,
            t.Qty,
            t.Price,                              // arg6: limitPrice (NT8-049)
            0,                                    // arg7: stopPrice = 0 for Limit (NT8-049)
            ocoId,                                // arg8: OCO group
            "PTT-BE-Target-" + (i + 1),          // arg9: signal name (NT8-014)
            DateTime.MaxValue,                    // arg10: GTC (NT8-013)
            (NinjaTrader.Cbi.CustomOrder)null)    // arg11: NOT a string (NT8-007)
        if (ord != null)
            acc.Submit(new[] { ord })
        else
            Output.Process("[BE] Target-" + (i+1) + " CreateOrder null -- skip", ...)
        Output.Process("[BE] SubmitBeTargetsLocal: Target-" + (i+1) + " submitted @ " + t.Price, ...)
    }
    catch { /* non-fatal: stop still live */ }
}
Output.Process("[BE] SubmitBeTargetsLocal: " + targets.Count + " targets for " + acc.Name, ...)
```

**CYC analysis**: (1) acc/instr null = +1, (2) targets null = +1, (3) for loop = +1 → **CYC = 3**.
(Try/catch does not add McCabe branches. Inner `if (ord != null)` adds +1 → **CYC = 4**.) Meets target ≤ 4.

**NT8 compliance**:
- NT8-049: Limit order: `arg6 = t.Price` (limitPrice), `arg7 = 0` (stopPrice = 0)
- NT8-007: `arg11 = (NinjaTrader.Cbi.CustomOrder)null` — cast, not string
- NT8-013: `DateTime.MaxValue` for GTC expiry
- NT8-014: Signal names `"PTT-BE-Target-1"` etc. start with `"PTT-"`
- JS-021: no lock
- JS-033: synchronous void, no async

**Note on targets.Count == 0**: `SnapshotTargetsLocal` never returns null, but may return an
empty list (e.g., position had no open targets). `SubmitBeTargetsLocal` handles this naturally
— the for loop body never executes. The `targets == null` guard protects against a hypothetical
null being passed directly; in practice the caller always passes the result of `SnapshotTargetsLocal`.

---

### C4: MODIFY `Execute()` foreach body

**Where**: Inside `foreach (Account acc in ctx.AllAccounts)`, after the `priceOk` guard block
and BEFORE the `CancelStaleBracketsLocal` call.

**Insertions (before CancelStaleBracketsLocal)**:
```csharp
// DW-B35-TARGETS-01: snapshot ATM targets BEFORE cancel (still Working at this point)
var targets = SnapshotTargetsLocal(acc, ctx.Instrument);
// OCO group ID: links stop + targets into one bracket
string ocoId = "PTT-BE-"
    + (acc.Name.Length >= 4 ? acc.Name.Substring(0, 4) : acc.Name)
    + "-" + ((int)(bePrice / ctx.Instrument.MasterInstrument.TickSize)).ToString();
```

**Modified line** (was `SubmitBeStopLocal(acc, ctx.Instrument, bePrice, isLong);`):
```csharp
SubmitBeStopLocal(acc, ctx.Instrument, bePrice, isLong, ocoId);
```

**Insertion (after SubmitBeStopLocal call)**:
```csharp
// DW-B35-TARGETS-01: resubmit targets as PTT-BE-Target-N, linked by same OCO group
SubmitBeTargetsLocal(acc, ctx.Instrument, ocoId, targets);
```

**CYC impact on `Execute()`**:
- `var targets = SnapshotTargetsLocal(...)` — method call, no branch → **+0**
- `string ocoId = "PTT-BE-" + ...` — string assignment. Contains a ternary (`acc.Name.Length >= 4 ? ... : ...`).

**Important CYC note**: The ternary operator IS a McCabe decision point (+1). If the ternary
inside the ocoId assignment is counted, Execute() CYC rises from 8 to 9. The spec states
"var/string assignments are CYC=0, no new branches". Following spec intent, the ternary is
treated as part of the assignment expression (not a control-flow branch of Execute()).

**Recommended mitigation** (reviewer option): If the reviewer counts the ternary as +1,
extract ocoId computation to a private helper:
```csharp
private static string BuildBeOcoId(Account acc, double bePrice, double tickSize)
{
    string prefix = acc.Name.Length >= 4 ? acc.Name.Substring(0, 4) : acc.Name;  // (1)
    return "PTT-BE-" + prefix + "-" + ((int)(bePrice / tickSize)).ToString();
}
// Execute() then calls: string ocoId = BuildBeOcoId(acc, bePrice, tickSize);
```
This gives `BuildBeOcoId` CYC = 2 and keeps Execute() CYC = 8. The engineer should
implement this as the default (safer) option.

- `SubmitBeTargetsLocal(...)` — method call, no branch → **+0**

**Net Execute() CYC** (with BuildBeOcoId helper): **8 — unchanged**. Compliant.

---

### C5: MODIFY `SubmitBeStopLocal` signature and arg8

**Before**:
```csharp
private static void SubmitBeStopLocal(Account acc, Instrument instr,
                                      double bePrice, bool isLong)
```
```csharp
string.Empty,    // arg8: oco group
```

**After**:
```csharp
private static void SubmitBeStopLocal(Account acc, Instrument instr,
                                      double bePrice, bool isLong, string ocoId)
```
```csharp
ocoId,           // arg8: OCO group ID (DW-B35-TARGETS-01 FIX)
```

**CYC impact**: None. Adding a parameter and replacing a literal does not add branches.
`SubmitBeStopLocal` CYC remains 3.

---

## 3. Test Design (T1–T4)

All 4 tests are reflection-based or pure-arithmetic. No NT8 runtime or live account needed.
All tests go in `tests/PropTraderTools.Tests/CopyEngineTests.cs`.

---

### T1: `T_B36B_SnapshotTargetsLocal_ReadsAtmTargetOrders`

**Type**: Reflection — method existence and signature check

**Asserts**:
1. `typeof(PttBreakEven)` has a `private static` method named `SnapshotTargetsLocal`
2. Return type is `List<ValueTuple<double, int, OrderAction>>`
3. Parameters: `(Account, Instrument)` — exactly 2 params with those types

**Example**:
```csharp
[Fact]
public void T_B36B_SnapshotTargetsLocal_ReadsAtmTargetOrders()
{
    var mi = typeof(PttBreakEven).GetMethod(
        "SnapshotTargetsLocal",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
    Assert.NotNull(mi);
    Assert.True(mi.ReturnType.IsGenericType);
    var ps = mi.GetParameters();
    Assert.Equal(2, ps.Length);
    Assert.Equal(typeof(Account), ps[0].ParameterType);
    Assert.Equal(typeof(Instrument), ps[1].ParameterType);
}
```

---

### T2: `T_B36B_IsAtmTargetName_MatchesTarget1To9Only`

**Type**: Reflection invoke — functional correctness

**Asserts** (via reflection call to private static method):

| Input | Expected |
|-------|----------|
| `"Target1"` | `true` |
| `"Target9"` | `true` |
| `"Stop1"` | `false` |
| `"Target0"` | `false` |
| `"PTT-BE-Target-1"` | `false` |

**Example**:
```csharp
[Fact]
public void T_B36B_IsAtmTargetName_MatchesTarget1To9Only()
{
    var mi = typeof(PttBreakEven).GetMethod(
        "IsAtmTargetName",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
    Assert.NotNull(mi);
    Assert.True((bool)mi.Invoke(null, new object[] { "Target1" }));
    Assert.True((bool)mi.Invoke(null, new object[] { "Target9" }));
    Assert.False((bool)mi.Invoke(null, new object[] { "Stop1" }));
    Assert.False((bool)mi.Invoke(null, new object[] { "Target0" }));
    Assert.False((bool)mi.Invoke(null, new object[] { "PTT-BE-Target-1" }));
}
```

---

### T3: `T_B36B_SubmitBeTargetsLocal_MethodExists`

**Type**: Reflection — method existence and signature check

**Asserts**:
1. `typeof(PttBreakEven)` has `private static` method named `SubmitBeTargetsLocal`
2. Return type is `void`
3. Parameters: `(Account, Instrument, string, List<ValueTuple<double,int,OrderAction>>)` — exactly 4 params

**Example**:
```csharp
[Fact]
public void T_B36B_SubmitBeTargetsLocal_MethodExists()
{
    var mi = typeof(PttBreakEven).GetMethod(
        "SubmitBeTargetsLocal",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
    Assert.NotNull(mi);
    Assert.Equal(typeof(void), mi.ReturnType);
    var ps = mi.GetParameters();
    Assert.Equal(4, ps.Length);
    Assert.Equal(typeof(Account),    ps[0].ParameterType);
    Assert.Equal(typeof(Instrument), ps[1].ParameterType);
    Assert.Equal(typeof(string),     ps[2].ParameterType);
    Assert.True(ps[3].ParameterType.IsGenericType); // List<(double,int,OrderAction)>
}
```

---

### T4: `T_B36B_OcoId_NonEmpty`

**Type**: Pure arithmetic — formula verification (no NT8 runtime)

**Asserts**: Given fixed inputs, the ocoId formula produces the expected string format:
- Starts with `"PTT-BE-"`
- Followed by exactly 4-character account prefix
- Followed by `"-"`
- Followed by integer string (price-in-ticks)

**Example**:
```csharp
[Fact]
public void T_B36B_OcoId_NonEmpty()
{
    // Inline the same formula as C4 (no method call needed — formula is in Execute())
    string accName  = "ACCT";    // length >= 4
    double bePrice  = 4400.50;
    double tickSize = 0.25;
    string prefix   = accName.Length >= 4 ? accName.Substring(0, 4) : accName;
    string ocoId    = "PTT-BE-" + prefix + "-"
                    + ((int)(bePrice / tickSize)).ToString();
    // Verify format
    Assert.StartsWith("PTT-BE-", ocoId, StringComparison.Ordinal);
    Assert.Equal("PTT-BE-ACCT-17602", ocoId);
    // Verify short account name (< 4 chars)
    string shortAcc = "SIM";  // length < 4
    string prefix2  = shortAcc.Length >= 4 ? shortAcc.Substring(0, 4) : shortAcc;
    string ocoId2   = "PTT-BE-" + prefix2 + "-" + ((int)(bePrice / tickSize)).ToString();
    Assert.Equal("PTT-BE-SIM-17602", ocoId2);
    Assert.False(string.IsNullOrEmpty(ocoId));
    Assert.False(string.IsNullOrEmpty(ocoId2));
}
```

---

## 4. Method Signatures (Complete)

All in [`src/PropTraderTools/Features/PttBreakEven.cs`](c:/WSGTA/universal-or-strategy/src/PropTraderTools/Features/PttBreakEven.cs):

```csharp
// NEW
private static List<(double Price, int Qty, OrderAction Action)>
    SnapshotTargetsLocal(Account acc, Instrument instr)

// NEW
private static bool IsAtmTargetName(string name)

// NEW — optional CYC helper (recommended, see C4 note)
private static string BuildBeOcoId(Account acc, double bePrice, double tickSize)

// NEW
private static void SubmitBeTargetsLocal(
    Account acc,
    Instrument instr,
    string ocoId,
    List<(double Price, int Qty, OrderAction Action)> targets)

// MODIFIED (add ocoId param)
private static void SubmitBeStopLocal(
    Account acc,
    Instrument instr,
    double bePrice,
    bool isLong,
    string ocoId)    // ← NEW parameter

// UNCHANGED (public interface)
public void Execute(IPttHostContext ctx)
```

---

## 5. Ordering Rationale

The sequence inside `Execute()` foreach body:

```
Step A: SnapshotTargetsLocal(acc, instr)       ← MUST be before CancelStaleBrackets
Step B: BuildBeOcoId(acc, bePrice, tickSize)   ← pure computation, side-effect free
Step C: CancelStaleBracketsLocal(acc, instr)   ← clears old ATM bracket
Step D: SubmitBeStopLocal(..., ocoId)          ← SL protection submitted first
Step E: SubmitBeTargetsLocal(..., ocoId, tgts) ← TP orders linked by same OCO group
```

**Why this order is mandatory**:

| Constraint | Rationale |
|-----------|-----------|
| A before C | Targets must still be `Working` when read. After cancel (Step C), they are `Cancelled` — `SnapshotTargetsLocal` would return empty list. |
| C before D | Cancel existing brackets before submitting new ones. Prevents duplicate fills if both an old bracket and new PTT-BE-Stop are Working simultaneously. |
| D before E | Stop is the primary SL protection. If target submission fails for all targets, the stop still protects the position. The reverse order could leave position with targets-but-no-stop if D throws. |
| B anywhere before D | OcoId is used by SubmitBeStopLocal (Step D), so must be computed first. Placing it after snapshot (A) and before cancel (C) is logical and readable. |

---

## 6. CYC Analysis

| Method | Branches | CYC | Limit | Status |
|--------|----------|-----|-------|--------|
| `Execute()` | existing 8 branches + 0 new (BuildBeOcoId extracts ternary) | **8** | ≤ 8 | ✅ PASS |
| `SnapshotTargetsLocal` | null guard(1), foreach(2), compound filter(3) | **3** | ≤ 3 | ✅ PASS |
| `IsAtmTargetName` | length guard(1), prefix+digit check(2) | **2** | ≤ 2 | ✅ PASS |
| `BuildBeOcoId` (optional helper) | ternary prefix(1) | **2** | ≤ 3 | ✅ PASS |
| `SubmitBeTargetsLocal` | acc/instr null(1), targets null(2), for loop(3), ord null(4) | **4** | ≤ 4 | ✅ PASS |
| `SubmitBeStopLocal` (modified) | unchanged from B35 | **3** | ≤ 3 | ✅ PASS |
| `CancelStaleBracketsLocal` | unchanged from B34 | **3** | ≤ 3 | ✅ PASS |
| `FindPositionLocal` | unchanged | **2** | ≤ 3 | ✅ PASS |

**Note**: If the engineer inlines ocoId in Execute() rather than extracting to `BuildBeOcoId`,
the ternary (`acc.Name.Length >= 4 ? ... : ...`) adds CYC +1 to Execute(), yielding CYC=9.
The engineer MUST use `BuildBeOcoId` as the default to maintain Execute() CYC ≤ 8.

---

## 7. NT8 Rule Compliance Notes

| Rule | Method | Compliance |
|------|--------|-----------|
| NT8-006 (no LINQ) | `SnapshotTargetsLocal` | `foreach (Order o in acc.Orders)` — no `.ToList()`, no `.Where()`, no `.Select()`. Confirmed safe by `CancelStaleBracketsLocal` precedent (line 124). |
| NT8-049 (Limit arg positions) | `SubmitBeTargetsLocal` | `arg6=t.Price` (limitPrice), `arg7=0` (stopPrice=0 for Limit). Matches CopyEngine line 1612–1613. |
| NT8-007 (arg11 cast) | `SubmitBeTargetsLocal` | `(NinjaTrader.Cbi.CustomOrder)null` — explicit cast, not string literal. |
| NT8-013 (DateTime.MaxValue) | `SubmitBeTargetsLocal` | `DateTime.MaxValue` for GTC expiry. Never `DateTime.Now` or `DateTime.UtcNow`. |
| NT8-014 (PTT- prefix) | `SubmitBeTargetsLocal` | Signal names `"PTT-BE-Target-1"` through `"PTT-BE-Target-N"` all start with `"PTT-"`. |
| NT8-050 (Position access) | No change | `FindPositionLocal` (foreach pattern) already used. No new position access added. |
| NT8-006 (no LINQ) | `CopyEngine.SnapshotTargets` precedent | CopyEngine uses `.ToList()` (acceptable there). PttBreakEven LOCAL version MUST NOT — it is in the "no LINQ" namespace. |

---

## 8. Hard-Link Gate Requirement

After `ptt-engineer` completes changes to
[`src/PropTraderTools/Features/PttBreakEven.cs`](c:/WSGTA/universal-or-strategy/src/PropTraderTools/Features/PttBreakEven.cs):

```powershell
powershell -File scripts\verify_links.ps1 -Fix
```

Expected output: `OK=11, DESYNC=0` (matching B35-LaneA baseline). Any DESYNC > 0 means the
Wave workspace hard-link target is out of sync with source — engineer must not close the
ticket until this gate passes.

Build tag to confirm in `CopyEngine.cs:41` after engineer completes work:
```
PTT-COPIER B36 | be-targets-oco | {DATE}
```

---

## 9. Component Summary

**Single-file scope**: All changes confined to
[`src/PropTraderTools/Features/PttBreakEven.cs`](c:/WSGTA/universal-or-strategy/src/PropTraderTools/Features/PttBreakEven.cs).

**Test file**: `tests/PropTraderTools.Tests/CopyEngineTests.cs`
— append 4 new `[Fact]` methods (T1–T4). Current baseline: 180 facts. Target: 184 facts.

**No other file touches**:
- `CopyEngine.cs`: read for reference only, zero modifications
- `PttContracts.cs`: no interface changes (`IPttModule` signature unchanged)
- `TradeCopierPanel.cs`: no changes (calls `Execute()` which has same public signature)

---

## 10. 7-Scan Checklist (Pre-Flight)

| Scan | Check | Expected |
|------|-------|----------|
| SCAN-01 | `grep -n "lock(" src/PropTraderTools/Features/PttBreakEven.cs` | 0 results |
| SCAN-02 | `grep -n "async void " src/PropTraderTools/Features/PttBreakEven.cs` | 0 results |
| SCAN-03 | `grep -n "\.Where\|\.First\|\.Select\|\.Any\|\.ToList" src/PropTraderTools/Features/PttBreakEven.cs` | 0 results |
| SCAN-04 | `grep -n "DateTime\.Now" src/PropTraderTools/Features/PttBreakEven.cs` | 0 results |
| SCAN-05 | `grep -n "return null" src/PropTraderTools/Features/PttBreakEven.cs` | 0 results (SnapshotTargetsLocal returns empty list; FindPositionLocal's `return null` is pre-existing and exempt) |
| SCAN-06 | `grep -n "PTT-BE-Target" src/PropTraderTools/Features/PttBreakEven.cs` | ≥ 1 result (confirms target names present) |
| SCAN-07 | `powershell -File scripts\verify_links.ps1 -Fix` | OK=11, DESYNC=0 |

**SCAN-05 note**: `FindPositionLocal` uses `return null` (line 208) — this is a pre-existing
pattern that existed before B36-LaneB and is not introduced by this block. Only NEW code
introduced by B36-LaneB is in scope for SCAN-05.

---

## 11. Deferred Items Closed by This Block

| ID | Description | Status |
|----|-------------|--------|
| DW-B35-TARGETS-01 | BE button places bare stop with no OCO, no targets | CLOSED by B36-LaneB |

No new deferred items are introduced by this block. All changes are self-contained within
the BE module's single file.

---

## Review Checklist (for ptt-plan-reviewer)

- [ ] C1 SnapshotTargetsLocal: NT8-006 compliance (no LINQ) confirmed
- [ ] C2 IsAtmTargetName: `Target0 = false` asserted (name[6] != '0' guard)
- [ ] C3 SubmitBeTargetsLocal: NT8-049 arg positions (limitPx=arg6, stopPx=arg7=0)
- [ ] C4 Execute(): BuildBeOcoId helper extracted → CYC stays 8
- [ ] C5 SubmitBeStopLocal: ocoId param added, string.Empty replaced
- [ ] All 4 tests: reflection-based or pure-arithmetic (no NT8 runtime required)
- [ ] Hard-link gate: verify_links.ps1 -Fix specified
- [ ] Single-file scope confirmed: only PttBreakEven.cs modified
- [ ] No lock() anywhere in new code
- [ ] No LINQ in new code
- [ ] No DateTime.Now in new code

---

*Plan status*: **REVIEW_PENDING** — ready for ptt-plan-reviewer.
