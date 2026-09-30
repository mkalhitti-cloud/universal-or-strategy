# B50-LaneC Tickets
## Fix: DW-B48-01 — Make CopyEngineTests.cs Compile and `dotnet test` Pass

**Epic**: B50-LaneC  
**Spec Req**: DW-B48-01  
**Plan status**: REVIEW_PASS (02-architecture-plan.md)  
**Engineer contract**: This file is the ONLY contract. Implement exactly what is written here.

---

## Ticket T1 — Fix CopyEngineTests.cs Compilation Errors

### Spec Requirements Satisfied
- DW-B48-01: `CopyEngineTests.cs` must compile without errors and `dotnet test` must pass.

### Files

| File | Wave workspace path |
|------|-------------------|
| `CopyEngine.cs` | `C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs` |
| `CopyEngineTests.cs` | `C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs` |

---

### Change Group A — `CopyEngine.cs`: Widen `CopyRule` access modifier

**File**: `CopyEngine.cs`  
**Line**: 173  
**Error fixed**: CS0246 — `CopyRule` not found (accessed from test code in same assembly)

**Before**:
```csharp
private readonly struct CopyRule
```

**After**:
```csharp
internal readonly struct CopyRule
```

**Rules**:
- JS-010: Use `internal` (minimum necessary visibility within the assembly). Never `public`.
- No other lines in `CopyEngine.cs` are touched.

---

### Change Group B — `CopyEngineTests.cs`: Replace all `ImmutableDictionary` usages

**File**: `CopyEngineTests.cs`  
**Error fixed**: CS0234 — `System.Collections.Immutable` not found (NT8-004 banned type)

**Step B1** — Locate every occurrence of `ImmutableDictionary` in the file:
```powershell
Select-String -Path CopyEngineTests.cs -Pattern "ImmutableDictionary"
```
Expected: 9 matches (lines approximately 482, 511, 541, 640–641, 684, 712–713, 827, 865).

**Sub-pattern A** (7 sites — direct empty-map argument):

Replace every standalone:
```csharp
System.Collections.Immutable.ImmutableDictionary<string, FollowerAtmMode>.Empty
```
with:
```csharp
new Dictionary<string, FollowerAtmMode>()
```

**Sub-pattern B** (2 sites — single-entry builder chain):

Site 1 (approximately lines 640–641):
```csharp
// BEFORE
var atmMap = System.Collections.Immutable.ImmutableDictionary<string, FollowerAtmMode>.Empty
    .SetItem("FollowerA", new FollowerAtmMode.Named("ScalpTemplate"));
// AFTER
var atmMap = new Dictionary<string, FollowerAtmMode> { { "FollowerA", new FollowerAtmMode.Named("ScalpTemplate") } };
```

Site 2 (approximately lines 712–713):
```csharp
// BEFORE
var atmMap = System.Collections.Immutable.ImmutableDictionary<string, FollowerAtmMode>.Empty
    .SetItem("FollowerB", new FollowerAtmMode.Market());
// AFTER
var atmMap = new Dictionary<string, FollowerAtmMode> { { "FollowerB", new FollowerAtmMode.Market() } };
```

**Rules**:
- NT8-004: `ImmutableDictionary` / `System.Collections.Immutable` is BANNED in NT8 (.NET FW 4.8).
- `using System.Collections.Generic;` is already present in the file (required for `ConcurrentBag`). Do NOT add a duplicate using.
- `CopyEngine.AddRule` already accepts `Dictionary<string, FollowerAtmMode>` — this is a test-side fix only; the production API does not change.
- Do NOT add `using System.Collections.Immutable;` — that would make NT8-004 worse.

**Verification after B**:
```powershell
Select-String -Path CopyEngineTests.cs -Pattern "ImmutableDictionary"
# Expected: 0 matches
```

---

### Change Group C — `CopyEngineTests.cs`: Remove two dead test methods

**File**: `CopyEngineTests.cs`  
**Lines**: approximately 1747–1765  
**Error fixed**: CS0246 — `DisarmTrailBe` not found  
**Reason**: `DisarmTrailBe` was deleted from `CopyEngine.cs` in B33 T8 (confirmed dead since B32 / DW-B32-05, comment at `CopyEngine.cs:2152`). Tests that call a deleted method are dead tests.

**Locate the methods**:
```powershell
Select-String -Path CopyEngineTests.cs -Pattern "DisarmTrailBe" -Context 2,2
```

**Remove entirely** (delete both `[Fact]` method bodies including the `[Fact]` attribute lines):

Method 1:
```csharp
[Fact]
public void DisarmTrailBe_WhenNotArmed_NoException()
{
    // ... (~6 lines)
}
```

Method 2:
```csharp
[Fact]
public void DisarmTrailBe_Idempotent_NoExceptionOnDoubleCall()
{
    // ... (~10 lines)
}
```

**Rules**:
- Do NOT add a replacement test. The production method is gone; the tests have no value.
- Do NOT modify any other `[Fact]` methods.
- Do NOT remove any surrounding whitespace or regions beyond the two method bodies.

**Verification after C**:
```powershell
Select-String -Path CopyEngineTests.cs -Pattern "DisarmTrailBe"
# Expected: 0 matches
```

---

### Method Signatures to Implement

No new methods are written in this ticket. The changes are:
1. One access modifier change on an existing type declaration.
2. Find-and-replace of banned type references in test argument expressions.
3. Deletion of two dead test methods.

---

### xUnit Tests

All existing `[Fact]` methods in `CopyEngineTests.cs` (after the two dead methods are removed)
constitute the test suite. No new `[Fact]` methods are written.

The engineer MUST confirm that `dotnet test` reports **all tests pass** after the changes.
Run:
```powershell
dotnet test C:\WSGTA\universal-or-strategy\src\PropTraderTools\PropTraderTools.csproj
```
Expected: `Passed! - Failed: 0, Errors: 0`

---

### 7-Scan Checklist (SCAN-01 through SCAN-07)

The engineer MUST execute each scan in order and confirm the expected result before marking T1 complete.

**SCAN-01 — CS0246 CopyRule**
```powershell
dotnet build C:\WSGTA\universal-or-strategy\src\PropTraderTools\PropTraderTools.csproj 2>&1 | Select-String "CS0246.*CopyRule"
```
Expected: **zero matches**

**SCAN-02 — CS0234 ImmutableDictionary**
```powershell
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs" -Pattern "ImmutableDictionary"
```
Expected: **zero matches**

**SCAN-03 — CS0433 Globals**
```powershell
dotnet build C:\WSGTA\universal-or-strategy\src\PropTraderTools\PropTraderTools.csproj 2>&1 | Select-String "CS0433"
```
Expected: **zero matches** (already fully qualified in production code; this lane does not touch Globals)

**SCAN-04 — CS0246 DisarmTrailBe**
```powershell
dotnet build C:\WSGTA\universal-or-strategy\src\PropTraderTools\PropTraderTools.csproj 2>&1 | Select-String "DisarmTrailBe"
```
Expected: **zero matches**

**SCAN-05 — Full build gate**
```powershell
dotnet build C:\WSGTA\universal-or-strategy\src\PropTraderTools\PropTraderTools.csproj
```
Expected: **0 Error(s)**

**SCAN-06 — Test runner green**
```powershell
dotnet test C:\WSGTA\universal-or-strategy\src\PropTraderTools\PropTraderTools.csproj
```
Expected: **Failed: 0, Errors: 0** (all existing tests pass)

**SCAN-07 — Hard-link integrity**
```powershell
powershell -File scripts\verify_links.ps1
```
Expected: **DESYNC=0 MISSING=0**

---

### Out-of-Scope

- **CS0433 `Globals`**: Already fully qualified at `CopyEngine.cs:2319`. Not touched.
- **Any other source files** not listed in the Files table above.
- **Test logic changes**: No assertions are modified. Only dead tests are removed and banned-type
  arguments are corrected to the already-accepted `Dictionary<K,V>`.
- **Production API changes**: `CopyEngine.AddRule` signature is unchanged.
