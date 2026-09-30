# BWAVE-DW-REPAIR-LANEB Architecture Plan

**Epic**: BWAVE-DW-REPAIR-LANEB
**Branch**: feature/bwave-dw-lane-b
**Brain Dir**: docs/brain/BWAVE-DW/Repair-LaneB/
**Architect**: ptt-architect (Phase 1)
**Date**: 2026-09-03
**Status**: REVIEW_PASS

---

## RULES CATALOG GATE

**GATE RESULT: PASS**

Scope confirmed against `docs/standards/jane-street/RULES_CATALOG.md`:

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (lock() ban) | No lock statements introduced | PASS |
| JS-001 (throw in hot path) | No throw statements | PASS |
| JS-002 (return null) | No return null in new code; existing helper pre-dates this repair and is test-only | PASS |
| JS-033 (async void) | No async code introduced | PASS |
| JS-036/037 (heap alloc) | No allocations | PASS |
| ASCII-only | All new identifiers and comment text are ASCII-only | PASS |
| xUnit-only | New test uses [Fact] + Assert.Null() — xUnit only, no NUnit/MSTest | PASS |

**Zero P0 violations. Work may proceed.**

---

## LANE-SPLIT GATE

### Gate Questions

| Q | Question | Answer | Evidence |
|---|----------|--------|----------|
| Q1 | Same method or within 50 lines? | NO | B2 touches `BwaveCycLaneCTests.cs`; B3 touches `PropTraderTools.csproj`. Different files. |
| Q2 | Fix B3 design depends on Fix B2 final design? | NO | csproj Compile entries are independent of the test method replacement. |
| Q3 | Each fix has standalone value if the other is blocked? | YES | B2 is independently testable with `dotnet test`; B3 is independently testable with `dotnet build`. |
| Q4 | Each fix has an independent SIM verification path? | YES | B2: `dotnet test`; B3: `dotnet build`. |

### Gate Result

**SINGLE-PIPELINE**

Rationale: Both fixes reside on the same branch (`feature/bwave-dw-lane-b`), have trivial scope
(one method replacement + two XML lines), and have no ordering dependency. Splitting into separate
lanes would add coordination overhead with zero concurrency benefit. Sequential execution in a single
pipeline is the correct choice.

Execution order: R-LB-1 first (test fix), then R-LB-2 (csproj fix). Either order is safe; this
order is chosen because the test change is the higher-risk item and benefits from being verified
first in isolation before the build-level change is layered on.

---

## Component Inventory

| ID | Component | File | Type | Risk |
|----|-----------|------|------|------|
| R-LB-1 | Replace obsolete DisarmAllAccounts tests | `src/PropTraderTools/Tests/BwaveCycLaneCTests.cs` | Test-only | LOW |
| R-LB-2 | Add missing csproj Compile entries | `src/PropTraderTools/PropTraderTools.csproj` | csproj XML | LOW |

No production `.cs` files are modified. No NinjaTrader 8 API calls are introduced.

---

## Prior Context (Deferred Backlog)

Source: `docs/brain/BWAVE-DW/LaneB/06-deferred-backlog.md` (FINAL_PASS, 2026-08-26)

| Deferred Item | Status in This Repair |
|---------------|-----------------------|
| DW-C38-01 (TryAdd null-slot guard) | NOT in scope. Remains open. |
| DW-WARN-B131 (Assert.Equal boolean xUnit2004) | NOT in scope. Remains open. |
| DW-C38-03 (parallel-lane observation) | R-LB-1 closes the test-side concern: failing tests that asserted NotNull on the now-deleted DisarmAllAccounts method are replaced with a correct deletion-confirming test. |

---

## Ticket R-LB-1: Replace Obsolete DisarmAllAccounts Tests

### Overview

`DisarmAllAccounts` was deleted from `TradeCopierPanel.cs`. Two [Fact] tests in
`BwaveCycR10HelperTests` still assert `NotNull` on the reflection result of that deleted method.
At runtime these tests throw `NullReferenceException` (or fail with Assert.NotNull on null).
Replace both failing tests with a single deletion-confirming test.

### File Modified

`src/PropTraderTools/Tests/BwaveCycLaneCTests.cs`

### Precise Change Description

**RETAIN** (do not touch):
- The private helper method `GetDisarmAllAccountsMethod()` (approx. line 999–1011). It is
  referenced by the replacement test. Its body uses reflection to search for `DisarmAllAccounts`
  on `TradeCopierPanel` (non-public static) and returns `null` when not found.

**DELETE** — remove both of the following [Fact] methods in full:

```csharp
[Fact]
public void DisarmAllAccounts_DoesNotThrow_WhenAccountAllIsNull()
{
    // Verify DisarmAllAccounts is private static on TradeCopierPanel.
    var m = GetDisarmAllAccountsMethod();
    Assert.NotNull(m);
    Assert.True(m.IsPrivate);
    Assert.True(m.IsStatic);
    Assert.False(m.IsPublic);
}

[Fact]
public void DisarmAllAccounts_CallsDisarmPendingBe_ForEachAccount()
{
    // Verify method exists and is static with no parameters.
    var m = GetDisarmAllAccountsMethod();
    Assert.NotNull(m);
    Assert.True(m.IsStatic);
    Assert.Equal(0, m.GetParameters().Length);
    Assert.Equal(typeof(void), m.ReturnType);
}
```

**INSERT** in place of the deleted methods (exactly one [Fact] method):

```csharp
[Fact]
public void DisarmAllAccounts_IsDeleted()
{
    // DW-C38-03: DisarmAllAccounts was deleted. Confirm absence.
    Assert.Null(GetDisarmAllAccountsMethod());
}
```

### Method Signature

```csharp
// New test method (xUnit [Fact], synchronous, no parameters, void return)
public void DisarmAllAccounts_IsDeleted()
```

### JS Rule Constraints

| Rule | Constraint | Status |
|------|-----------|--------|
| JS-021 | No lock() | No lock statements — PASS |
| JS-033 | No async void | Synchronous method — PASS |
| ASCII | ASCII-only identifiers | All ASCII — PASS |
| xUnit | [Fact] only | Uses xUnit [Fact] + Assert.Null() — PASS |

### CYC

New method: CYC = 1 (no branches, no loops). Well within <= 8 limit.

### Acceptance Criteria

1. The two deleted test method names (`DisarmAllAccounts_DoesNotThrow_WhenAccountAllIsNull`,
   `DisarmAllAccounts_CallsDisarmPendingBe_ForEachAccount`) no longer appear in the test assembly.
2. The new test `DisarmAllAccounts_IsDeleted` exists in `BwaveCycR10HelperTests`.
3. `dotnet test` for `BwaveCycR10HelperTests` shows `DisarmAllAccounts_IsDeleted` PASS.
4. The helper `GetDisarmAllAccountsMethod()` is still present and unmodified.
5. No other tests in `BwaveCycR10HelperTests` are affected.

### Verification Command

```powershell
dotnet test src/PropTraderTools --filter "FullyQualifiedName~BwaveCycR10HelperTests" --verbosity normal
```

Expected: `DisarmAllAccounts_IsDeleted` = PASS. No failures. The two old method names do not appear.

### SCAN CHECKLIST

| Scan ID | Check | Command | Expected Result |
|---------|-------|---------|-----------------|
| SCAN-01 | No `lock()` | `grep -n "lock(" src/PropTraderTools/Tests/BwaveCycLaneCTests.cs` | 0 results |
| SCAN-02 | No `async void` | `grep -n "async void" src/PropTraderTools/Tests/BwaveCycLaneCTests.cs` | 0 results |
| SCAN-03 | No `return null` (new code only) | `grep -n "return null" src/PropTraderTools/Tests/BwaveCycLaneCTests.cs` | 0 new nulls (existing helper pre-dates this repair and is unchanged) |
| SCAN-04 | No `throw new` (new code only) | `grep -n "throw new" src/PropTraderTools/Tests/BwaveCycLaneCTests.cs` | 0 new throws introduced by this ticket |
| SCAN-05 | CYC <= 8 | `python scripts/complexity_audit.py` | PASS — new method `DisarmAllAccounts_IsDeleted` has CYC=1 |
| SCAN-06 | ASCII-only | byte scan of `BwaveCycLaneCTests.cs` | PASS — no non-ASCII characters introduced |
| SCAN-07 | xUnit only | `grep -n "using NUnit\|using Microsoft.VisualStudio.TestTools" src/PropTraderTools/Tests/BwaveCycLaneCTests.cs` | 0 results |

---

## Ticket R-LB-2: Add Missing csproj Compile Entries

### Overview

Two test files exist on disk at:
- `src/PropTraderTools/Tests/BwaveDwLaneATests.cs`
- `src/PropTraderTools/Tests/BwaveDwLaneBTests.cs`

Neither file has a `<Compile Include>` entry in `PropTraderTools.csproj`. Without these entries,
`dotnet build` cannot compile the two test files, producing build errors. This fix adds the two
missing entries in the existing `<ItemGroup>` that contains all other test compile entries.

### File Modified

`src/PropTraderTools/PropTraderTools.csproj`

### Precise Change Description

**Current state** (lines 176–179 of PropTraderTools.csproj):

```xml
    <Compile Include="Tests\BwaveCycLaneCTests.cs" />
    <Compile Include="Tests\BwaveCycLaneAR9Tests.cs" />
    <Compile Include="Tests\BwaveCycLaneBTests.cs" />
  </ItemGroup>
```

**Target state** (after fix):

```xml
    <Compile Include="Tests\BwaveCycLaneCTests.cs" />
    <Compile Include="Tests\BwaveCycLaneAR9Tests.cs" />
    <Compile Include="Tests\BwaveCycLaneBTests.cs" />
    <Compile Include="Tests\BwaveDwLaneATests.cs" />
    <Compile Include="Tests\BwaveDwLaneBTests.cs" />
  </ItemGroup>
```

**Net change**: Insert 2 lines before the closing `</ItemGroup>` tag (line 179). No existing lines
removed or modified.

### JS Rule Constraints

Not applicable — this is a pure XML csproj edit. No C# code is written. No rule violations possible.

### Acceptance Criteria

1. `PropTraderTools.csproj` contains `<Compile Include="Tests\BwaveDwLaneATests.cs" />`.
2. `PropTraderTools.csproj` contains `<Compile Include="Tests\BwaveDwLaneBTests.cs" />`.
3. No existing `<Compile Include>` entries are removed or modified.
4. `dotnet build src/PropTraderTools/PropTraderTools.csproj` completes with 0 errors.

### Verification Command

```powershell
dotnet build src/PropTraderTools/PropTraderTools.csproj --verbosity minimal
```

Expected: `Build succeeded. 0 Error(s)`.

### SCAN CHECKLIST

This ticket modifies a pure XML file (`PropTraderTools.csproj`). No C# code is introduced.
SCAN-01 through SCAN-04 and SCAN-07 return 0 results trivially — there is no C# syntax in a csproj file.

| Scan ID | Check | Command | Expected Result |
|---------|-------|---------|-----------------|
| SCAN-01 | No `lock()` | `grep -n "lock(" src/PropTraderTools/PropTraderTools.csproj` | 0 results (XML file — no C# code) |
| SCAN-02 | No `async void` | `grep -n "async void" src/PropTraderTools/PropTraderTools.csproj` | 0 results (XML file — no C# code) |
| SCAN-03 | No `return null` (new code only) | `grep -n "return null" src/PropTraderTools/PropTraderTools.csproj` | 0 results (XML file — no C# code) |
| SCAN-04 | No `throw new` (new code only) | `grep -n "throw new" src/PropTraderTools/PropTraderTools.csproj` | 0 results (XML file — no C# code) |
| SCAN-05 | CYC <= 8 | N/A — csproj XML edit, no C# methods | N/A — no complexity introduced |
| SCAN-06 | ASCII-only | byte scan of `PropTraderTools.csproj` | PASS — two new `<Compile Include>` lines use ASCII-only characters |
| SCAN-07 | xUnit only | `grep -n "NUnit\|MSTest" src/PropTraderTools/PropTraderTools.csproj` | 0 results (XML file — no C# code) |

---

## Combined Verification

After both tickets are applied:

```powershell
# Step 1: Build succeeds (R-LB-2 verified)
dotnet build src/PropTraderTools/PropTraderTools.csproj --verbosity minimal

# Step 2: All BwaveCyc R10 tests pass (R-LB-1 verified)
dotnet test src/PropTraderTools --filter "FullyQualifiedName~BwaveCycR10HelperTests" --verbosity normal

# Step 3: Full test run — confirm no regressions
dotnet test src/PropTraderTools --verbosity minimal
```

---

## Risk Assessment

| Ticket | Risk Level | Rationale |
|--------|-----------|-----------|
| R-LB-1 | LOW | Test-only change. No production code affected. Replaces NullReferenceException-throwing tests with a correct assertion. |
| R-LB-2 | LOW | csproj XML only. Adds compile entries for files already on disk. No production code affected. Reversible by removing two lines. |

**Overall repair risk: LOW.**

---

## NT8 API Surface

Not applicable. Neither fix uses NinjaTrader 8 APIs.

Key NT8 facts (embedded per protocol for completeness):
- `AtmStrategyChangeStopTarget()` — StrategyBase-only, NOT AddOnBase
- `AtmStrategyCreate()` — StrategyBase-only, NOT AddOnBase
- `Account.Change()` — AddOnBase available but silent no-op on ATM-owned brackets
- `Account.Cancel()` + `Account.CreateOrder()` + `Submit()` — correct AddOn cancel+resubmit pattern

None of these apply to this repair.

---

## Threading Model

Not applicable. Both fixes are synchronous, test-only or XML-only. No Dispatcher.InvokeAsync,
no ConcurrentQueue, no lock() (banned per JS-021).

---

## Summary

| Item | Value |
|------|-------|
| Lane-split gate | SINGLE-PIPELINE |
| Tickets | 2 (R-LB-1, R-LB-2) |
| Production files modified | 0 |
| Test files modified | 1 (BwaveCycLaneCTests.cs) |
| csproj files modified | 1 (PropTraderTools.csproj) |
| New test methods | 1 (`DisarmAllAccounts_IsDeleted`, CYC=1) |
| Deleted test methods | 2 (obsolete NotNull assertions) |
| Added XML lines | 2 (Compile Include entries) |
| P0 violations | 0 |
| NT8 API usage | None |
| Overall risk | LOW |
