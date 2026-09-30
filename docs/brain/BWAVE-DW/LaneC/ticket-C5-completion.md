# Ticket C-5 Completion Report

**Epic**: BWAVE-DW LaneC
**Ticket**: C-5 -- B76Tests.cs IL-Scanning Fixes
**DW Items Closed**: DW-C39-11, DW-C39-12
**File Modified**: `src/PropTraderTools/B76Tests.cs` (ROOT level)
**Engineer**: ptt-engineer
**Date**: 2026-09-04

---

## Summary

Implemented two fixes to `B76Tests.cs`:

1. **DW-C39-11**: Removed fragile cross-assembly `MetadataToken` comparison in `T_B76_08`.
   Replaced with stable `module.ResolveMethod(token)` + name+declaring-type check.

2. **DW-C39-12**: Annotated all IL assertion tests (`T_B76_02`, `T_B76_03`, `T_B76_04`,
   `T_B76_05`, `T_B76_06`, `T_B76_11`) with `// IL assertion:` comments documenting the
   dependency, changed assertions to use lower-bound/existence checks (not exact offsets or
   exact counts), and extracted two private helpers (`CollectCallSiteOffsets`,
   `FindFirstCallSiteOffset`) to reduce CYC on `T_B76_04` and `T_B76_05`.

---

## DW-C39-11 Fix: T_B76_08 MetadataToken Replacement

### Before

```csharp
var interlockedExchangeMi = typeof(System.Threading.Interlocked).GetMethod(
    "Exchange",
    new Type[] { typeof(int).MakeByRefType(), typeof(int) }
);
Assert.NotNull(interlockedExchangeMi);

int exchangeToken = interlockedExchangeMi.MetadataToken;

// ... IL loop ...
if (token == exchangeToken)   // BROKEN: MemberRef != MethodDef across assemblies
{
    foundExchange = true;
    break;
}
```

### After

```csharp
var module = typeof(CopyEngine).Module;
bool foundExchange = false;
for (int i = 0; i < il.Length - 4; i++)
{
    if (il[i] == 0x28 || il[i] == 0x6F) // call or callvirt
    {
        int token = il[i + 1] | (il[i + 2] << 8) | (il[i + 3] << 16) | (il[i + 4] << 24);
        try
        {
            // DW-C39-11: resolve by name + declaring type (stable across assembly boundaries).
            // Raw token comparison fails for cross-assembly MemberRef tokens.
            var mb = module.ResolveMethod(token) as MethodBase;
            if (
                mb != null
                && mb.Name == "Exchange"
                && mb.DeclaringType == typeof(System.Threading.Interlocked)
            )
            {
                foundExchange = true;
                break;
            }
        }
        catch
        { /* token resolves to a non-method or is not resolvable in this context -- skip */
        }
    }
}
```

**Rationale**: `Interlocked.Exchange` lives in `mscorlib`/`System.Runtime`. When CopyEngine
(a different assembly) calls it, the IL emits a `MemberRef` token. The `MetadataToken` property
on the resolved `MethodInfo` returns the `MethodDef` from the *declaring* assembly. These two
token values differ: `MemberRef` vs `MethodDef` are in different metadata tables (0x0A vs 0x06
high byte). Using `module.ResolveMethod(token)` resolves the `MemberRef` to a `MethodBase`, and
comparing `Name` + `DeclaringType` is stable regardless of assembly layout.

---

## DW-C39-12 Fix: Fragile IL Assertions

### Decision per test

| Test | Fix Applied | Rationale |
|------|-------------|-----------|
| T_B76_02 | Option B: annotated | String literal scan via `module.ResolveString` is same-module (stable). Scan checks existence anywhere in body (not fixed offset). Added `// IL assertion:` comment. |
| T_B76_03 | Option B: annotated | Same as T_B76_02 for "flat-race skip" string. |
| T_B76_04 | Option B: refactored + helper | Replaced inline IL loop with `CollectCallSiteOffsets` helper. Count >= 2 (not exact). |
| T_B76_05 | Option B: refactored + helper | Replaced inline IL loop with `CollectCallSiteOffsets` + `FindFirstCallSiteOffset` helpers. Offset ordering preserved (verifies sequencing invariant). |
| T_B76_06 | Option B: annotated | Local count >= 5 (lower bound, not exact). Added comment documenting compiler may allocate more locals. |
| T_B76_11 | Option B: annotated | String literal scan via `module.ResolveString` is same-module (stable). Added `// IL assertion:` comment. Checks existence anywhere in body. |

### Private helpers added (CYC <= 5 each)

**`CollectCallSiteOffsets(byte[] il, Module module, string methodName)`**
- Returns all call/callvirt offsets where the resolved method name matches `methodName` on `CopyEngine`.
- CYC = 5 (base + for + if-opcode + try/catch + if-name-match).
- Used by T_B76_04 and T_B76_05.

**`FindFirstCallSiteOffset(byte[] il, Module module, string methodName)`**
- Returns the first offset matching `methodName` on `CopyEngine`, or -1.
- CYC = 5 (base + for + if-opcode + try/catch + if-name-match).
- Used by T_B76_05.

No new `[Fact]` methods added. All modifications are in-place on existing test methods.

---

## 7-Scan Results

| Scan | Command | Result |
|------|---------|--------|
| SCAN-01 | `Select-String -Path "src/PropTraderTools/B76Tests.cs" -Pattern "lock\("` | **0 results** |
| SCAN-02 | `Select-String -Path "src/PropTraderTools/B76Tests.cs" -Pattern "async void"` | **0 results in code** (comment-only: header banner) |
| SCAN-03 | `Select-String -Path "src/PropTraderTools/B76Tests.cs" -Pattern "return null"` | **0 results in code** (comment-only: header banner) |
| SCAN-04 | `Select-String -Path "src/PropTraderTools/B76Tests.cs" -Pattern "throw new"` | **0 results** |
| SCAN-05 | CYC estimation for all modified/new methods | **PASS -- all <= 8** (T_B76_04=3, T_B76_05=4, T_B76_08=8, helpers=5 each) |
| SCAN-06 | PowerShell byte scan: `([System.IO.File]::ReadAllBytes(...) \| Where-Object { $_ -gt 127 }).Count` | **0 non-ASCII bytes** |
| SCAN-07 | `Select-String -Path "src/PropTraderTools/B76Tests.cs" -Pattern "using NUnit\|using Microsoft\.VisualStudio"` | **0 results** |

All 7 scans: **ZERO violations**.

### Additional acceptance-criterion check

`Select-String -Path "src/PropTraderTools/B76Tests.cs" -Pattern "MetadataToken"` -- **0 results** ✅

---

## Build Result

```
dotnet build src/PropTraderTools/PropTraderTools.csproj

Build succeeded.
    1 Warning(s)   <-- pre-existing xUnit2004 in B131Tests.cs (out of scope)
    0 Error(s)
```

**BUILD_PASS**

---

## DW Items Closed

| DW Item | Status | Resolution |
|---------|--------|------------|
| DW-C39-11 | CLOSED | T_B76_08: MetadataToken replaced with `module.ResolveMethod` + name+declaring-type check. Zero `MetadataToken` comparisons remain in file. |
| DW-C39-12 | CLOSED | T_B76_02/03/04/05/06/11: All fragile IL assertions annotated with `// IL assertion:` comments, refactored to use lower-bound/existence checks. Two private helpers extracted to keep CYC <= 8. |

---

*ptt-engineer | BWAVE-DW LaneC | Ticket C-5 | BUILD_PASS*
