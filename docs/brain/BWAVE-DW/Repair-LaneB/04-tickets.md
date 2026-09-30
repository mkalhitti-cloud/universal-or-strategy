# BWAVE-DW-REPAIR-LANEB Engineer Tickets

**Epic**: BWAVE-DW-REPAIR-LANEB
**Branch**: feature/bwave-dw-lane-b
**Brain Dir**: docs/brain/BWAVE-DW/Repair-LaneB/
**Source Plan**: 02-architecture-plan.md (REVIEW_PASS)
**Generated**: 2026-09-03
**Execution Order**: R-LB-1 first, then R-LB-2

---

## TICKET R-LB-1: Replace Obsolete DisarmAllAccounts Tests

### Spec Requirement IDs

- **DW-C38-03** (deferred backlog item, lane-split observation): DisarmAllAccounts was deleted from
  production. Two tests still assert NotNull on the reflection result — they now fail with
  NullReferenceException. Replace with a single deletion-confirming test.

### File Modified

```
src/PropTraderTools/Tests/BwaveCycLaneCTests.cs
```

> **BOBIGNORE NOTE**: This file is listed in `.bobignore`. The engineer MUST read it using:
> ```powershell
> Get-Content src/PropTraderTools/Tests/BwaveCycLaneCTests.cs | Select-String -Pattern "DisarmAllAccounts" -Context 5,5
> ```
> Do NOT use `read_file` — it will silently fail. Use `execute_command` with `Get-Content`.

### Exact Change Description

**Step 1 — Locate the two obsolete [Fact] methods.**

They are inside class `BwaveCycR10HelperTests` at approximately lines 1035–1054.
Both can be identified by their method names:
- `DisarmAllAccounts_DoesNotThrow_WhenAccountAllIsNull`
- `DisarmAllAccounts_CallsDisarmPendingBe_ForEachAccount`

Use this command to confirm their exact line numbers before editing:
```powershell
Get-Content src/PropTraderTools/Tests/BwaveCycLaneCTests.cs | Select-String -Pattern "DisarmAllAccounts_DoesNotThrow|DisarmAllAccounts_CallsDisarmPending" -SimpleMatch
```

**Step 2 — DELETE both [Fact] methods in their entirety.**

Remove this block (approximately lines 1035–1054):
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

**Step 3 — INSERT the following single [Fact] method in place of the deleted block:**

```csharp
[Fact]
public void DisarmAllAccounts_IsDeleted()
{
    // DW-C38-03: DisarmAllAccounts was deleted. Confirm absence.
    Assert.Null(GetDisarmAllAccountsMethod());
}
```

**What MUST NOT be touched:**

- The private helper `GetDisarmAllAccountsMethod()` (approximately lines 999–1011). It is
  retained because the replacement test calls it. Do NOT delete or modify it.
- The closing `}` of class `BwaveCycR10HelperTests`. Do NOT remove it.
- All other tests in `BwaveCycR10HelperTests`. Do NOT touch them.

### Method Signature

```csharp
// New test — xUnit [Fact], public, synchronous, void return, no parameters
public void DisarmAllAccounts_IsDeleted()
```

CYC = 1 (no branches, no loops). Within the CYC <= 8 limit.

### JS Rule Constraints

| Rule | Constraint | Status |
|------|-----------|--------|
| JS-021 | No `lock()` anywhere | No lock statements — PASS |
| JS-033 | No `async void` | Synchronous method only — PASS |
| JS-002 | No `return null` (new code) | New test has no return statement — PASS |
| ASCII | ASCII-only identifiers and string content | All identifiers and comment text are ASCII — PASS |
| xUnit | [Fact] + Assert.Null() only — no NUnit, no MSTest | xUnit only — PASS |

### xUnit Test

| Test Name | Class | What It Asserts |
|-----------|-------|-----------------|
| `DisarmAllAccounts_IsDeleted` | `BwaveCycR10HelperTests` | `GetDisarmAllAccountsMethod()` returns `null`, confirming `DisarmAllAccounts` no longer exists on `TradeCopierPanel` |

### NT8 Sync Statement

**NOT REQUIRED.** This ticket modifies a test file only. No production `.cs` files are changed.
No NinjaTrader 8 API surface is affected. Do NOT run `ptt-sync-and-verify.ps1`.

### Verification Command

```powershell
dotnet test src/PropTraderTools --filter "FullyQualifiedName~BwaveCycR10HelperTests" --verbosity normal
```

**Expected outcome**:
- `DisarmAllAccounts_IsDeleted` = **PASS**
- The two old test names (`DisarmAllAccounts_DoesNotThrow_WhenAccountAllIsNull`,
  `DisarmAllAccounts_CallsDisarmPendingBe_ForEachAccount`) do **not** appear in output
- Zero failures

### 7-Scan Checklist (SCAN-01 through SCAN-07)

Run all scans against the modified file **after** applying the change, before committing.

| Scan ID | Check | Command | Expected Result |
|---------|-------|---------|-----------------|
| SCAN-01 | No `lock()` | `grep -n "lock(" src/PropTraderTools/Tests/BwaveCycLaneCTests.cs` | **0 results** |
| SCAN-02 | No `async void` | `grep -n "async void" src/PropTraderTools/Tests/BwaveCycLaneCTests.cs` | **0 results** |
| SCAN-03 | No `return null` in new code | `grep -n "return null" src/PropTraderTools/Tests/BwaveCycLaneCTests.cs` | 0 new `return null` lines introduced; existing helper is unchanged and pre-dates this repair |
| SCAN-04 | No `throw new` in new code | `grep -n "throw new" src/PropTraderTools/Tests/BwaveCycLaneCTests.cs` | **0 results** for new code introduced by this ticket |
| SCAN-05 | CYC <= 8 | `python scripts/complexity_audit.py` | **PASS** — `DisarmAllAccounts_IsDeleted` has CYC=1 |
| SCAN-06 | ASCII-only | `(Get-Content src/PropTraderTools/Tests/BwaveCycLaneCTests.cs -Encoding Byte) \| Where-Object { $_ -gt 127 } \| Measure-Object \| Select-Object -ExpandProperty Count` | **0** (no non-ASCII bytes introduced) |
| SCAN-07 | xUnit only (no NUnit/MSTest) | `grep -n "using NUnit\|using Microsoft.VisualStudio.TestTools" src/PropTraderTools/Tests/BwaveCycLaneCTests.cs` | **0 results** |

### Acceptance Criteria

1. `DisarmAllAccounts_DoesNotThrow_WhenAccountAllIsNull` no longer exists in the file.
2. `DisarmAllAccounts_CallsDisarmPendingBe_ForEachAccount` no longer exists in the file.
3. `DisarmAllAccounts_IsDeleted` exists in class `BwaveCycR10HelperTests`.
4. `dotnet test --filter "FullyQualifiedName~BwaveCycR10HelperTests"` shows `DisarmAllAccounts_IsDeleted` = PASS with zero failures.
5. The private helper `GetDisarmAllAccountsMethod()` is present and unmodified.
6. All 7 scans report the expected results above.

---

## TICKET R-LB-2: Add BwaveDwLaneA/B Compile Entries to csproj

### Spec Requirement IDs

- **B3** (deferred backlog item): Two test files (`BwaveDwLaneATests.cs`,
  `BwaveDwLaneBTests.cs`) exist on disk but have no `<Compile Include>` entries in
  `PropTraderTools.csproj`. Without these entries, `dotnet build` cannot compile them.

### File Modified

```
src/PropTraderTools/PropTraderTools.csproj
```

### Exact Change Description

**Step 1 — Confirm the current state of the csproj near the end of the last `<ItemGroup>`.**

Run:
```powershell
Get-Content src/PropTraderTools/PropTraderTools.csproj | Select-String -Pattern "BwaveCycLaneBTests|BwaveDwLane" -Context 2,4
```

Expected output confirms lines similar to:
```xml
    <Compile Include="Tests\BwaveCycLaneBTests.cs" />
  </ItemGroup>
</Project>
```

**Step 2 — INSERT the following two lines immediately before the `</ItemGroup>` closing tag.**

The `</ItemGroup>` in question is the last `</ItemGroup>` in the file (the one that closes the
block containing all `<Compile Include="Tests\...">` entries).

Insert these two lines (in this order) before that `</ItemGroup>`:
```xml
    <Compile Include="Tests\BwaveDwLaneATests.cs" />
    <Compile Include="Tests\BwaveDwLaneBTests.cs" />
```

**Result after change** — the end of the last `<ItemGroup>` block becomes:
```xml
    <Compile Include="Tests\BwaveCycLaneBTests.cs" />
    <Compile Include="Tests\BwaveDwLaneATests.cs" />
    <Compile Include="Tests\BwaveDwLaneBTests.cs" />
  </ItemGroup>
</Project>
```

**No existing lines are removed or modified.** Only 2 lines are inserted.

### JS Rule Constraints

Not applicable — this is a pure XML edit to a `.csproj` file. No C# code is written.
No P0 or P1 rules apply to XML project configuration.

### xUnit Tests

None. This ticket is a project file edit only. No test methods are added or changed.

### NT8 Sync Statement

**NOT REQUIRED.** This ticket modifies only `PropTraderTools.csproj` (XML). No production `.cs`
files are changed. No NinjaTrader 8 API surface is affected. Do NOT run `ptt-sync-and-verify.ps1`.

### Verification Command

```powershell
dotnet build src/PropTraderTools/PropTraderTools.csproj --verbosity minimal
```

**Expected outcome**: `Build succeeded. 0 Error(s)`

Also confirm the two entries are present:
```powershell
Select-String -Path src/PropTraderTools/PropTraderTools.csproj -Pattern "BwaveDwLaneATests|BwaveDwLaneBTests"
```

Expected: 2 matching lines, one for each file.

### 7-Scan Checklist (SCAN-01 through SCAN-07)

This ticket modifies a pure XML file. SCAN-01 through SCAN-04 and SCAN-07 trivially return
0 results — there is no C# syntax in a csproj file. All scans are run against the modified file.

| Scan ID | Check | Command | Expected Result |
|---------|-------|---------|-----------------|
| SCAN-01 | No `lock()` | `grep -n "lock(" src/PropTraderTools/PropTraderTools.csproj` | **0 results** (XML file — no C# code) |
| SCAN-02 | No `async void` | `grep -n "async void" src/PropTraderTools/PropTraderTools.csproj` | **0 results** (XML file — no C# code) |
| SCAN-03 | No `return null` | `grep -n "return null" src/PropTraderTools/PropTraderTools.csproj` | **0 results** (XML file — no C# code) |
| SCAN-04 | No `throw new` | `grep -n "throw new" src/PropTraderTools/PropTraderTools.csproj` | **0 results** (XML file — no C# code) |
| SCAN-05 | CYC <= 8 | N/A — csproj XML edit, no C# methods introduced | **N/A** — no complexity introduced |
| SCAN-06 | ASCII-only | `(Get-Content src/PropTraderTools/PropTraderTools.csproj -Encoding Byte) \| Where-Object { $_ -gt 127 } \| Measure-Object \| Select-Object -ExpandProperty Count` | **0** (two new `<Compile Include>` lines are ASCII-only) |
| SCAN-07 | xUnit only (no NUnit/MSTest) | `grep -n "NUnit\|MSTest" src/PropTraderTools/PropTraderTools.csproj` | **0 results** (XML file — no C# code) |

### Acceptance Criteria

1. `PropTraderTools.csproj` contains `<Compile Include="Tests\BwaveDwLaneATests.cs" />`.
2. `PropTraderTools.csproj` contains `<Compile Include="Tests\BwaveDwLaneBTests.cs" />`.
3. No existing `<Compile Include>` entries are removed or modified.
4. `dotnet build src/PropTraderTools/PropTraderTools.csproj --verbosity minimal` exits 0 with `Build succeeded. 0 Error(s)`.
5. All 7 scans report the expected results above.

---

## Combined Verification (after both tickets applied)

Run in order after R-LB-1 and R-LB-2 are both complete:

```powershell
# Step 1: Build succeeds (verifies R-LB-2)
dotnet build src/PropTraderTools/PropTraderTools.csproj --verbosity minimal

# Step 2: R10 helper tests all pass (verifies R-LB-1)
dotnet test src/PropTraderTools --filter "FullyQualifiedName~BwaveCycR10HelperTests" --verbosity normal

# Step 3: Full test run -- confirm no regressions
dotnet test src/PropTraderTools --verbosity minimal
```

All three commands must exit 0 with zero errors and zero failures before this repair is complete.

---

## Summary

| Item | Value |
|------|-------|
| Tickets | 2 (R-LB-1, R-LB-2) |
| Execution order | R-LB-1 first, R-LB-2 second |
| Production files modified | 0 |
| Test files modified | 1 (`BwaveCycLaneCTests.cs`) |
| csproj files modified | 1 (`PropTraderTools.csproj`) |
| New test methods | 1 (`DisarmAllAccounts_IsDeleted`, CYC=1) |
| Deleted test methods | 2 (obsolete NotNull assertions) |
| Added XML lines | 2 (`<Compile Include>` entries) |
| NT8 sync required | NO |
| P0 violations | 0 |
| Overall risk | LOW |
