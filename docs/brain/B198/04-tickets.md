# B198 Tickets
**Author**: ptt-architect (Phase 3)
**Date**: 2026-10-13
**Epic**: B198 -- SimStop N-bracket generalisation + QTY instrument + fo=NULL drain + ATM dir fix
**Plan source**: docs/brain/B198/02-architecture-plan.md (REVIEW_PASS -- Cycle 2)
**Plan review source**: docs/brain/B198/02-plan-review.md (REVIEW_PASS)

---

## Ticket 1 — SimStop N-General Bracket Support (DW-B198-SIMSTP-01)

### Spec Requirement IDs
- B198-orchestrator-prompt.md §TICKET 1, Part A: 6 Predicate Fixes
- B198-orchestrator-prompt.md §TICKET 1, Part B: B1 TryParseStopSuffix, B2 CaptureOtherLegTargetPrices+ResubmitCollateralLegs, B3 FindLeaderCollateralOrder, B4 CaptureLinkedTargetPrice (auto-covered), B5 FindLeaderStopPrice (newly discovered)
- DW-B198-SIMSTP-01 (new fix -- never existed, confirmed by megacommit audit)
- DW-B198-FINDLDR-01 (newly discovered by architect -- closed by this ticket)
- DW-B198-CAPTURELEG-01 (newly discovered by architect -- closed by this ticket)
- Director requirement: fix must work for ANY N bracket groups (not only 2)

### Objective
Add SimStop prefix recognition to all 6 bracket predicates and generalise the drag infrastructure so that ATM templates using stop orders named "SimStopN" work correctly for any N bracket groups. This stops the double-Sell-copy cascade that produces reverse Short positions on followers.

### Scope Lock
SCOPE: This ticket touches ONLY:
- `src/PropTraderTools/CopyEngine.cs` (10 methods + 1 const declaration)
- `src/PropTraderTools/Tests/B198Tests.cs` (new file -- T1 tests only; T2/T3/T4 tests added by their respective tickets)

Do NOT read or modify: any other ticket's scope. Do NOT touch TradeCopierPanel.cs or any other file.

### Pre-conditions
- B198/02-architecture-plan.md has REVIEW_PASS (confirmed).
- B198/02-plan-review.md VIO-1 and VIO-2 are RESOLVED (confirmed).
- `src/PropTraderTools/Tests/B198Tests.cs` does NOT yet exist.
- Grep confirms zero existing `StartsWith("SimStop"` matches in CopyEngine.cs (confirmed by architect appendix).
- No prior ticket has modified CopyEngine.cs in this epic (T1 is first).

### Method Signatures

All methods are in `src/PropTraderTools/CopyEngine.cs`. No new methods are created; existing methods are modified in-place.

#### Part A: 6 Predicate Fixes

```
internal static bool IsNativeExitName(string name)           // line 2564 -- ADD SimStop branch
internal static bool IsAtmBracketName(string name)           // line 858  -- ADD SimStop clause to OR
private bool IsBracketLeg(Order order)                       // line 5880 -- ADD SimStop clause to OR
private static bool IsBracketLegStatic(Order order)          // line 5861 -- ADD SimStop clause to OR
internal static bool IsAtmSTPOrder(Order order)              // line 3044 -- ADD SimStop clause to OR
private static bool IsStopLeg(Order order)                   // line 5848 -- ADD SimStop clause to OR (with null guard)
```

#### Part B: Infrastructure Fixes

```
private static bool TryParseStopSuffix(string stopName, out string suffix)     // line 3377 -- FULL REPLACEMENT
private double[] CaptureOtherLegTargetPrices(Order fo, Account leaderAcct)     // line 3348 -- array + loop + guard fix
private void ResubmitCollateralLegs(Account acc, double[] otherLegPrices, ...) // line 3532 -- loop bound fix
private static Order FindLeaderCollateralOrder(Order leaderOrder, string suffix) // line 3405 -- SimStop lookup
private static double FindLeaderStopPrice(Account? leaderAccount, int bracketIndex) // line 3884 -- SimStop lookup
```

#### New Constant

```
private const int MaxBracketLegs = 9;    // class-level, placed with other constants near top of class
```

### Exact Code Changes

**IMPORTANT**: Read the current CopyEngine.cs at the specified line before applying each change to verify the exact surrounding context. Line numbers may shift if prior edits in the same session have already moved them.

---

#### Change A1: IsNativeExitName (line 2564)

Locate the method `IsNativeExitName`. Find the line containing `return false;` that terminates the method after all other checks.

BEFORE (the terminal return false at end of method body):
```csharp
            return false;
```

AFTER (insert the SimStop branch immediately before that `return false`):
```csharp
            if (name.StartsWith("SimStop", StringComparison.Ordinal))
                return true;
            return false;
```

CYC impact: 4 → 5.

---

#### Change A2: IsAtmBracketName (line 858)

Locate the method `IsAtmBracketName`. Find the existing compound OR expression that currently contains `name.StartsWith("Stop")` and/or `name.StartsWith("Target")` clauses.

Add the following OR clause to the existing boolean expression (compound OR on same expression -- 0 McCabe delta on the expression body itself, +1 on overall method entry path):

```csharp
|| (name.StartsWith("SimStop", StringComparison.Ordinal) && name.Length > 7 && char.IsDigit(name[7]))
```

CYC impact: 1 → 2.

---

#### Change A3: IsBracketLeg (line 5880)

Locate `IsBracketLeg`. Find the OR expression that currently contains `order.Name.StartsWith("Stop", StringComparison.Ordinal)` or equivalent.

Add to OR:
```csharp
|| order.Name.StartsWith("SimStop", StringComparison.Ordinal)
```

CYC impact: 1 → 1 (compound OR on expression body, 0 McCabe delta).

---

#### Change A4: IsBracketLegStatic (line 5861)

Locate `IsBracketLegStatic`. Find the OR expression that contains `StartsWith("Stop")`.

Add to OR:
```csharp
|| order.Name.StartsWith("SimStop", StringComparison.Ordinal)
```

CYC impact: 1 → 1 (compound OR, 0 McCabe delta).

---

#### Change A5: IsAtmSTPOrder (line 3044)

Locate `IsAtmSTPOrder`. Find the OR expression that contains `StartsWith("Stop")`.

Add to OR:
```csharp
|| order.Name.StartsWith("SimStop", StringComparison.Ordinal)
```

CYC impact: 1 → 1 (compound OR, 0 McCabe delta).

---

#### Change A6: IsStopLeg (line 5848)

Locate `IsStopLeg`. Find the OR expression that contains `StartsWith("Stop")`.

Add to OR (include null guard because plan §3 Part A specifies it for this site):
```csharp
|| (order.Name != null && order.Name.StartsWith("SimStop", StringComparison.Ordinal))
```

CYC impact: 1 → 1 (compound OR, 0 McCabe delta).

---

#### Change B0: MaxBracketLegs constant declaration

Locate the block of class-level constants near the top of the CopyEngine class (where other `private const int` or `private static readonly` constants are declared).

Add:
```csharp
private const int MaxBracketLegs = 9;
```

Place this declaration with the existing constants. No other constants should be modified.

---

#### Change B1: TryParseStopSuffix FULL REPLACEMENT (lines 3377-3387)

Locate the method `TryParseStopSuffix`. REPLACE the entire method body (from opening brace to closing brace inclusive) with:

```csharp
private static bool TryParseStopSuffix(string stopName, out string suffix)
{
    suffix = null;
    if (string.IsNullOrEmpty(stopName))                              // (1) if
        return false;
    int prefixLen;
    if (stopName.StartsWith("SimStop", StringComparison.Ordinal))    // (2) if
        prefixLen = 7;
    else if (stopName.StartsWith("Stop", StringComparison.Ordinal))  // (3) else if
        prefixLen = 4;
    else
        return false;
    if (stopName.Length <= prefixLen)                                 // (4) if
        return false;
    string raw = stopName.Substring(prefixLen);
    if (!int.TryParse(raw, out int n) || n < 1)                      // (5) if -- n > 3 cap REMOVED
        return false;
    suffix = raw;
    return true;
}
```

CYC impact: 3 → 5. JS-021: no lock. JS-001: no throw. ASCII-only.

The `n > 3` cap is intentionally removed. `CaptureLinkedTargetPrice` (B4) is auto-covered by this fix -- it calls `TryParseStopSuffix` internally and requires no further change.

---

#### Change B2a: CaptureOtherLegTargetPrices -- array size (line 3350)

Locate `CaptureOtherLegTargetPrices`. Find the line:
```csharp
new double[3]
```

Replace with:
```csharp
new double[MaxBracketLegs]
```

---

#### Change B2b: CaptureOtherLegTargetPrices -- StartsWith guard (line 3351)

In the same method, find the early-return guard:
```csharp
if (!fo.Name.StartsWith("Stop"))
```

Replace with:
```csharp
if (!fo.Name.StartsWith("Stop") && !fo.Name.StartsWith("SimStop"))
```

This prevents the guard from firing and returning all-zeros when `fo.Name` is "SimStop1".
CYC impact: 0 McCabe delta (compound AND on existing if, same branch count).

---

#### Change B2c: CaptureOtherLegTargetPrices -- loop cap (line 3355)

In the same method, find:
```csharp
for (int i = 1; i <= 3; i++)
```

Replace with:
```csharp
for (int i = 1; i <= MaxBracketLegs; i++)
```

CYC impact: 0 (loop bound change, not a new branch). Overall method CYC: 6 → 6.

---

#### Change B2d: ResubmitCollateralLegs -- loop cap (line 3541)

Locate `ResubmitCollateralLegs`. Find:
```csharp
for (int i = 1; i <= 3; i++)
```

Replace with:
```csharp
for (int i = 1; i <= MaxBracketLegs; i++)
```

The `otherLegPrices` array is now size `MaxBracketLegs` (from B2a), so the index `otherLegPrices[i - 1]` is safe for i = 1..MaxBracketLegs. No other change to indexing expressions.
CYC impact: 0 (loop bound change, not a new branch).

---

#### Change B3: FindLeaderCollateralOrder (line 3405-3416)

Locate `FindLeaderCollateralOrder`. The current method builds `stopName = "Stop" + suffix` and `tgtName = "Target" + suffix` and searches `leaderOrder.Account.Orders`.

Add a third lookup name and include it in the inner OR check. The revised method body:

```csharp
private static Order FindLeaderCollateralOrder(Order leaderOrder, string suffix)
{
    if (leaderOrder?.Account?.Orders == null || string.IsNullOrEmpty(suffix))
        return null;
    string stopName    = "Stop"    + suffix;
    string tgtName     = "Target"  + suffix;
    string simStopName = "SimStop" + suffix;          // NEW
    foreach (var o in leaderOrder.Account.Orders.ToList())
    {
        if (o != null && (o.Name == stopName || o.Name == tgtName || o.Name == simStopName)) // NEW: || simStopName
            return o;
    }
    return null;
}
```

The `|| o.Name == simStopName` is added to an existing compound OR expression inside the existing `if` branch. Zero new McCabe branches.
CYC impact: 3 → 3.

---

#### Change B5: FindLeaderStopPrice (line 3884)

Locate `FindLeaderStopPrice`. The current method builds `"Stop" + bracketIndex.ToString()` and searches leader account orders.

Add `simStopName` and include it in the inner OR check. The revised method body:

```csharp
private static double FindLeaderStopPrice(Account? leaderAccount, int bracketIndex)
{
    if (leaderAccount == null)
        return 0.0;
    if (bracketIndex <= 0)
        return 0.0;
    var targetName  = "Stop"    + bracketIndex.ToString();
    var simStopName = "SimStop" + bracketIndex.ToString();   // NEW
    foreach (var order in leaderAccount.Orders.ToList())
    {
        if (
            (order.Name == targetName || order.Name == simStopName)  // NEW: || simStopName
            && (order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted)
        )
            return order.StopPrice;
    }
    return 0.0;
}
```

The `|| order.Name == simStopName` is compound OR on existing boolean expression = 0 McCabe delta.
CYC impact: 6 → 6.

---

### JS Rule Constraints

| Rule | How this ticket satisfies it |
|------|------------------------------|
| JS-021 (no lock) | No lock() in any new or modified code. All changes are expression-level OR additions or method body replacements. No shared mutable state introduced. |
| JS-001 (no throw in hot path) | No throw statements added. TryParseStopSuffix replacement returns bool with out param -- no throw. |
| JS-002 (no return null in hot path) | FindLeaderCollateralOrder returns null as documented NT8 absence signal (existing project contract, exempt). FindLeaderStopPrice returns 0.0 (not null) for the absence case. |
| JS-033 (no async void) | No new async methods. All changes are synchronous. |
| JS-066 (CYC <= 8) | Maximum CYC after changes: TryParseStopSuffix = 5, IsNativeExitName = 5. All other methods 1-6. None exceed 8. See CYC table below. |
| JS-008 (no mutable struct field) | No struct fields modified. MaxBracketLegs is a const. |
| JS-082 analog (no magic numbers) | MaxBracketLegs = 9 declared as named const. No raw integer literals for the bound. |
| ASCII-only | All new string literals: "SimStop", "Stop", "Target", "SimStop". All log strings ASCII. No Unicode, no emoji, no curly quotes. |
| xUnit [Fact] | All 16 T1 tests use [Fact] only. No NUnit, no MSTest. |

### CYC Table (Ticket 1 methods)

| Method | Before CYC | After CYC | JS-066 |
|--------|-----------|----------|--------|
| `IsNativeExitName` | 4 | 5 | PASS |
| `IsAtmBracketName` | 1 | 2 | PASS |
| `IsBracketLeg` | 1 | 1 | PASS |
| `IsBracketLegStatic` | 1 | 1 | PASS |
| `IsAtmSTPOrder` | 1 | 1 | PASS |
| `IsStopLeg` | 1 | 1 | PASS |
| `TryParseStopSuffix` | 3 | 5 | PASS |
| `CaptureOtherLegTargetPrices` | 6 | 6 | PASS |
| `ResubmitCollateralLegs` | 5 | 5 | PASS |
| `FindLeaderCollateralOrder` | 3 | 3 | PASS |
| `FindLeaderStopPrice` | 6 | 6 | PASS |

### xUnit [Fact] Names

All tests go in `src/PropTraderTools/Tests/B198Tests.cs`. All use `[Fact]`. Source-text assertions use `System.IO.File.ReadAllText` to read CopyEngine.cs (same pattern as B197Tests.cs -- avoids TypeInitializationException on CopyEngine static initializer).

`IsAtmBracketName` and `IsNativeExitName` are `internal static` -- call directly.
`TryParseStopSuffix` is `private static` -- source-text assertion only.
`IsBracketLeg`, `IsBracketLegStatic`, `IsAtmSTPOrder`, `IsStopLeg` are private or require NT8 runtime -- source-text assertion only.

```
T1-01: IsAtmBracketName_SimStop1_ReturnsTrue
       Assert: CopyEngineInternals.IsAtmBracketName("SimStop1") == true

T1-02: IsAtmBracketName_SimStop9_ReturnsTrue
       Assert: CopyEngineInternals.IsAtmBracketName("SimStop9") == true

T1-03: IsAtmBracketName_SimStopNoDigit_ReturnsFalse
       Assert: CopyEngineInternals.IsAtmBracketName("SimStop") == false

T1-04: IsNativeExitName_SimStop1_ReturnsTrue
       Assert: CopyEngineInternals.IsNativeExitName("SimStop1") == true

T1-05: IsNativeExitName_SimStop99_ReturnsTrue
       Assert: CopyEngineInternals.IsNativeExitName("SimStop99") == true

T1-06: TryParseStopSuffix_SimStop1_ReturnsTrueS1
       Assert.Contains: source text of TryParseStopSuffix body contains
         StartsWith("SimStop", StringComparison.Ordinal)

T1-07: TryParseStopSuffix_SimStop2_ReturnsTrueS2
       Assert.Contains: source text of TryParseStopSuffix body contains
         StartsWith("SimStop", StringComparison.Ordinal)
       (same assertion as T1-06 -- confirms SimStop branch present; separate [Fact] per spec)

T1-08: TryParseStopSuffix_SimStop4_ReturnsTrueS4
       Assert.DoesNotContain: source text of TryParseStopSuffix body does NOT contain
         n > 3
       (confirms n > 3 cap is removed)

T1-09: TryParseStopSuffix_Stop4_ReturnsTrueS4
       Assert.DoesNotContain: source text of TryParseStopSuffix body does NOT contain
         n > 3
       (same DoesNotContain assertion -- confirms cap removal; separate [Fact] per spec)

T1-10: TryParseStopSuffix_Stop3_RegressionTrue
       Assert.Contains: source text of TryParseStopSuffix body contains
         StartsWith("Stop", StringComparison.Ordinal)
       (confirms Stop prefix still handled -- regression guard)

T1-11: TryParseStopSuffix_SimTarget1_ReturnsFalse
       Assert.DoesNotContain: source text of TryParseStopSuffix body does NOT contain
         StartsWith("SimTarget"
       (confirms SimTarget is not matched -- only "Stop" and "SimStop" prefixes handled)

T1-12: MaxBracketLegs_ConstEquals9
       Assert.Contains: source text of CopyEngine.cs contains
         MaxBracketLegs = 9

T1-13: IsBracketLeg_SimStop1_ReturnsTrue
       Assert.Contains: source text of CopyEngine.cs within IsBracketLeg body contains
         StartsWith("SimStop", StringComparison.Ordinal)

T1-14: IsBracketLegStatic_SimStop1_ReturnsTrue
       Assert.Contains: source text of CopyEngine.cs within IsBracketLegStatic body contains
         StartsWith("SimStop", StringComparison.Ordinal)

T1-15: IsAtmSTPOrder_SimStop1_ReturnsTrue
       Assert.Contains: source text of CopyEngine.cs within IsAtmSTPOrder body contains
         StartsWith("SimStop", StringComparison.Ordinal)

T1-16: IsStopLeg_SimStop1_ReturnsTrue
       Assert.Contains: source text of CopyEngine.cs within IsStopLeg body contains
         StartsWith("SimStop", StringComparison.Ordinal)
```

**Test file pattern**: Load source text once in the test class constructor or shared fixture, then run substring assertions. Example pattern (from B197Tests.cs):
```csharp
private readonly string _src = System.IO.File.ReadAllText(
    System.IO.Path.Combine(
        System.AppContext.BaseDirectory, "..", "..", "..", "..", "..",
        "src", "PropTraderTools", "CopyEngine.cs"));
```

### 7-Scan Checklist

```
[ ] SCAN-01: JS-021 -- No lock() in new/modified code
    Command: Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "lock\s*\(" | Where-Object { $_.Line -notmatch "//" }
    Expected: Zero matches in T1 change scope (lines ~858, ~2564, ~3044, ~3350, ~3377, ~3405, ~3532, ~3848, ~3880, ~5848, ~5861, ~5880).

[ ] SCAN-02: JS-033 -- No async void (non-event-handler)
    Command: Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "async\s+void\s+\w+" | Where-Object { $_.Line -notmatch "handler|Handler|Event" }
    Expected: Zero new async void methods introduced by T1.

[ ] SCAN-03: JS-001/JS-002 -- No throw/return null in hot path
    Command: Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "throw\s+new\s+\w+Exception" | Where-Object { $_.Line -notmatch "//" }
    Expected: Zero in T1 change scope. FindLeaderCollateralOrder returns null as NT8 absence signal (exempt per plan).

[ ] SCAN-04: JS-066 -- All modified methods CYC <= 8
    Command: python -m lizard src/PropTraderTools/CopyEngine.cs -C 8 --csv | Select-String "IsNativeExitName|IsAtmBracketName|IsBracketLeg|IsBracketLegStatic|IsAtmSTPOrder|IsStopLeg|TryParseStopSuffix|CaptureOtherLeg|ResubmitCollateral|FindLeaderCollateral|FindLeaderStop"
    Expected: All rows CCN <= 8. Confirmed maximums: TryParseStopSuffix=5, IsNativeExitName=5.

[ ] SCAN-05: ASCII-only -- No Unicode/emoji in C# string literals
    Command: Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "[^\x00-\x7F]"
    Expected: Zero non-ASCII characters in T1 change scope.

[ ] SCAN-06: xUnit -- All tests use [Fact] only
    Command: Select-String -Path "src/PropTraderTools/Tests/B198Tests.cs" -Pattern "NUnit|MSTest|\[TestMethod\]|\[Test\]"
    Expected: Zero matches. All T1 tests use [Fact] from xUnit.

[ ] SCAN-07: Sync field -- No new non-readonly non-atomic shared fields
    T1 introduces only MaxBracketLegs which is a const (not a shared field).
    Command: Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "MaxBracketLegs"
    Expected: Declaration is `private const int MaxBracketLegs = 9;`. No instance field. No lock around it.
```

### Acceptance Criteria

1. `dotnet test` passes all T1-01 through T1-16 tests (16 tests GREEN).
2. `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "StartsWith\(\"SimStop"` returns matches at all 6 predicate sites (A1-A6) plus B1/B2/B3/B5.
3. `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "MaxBracketLegs = 9"` returns one match at class-level const declaration.
4. `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "Substring\(4\)"` returns ZERO matches in TryParseStopSuffix (the method body has been replaced).
5. `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "n > 3"` returns ZERO matches in TryParseStopSuffix.
6. `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "i <= 3"` returns ZERO matches in CaptureOtherLegTargetPrices and ResubmitCollateralLegs (replaced by MaxBracketLegs).
7. `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "double\[3\]"` returns ZERO matches in CaptureOtherLegTargetPrices.
8. SCAN-01 through SCAN-07 all PASS.
9. Lizard CYC check: all 11 modified methods <= 8. Maximum is TryParseStopSuffix = 5.

---

## Ticket 2a — QTY Bug Instrumentation (DW-B198-QTY-01 Phase A)

### Spec Requirement IDs
- B198-orchestrator-prompt.md §TICKET 2: "Architect MUST instrument before designing the fix"
- B198-orchestrator-prompt.md §TICKET 2: instrumentation steps 1-4
- 02-architecture-plan.md §4 T2a: "Add 2 instrumentation log lines to SendCopyWithAtm. No logic change."

### Objective
Add exactly 2 diagnostic log lines to `SendCopyWithAtm` in CopyEngine.cs to capture the qty passed to `CreateOrder` and the qty on the returned order. No logic change. No CYC delta. Engineer runs one SIM entry and records log evidence in `docs/brain/B198/ticket-2a-sim-evidence.md`.

### Scope Lock
SCOPE: This ticket touches ONLY:
- `src/PropTraderTools/CopyEngine.cs` (SendCopyWithAtm method only -- 2 log lines added)
- `src/PropTraderTools/Tests/B198Tests.cs` (T2a test section -- 1 source-text assertion)
- `docs/brain/B198/ticket-2a-sim-evidence.md` (NEW file -- engineer creates after SIM run)

Do NOT read or modify: any other method, any other file, Ticket 2b scope.

### Pre-conditions
- Ticket 1 is VERIFY_PASS (T1 changes do not overlap T2a method scope but must be in place for SIM-2 to be meaningful).
- `SendCopyWithAtm` is still `private bool` return type (T3 changes return type -- T3 executes AFTER T2a).
  - **NOTE**: If T3 has been applied before T2a, the method signature will be `private Order? SendCopyWithAtm(...)`. The log line insertions are independent of return type. Adjust the `if (order == null) return ...` guard accordingly (return null instead of false).
- Engineer has NT8 SIM environment ready: Leader=Sim101, Followers=Sim102/103/104, MGC DEC26, MGC $400 SL4 template.

### Method Signatures

No new methods. One existing method modified:

```
private bool SendCopyWithAtm(Account follower, Instrument instrument, in CopySignal signal, NamedAtmMode namedMode)
// line ~4999 in CopyEngine.cs
// (Return type becomes Order? after T3 -- log lines are compatible with both signatures)
```

### Exact Code Changes

#### Change T2a-1: Log line before CreateOrder

Locate `SendCopyWithAtm`. Find the line that calls `follower.CreateOrder(...)` (approximately line 5008). Immediately BEFORE that call, insert:

```csharp
StatusUpdate?.Invoke(
    follower.Name + ": ATM-send qty=" + signal.Quantity + " tmpl=" + namedMode.TemplateName);
```

This single log line fires once per follower per ATM entry. ASCII-only. No branching (the `?.Invoke` null-conditional is not a McCabe branch). CYC unchanged.

---

#### Change T2a-2: Log line after CreateOrder null check

Locate the `if (order == null) return false;` (or `return null;` if T3 already applied) guard that follows `CreateOrder`. Immediately AFTER that guard, insert:

```csharp
StatusUpdate?.Invoke(
    follower.Name + ": ATM-order qty=" + order.Quantity);
```

This fires only when `order` is non-null. The placement is after the null-guard so `order.Quantity` is safe. No new branch (null-conditional is not McCabe). CYC unchanged (4 before = 4 after).

---

#### SIM Protocol (engineer must execute after implementing T2a)

1. Build and deploy PTT to NinjaTrader SIM.
2. Place ONE ATM entry on Leader=Sim101, qty=10, template=MGC $400 SL4.
3. For each follower (Sim102/Sim103/Sim104), locate in output log:
   - Line containing `": ATM-send qty=10 tmpl=MGC $400 SL4"`
   - Line containing `": ATM-order qty="`
4. Record the qty value from `ATM-order qty=X` for each follower.
5. Record follower position after fill.
6. Write ALL evidence to `docs/brain/B198/ticket-2a-sim-evidence.md` using this template:

```markdown
# T2a SIM Evidence
Date: [date]
Setup: Leader=Sim101, Followers=Sim102/103/104, MGC DEC26, MGC $400 SL4, leader qty=10

## Log evidence per follower
Sim102: ATM-send qty=[X], ATM-order qty=[Y], follower position after fill=[Z]
Sim103: ATM-send qty=[X], ATM-order qty=[Y], follower position after fill=[Z]
Sim104: ATM-send qty=[X], ATM-order qty=[Y], follower position after fill=[Z]

## Hypothesis determination
H1 (no bug): ATM-send qty=10, ATM-order qty=10, position=Long 10 -> T2b: CANCEL
H2 (CreateOrder reduces qty): ATM-send qty=10, ATM-order qty=5 -> T2b: IMPLEMENT H2 fix
H3 (StartAtmStrategy splits): ATM-send qty=10, ATM-order qty=10, position=Long 5 -> T2b: DEFERRED

Confirmed hypothesis: [H1 / H2 / H3]
```

### JS Rule Constraints

| Rule | How this ticket satisfies it |
|------|------------------------------|
| JS-021 (no lock) | No lock() added. StatusUpdate?.Invoke is a null-conditional event invocation -- no shared state, no lock. |
| JS-001 (no throw) | No throw statements. |
| JS-033 (no async void) | No new async methods. |
| JS-066 (CYC <= 8) | Zero McCabe delta. `StatusUpdate?.Invoke(...)` is not a branching construct. SendCopyWithAtm CYC: 4 → 4. |
| ASCII-only | Log strings: "ATM-send qty=", "tmpl=", "ATM-order qty=" -- all ASCII. No Unicode. |
| xUnit [Fact] | T2a test is [Fact] source-text assertion. No NUnit, no MSTest. |

### xUnit [Fact] Names

```
T2a-01: SendCopyWithAtm_LogsAtmSendQtyBeforeCreateOrder
         Assert.Contains: source text of CopyEngine.cs contains
           "ATM-send qty=" + signal.Quantity + " tmpl=" + namedMode.TemplateName
         (confirms the before-CreateOrder log line is present)

T2a-02: SendCopyWithAtm_LogsAtmOrderQtyAfterCreateOrder
         Assert.Contains: source text of CopyEngine.cs contains
           "ATM-order qty=" + order.Quantity
         (confirms the after-CreateOrder log line is present)
```

Both assertions use `System.IO.File.ReadAllText` to read CopyEngine.cs (same source-text pattern as T1 tests). Both are in the T2a section of B198Tests.cs.

### 7-Scan Checklist

```
[ ] SCAN-01: JS-021 -- No lock() in new/modified code
    Command: Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "lock\s*\(" | Where-Object { $_.Line -notmatch "//" }
    Expected: Zero matches in SendCopyWithAtm T2a change scope.

[ ] SCAN-02: JS-033 -- No async void (non-event-handler)
    Expected: No new async methods. SendCopyWithAtm is synchronous.

[ ] SCAN-03: JS-001/JS-002 -- No throw/return null in hot path
    Expected: No throw added. No new return null introduced.

[ ] SCAN-04: JS-066 -- SendCopyWithAtm CYC unchanged
    Command: python -m lizard src/PropTraderTools/CopyEngine.cs -C 8 --csv | Select-String "SendCopyWithAtm"
    Expected: CCN = 4 (unchanged). The two StatusUpdate?.Invoke calls are not branches.

[ ] SCAN-05: ASCII-only -- No Unicode/emoji in log strings
    Command: Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "[^\x00-\x7F]"
    Expected: Zero non-ASCII characters in T2a change scope.

[ ] SCAN-06: xUnit -- All T2a tests use [Fact] only
    Command: Select-String -Path "src/PropTraderTools/Tests/B198Tests.cs" -Pattern "NUnit|MSTest|\[TestMethod\]|\[Test\]"
    Expected: Zero matches.

[ ] SCAN-07: Sync field -- No new non-readonly non-atomic shared fields
    T2a introduces zero new fields. No shared state.
    Expected: No new fields added to CopyEngine class by T2a.
```

### Acceptance Criteria

1. `dotnet test` passes T2a-01 and T2a-02 (2 tests GREEN).
2. `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "ATM-send qty="` returns exactly 1 match (in SendCopyWithAtm).
3. `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "ATM-order qty="` returns exactly 1 match (in SendCopyWithAtm).
4. Lizard CYC check: SendCopyWithAtm CCN = 4 (unchanged from before T2a).
5. SCAN-01 through SCAN-07 all PASS.
6. `docs/brain/B198/ticket-2a-sim-evidence.md` created with all required fields filled in and hypothesis confirmed (H1, H2, or H3).

---

## Ticket 2b — QTY Bug Fix (DW-B198-QTY-01 Phase B)

### Spec Requirement IDs
- B198-orchestrator-prompt.md §TICKET 2: "Fix design depends on log evidence"
- 02-architecture-plan.md §4 T2b: H1/H2/H3 fix paths
- DW-B198-QTY-01 (Phase B -- conditional on T2a evidence)

### Objective
Implement the QTY bug fix (H2 path) if T2a SIM evidence confirms `ATM-order qty=5` while leader qty=10. If H1 is confirmed (no bug), this ticket is CANCELLED with no code changes. If H3 is confirmed, this ticket is DEFERRED.

### Scope Lock
SCOPE: This ticket touches ONLY (if H2 confirmed):
- `src/PropTraderTools/CopyEngine.cs` (new field `_atmTemplateGroupQty`, modification to `CheckAtmTemplatesForReverse(string)` and `SendCopyWithAtm`)
- `src/PropTraderTools/Tests/B198Tests.cs` (T2b test section)

Do NOT read or modify: any other ticket's scope. `PendingDispatchDrain`, drain path methods, ATM dir path -- all outside T2b scope.

### Pre-conditions

**MANDATORY GATE**: T2a SIM evidence must be recorded in `docs/brain/B198/ticket-2a-sim-evidence.md` before this ticket executes.

**Decision tree**:
- If T2a evidence shows **H1** (ATM-order qty=10, follower position=Long 10): close T2b as **CANNOT-REPRODUCE**. Write `docs/brain/B198/ticket-2b-cancelled.md` with the evidence summary. No code changes. DW-B198-QTY-01 is CLOSED.
- If T2a evidence shows **H2** (ATM-order qty=5): implement the fix below.
- If T2a evidence shows **H3** (ATM-order qty=10, follower position=Long 5): this ticket is DEFERRED. Write `docs/brain/B198/ticket-2b-deferred.md`. DW-B198-QTY-T2B-DESIGN remains OPEN. No code changes for T2b.

The remainder of this ticket describes the **H2 path only**.

### Method Signatures (H2 path)

```
private readonly ConcurrentDictionary<string, int> _atmTemplateGroupQty
    = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
// NEW field -- declared at class level with other readonly ConcurrentDictionary fields

private bool TryGetAtmTemplateGroupCount(string templateName, out int groupCount)
// NEW helper method -- reads _atmTemplateGroupQty populated by CheckAtmTemplatesForReverse
// Signature: private bool TryGetAtmTemplateGroupCount(string templateName, out int groupCount)

internal void CheckAtmTemplatesForReverse(string atmDir)
// MODIFIED -- already exists; extend to also parse <Quantity> per template XML and populate _atmTemplateGroupQty
// CYC: 6 → 7 (one new branch for qty-parse step)

private bool SendCopyWithAtm(Account follower, Instrument instrument, in CopySignal signal, NamedAtmMode namedMode)
// MODIFIED -- already exists (or Order? after T3 is applied)
// CYC: 4 → 5 (one new branch for group-qty lookup and division)
```

### Exact Code Changes (H2 path)

#### Change T2b-1: New field `_atmTemplateGroupQty`

In the CopyEngine class, with the other `private readonly ConcurrentDictionary` field declarations, add:

```csharp
// T2b: ATM template per-group qty parsed from XML. Key=templateName (OrdinalIgnoreCase). Value=perGroupQty.
// Populated in CheckAtmTemplatesForReverse. Read in SendCopyWithAtm. ConcurrentDictionary -- no lock. JS-021.
private readonly ConcurrentDictionary<string, int> _atmTemplateGroupQty =
    new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
```

---

#### Change T2b-2: Extend `CheckAtmTemplatesForReverse(string)` to parse qty

Locate `CheckAtmTemplatesForReverse(string atmDir)` (line ~1891). Inside the loop that already iterates XML files and parses `Reverse` value:

After the existing Reverse-check logic for each template XML file, add qty parsing:

```csharp
// T2b: parse per-group qty from template XML.
var qtyNode = doc.SelectSingleNode("//Quantity");
if (qtyNode != null && int.TryParse(qtyNode.InnerText, out int perGroupQty) && perGroupQty > 0)
    _atmTemplateGroupQty.TryAdd(System.IO.Path.GetFileNameWithoutExtension(f), perGroupQty);
```

This adds 1 new `if` branch (the `if (qtyNode != null ...)` compound). CYC: 6 → 7.

**Note on XML node path**: The exact XPath `//Quantity` must be verified against actual MGC $400 SL4 template XML. If the node is nested differently (e.g. `//AtmStrategy/Quantity`), adjust accordingly. The engineer MUST open one template XML file to confirm the node path before applying this change.

---

#### Change T2b-3: New helper `TryGetAtmTemplateGroupCount`

Add this new private method to CopyEngine class (place near `CheckAtmTemplatesForReverse`):

```csharp
private bool TryGetAtmTemplateGroupCount(string templateName, out int groupCount)
{
    if (string.IsNullOrEmpty(templateName))    // (1) if
    {
        groupCount = 0;
        return false;
    }
    return _atmTemplateGroupQty.TryGetValue(templateName, out groupCount);
}
```

CYC: 2 (1 if-null-guard + 1 from method entry). ASCII-only. No lock. No throw.

---

#### Change T2b-4: SendCopyWithAtm -- per-group qty division

Locate `SendCopyWithAtm`. After the T2a log line (before `CreateOrder`), add the per-group qty lookup and conditional division:

```csharp
// T2b (H2): if template group qty known, pass per-group qty to CreateOrder.
int qtyToSend = signal.Quantity;
if (TryGetAtmTemplateGroupCount(namedMode.TemplateName, out int groupCount)  // (new branch) CYC 4 -> 5
    && groupCount > 0
    && signal.Quantity > groupCount)
    qtyToSend = signal.Quantity / groupCount;
```

Then update the `CreateOrder` call to use `qtyToSend` instead of `signal.Quantity`:

```csharp
// Replace: follower.CreateOrder(..., signal.Quantity, ...)
// With:    follower.CreateOrder(..., qtyToSend, ...)
```

CYC impact: +1 (the new `if (TryGetAtmTemplateGroupCount(...) && ...)` branch). SendCopyWithAtm CYC: 4 → 5.

---

### JS Rule Constraints

| Rule | How this ticket satisfies it |
|------|------------------------------|
| JS-021 (no lock) | `_atmTemplateGroupQty` is ConcurrentDictionary -- lock-free. TryGetValue and TryAdd are lock-free operations. No lock() anywhere. |
| JS-001 (no throw) | No throw in TryGetAtmTemplateGroupCount or SendCopyWithAtm changes. |
| JS-033 (no async void) | No new async methods. |
| JS-066 (CYC <= 8) | CheckAtmTemplatesForReverse(string): 6 → 7 (PASS). SendCopyWithAtm: 4 → 5 (PASS). TryGetAtmTemplateGroupCount: 2 (PASS). All <= 8. |
| JS-008 (mutable struct) | No struct fields modified. New field is class-level ConcurrentDictionary. |
| ASCII-only | All new strings ASCII. No Unicode. |
| xUnit [Fact] | T2b tests use [Fact] only. |

### CYC Table (Ticket 2b H2 methods)

| Method | Before CYC | After CYC | JS-066 |
|--------|-----------|----------|--------|
| `CheckAtmTemplatesForReverse(string)` | 6 | 7 | PASS |
| `SendCopyWithAtm` | 4 | 5 | PASS |
| `TryGetAtmTemplateGroupCount` | N/A (new) | 2 | PASS |

### xUnit [Fact] Names

```
T2b-01: AtmTemplateGroupQty_FieldExists
        Assert.Contains: source text of CopyEngine.cs contains
          _atmTemplateGroupQty
        (confirms the new ConcurrentDictionary field was declared)

T2b-02: SendCopyWithAtm_DividesQtyByGroupCount
        Assert.Contains: source text of CopyEngine.cs contains
          signal.Quantity / groupCount
        (confirms the per-group qty division logic is present)
```

If H1 confirmed (ticket cancelled), both T2b-01 and T2b-02 tests are SKIPPED (or not added to B198Tests.cs). Engineer writes `ticket-2b-cancelled.md` instead.

### 7-Scan Checklist

```
[ ] SCAN-01: JS-021 -- No lock() in new/modified code
    Command: Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "lock\s*\(" | Where-Object { $_.Line -notmatch "//" }
    Expected: Zero matches in T2b change scope (_atmTemplateGroupQty, TryGetAtmTemplateGroupCount, CheckAtmTemplatesForReverse, SendCopyWithAtm).

[ ] SCAN-02: JS-033 -- No async void
    Expected: No new async methods.

[ ] SCAN-03: JS-001/JS-002 -- No throw/return null in hot path
    Expected: TryGetAtmTemplateGroupCount returns bool with out param. No throw. No return null.

[ ] SCAN-04: JS-066 -- All modified methods CYC <= 8
    Command: python -m lizard src/PropTraderTools/CopyEngine.cs -C 8 --csv | Select-String "SendCopyWithAtm|CheckAtmTemplatesForReverse|TryGetAtmTemplateGroupCount"
    Expected: SendCopyWithAtm=5, CheckAtmTemplatesForReverse=7, TryGetAtmTemplateGroupCount=2. All <= 8.

[ ] SCAN-05: ASCII-only
    Command: Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "[^\x00-\x7F]"
    Expected: Zero non-ASCII in T2b scope.

[ ] SCAN-06: xUnit only
    Command: Select-String -Path "src/PropTraderTools/Tests/B198Tests.cs" -Pattern "NUnit|MSTest|\[TestMethod\]|\[Test\]"
    Expected: Zero matches.

[ ] SCAN-07: Sync field check
    Command: Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "_atmTemplateGroupQty" -Context 2 | Where-Object { $_.Context.PreContext -match "lock" -or $_.Context.PostContext -match "lock" }
    Expected: Zero lock blocks around _atmTemplateGroupQty.
```

### Acceptance Criteria

**If H1**: `docs/brain/B198/ticket-2b-cancelled.md` created. `dotnet test` still GREEN on all prior tests. No code changes.

**If H2**:
1. `dotnet test` passes T2b-01 and T2b-02 (2 tests GREEN in addition to all prior).
2. `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "_atmTemplateGroupQty"` returns matches at field declaration, TryAdd site (CheckAtmTemplatesForReverse), and TryGetValue site (TryGetAtmTemplateGroupCount).
3. `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "signal.Quantity / groupCount"` returns 1 match in SendCopyWithAtm.
4. Lizard CYC: CheckAtmTemplatesForReverse(string) = 7, SendCopyWithAtm = 5. Both <= 8.
5. SCAN-01 through SCAN-07 all PASS.

**If H3**: `docs/brain/B198/ticket-2b-deferred.md` created. No code changes.

---

## Ticket 3 — fo=NULL Drain Path Fix (DW-B195-01)

### Spec Requirement IDs
- B198-orchestrator-prompt.md §TICKET 3: fo=NULL on Drain Path
- DW-B195-01 (originally DW-B167-DEFER-02, open since 2026-09-18)
- 02-architecture-plan.md §5: Full drain path fix design (Changes 1-5)
- 02-plan-review.md VIO-1 (RESOLVED): TrySetSlateReadyOnDrainFill must wire drainFo into SyncFollowerBracket
- SIM-3 PASS condition: ZERO `fo=NULL` in TP4-SFB log lines after drain-path fills (requires BOTH T1 + T3)

### Objective
Thread the follower entry order reference through the drain path so that `SyncFollowerBracket` receives the actual `Order` object (not null) when called after a drain-path fill. Resolves the timing race where `TrySetSlateReadyOnDrainFill` calls `SyncFollowerBracket` before NT8 populates the ATM bracket orders on the follower account.

### Scope Lock
SCOPE: This ticket touches ONLY:
- `src/PropTraderTools/CopyEngine.cs` (new field, modified SendCopyWithAtm return type, modified SubmitDrainedEntry, modified TrySetSlateReadyOnDrainFill, modified AbortDrainOnFill, modified TryDrainWatchdog)
- `src/PropTraderTools/Tests/B198Tests.cs` (T3 test section)

Do NOT read or modify: `PendingDispatchDrain` struct itself (we use a class-level dict instead to avoid JS-008 mutable struct violation). Do NOT touch ATM dir fix, QTY fix, or predicate fix methods.

### Pre-conditions
- Ticket 1 is VERIFY_PASS (predicate fixes required so SyncFollowerBracket can locate SimStop orders after the drain fill).
- Ticket 2a SIM evidence is recorded (T2a log lines are present in SendCopyWithAtm -- T3 is compatible with T2a changes).
- T2b status is determined (H1 cancelled or H2 implemented or H3 deferred -- T3 is independent of T2b outcome).
- CopyEngine.cs currently has `SendCopyWithAtm` returning `bool` (or `Order?` if T2b/T3 already partially applied -- see note below).

**IMPORTANT NOTE on ordering**: T3 Change 1 changes `SendCopyWithAtm` return type from `bool` to `Order?`. If T2b (H2 path) was applied first and also modified SendCopyWithAtm, the engineer must read the current state of SendCopyWithAtm before applying T3 changes. The return type change is the same in both cases (T3 owns it). T2b changes are to the method body only.

### Method Signatures

#### New Field
```csharp
private readonly ConcurrentDictionary<string, Order> _drainFollowerEntryOrders =
    new ConcurrentDictionary<string, Order>(StringComparer.Ordinal);
// Keyed by followerAcctKey (= follower Account.Name). Populated in SubmitDrainedEntry.
// Consumed in TrySetSlateReadyOnDrainFill. Cleaned up in AbortDrainOnFill and TryDrainWatchdog.
```

#### Modified Methods
```
private Order? SendCopyWithAtm(Account follower, Instrument instrument, in CopySignal signal, NamedAtmMode namedMode)
// RETURN TYPE CHANGE: bool -> Order?
// Returns: the created Order on success; null on failure.
// CYC: 4 (unchanged -- return type change only, no new branches)

private void SubmitDrainedEntry(string acctKey, Account follower, PendingDispatchDrain payload)
// MODIFIED: capture returned Order? from SendCopyWithAtm; store in _drainFollowerEntryOrders if non-null
// CYC: 5 -> 6 (+1 branch for if (newOrder != null))

private void TrySetSlateReadyOnDrainFill(...)
// MODIFIED: after setting _followerSlateReady, TryGetValue + TryRemove from _drainFollowerEntryOrders; pass drainFo to SyncFollowerBracket
// CYC: 5 -> 5 (TryGetValue + TryRemove = 0 new McCabe branches)

private void AbortDrainOnFill(string acctKey, ...)
// MODIFIED: add TryRemove cleanup for _drainFollowerEntryOrders
// CYC: 3 -> 3 (TryRemove = no branch)

private void TryDrainWatchdog(...)
// MODIFIED: add TryRemove cleanup inside expiry block
// CYC: 4 -> 4 (TryRemove inside existing if block = no new branch)
```

### Exact Code Changes

#### Change T3-1: New field `_drainFollowerEntryOrders`

Add to CopyEngine class with the other `private readonly ConcurrentDictionary` field declarations:

```csharp
// T3: tracks follower entry orders placed via drain path, keyed by followerAcctKey.
// Populated in SubmitDrainedEntry after SendCopyWithAtm. Removed on fill or abort.
// JS-021: ConcurrentDictionary -- no lock. JS-002: Order? nullable is intentional (NT8 absence signal).
private readonly ConcurrentDictionary<string, Order> _drainFollowerEntryOrders =
    new ConcurrentDictionary<string, Order>(StringComparer.Ordinal);
```

---

#### Change T3-2: SendCopyWithAtm return type change

Locate the method signature:
```csharp
private bool SendCopyWithAtm(Account follower, ...)
```

Change to:
```csharp
private Order? SendCopyWithAtm(Account follower, ...)
```

Inside the method body, replace all `return true;` with `return order;` (returning the created Order object) and all `return false;` with `return null;` (returning null for failure cases).

**6 call sites that must be updated** (read each site before changing):

| Approx line | Current code | Replacement |
|-------------|-------------|-------------|
| 2759 | `bool ok = SendCopyWithAtm(...)` | `bool ok = (SendCopyWithAtm(...) != null)` |
| 2870 | `bool ok = SendCopyWithAtm(...)` | `bool ok = (SendCopyWithAtm(...) != null)` |
| 2987 | `SendCopyWithAtm(...)` (result discarded) | no change needed (discard is still valid) |
| 4522 | `SendCopyWithAtm(...)` (result discarded) | no change needed (discard is still valid) |
| 6966 | `SendCopyWithAtm(...)` (result discarded) | no change needed (discard is still valid) |
| 7104 | `SendCopyWithAtm(...)` (result to be captured) | `Order? newOrder = SendCopyWithAtm(...)` (capture for T3-3 storage) |

**Note**: The engineer MUST verify the exact lines by searching for all `SendCopyWithAtm(` occurrences in CopyEngine.cs before applying changes. Line numbers may have shifted from earlier tickets.

CYC impact: 4 → 4 (return type change only, no new branches).

---

#### Change T3-3: SubmitDrainedEntry -- store returned Order

Locate `SubmitDrainedEntry`. Find the call to `SendCopyWithAtm` on the Named ATM path (approximately line 7104). After that call (which is now changed to `Order? newOrder = SendCopyWithAtm(...)` per T3-2), add:

```csharp
if (newOrder != null)                                          // NEW branch: CYC 5 -> 6
    _drainFollowerEntryOrders.TryAdd(acctKey, newOrder);
```

CYC impact: +1 (null-check branch). SubmitDrainedEntry CYC: 5 → 6.

---

#### Change T3-4: TrySetSlateReadyOnDrainFill -- retrieve and pass drainFo

Locate `TrySetSlateReadyOnDrainFill` (line ~1070). Find the existing `SyncFollowerBracket(...)` call within this method. Before that call (after the existing `_followerSlateReady[...] = true;` assignment), insert:

```csharp
// T3: retrieve and thread the stored follower entry order into SyncFollowerBracket.
// TryGetValue + TryRemove = 0 new branches; CYC stays at 5.
string acctKey = e.Order.Account.Name;
_drainFollowerEntryOrders.TryGetValue(acctKey, out Order drainFo);
_drainFollowerEntryOrders.TryRemove(acctKey, out _);
```

Then update the existing `SyncFollowerBracket(...)` call to pass `drainFo` instead of `null` as the `fo` parameter:

```csharp
// Replace: SyncFollowerBracket(acc, null, ...)
// With:    SyncFollowerBracket(acc, drainFo, ...)
```

`drainFo` may be null if `SendCopyWithAtm` failed (TryGetValue assigns default(Order) = null when key not found). In that case the `fo=NULL` log is still correct (SendCopyWithAtm failure is a different problem).

CYC impact: TryGetValue and TryRemove are method calls that assign via out parameters. Zero new McCabe branches. TrySetSlateReadyOnDrainFill CYC: 5 → 5 (unchanged).

---

#### Change T3-5: AbortDrainOnFill -- cleanup

Locate `AbortDrainOnFill` (line ~7126). Add at the beginning of the method (or alongside the existing `_pendingDispatchDrains.TryRemove` call):

```csharp
_drainFollowerEntryOrders.TryRemove(acctKey, out _);  // T3: cleanup stored entry order on abort
```

CYC impact: TryRemove is a method call, not a branch. AbortDrainOnFill CYC: 3 → 3 (unchanged).

---

#### Change T3-6: TryDrainWatchdog -- cleanup

Locate `TryDrainWatchdog` (line ~7133). Find the block inside `if (now - kv.Value.TimestampTicks > 2000L)` (watchdog expiry). Inside that block, add:

```csharp
_drainFollowerEntryOrders.TryRemove(kv.Key, out _);  // T3: cleanup stored entry order on watchdog expiry
```

CYC impact: TryRemove inside existing if block = no new branch. TryDrainWatchdog CYC: 4 → 4 (unchanged).

---

### JS Rule Constraints

| Rule | How this ticket satisfies it |
|------|------------------------------|
| JS-021 (no lock) | `_drainFollowerEntryOrders` is ConcurrentDictionary. TryAdd, TryGetValue, TryRemove are lock-free. No lock() anywhere in T3 scope. |
| JS-001 (no throw) | No throw statements added. |
| JS-002 (no return null) | SendCopyWithAtm returns `Order?` -- null is the NT8 established absence signal for CreateOrder failure. This is the project's documented null-as-absence contract (noted in plan review as exempt). The `Order?` nullable annotation makes this explicit. |
| JS-033 (no async void) | No new async methods. |
| JS-066 (CYC <= 8) | Maximum: SubmitDrainedEntry = 6 (PASS). All other T3 methods unchanged (4-5). All <= 8. |
| JS-008 (no mutable struct field) | `PendingDispatchDrain` struct is NOT modified. A class-level ConcurrentDictionary is used instead. This avoids the JS-008 mutable struct field violation that the spec's literal "struct field" wording would have triggered. |
| JS-009 (ConcurrentDictionary for shared collections) | `_drainFollowerEntryOrders` uses ConcurrentDictionary with Ordinal comparer. |
| ASCII-only | All new log strings ASCII. Comment strings ASCII. No Unicode. |
| xUnit [Fact] | T3 tests use [Fact] only. |

### CYC Table (Ticket 3 methods)

| Method | Before CYC | After CYC | JS-066 |
|--------|-----------|----------|--------|
| `SendCopyWithAtm` | 4 | 4 | PASS (return type change only) |
| `SubmitDrainedEntry` | 5 | 6 | PASS |
| `TrySetSlateReadyOnDrainFill` | 5 | 5 | PASS |
| `AbortDrainOnFill` | 3 | 3 | PASS |
| `TryDrainWatchdog` | 4 | 4 | PASS |

### xUnit [Fact] Names

```
T3-01: DrainFollowerEntryOrders_FieldExists
       Assert.Contains: source text of CopyEngine.cs contains
         _drainFollowerEntryOrders

T3-02: SendCopyWithAtm_ReturnsOrderType
       Assert.Contains: source text of CopyEngine.cs contains
         Order? SendCopyWithAtm
       (confirms return type is Order?, not bool)

T3-03: SubmitDrainedEntry_StoresEntryOrder
       Assert.Contains: source text of CopyEngine.cs contains
         _drainFollowerEntryOrders.TryAdd
       (confirms order is stored in SubmitDrainedEntry)

T3-04: TrySetSlateReadyOnDrainFill_RetrievesAndPassesDrainFo
       Assert.Contains: source text of CopyEngine.cs contains
         _drainFollowerEntryOrders.TryGetValue
       (confirms the wiring step -- retrieval in TrySetSlateReadyOnDrainFill)
```

### 7-Scan Checklist

```
[ ] SCAN-01: JS-021 -- No lock() in new/modified code
    Command: Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "lock\s*\(" | Where-Object { $_.Line -notmatch "//" }
    Expected: Zero matches in T3 change scope (_drainFollowerEntryOrders, SendCopyWithAtm, SubmitDrainedEntry, TrySetSlateReadyOnDrainFill, AbortDrainOnFill, TryDrainWatchdog).

[ ] SCAN-02: JS-033 -- No async void
    Expected: No new async methods introduced.

[ ] SCAN-03: JS-001/JS-002 -- No throw/return null
    Expected: SendCopyWithAtm returns null on failure (NT8 absence signal -- established project contract, exempt). No throw.

[ ] SCAN-04: JS-066 -- All modified methods CYC <= 8
    Command: python -m lizard src/PropTraderTools/CopyEngine.cs -C 8 --csv | Select-String "SendCopyWithAtm|SubmitDrainedEntry|TrySetSlateReadyOnDrainFill|AbortDrainOnFill|TryDrainWatchdog"
    Expected: SubmitDrainedEntry=6, all others <= 5.

[ ] SCAN-05: ASCII-only
    Command: Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "[^\x00-\x7F]"
    Expected: Zero non-ASCII in T3 scope.

[ ] SCAN-06: xUnit only
    Command: Select-String -Path "src/PropTraderTools/Tests/B198Tests.cs" -Pattern "NUnit|MSTest|\[TestMethod\]|\[Test\]"
    Expected: Zero matches.

[ ] SCAN-07: Sync field check
    Command: Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "_drainFollowerEntryOrders" -Context 2 | Where-Object { $_.Context.PreContext -match "lock" -or $_.Context.PostContext -match "lock" }
    Expected: Zero lock blocks around _drainFollowerEntryOrders.
```

### Acceptance Criteria

1. `dotnet test` passes T3-01 through T3-04 (4 tests GREEN in addition to all prior T1/T2 tests).
2. `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "Order\? SendCopyWithAtm"` returns exactly 1 match (the method signature).
3. `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "private bool SendCopyWithAtm"` returns ZERO matches (old signature replaced).
4. All 6 `SendCopyWithAtm` call sites updated correctly (2 capture bool via `!= null`, 3 discard unchanged, 1 captures `Order? newOrder`).
5. `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "_drainFollowerEntryOrders.TryAdd"` returns 1 match (SubmitDrainedEntry).
6. `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "_drainFollowerEntryOrders.TryGetValue"` returns 1 match (TrySetSlateReadyOnDrainFill).
7. `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "_drainFollowerEntryOrders.TryRemove"` returns 3 matches (TrySetSlateReadyOnDrainFill + AbortDrainOnFill + TryDrainWatchdog).
8. Lizard CYC check: SubmitDrainedEntry = 6, SendCopyWithAtm <= 5 (or 5 if T2b H2 applied), TrySetSlateReadyOnDrainFill = 5.
9. SCAN-01 through SCAN-07 all PASS.

---

## Ticket 4 — ATM Template Dir Startup Fix (DW-B198-ATMDIR-01)

### Spec Requirement IDs
- B198-orchestrator-prompt.md §TICKET 4: ATM Template Dir Startup Fix
- DW-B198-ATMDIR-01
- 02-architecture-plan.md §6: "CheckAtmTemplatesForReverse() path fix + warning log upgrade"
- Log evidence: `[PTT-STARTUP-INFO] ATM template dir not found` fires on every startup (output.md line 1 and 520)

### Objective
Correct the wrong subfolder path in the parameterless `CheckAtmTemplatesForReverse()` overload (uses "ATM" instead of "AtmStrategy"), and upgrade the dir-not-found log from INFO to WARN with the actual checked path included. Stops the spurious startup warning.

### Scope Lock
SCOPE: This ticket touches ONLY:
- `src/PropTraderTools/CopyEngine.cs` (CheckAtmTemplatesForReverse() parameterless overload + log string in CheckAtmTemplatesForReverse(string))
- `src/PropTraderTools/Tests/B198Tests.cs` (T4 test section -- 2 assertions)

Do NOT read or modify: any method other than the two CheckAtmTemplatesForReverse overloads. Do NOT touch TradeCopierPanel.cs.

### Pre-conditions
- Tickets 1, 2a, and 3 may be in any state (T4 is P2 and independent of T1-T3 changes).
- T4 may execute in parallel with T2b or T3 -- no overlapping change scope.
- The parameterless overload `CheckAtmTemplatesForReverse()` currently uses `"Templates", "ATM"` as path segments (confirmed by architect grep; current wrong path produces the dir-not-found log).

### Method Signatures

No new methods. Two existing methods modified:

```
internal void CheckAtmTemplatesForReverse()
// Parameterless overload -- line ~1883-1885
// MODIFIED: path string "ATM" -> "AtmStrategy"
// CYC: 1 -> 1 (no change -- pure string fix)

internal void CheckAtmTemplatesForReverse(string atmDir)
// String overload -- line ~1891-1937
// MODIFIED: dir-not-found log string upgraded from INFO to WARN with path appended
// CYC: 6 -> 6 (text change only, no new branches)
```

### Exact Code Changes

#### Change T4-1: CheckAtmTemplatesForReverse() -- path fix (line ~1883-1885)

Locate the parameterless overload of `CheckAtmTemplatesForReverse`:

CURRENT CODE:
```csharp
internal void CheckAtmTemplatesForReverse()
    => CheckAtmTemplatesForReverse(
        System.IO.Path.Combine(NinjaTrader.Core.Globals.UserDataDir, "Templates", "ATM"));
```

REPLACEMENT:
```csharp
internal void CheckAtmTemplatesForReverse()
    => CheckAtmTemplatesForReverse(
        System.IO.Path.Combine(NinjaTrader.Core.Globals.UserDataDir, "templates", "AtmStrategy"));
```

Changed: `"Templates", "ATM"` → `"templates", "AtmStrategy"`.

On Windows, filesystem path lookups are case-insensitive, so "templates" vs "Templates" would work either way. The issue is the folder name "ATM" which does not exist -- the correct NT8 subfolder is "AtmStrategy". The fix corrects "ATM" → "AtmStrategy".

CYC impact: 1 → 1. Pure string literal change.

---

#### Change T4-2: CheckAtmTemplatesForReverse(string) -- dir-not-found log upgrade (line ~1897-1899)

Locate `CheckAtmTemplatesForReverse(string atmDir)`. Find the dir-not-found log line. It currently reads:

CURRENT CODE (approximate):
```csharp
NinjaTrader.Code.Output.Process("[PTT-STARTUP-INFO] ATM template dir not found -- skipping Reverse check", ...);
```

REPLACEMENT:
```csharp
NinjaTrader.Code.Output.Process("[PTT-STARTUP-WARN] ATM template dir not found: " + atmDir + " -- skipping Reverse check", ...);
```

Changes:
- `[PTT-STARTUP-INFO]` → `[PTT-STARTUP-WARN]` (upgrade severity)
- append `: " + atmDir + "` after `not found` (include actual checked path)

CYC impact: 6 → 6. Text change only, no new branches.

**Note on existing Reverse=true warning**: The existing `[PTT-STARTUP-WARN] ATM template {name} has Reverse=Yes` log at lines ~1918-1922 is correct and satisfies the spec requirement for Reverse=true warning. Do NOT modify that log line.

---

### JS Rule Constraints

| Rule | How this ticket satisfies it |
|------|------------------------------|
| JS-021 (no lock) | No lock() added. Pure string and log changes. |
| JS-001 (no throw) | No throw statements. |
| JS-033 (no async void) | No new async methods. |
| JS-066 (CYC <= 8) | Both overloads: CYC unchanged (1 and 6). |
| ASCII-only | All string literals: "templates", "AtmStrategy", "[PTT-STARTUP-WARN]", " -- skipping Reverse check" -- all ASCII. No Unicode. |
| xUnit [Fact] | T4 tests use [Fact] only. |

### CYC Table (Ticket 4 methods)

| Method | Before CYC | After CYC | JS-066 |
|--------|-----------|----------|--------|
| `CheckAtmTemplatesForReverse()` | 1 | 1 | PASS |
| `CheckAtmTemplatesForReverse(string)` | 6 | 6 | PASS |

### xUnit [Fact] Names

```
T4-01: CheckAtmTemplatesForReverse_UsesAtmStrategyPath
       Assert.Contains: source text of CopyEngine.cs contains
         "templates", "AtmStrategy"
       (confirms the correct subfolder path is used -- NOT "ATM")

T4-02: CheckAtmTemplatesForReverse_DirNotFoundLogsPath
       Assert.Contains: source text of CopyEngine.cs contains
         [PTT-STARTUP-WARN] ATM template dir not found:
       (confirms the upgraded log level and path inclusion)
```

Both tests use `System.IO.File.ReadAllText` source-text assertion pattern from B197Tests.cs.

### 7-Scan Checklist

```
[ ] SCAN-01: JS-021 -- No lock() in new/modified code
    Command: Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "lock\s*\(" | Where-Object { $_.Line -notmatch "//" }
    Expected: Zero new lock() in CheckAtmTemplatesForReverse scope.

[ ] SCAN-02: JS-033 -- No async void
    Expected: No new async methods.

[ ] SCAN-03: JS-001/JS-002 -- No throw/return null
    Expected: No throw statements. No return null added (the method is void).

[ ] SCAN-04: JS-066 -- All modified methods CYC <= 8
    Command: python -m lizard src/PropTraderTools/CopyEngine.cs -C 8 --csv | Select-String "CheckAtmTemplatesForReverse"
    Expected: Both overloads CCN <= 8. Parameterless = 1, string overload = 6.

[ ] SCAN-05: ASCII-only
    Command: Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "[^\x00-\x7F]"
    Expected: Zero non-ASCII in T4 change scope.

[ ] SCAN-06: xUnit only
    Command: Select-String -Path "src/PropTraderTools/Tests/B198Tests.cs" -Pattern "NUnit|MSTest|\[TestMethod\]|\[Test\]"
    Expected: Zero matches.

[ ] SCAN-07: Sync field -- No new non-readonly non-atomic shared fields
    T4 introduces no new fields. Pure string changes only.
    Expected: No new fields added to CopyEngine class by T4.
```

### Acceptance Criteria

1. `dotnet test` passes T4-01 and T4-02 (2 tests GREEN in addition to all prior T1/T2/T3 tests).
2. `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern '"ATM"'` returns ZERO matches in the `CheckAtmTemplatesForReverse` parameterless overload (the wrong path is removed).
3. `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern '"AtmStrategy"'` returns 1 match in `CheckAtmTemplatesForReverse` parameterless overload.
4. `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "\[PTT-STARTUP-WARN\] ATM template dir not found:"` returns 1 match.
5. `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "\[PTT-STARTUP-INFO\] ATM template dir not found"` returns ZERO matches (old INFO log replaced).
6. Lizard CYC check: CheckAtmTemplatesForReverse() = 1, CheckAtmTemplatesForReverse(string) = 6 (or 7 if T2b H2 was also applied to this method). All <= 8.
7. SCAN-01 through SCAN-07 all PASS.

---

## Execution Order

```
T1  (DW-B198-SIMSTP-01)  -- P0, execute first
T2a (DW-B198-QTY-01 A)   -- P0, execute after T1 (SIM requires T1 changes to prevent cascade)
T2b (DW-B198-QTY-01 B)   -- P0, execute after T2a SIM evidence is recorded
T3  (DW-B195-01)          -- P1, execute after T1 + T2a (T1 predicate fixes required for SIM-3)
T4  (DW-B198-ATMDIR-01)   -- P2, independent (may execute in parallel with T2b or T3)
```

T3 Change 2 (SendCopyWithAtm return type) and T2b Change T2b-4 (SendCopyWithAtm body) both touch `SendCopyWithAtm`. If both are applied, apply T2b first (body changes), then T3 (return type + call site updates). Do not interleave.

---

## Aggregate CYC Summary (all B198 tickets)

| Method | File | T1 After | T2a After | T2b H2 After | T3 After | T4 After | Final | JS-066 |
|--------|------|----------|----------|-------------|---------|---------|-------|--------|
| IsNativeExitName | CopyEngine.cs | 5 | 5 | 5 | 5 | 5 | 5 | PASS |
| IsAtmBracketName | CopyEngine.cs | 2 | 2 | 2 | 2 | 2 | 2 | PASS |
| IsBracketLeg | CopyEngine.cs | 1 | 1 | 1 | 1 | 1 | 1 | PASS |
| IsBracketLegStatic | CopyEngine.cs | 1 | 1 | 1 | 1 | 1 | 1 | PASS |
| IsAtmSTPOrder | CopyEngine.cs | 1 | 1 | 1 | 1 | 1 | 1 | PASS |
| IsStopLeg | CopyEngine.cs | 1 | 1 | 1 | 1 | 1 | 1 | PASS |
| TryParseStopSuffix | CopyEngine.cs | 5 | 5 | 5 | 5 | 5 | 5 | PASS |
| CaptureOtherLegTargetPrices | CopyEngine.cs | 6 | 6 | 6 | 6 | 6 | 6 | PASS |
| ResubmitCollateralLegs | CopyEngine.cs | 5 | 5 | 5 | 5 | 5 | 5 | PASS |
| FindLeaderCollateralOrder | CopyEngine.cs | 3 | 3 | 3 | 3 | 3 | 3 | PASS |
| FindLeaderStopPrice | CopyEngine.cs | 6 | 6 | 6 | 6 | 6 | 6 | PASS |
| SendCopyWithAtm | CopyEngine.cs | 4 | 4 | 5 | 4/5 | 4/5 | 4-5 | PASS |
| SubmitDrainedEntry | CopyEngine.cs | 5 | 5 | 5 | 6 | 6 | 6 | PASS |
| TrySetSlateReadyOnDrainFill | CopyEngine.cs | 5 | 5 | 5 | 5 | 5 | 5 | PASS |
| AbortDrainOnFill | CopyEngine.cs | 3 | 3 | 3 | 3 | 3 | 3 | PASS |
| TryDrainWatchdog | CopyEngine.cs | 4 | 4 | 4 | 4 | 4 | 4 | PASS |
| CheckAtmTemplatesForReverse() | CopyEngine.cs | 1 | 1 | 1 | 1 | 1 | 1 | PASS |
| CheckAtmTemplatesForReverse(string) | CopyEngine.cs | 6 | 6 | 7 | 7 | 6/7 | 6-7 | PASS |
| TryGetAtmTemplateGroupCount | CopyEngine.cs | N/A | N/A | 2 | 2 | 2 | 2 | PASS |

**CYC ceiling violations: NONE.** Maximum across all tickets is 7 (CheckAtmTemplatesForReverse(string) with T2b H2 applied). All <= 8.

---

*Written by: ptt-architect*
*Epic: B198*
*Phase: 3 (Ticket Generation)*
*Source plan: docs/brain/B198/02-architecture-plan.md (REVIEW_PASS Cycle 2)*
*Returns: TICKETS_COMPLETE*
