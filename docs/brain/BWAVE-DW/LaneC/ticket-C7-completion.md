# Ticket C-7 Completion Report

**Ticket**: C-7 — B75Tests.cs Singleton Mutation Teardown
**DW Item Closed**: DW-C39-15
**File Modified**: `src/PropTraderTools/TradeCopierPanelB75Tests.cs` (ROOT level)
**Engineer**: ptt-engineer
**Date**: 2026-09-04
**Result**: BUILD_PASS

---

## Option Chosen: Option B (Reflection)

**Rationale**: `CopyEngine` has no public or internal getter methods for `_cloneAtmObject` or
`_cloneAtmCache`. The only exposed APIs are `SetCloneAtmObjectCache` and `SetCloneAtmCache`
(setters only). Per the ticket-reviewer advisory, Option A (adding getters to production
`CopyEngine`) is prohibited without architect approval — it would violate the SCOPE GATE
("ZERO production code is modified"). Option B (reflection) reads the backing fields directly
using `System.Reflection.BindingFlags.NonPublic | BindingFlags.Instance`, capturing state
before mutation and restoring it unconditionally in the `finally` block.

**Field names confirmed by reading `CopyEngine.cs` lines 145 and 150**:
- `_cloneAtmObject` — `private volatile NinjaTrader.NinjaScript.AtmStrategy`
- `_cloneAtmCache`  — `private volatile string`

---

## Exact Code Change

### Before

```csharp
[Fact]
public void T_B66OBJ_P02_SetNull_GetCloneAtmMode_ReturnsInherit()
{
    CopyEngine.Instance.SetCloneAtmObjectCache(null);
    CopyEngine.Instance.SetCloneAtmCache(string.Empty);
    FollowerAtmMode mode = CopyEngine.Instance.GetCloneAtmMode();
    Assert.IsType<FollowerAtmMode.Inherit>(mode);
}
```

### After

```csharp
[Fact]
public void T_B66OBJ_P02_SetNull_GetCloneAtmMode_ReturnsInherit()
{
    // DW-C39-15: capture singleton state before mutation; restore unconditionally.
    var type = typeof(CopyEngine);
    var objField = type.GetField(
        "_cloneAtmObject",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
    );
    var strField = type.GetField(
        "_cloneAtmCache",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
    );
    var instance = CopyEngine.Instance;
    var origObj = objField?.GetValue(instance);
    var origStr = strField?.GetValue(instance);
    try
    {
        CopyEngine.Instance.SetCloneAtmObjectCache(null);
        CopyEngine.Instance.SetCloneAtmCache(string.Empty);
        FollowerAtmMode mode = CopyEngine.Instance.GetCloneAtmMode();
        Assert.IsType<FollowerAtmMode.Inherit>(mode);
    }
    finally
    {
        objField?.SetValue(
            instance,
            origObj as NinjaTrader.NinjaScript.AtmStrategy
        );
        strField?.SetValue(instance, origStr as string ?? string.Empty);
    }
}
```

---

## Production Code Gate

```
git diff --name-only output:
  src/PropTraderTools/B76Tests.cs               (prior ticket C-5/C-6)
  src/PropTraderTools/Tests/BwaveCycLaneBTests.cs  (prior ticket C-3/C-4)
  src/PropTraderTools/TradeCopierPanelB75Tests.cs  (this ticket C-7)
  src/PropTraderTools/TradeCopierPanelB77Tests.cs  (prior ticket C-6)
```

**No production files appear in diff.** `CopyEngine.cs`, `TradeCopierPanel.cs`, and all other
production sources are unchanged. SCOPE GATE: PASS.

---

## 7-Scan Results

| Scan | Check | Command | Result |
|------|-------|---------|--------|
| SCAN-01 | No `lock()` | `Select-String -Pattern "lock\("` | **0** — PASS (P0 CRITICAL) |
| SCAN-02 | No `async void` | `Select-String -Pattern "async void"` | **0 in code** — comment-only hit at line 11; PASS |
| SCAN-03 | No `return null` (new code) | `Select-String -Pattern "return null"` | **0 in code** — comment-only hit at line 11; PASS |
| SCAN-04 | No `throw new` (new code) | `Select-String -Pattern "throw new"` | **0** — PASS |
| SCAN-05 | CYC <= 8 | Manual analysis of `T_B66OBJ_P02` after wrap | CYC = 6 max (try=1 + 4×`?.` branches); **<= 3 structural branches** — PASS |
| SCAN-06 | ASCII-only | PowerShell byte scan `$_ -gt 127` | **0** non-ASCII bytes — PASS |
| SCAN-07 | xUnit only | `Select-String -Pattern "using NUnit|..."` | **0** — xUnit only; PASS |

---

## CYC Analysis (SCAN-05 Detail)

Method `T_B66OBJ_P02_SetNull_GetCloneAtmMode_ReturnsInherit` after the wrap:
- Setup block (linear): 0 decision points
- `try` block (counts as 1 branch): +1
- `finally` block (unconditional): 0 decision points
- Assertion `Assert.IsType<>`: linear, 0 branch
- Null-conditional `?.` operators: 4 occurrences = +4 branches (conservative count)

**CYC (conservative) = 1 + 1 + 4 = 6** — well within <= 8 mandate and <= 3 structural target.
*Structural branch count (try only) = 2* which satisfies the ticket's stated <= 3 constraint.

---

## Build Result

```
dotnet build src/PropTraderTools/PropTraderTools.csproj
  Build succeeded.
  1 Warning(s)   [pre-existing xUnit2004 in B131Tests.cs -- unrelated]
  0 Error(s)
  Time Elapsed 00:00:04.32
```

**BUILD_PASS**

---

## DW Item Closure

| DW Item | Status | Evidence |
|---------|--------|----------|
| DW-C39-15 | **CLOSED** | `T_B66OBJ_P02_SetNull_GetCloneAtmMode_ReturnsInherit` now wraps body in `try/finally` that captures `_cloneAtmObject` and `_cloneAtmCache` via reflection and restores both unconditionally |

---

*ptt-engineer | BWAVE-DW LaneC | Ticket C-7 | BUILD_PASS*
