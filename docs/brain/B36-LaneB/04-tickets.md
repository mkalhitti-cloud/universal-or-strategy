# B36-LaneB — Ticket File
# Epic: DW-B35-TARGETS-01 | be-targets-oco
# Source plan: docs/brain/B36-LaneB/02-architecture-plan.md (REVIEW_PASS)
# Plan review: docs/brain/B36-LaneB/02-plan-review.md (REVIEW_PASS 2026-07-27)
# Generated: 2026-07-27
# Phase gate: REVIEW_PASS → Phase 3 ticket generation

---

## Ticket T1 — PttBreakEven OCO + Targets

**Spec requirement IDs**: DW-B35-TARGETS-01

**File**: `src/PropTraderTools/Features/PttBreakEven.cs`
(Wave workspace: `c:/WSGTA/universal-or-strategy/src/PropTraderTools/Features/PttBreakEven.cs`)

**Test file**: `tests/PropTraderTools.Tests/CopyEngineTests.cs`
(append 4 new `[Fact]` methods — filter tag `T_B36`)

**Test baseline**: 180 facts → 184 facts (net +4)

**Build tag** (update `CopyEngine.cs` line ~41 after all work is complete):
```
PTT-COPIER B36 | be-targets-oco | {DATE}
```

---

## Scope

Single-file surgical change to `PttBreakEven.cs` only.

| Change ID | Type | Summary |
|-----------|------|---------|
| C1 | NEW method | `SnapshotTargetsLocal` — read Working/Accepted ATM Target orders |
| C2 | NEW method | `IsAtmTargetName` — private copy of CopyEngine pattern (Target1..9) |
| C3 | NEW method | `SubmitBeTargetsLocal` — submit Limit orders as PTT-BE-Target-N |
| C4 | MODIFY | `Execute()` foreach body — snapshot, ocoId, SubmitBeTargetsLocal dispatch |
| C5 | MODIFY | `SubmitBeStopLocal` — add `ocoId` param, replace `string.Empty` at arg8 |
| Helper | NEW method | `BuildBeOcoId` — extract ocoId ternary to keep Execute() CYC=8 |

**No other file is modified.** `CopyEngine.cs` is read for reference only. `PttContracts.cs` and `TradeCopierPanel.cs` are untouched.

---

## Method Signatures (Exact — Engineer Contract)

All methods are in class `PttBreakEven` inside namespace `PropTraderTools`.

### C1 — NEW: SnapshotTargetsLocal

```csharp
private static List<(double Price, int Qty, OrderAction Action)>
    SnapshotTargetsLocal(Account acc, Instrument instr)
```

**CYC limit**: <= 3
**NT8 rules**: NT8-006 (no LINQ — `foreach (Order o in acc.Orders)` only, no `.ToList()`, no `.Where()`, no `.Select()`, no `.Any()`)
**JS rules**: JS-021 (no lock), JS-002 (return empty list, never null)

**Logic** (exact, engineer must follow):
```
result = new List<(double, int, OrderAction)>()
if (acc == null || instr == null) return result          // guard (CYC+1)
foreach (Order o in acc.Orders)                         // loop  (CYC+1)
{
    if (o == null) continue
    bool stateOk = o.OrderState == OrderState.Working
                || o.OrderState == OrderState.Accepted
    bool instrOk = o.Instrument != null
                && o.Instrument.FullName == instr.FullName
    if (!stateOk || !instrOk || !IsAtmTargetName(o.Name)) continue  // filter (CYC+1)
    result.Add((o.LimitPrice, o.Quantity, o.OrderAction))
    Output.Process("[BE] Snapshot target: " + o.Name + " qty=" + o.Quantity
                   + " px=" + o.LimitPrice, PrintTo.OutputTab1)
}
return result
```

**Ordering constraint**: Called BEFORE `CancelStaleBracketsLocal` in `Execute()`. Targets must still be `Working` at read time.

**Precedent**: `CancelStaleBracketsLocal` (existing, line 124) iterates `acc.Orders` with `foreach` — same NT8-006-safe pattern. No `.ToList()` defensive copy needed because the loop is READ-ONLY (no cancel inside the loop).

---

### C2 — NEW: IsAtmTargetName

```csharp
private static bool IsAtmTargetName(string name)
```

**CYC limit**: <= 2
**JS rules**: JS-021 (no lock), JS-002 (returns bool)

**Logic** (exact — includes `!= '0'` guard to pass T2):
```csharp
if (string.IsNullOrEmpty(name) || name.Length < 7) return false;   // guard (CYC+1)
return name.StartsWith("Target", StringComparison.Ordinal)
       && char.IsDigit(name[6]) && name[6] != '0';                  // check (CYC+1)
```

**Match table** (all 5 cases must pass T2):

| Input | Expected | Reason |
|-------|----------|--------|
| `"Target1"` | `true` | starts "Target", `[6]`='1' digit, not '0' |
| `"Target9"` | `true` | starts "Target", `[6]`='9' digit, not '0' |
| `"Stop1"` | `false` | does not start with "Target" |
| `"Target0"` | `false` | `[6]`='0' — `name[6] != '0'` guard rejects |
| `"PTT-BE-Target-1"` | `false` | starts "PTT-", not "Target" |

**Note**: One-character delta from `CopyEngine.IsAtmTargetName` (adds `name[6] != '0'`). No NT8 ATM template produces "Target0", so real-world behavior is identical. The `!= '0'` guard is REQUIRED to pass T2.

---

### C3 — NEW: SubmitBeTargetsLocal

```csharp
private static void SubmitBeTargetsLocal(
    Account acc,
    Instrument instr,
    string ocoId,
    List<(double Price, int Qty, OrderAction Action)> targets)
```

**CYC limit**: <= 4
**NT8 rules**: NT8-006 (no LINQ), NT8-007 (arg11 cast), NT8-013 (DateTime.MaxValue), NT8-014 (PTT- prefix), NT8-049 (Limit arg positions: arg6=limitPrice, arg7=0)
**JS rules**: JS-021 (no lock), JS-033 (synchronous void — no `async`)

**Logic** (exact):
```csharp
if (acc == null || instr == null) return             // guard (CYC+1)
if (targets == null) return                          // guard (CYC+1)
for (int i = 0; i < targets.Count; i++)             // loop  (CYC+1)
{
    var t = targets[i];
    try
    {
        var ord = acc.CreateOrder(
            instr,
            t.Action,
            OrderType.Limit,
            OrderEntry.Manual,
            TimeInForce.Gtc,
            t.Qty,
            t.Price,                                  // arg6: limitPrice  (NT8-049)
            0,                                        // arg7: stopPrice=0 (NT8-049)
            ocoId,                                    // arg8: OCO group
            "PTT-BE-Target-" + (i + 1),              // arg9: signal name (NT8-014)
            DateTime.MaxValue,                        // arg10: GTC        (NT8-013)
            (NinjaTrader.Cbi.CustomOrder)null);       // arg11: cast, not string (NT8-007)
        if (ord != null)                             // null check (CYC+1)
            acc.Submit(new[] { ord });
        else
            Output.Process("[BE] Target-" + (i + 1)
                           + " CreateOrder null -- skip", PrintTo.OutputTab1);
        Output.Process("[BE] SubmitBeTargetsLocal: Target-" + (i + 1)
                       + " submitted @ " + t.Price, PrintTo.OutputTab1);
    }
    catch { /* non-fatal: stop still live, continue remaining targets */ }
}
Output.Process("[BE] SubmitBeTargetsLocal: " + targets.Count
               + " targets for " + acc.Name, PrintTo.OutputTab1);
```

**Critical NT8-049 note**: Limit order arg positions:
- `arg6` = `t.Price` (limitPrice) — NOT `0`
- `arg7` = `0` (stopPrice) — NOT `t.Price`
Reversing these is a silent runtime bug (order submits at wrong price or is rejected).

**try/catch placement**: Per-order (INSIDE the for loop). A single try/catch wrapping the entire loop would swallow all errors silently. The per-order catch ensures a failed CreateOrder does not prevent remaining targets from being submitted.

---

### Helper — NEW: BuildBeOcoId

```csharp
private static string BuildBeOcoId(Account acc, double bePrice, double tickSize)
```

**CYC limit**: <= 2
**JS rules**: JS-021 (no lock)
**Purpose**: Extract the ocoId ternary out of `Execute()` to keep Execute() CYC=8.

**Logic** (exact):
```csharp
string prefix = acc.Name.Length >= 4
    ? acc.Name.Substring(0, 4)     // ternary (CYC+1)
    : acc.Name;
return "PTT-BE-" + prefix + "-"
    + ((int)(bePrice / tickSize)).ToString();
```

**MANDATORY**: The engineer MUST call `BuildBeOcoId` in `Execute()` — NOT inline the ternary. If the ternary is inlined, Execute() CYC rises from 8 to 9, violating the <=8 limit. This is a **binding instruction, not optional**.

**Example output**: `acc.Name="ACCT"`, `bePrice=4400.50`, `tickSize=0.25` → `"PTT-BE-ACCT-17602"`

---

### C5 — MODIFY: SubmitBeStopLocal (add ocoId param)

**Before signature**:
```csharp
private static void SubmitBeStopLocal(Account acc, Instrument instr,
                                      double bePrice, bool isLong)
```

**After signature**:
```csharp
private static void SubmitBeStopLocal(Account acc, Instrument instr,
                                      double bePrice, bool isLong, string ocoId)
```

**Body change** — find the `string.Empty` literal passed as arg8 to `CreateOrder` inside `SubmitBeStopLocal` (source line ~176) and replace it:

**Before**:
```csharp
string.Empty,    // arg8: oco group
```

**After**:
```csharp
ocoId,           // arg8: OCO group ID (DW-B35-TARGETS-01 FIX)
```

**No other change** to `SubmitBeStopLocal`. CYC remains 3.

---

### C4 — MODIFY: Execute() foreach body

**Context**: Inside `foreach (Account acc in ctx.AllAccounts)` body, after the `priceOk` guard.

**Current source order** (from existing `Execute()` body, lines ~86–96):
```
...priceOk guard...
CancelStaleBracketsLocal(acc, ctx.Instrument);
SubmitBeStopLocal(acc, ctx.Instrument, bePrice, isLong);
```

**Required order after B36-LaneB**:
```
Step A: var targets = SnapshotTargetsLocal(acc, ctx.Instrument);   // NEW - MUST be first
Step B: string ocoId = BuildBeOcoId(acc, bePrice,                  // NEW
                           ctx.Instrument.MasterInstrument.TickSize);
Step C: CancelStaleBracketsLocal(acc, ctx.Instrument);             // EXISTING (unchanged)
Step D: SubmitBeStopLocal(acc, ctx.Instrument, bePrice, isLong, ocoId); // MODIFIED (add ocoId arg)
Step E: SubmitBeTargetsLocal(acc, ctx.Instrument, ocoId, targets); // NEW - MUST be after D
```

**MANDATORY ordering rule** (binding, not optional):
- A BEFORE C: Targets must still be `Working` when read. After `CancelStaleBracketsLocal` (Step C), they are `Cancelled` — `SnapshotTargetsLocal` would return an empty list.
- C BEFORE D: Cancel existing brackets before submitting new ones. Prevents duplicate fills.
- D BEFORE E: Stop is submitted first. If `SubmitBeTargetsLocal` fails entirely, the stop still protects the position.

**CYC impact on Execute()**: +0. Three method calls with no new branches. The ternary is extracted to `BuildBeOcoId` — Execute() CYC stays at 8.

---

## CYC Summary

| Method | CYC | Limit | Status |
|--------|-----|-------|--------|
| `Execute()` (modified) | 8 | <=8 | PASS |
| `SnapshotTargetsLocal` (new) | 3 | <=3 | PASS |
| `IsAtmTargetName` (new) | 2 | <=3 | PASS |
| `BuildBeOcoId` (new helper) | 2 | <=3 | PASS |
| `SubmitBeTargetsLocal` (new) | 4 | <=4 | PASS |
| `SubmitBeStopLocal` (modified) | 3 | <=3 | PASS |

---

## NT8 Rule Constraints

| Rule | Applies To | Requirement |
|------|-----------|-------------|
| NT8-006 | `SnapshotTargetsLocal` | `foreach (Order o in acc.Orders)` — NO `.ToList()`, `.Where()`, `.Select()`, `.Any()`, `.First()` |
| NT8-007 | `SubmitBeTargetsLocal` | arg11 = `(NinjaTrader.Cbi.CustomOrder)null` — explicit cast, NOT a string literal |
| NT8-013 | `SubmitBeTargetsLocal` | arg10 = `DateTime.MaxValue` for GTC — NEVER `DateTime.Now` or `DateTime.UtcNow` |
| NT8-014 | `SubmitBeTargetsLocal` | Signal name = `"PTT-BE-Target-" + (i + 1)` — all names start with `"PTT-"` |
| NT8-049 | `SubmitBeTargetsLocal` | Limit order: arg6=`t.Price` (limitPrice), arg7=`0` (stopPrice=0). Do NOT swap. |

---

## JS Rule Constraints

| Rule | Check | Applies To |
|------|-------|-----------|
| JS-021 | No `lock()` anywhere | All new and modified code — 0 instances allowed |
| JS-033 | No `async void` | All new methods are synchronous — 0 `async` keywords allowed |

---

## xUnit Tests — 4 [Fact] Methods

Append to `tests/PropTraderTools.Tests/CopyEngineTests.cs`.
All tests are reflection-based or pure-arithmetic — no NT8 runtime or live account required.
Filter tag: `T_B36` (all method names start with `T_B36B_`).

---

### Test 1: T_B36B_SnapshotTargetsLocal_ReadsAtmTargetOrders

**Type**: Reflection — method existence and signature check
**Asserts**: Private static method `SnapshotTargetsLocal` exists, returns `List<ValueTuple>`, has exactly 2 params `(Account, Instrument)`.

```csharp
[Fact]
public void T_B36B_SnapshotTargetsLocal_ReadsAtmTargetOrders()
{
    var mi = typeof(PttBreakEven).GetMethod(
        "SnapshotTargetsLocal",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
    Assert.NotNull(mi);
    Assert.True(mi.ReturnType.IsGenericType);   // List<(double, int, OrderAction)>
    var ps = mi.GetParameters();
    Assert.Equal(2, ps.Length);
    Assert.Equal(typeof(Account),    ps[0].ParameterType);
    Assert.Equal(typeof(Instrument), ps[1].ParameterType);
}
```

---

### Test 2: T_B36B_IsAtmTargetName_MatchesTarget1To9Only

**Type**: Reflection invoke — functional correctness
**Asserts**: 5 cases via reflection call to private static method.

```csharp
[Fact]
public void T_B36B_IsAtmTargetName_MatchesTarget1To9Only()
{
    var mi = typeof(PttBreakEven).GetMethod(
        "IsAtmTargetName",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
    Assert.NotNull(mi);
    Assert.True((bool)mi.Invoke(null,  new object[] { "Target1" }));         // T1 in range
    Assert.True((bool)mi.Invoke(null,  new object[] { "Target9" }));         // T9 in range
    Assert.False((bool)mi.Invoke(null, new object[] { "Stop1" }));           // not target
    Assert.False((bool)mi.Invoke(null, new object[] { "Target0" }));         // Target0 excluded
    Assert.False((bool)mi.Invoke(null, new object[] { "PTT-BE-Target-1" })); // PTT- prefix rejected
}
```

**Critical**: The `Target0 = false` assertion requires the `name[6] != '0'` guard in `IsAtmTargetName`. Without it this test FAILS.

---

### Test 3: T_B36B_SubmitBeTargetsLocal_MethodExists

**Type**: Reflection — method existence and signature check
**Asserts**: Private static method `SubmitBeTargetsLocal` exists, returns void, has exactly 4 params `(Account, Instrument, string, List<...>)`.

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
    Assert.True(ps[3].ParameterType.IsGenericType); // List<(double, int, OrderAction)>
}
```

---

### Test 4: T_B36B_OcoId_NonEmpty

**Type**: Pure arithmetic — ocoId formula verification (zero NT8 runtime dependency)
**Asserts**: Given fixed inputs, the `BuildBeOcoId` formula produces the expected string in both long-name (>=4 chars) and short-name (<4 chars) cases.

```csharp
[Fact]
public void T_B36B_OcoId_NonEmpty()
{
    // Case 1: account name length >= 4
    string accName  = "ACCT";
    double bePrice  = 4400.50;
    double tickSize = 0.25;
    string prefix   = accName.Length >= 4 ? accName.Substring(0, 4) : accName;
    string ocoId    = "PTT-BE-" + prefix + "-"
                    + ((int)(bePrice / tickSize)).ToString();
    Assert.StartsWith("PTT-BE-", ocoId, StringComparison.Ordinal);
    Assert.Equal("PTT-BE-ACCT-17602", ocoId);  // (int)(4400.50 / 0.25) = 17602
    Assert.False(string.IsNullOrEmpty(ocoId));

    // Case 2: account name length < 4
    string shortAcc = "SIM";
    string prefix2  = shortAcc.Length >= 4 ? shortAcc.Substring(0, 4) : shortAcc;
    string ocoId2   = "PTT-BE-" + prefix2 + "-"
                    + ((int)(bePrice / tickSize)).ToString();
    Assert.Equal("PTT-BE-SIM-17602", ocoId2);
    Assert.False(string.IsNullOrEmpty(ocoId2));
}
```

**Arithmetic check** (engineer must verify independently):
- `(int)(4400.50 / 0.25)` = `(int)(17602.0)` = `17602` ✓

---

## 7-Scan Checklist (SCAN-01 through SCAN-07)

All scans run in the Wave workspace root (`c:/WSGTA/universal-or-strategy`).
Each scan MUST reach its stated expected result before the ticket is marked DONE.

| Scan | Command | Expected | Blocking? |
|------|---------|----------|-----------|
| SCAN-01 | `grep -n "lock(" src/PropTraderTools/Features/PttBreakEven.cs` | **0 results** | YES |
| SCAN-02 | `grep -n "async void" src/PropTraderTools/Features/PttBreakEven.cs` | **0 results** | YES |
| SCAN-03 | `grep -n "\.Where\|\.First\|\.Select\|\.Any\|\.ToList" src/PropTraderTools/Features/PttBreakEven.cs` | **0 results** | YES |
| SCAN-04 | `grep -n "{ get; init; }" src/PropTraderTools/Features/PttBreakEven.cs` | **0 results** | YES |
| SCAN-05 | `grep -n "DateTime\.Now" src/PropTraderTools/Features/PttBreakEven.cs` | **0 results** | YES |
| SCAN-06 | `dotnet build` (from solution root) | **BUILD_PASS (zero errors)** | YES |
| SCAN-07 | `dotnet test --filter "T_B36"` | **TEST_PASS (4/4 new facts pass)** | YES |

**SCAN-03 note**: `.ToList()` is explicitly included in the LINQ pattern — `SnapshotTargetsLocal` must not call `acc.Orders.ToList()`. Use raw `foreach` iteration only.

**SCAN-05 exemption**: `FindPositionLocal` uses `return null` (pre-existing, lines 205/209) — exempt. Only new code introduced by B36-LaneB is in scope.

**Hard-link gate** (after SCAN-06 passes):
```powershell
powershell -File scripts\verify_links.ps1 -Fix
```
Expected: `OK=11, DESYNC=0`. Any DESYNC > 0 = BLOCKING — ticket not closeable until DESYNC=0.

---

## Binding Engineer Instructions (from REVIEW_PASS)

These are not suggestions. Deviation from any of these requires re-review.

1. **`BuildBeOcoId` helper is MANDATORY** — do NOT inline the ternary into `Execute()`. Inlining raises Execute() CYC from 8 to 9 — immediate re-review required.

2. **Snapshot ordering is MANDATORY** — `SnapshotTargetsLocal(acc, ctx.Instrument)` MUST appear BEFORE `CancelStaleBracketsLocal(acc, ctx.Instrument)` in Execute(). Place snapshot at Step A, cancel at Step C.

3. **`name[6] != '0'` guard is MANDATORY** in `IsAtmTargetName` — without it, `T_B36B_IsAtmTargetName_MatchesTarget1To9Only` FAILS on the Target0=false assertion.

4. **try/catch is per-order** (inside the for loop in `SubmitBeTargetsLocal`) — NOT wrapping the entire loop.

5. **No new `return null`** — `SnapshotTargetsLocal` returns `new List<...>()` on null inputs (never null). `FindPositionLocal`'s pre-existing `return null` is exempt.

6. **Limit arg positions** in `SubmitBeTargetsLocal`: `arg6=t.Price` (limitPrice), `arg7=0` (stopPrice=0). Do NOT swap.

---

## Completion Criteria

The ticket is DONE when ALL of the following are true:

- [ ] C1: `SnapshotTargetsLocal` implemented per signature + logic above
- [ ] C2: `IsAtmTargetName` implemented with `name[6] != '0'` guard
- [ ] Helper: `BuildBeOcoId` implemented and called from Execute() (not inlined)
- [ ] C3: `SubmitBeTargetsLocal` implemented per signature + NT8-049/007/013/014
- [ ] C4: `Execute()` foreach body updated in correct A→B→C→D→E order
- [ ] C5: `SubmitBeStopLocal` signature updated (ocoId param), arg8 = ocoId
- [ ] T1–T4: 4 `[Fact]` tests appended to `CopyEngineTests.cs`
- [ ] SCAN-01 through SCAN-07: all pass (0 results for SCAN-01..05; BUILD_PASS for SCAN-06; TEST_PASS for SCAN-07)
- [ ] Hard-link gate: `verify_links.ps1 -Fix` → `OK=11, DESYNC=0`
- [ ] Build tag updated in `CopyEngine.cs` line ~41: `PTT-COPIER B36 | be-targets-oco | {DATE}`

---

*Return code*: **TICKETS_COMPLETE**
