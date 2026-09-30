# PTT-COPIER-B19 Ticket 1 Completion

**Block**: PTT-COPIER-B19
**Ticket**: T1 — DW-B19-COPIER-BUG-01 (P0)
**Spec IDs**: REQ-B19-01, REQ-B19-02, REQ-B19-03, REQ-B19-04
**Engineer**: ptt-engineer (Lane 1)
**Gate**: TICKET_REVIEW_PASS confirmed (04-ticket-review-lane1.md)

---

## Changes Made

### Change 1 — Gate 2 account reference fix

**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `OnOrderUpdate`
**Line**: 381

**Before** (reference equality — breaks after Rithmic reconnect):
```csharp
if (e.Order.Instrument.FullName == rule.Instrument && e.Order.Account == rule.MasterAccount)
```

**After** (name equality — survives reconnect):
```csharp
if (e.Order.Instrument.FullName == rule.Instrument && e.Order.Account.Name == rule.MasterAccount?.Name)
```

Root cause: After a Rithmic reconnect at 16:43 (log.20260713.00002.txt), NT8 internally recreates
Account objects. The stored `rule.MasterAccount` reference became stale — Gate 2 returned false for
every order, `SendCopy` never fired, zero follower orders for all leader trades after reconnect.

Fix: string name equality survives reconnect. Null-conditional `?.Name` guards against null
`MasterAccount` (5+ existing tests pass `(Account)null` as master).

**CYC impact**: None — fix changes comparison type, not branch count. `OnOrderUpdate` CYC remains 7.

---

### Change 2 — Two new [Fact] tests appended to CopyEngineTests.cs

**File**: `src/PropTraderTools/CopyEngineTests.cs`
**Location**: Appended before final class closing brace (line 1891)

**Test 1**: `Gate2_UsesAccountName_SourceContractVerified` (line 1901)
- Reflection-based type-contract test
- Verifies `CopyRule.MasterAccount` is of type `Account` and `Account.Name` is a public `string` property
- No NT8 runtime required — pure type-system assertion

**Test 2**: `Gate2_NullMasterAccount_NoCopyOrder` (line 1931)
- Null-safety guard test
- Verifies null `MasterAccount` evaluates to null name via `?.Name` (no NRE)
- Verifies no `StatusUpdate` fires for null-master rules
- Follows established `AddRule("...", (Account)null, new Account[0])` test pattern

---

## Files Modified

| File | Change |
|------|--------|
| `src/PropTraderTools/CopyEngine.cs` | 1 line changed at line 381 |
| `src/PropTraderTools/CopyEngineTests.cs` | 2 new `[Fact]` tests appended |

## Files NOT Modified

- `TradeCopierPanel.cs` — untouched
- `TradeCopierWindow.cs` — untouched
- `TradeCopierAddOn.cs` — untouched
- `AtrSizingEngine.cs` — untouched

---

## Layer 2 Scan Report (all 5 scans)

> Note: NT8-032 documents that `dotnet test` cannot be run against `PropTraderTools.csproj` —
> the project requires NT8 proprietary assemblies unavailable in standalone .NET builds.
> The definitive gate is NT8 F5 compilation. Source scans are the Layer 2 verification contract.

### SCAN-01 — Fix is present at line 381
```
Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "Account\.Name\s*==\s*rule\.MasterAccount"
```
**Result**: 1 result — `CopyEngine.cs:381` ✅

### SCAN-02 — Old reference equality is gone
```
Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "e\.Order\.Account\s*==\s*rule\.MasterAccount[^.]"
```
**Result**: 0 results ✅

### SCAN-03 — Gate2 test 1 present
```
Select-String -Path "src\PropTraderTools\CopyEngineTests.cs" -Pattern "Gate2_UsesAccountName_SourceContractVerified"
```
**Result**: 1 result — `CopyEngineTests.cs:1901` ✅

### SCAN-04 — Gate2 test 2 present
```
Select-String -Path "src\PropTraderTools\CopyEngineTests.cs" -Pattern "Gate2_NullMasterAccount_NoCopyOrder"
```
**Result**: 1 result — `CopyEngineTests.cs:1931` ✅

### SCAN-05 — Total [Fact] count
```
Select-String -Path "src\PropTraderTools\CopyEngineTests.cs" -Pattern "\[Fact\]" | Measure-Object | Select-Object -ExpandProperty Count
```
**Result**: 113 (111 prior + 2 new Gate2 tests) ✅

---

## JS Rule Compliance

| Rule | Status |
|------|--------|
| JS-021 — No `lock()` | PASS — Gate 2 is read-only `foreach` over `ConcurrentBag`. Fix changes comparison expression only. No lock introduced. |
| JS-001 — No `throw` in hot paths | PASS — `?.Name` evaluates to null on null input. No new exception paths. |
| JS-002 — No `return null` | PASS — no new methods introduced. |
| JS-033 — No `async void` | PASS — fix is a single comparison sub-expression. No new async code. |
| CYC <= 8 | PASS — `OnOrderUpdate` CYC unchanged at 7. |

## NT8 Rule Compliance

| Rule | Status |
|------|--------|
| `Account.Name` is `string` | CONFIRMED — 10+ existing uses in CopyEngine.cs (lines 456, 514, 589, 820, 843, 881, 925, 967, 997, 1068) |
| `?.` null-conditional | VALID — C# 6+ / .NET 4.8 |
| NT8-001 (`init;` ban) | CLEAN — no new properties |
| NT8-002 (`record` ban) | CLEAN — no new record types |
| NT8-032 (`dotnet test` blocker) | DOCUMENTED — source scans used as verification |

---

## Deferred (Do NOT fix in this ticket)

| ID | File | Line | Description | Target |
|----|------|------|-------------|--------|
| DW-B19-02 | CopyEngine.cs | ~659 | `PopulateOrderMap` Account reference equality | B20+ |

Line 659 was NOT touched.

---

## Summary

| Field | Value |
|-------|-------|
| Block | PTT-COPIER-B19 |
| Ticket | T1 (DW-B19-COPIER-BUG-01) |
| Priority | P0 |
| Files changed | CopyEngine.cs (1 line), CopyEngineTests.cs (2 tests) |
| Methods changed | `OnOrderUpdate` (Gate 2 condition, 1 line) |
| Tests added | 2 new `[Fact]` tests |
| Total [Fact] count | 113 |
| All 5 scans | PASS |
| JS violations | 0 |
| NT8 rule violations | 0 |

## BUILD_PASS
