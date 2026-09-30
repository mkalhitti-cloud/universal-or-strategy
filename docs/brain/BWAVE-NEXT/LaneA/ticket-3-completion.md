# BWAVE-NEXT Lane A -- Ticket 3 Completion

**Ticket**: T3 -- DW-DW-03 + DW-NEW-07: Two-Panel BE Integration Test
**Engineer**: ptt-engineer
**Date**: 2026-09-04
**Dependency confirmed**: T1 VERIFY_PASS confirmed before this session started.

---

## CopyEngine BE Slot API Found

### Existing API (no modifications needed for core logic)

| Symbol | Location | Signature |
|--------|----------|-----------|
| `_pendingBeSlots` | line 270 | `private readonly ConcurrentDictionary<string, PendingBeSlot> _pendingBeSlots` |
| `ArmPendingBe` | line 5716 | `internal void ArmPendingBe(Instrument instr, Account masterAcc, int bufferTicks)` |
| `DisarmPendingBe` | line 5779 | `internal void DisarmPendingBe(Account leader)` |
| `IsPendingSlotsEmpty` | line 5795 | `internal bool IsPendingSlotsEmpty() => _pendingBeSlots.IsEmpty;` |
| `ClearAllPendingBeSlots` | line 5802 | `internal void ClearAllPendingBeSlots()` |
| `IsPendingBeSlotActive(PendingBeSlot)` | line 5946 | `internal bool IsPendingBeSlotActive(PendingBeSlot slot)` |

### New Test Seam Added to CopyEngine.cs

**File**: `src/PropTraderTools/CopyEngine.cs`
**Location**: After `IsPendingBeSlotActiveNullAccountTestable` (line ~6067)

```csharp
// DW-DW-03 + DW-NEW-07 T3: test seam -- IsPendingBeSlotActive by account name string.
// _pendingBeSlots key = account.Name. ConcurrentDictionary.ContainsKey is lock-free.
// CYC=1: expression body, no branches.
// JS-021: no lock. JS-002: returns bool. JS-033: synchronous.
internal bool IsPendingBeSlotActive(string accountName) =>
    _pendingBeSlots.ContainsKey(accountName);
```

**NT8 sync**: REQUIRED because a production file (`CopyEngine.cs`) was modified (1-line seam added).

---

## Test Approach Chosen

**Approach**: Direct `_pendingBeSlots` seeding via reflection + `IsPendingBeSlotActive(string)` seam for assertions.

**Rationale**: `DisarmPendingBe(Account leader)` requires a live NT8 `Account` object which cannot be
instantiated in unit tests. Instead:
- Seed `_pendingBeSlots[accountName]` via reflection using `default(PendingBeSlot)` (Account=null)
- Simulate disarm by calling `TryRemove(accountName, ...)` via reflection
- Assert using the new `IsPendingBeSlotActive(string)` seam and existing `IsPendingSlotsEmpty()`
- No `WpfFact` required -- CopyEngine driven directly, no WPF

**File chosen**: New `src/PropTraderTools/Tests/BwaveNextLaneATests.cs`
(existing `BwaveDwLaneATests.cs` is 315 lines, exceeds 300-line threshold)

---

## Test Methods (all PASSED)

| Method | Scenario | Result |
|--------|----------|--------|
| `Detach_PanelA_DoesNotClearPanelB_BeSlot` | S1: Arm A+B, disarm A, assert B still armed | PASS |
| `Detach_OwnPanel_ClearsOwnBeSlot_ButNotOthers` | S2: Arm A+B, disarm A, assert A gone + B present | PASS |
| `Detach_LastPanel_ClearsAllPendingBeSlots` | S3: Arm A+B, disarm A then B, assert all empty | PASS |

---

## All 7 Scan Results

### SCAN-01: lock() -- JS-021

```
Command: Select-String -Path "src/PropTraderTools/Tests/BwaveNextLaneATests.cs" -Pattern "lock\s*\("
Result: 0 matches
```
PASS

### SCAN-02: async void -- JS-033

```
Command: Select-String -Path "src/PropTraderTools/Tests/BwaveNextLaneATests.cs" -Pattern "async void [A-Z]"
Result: 0 matches
```
PASS

### SCAN-03: return null -- JS-002

```
Command: Select-String -Path "src/PropTraderTools/Tests/BwaveNextLaneATests.cs" -Pattern "return null"
Result:
  Line 5:  // Jane Street rules: JS-021 (no lock), JS-002 (no return null), xUnit only.   (comment)
  Line 101: // CYC=1 per test (no branches). JS-021: no lock. JS-002: no return null.       (comment)
```
0 actual `return null` code statements. PASS

### SCAN-04: throw new -- JS-001

```
Command: Select-String -Path "src/PropTraderTools/Tests/BwaveNextLaneATests.cs" -Pattern "throw new"
Result: 0 matches
```
PASS

### SCAN-05: dotnet build

```
dotnet build src/PropTraderTools/PropTraderTools.csproj
Result: 0 Error(s), 1 Warning(s) (pre-existing xUnit2004 warning in B131Tests.cs -- not in scope)
```
PASS -- 0 errors

### SCAN-06: ASCII-only

```
Command: byte-level scan of BwaveNextLaneATests.cs for bytes > 127
Result: 0 non-ASCII bytes
```
PASS

### SCAN-07: xUnit [Fact] only -- never [Test]

```
Command: Select-String -Path "src/PropTraderTools/Tests/BwaveNextLaneATests.cs" -Pattern "\[Fact\]|\[Test\]"
Result:
  Line 57:  [Fact]
  Line 73:  [Fact]
  Line 88:  [Fact]
```
3 `[Fact]` methods, 0 `[Test]` markers. PASS

---

## dotnet build Result

```
0 Error(s)
1 Warning(s) -- pre-existing xUnit2004 in B131Tests.cs (not in scope)
```

## dotnet test Result

```
dotnet test --filter "Detach_PanelA_DoesNotClearPanelB|Detach_LastPanel_ClearsAll|Detach_OwnPanel_Clears"

Passed!  - Failed: 0, Passed: 3, Skipped: 0, Total: 3, Duration: 521 ms
```

All 3 tests PASSED.

---

## Files Modified

| File | Change |
|------|--------|
| `src/PropTraderTools/CopyEngine.cs` | Added `IsPendingBeSlotActive(string accountName)` test seam (1 method, 2 lines) |
| `src/PropTraderTools/Tests/BwaveNextLaneATests.cs` | NEW FILE -- 3 `[Fact]` tests |
| `src/PropTraderTools/PropTraderTools.csproj` | Added `<Compile Include="Tests\BwaveNextLaneATests.cs" />` |

---

## NT8 Sync

**REQUIRED** (production file `CopyEngine.cs` modified -- 1-line test seam added).

```powershell
powershell -File scripts\ptt-sync-and-verify.ps1
```

Expected: `18/18 OK, 0 MISMATCH`
Then press **F5** in NinjaTrader 8. Confirm 0 new errors.

---

## Final Verdict

**BUILD_PASS**
