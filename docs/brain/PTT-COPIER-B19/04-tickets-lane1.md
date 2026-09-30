# PTT-COPIER-B19 — Implementation Tickets
# Block: PTT-COPIER-B19
# Ticket: DW-B19-COPIER-BUG-01 (P0)
# Author: ptt-architect
# Plan source: docs/brain/PTT-COPIER-B19/02-architecture-plan.md (REVIEW_PASS)
# Plan review: docs/brain/PTT-COPIER-B19/02-plan-review.md (REVIEW_PASS Cycle 2)
# Lane: Lane 1 (CopyEngine.cs only)
# Total tickets: 1 (T1)

---

## Ticket T1: DW-B19-COPIER-BUG-01 — Gate 2 Account Reference Fix

### Spec Requirement IDs

| ID | Requirement |
|----|-------------|
| REQ-B19-01 | Gate 2 must use string name equality, not reference equality, for account matching |
| REQ-B19-02 | Two new `[Fact]` tests: `Gate2_UsesAccountName_SourceContractVerified` and `Gate2_NullMasterAccount_NoCopyOrder` |
| REQ-B19-03 | No regressions — all 111 prior tests must still pass |
| REQ-B19-04 | Zero `lock()` in `CopyEngine.cs` |

---

### Root Cause

`CopyEngine.cs` line 381 (Gate 2 of `OnOrderUpdate`) compares `e.Order.Account == rule.MasterAccount`
using C# **object reference equality**. After a Rithmic reconnect at 16:43 (log.20260713.00002.txt),
NinjaTrader internally recreates Account objects. The stored `rule.MasterAccount` reference is stale —
a different heap address for the same logical account. Gate 2 returns false for every order. `SendCopy`
never fires. **Zero follower orders** for all leader trades after reconnect.

### Fix — One Line Change

**File**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs`
**Method**: `OnOrderUpdate`
**Line**: ~381

```csharp
// BEFORE (reference equality — breaks after Rithmic reconnect):
if (e.Order.Instrument.FullName == rule.Instrument && e.Order.Account == rule.MasterAccount)

// AFTER (name equality — survives reconnect):
if (e.Order.Instrument.FullName == rule.Instrument && e.Order.Account.Name == rule.MasterAccount?.Name)
```

**Rationale for `?.Name`**: `AddRule` accepts `(Account)null` as master (verified by 5+ existing tests
at CopyEngineTests.cs lines 68, 88, 125, 136). Without the null-conditional, null MasterAccount rules
would throw NRE in Gate 2. With `?.Name`, null evaluates to `null`, Gate 2 returns false (no match),
no exception. `e.Order.Account` (live order's account) is never null in NT8 runtime.

---

### Files Modified

| File | Nature |
|------|--------|
| `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs` | 1 line change at line ~381 |
| `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs` | 2 new `[Fact]` tests appended |

### Files NOT Modified

- `TradeCopierPanel.cs`
- `TradeCopierWindow.cs`
- `TradeCopierAddOn.cs`
- `AtrSizingEngine.cs`

---

### Method Signatures

#### CopyEngine.cs — `OnOrderUpdate` (signature UNCHANGED; body: 1 line only)

- Method signature: unchanged — NT8 event handler subscribed via infrastructure
- Change scope: line ~381 only — NO other lines touched
- Surrounding context must NOT be modified: Gate 1, Gate 2.5, Gate B, the foreach loop structure

---

### Test Contract — Two New xUnit [Fact] Tests

Both tests are appended to the existing `CopyEngineTests` class in `CopyEngineTests.cs`.
They follow the established structural-contract pattern used throughout the test file:
reflection-based assertions that do not require NT8 runtime (`Account` cannot be instantiated in tests).

#### Test 1: `Gate2_UsesAccountName_SourceContractVerified`

**Purpose**: Verify the type-contract pre-conditions for the Gate 2 fix. Specifically, confirm
that `Account` (as stored in `CopyRule.MasterAccount`) has a `Name` property of type `string`.

**Implementation guide**:
1. Get the `_rules` field from `CopyEngine` via reflection.
2. Extract the generic element type from `ConcurrentBag<CopyRule>` — that is `CopyRule`.
3. Get the `MasterAccount` field from the `CopyRule` type.
4. Verify `MasterAccount` is of type `Account` (field type name == "Account").
5. Get the `Name` property from the `Account` type.
6. Assert `Name` property exists (`Assert.NotNull`) and its return type is `string`.

```csharp
[Fact]
public void Gate2_UsesAccountName_SourceContractVerified()
{
    // Get _rules field -- ConcurrentBag<CopyRule>
    var fi = typeof(CopyEngine).GetField("_rules",
        BindingFlags.NonPublic | BindingFlags.Instance);
    Assert.NotNull(fi);

    // CopyRule is the generic element type of the bag
    var copyRuleType = fi.FieldType.GetGenericArguments()[0];
    Assert.NotNull(copyRuleType);

    // MasterAccount field must exist on CopyRule
    var masterField = copyRuleType.GetField("MasterAccount",
        BindingFlags.NonPublic | BindingFlags.Instance);
    Assert.NotNull(masterField);

    // MasterAccount must be of type Account
    var accountType = masterField.FieldType;
    Assert.Equal("Account", accountType.Name);

    // Account.Name must be a public instance string property
    var nameProp = accountType.GetProperty("Name",
        BindingFlags.Public | BindingFlags.Instance);
    Assert.NotNull(nameProp);
    Assert.Equal(typeof(string), nameProp.PropertyType);
}
```

#### Test 2: `Gate2_NullMasterAccount_NoCopyOrder`

**Purpose**: Verify that when a rule has a null `MasterAccount`, the null-conditional `?.Name`
does not throw `NullReferenceException` and no copy dispatch fires. Guards against regression
from `rule.MasterAccount.Name` (non-null-conditional).

**Implementation guide**:
1. Set engine disabled (`SetEnabled(false)`) — no copy dispatch.
2. Subscribe to `StatusUpdate` to detect spurious fires.
3. Call `AddRule` with `(Account)null` master — follows existing test pattern.
4. Get `_rules` bag via reflection; iterate; for each rule get `MasterAccount` via reflection.
5. Simulate the null-conditional: if `masterAccount == null`, name is null (not exception).
6. Assert name is null (correct null-safe evaluation).
7. Assert no StatusUpdate fired.

```csharp
[Fact]
public void Gate2_NullMasterAccount_NoCopyOrder()
{
    _engine.SetEnabled(false);
    bool statusFired = false;
    _statusHandler = _ => statusFired = true;
    _engine.StatusUpdate += _statusHandler;

    // AddRule with null master -- accepted input, 5+ existing tests confirm this
    var addEx = Record.Exception(() => _engine.AddRule("B19NULL", (Account)null, new Account[0]));
    Assert.Null(addEx);

    // Verify _rules bag contains the null-master rule
    var fi = typeof(CopyEngine).GetField("_rules",
        BindingFlags.NonPublic | BindingFlags.Instance);
    Assert.NotNull(fi);
    var bag = fi.GetValue(_engine);
    var copyRuleType = fi.FieldType.GetGenericArguments()[0];
    var masterField = copyRuleType.GetField("MasterAccount",
        BindingFlags.NonPublic | BindingFlags.Instance);
    Assert.NotNull(masterField);

    // Walk the bag and verify null-conditional .Name evaluation does not throw
    bool foundNullMaster = false;
    foreach (var boxed in (System.Collections.IEnumerable)bag)
    {
        var instr = (string)copyRuleType.GetField("Instrument",
            BindingFlags.NonPublic | BindingFlags.Instance).GetValue(boxed);
        if (instr != "B19NULL") continue;
        var masterAccount = masterField.GetValue(boxed);
        // Simulate rule.MasterAccount?.Name
        string name = masterAccount == null ? null
            : (string)masterAccount.GetType().GetProperty("Name",
                BindingFlags.Public | BindingFlags.Instance).GetValue(masterAccount);
        Assert.Null(name); // null master -> null name -> Gate 2 no-match (correct)
        foundNullMaster = true;
    }
    Assert.True(foundNullMaster, "Rule B19NULL with null master not found in _rules");

    // No StatusUpdate must have fired from copy dispatch path
    Assert.False(statusFired);
}
```

---

### JS Rule Constraints

| Rule | Description | Required Status |
|------|-------------|----------------|
| JS-021 | No `lock()` anywhere in src/ | PASS — Gate 2 is read-only `foreach` over `ConcurrentBag`. Fix changes condition expression only. No lock introduced. |
| JS-001 | No `throw new XxxException` in hot paths | PASS — null-conditional `?.Name` evaluates to null on null input. No new exception paths. |
| JS-002 | No `return null` for missing values | PASS — no new methods introduced. |
| CYC ≤ 8 | All new/changed methods | PASS — `OnOrderUpdate` CYC unchanged at 7 (fix changes comparison type, not branch count). |

---

### NT8 Constraints

| Rule | Requirement | Status |
|------|-------------|--------|
| `Account.Name` validity | `string` property confirmed via 10+ existing uses in CopyEngine.cs (lines 456, 514, 589, 820, 843, 881, 925, 967, 997, 1068) | CONFIRMED |
| C# null-conditional `?.` | Valid in .NET 4.8 / C# 6+. NT8 compiler supports this pattern. | VALID |
| NT8-001 (`init;` ban) | No new properties introduced | CLEAN |
| NT8-002 (`record` ban) | No new record types | CLEAN |
| NT8-003 (`volatile double` ban) | No volatile fields | CLEAN |
| NT8-004 (`ImmutableDictionary` ban) | No immutable collections | CLEAN |
| NT8-007 (`CreateOrder` arg 12) | No `CreateOrder` calls in changed lines | N/A |

---

### 7-SCAN CHECKLIST — Engineer runs ALL 7 to zero before BUILD_PASS

(Defense in depth Layer 1 — mandatory, non-skippable)

#### SCAN-01 — Old reference equality gone
```powershell
Select-String -Path "c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs" -Pattern "e\.Order\.Account ==" -CaseSensitive
```
**Expected: 0 results** — old `== rule.MasterAccount` pattern is gone

#### SCAN-02 — New name equality present
```powershell
Select-String -Path "c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs" -Pattern "\.Account\.Name ==" -CaseSensitive
```
**Expected: exactly 1 result** — the fixed Gate 2 condition at line ~381

#### SCAN-03 — No lock() introduced
```powershell
Select-String -Path "c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs" -Pattern "lock\s*\(" -CaseSensitive
```
**Expected: 0 results**

#### SCAN-04 — No async void introduced
```powershell
Select-String -Path "c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs" -Pattern "async void " -CaseSensitive
```
**Expected: 0 results**

#### SCAN-05 — Build clean
```powershell
dotnet build "c:\WSGTA\universal-or-strategy\src\PropTraderTools\PropTraderTools.csproj"
```
**Expected: 0 errors, 0 warnings**

#### SCAN-06 — New tests pass
```powershell
dotnet test "c:\WSGTA\universal-or-strategy\src\PropTraderTools\" --filter "Gate2"
```
**Expected: both Gate2 tests pass** (Gate2_UsesAccountName_SourceContractVerified + Gate2_NullMasterAccount_NoCopyOrder)

#### SCAN-07 — No regression
```powershell
dotnet test "c:\WSGTA\universal-or-strategy\src\PropTraderTools\"
```
**Expected: all 113 [Fact] tests pass** (111 prior + 2 new)

---

### Completion Criteria

- [ ] CopyEngine.cs line ~381 changed: `e.Order.Account == rule.MasterAccount` → `e.Order.Account.Name == rule.MasterAccount?.Name`
- [ ] `Gate2_UsesAccountName_SourceContractVerified` [Fact] added to CopyEngineTests.cs and passes
- [ ] `Gate2_NullMasterAccount_NoCopyOrder` [Fact] added to CopyEngineTests.cs and passes
- [ ] SCAN-01 through SCAN-07 all zero/pass
- [ ] Total [Fact] count: 113 (111 prior + 2 new)
- [ ] ticket-1-completion.md written with Layer 2 scan report

### Deferred Item — Do NOT Fix in This Ticket

| ID | File | Line | Description | Target |
|----|------|------|-------------|--------|
| DW-B19-02 | CopyEngine.cs | ~659 | `PopulateOrderMap` dedup guard uses Account reference equality; breaks after reconnect → duplicate FollowerBinding entries. Fix: `.Name == .Name` | B20+ |

Engineer must NOT touch line 659 in this ticket.

---

## Summary

| Field | Value |
|-------|-------|
| Block | PTT-COPIER-B19 |
| Ticket | T1 only (single-ticket block) |
| Spec ID | DW-B19-COPIER-BUG-01 |
| Priority | P0 |
| Files | CopyEngine.cs (1 line), CopyEngineTests.cs (2 tests) |
| Methods changed | `OnOrderUpdate` (Gate 2 condition, 1 line) |
| Tests added | 2 new `[Fact]` tests |
| Total tests after | 113 |
| 7-scan result | All 7 must pass |
| CYC | Unchanged (OnOrderUpdate CYC=7) |
| JS violations | 0 |
| NT8 rule violations | 0 |
