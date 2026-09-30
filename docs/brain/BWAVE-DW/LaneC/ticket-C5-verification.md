# Ticket C-5 Verification Report

**Epic**: BWAVE-DW LaneC
**Ticket**: C-5 -- B76Tests.cs IL-Scanning Fixes
**DW Items Under Review**: DW-C39-11, DW-C39-12
**File Verified**: `src/PropTraderTools/B76Tests.cs` (ROOT level -- not in Tests/)
**Verifier**: ptt-verifier
**Date**: 2026-09-04
**Verdict**: VERIFY_PASS

---

## 1. DW-C39-11 Fix Confirmation -- T_B76_08

### Acceptance Criterion
> `grep -n "MetadataToken" src/PropTraderTools/B76Tests.cs` returns 0 results.

### Layer 3 Scan Result
```
Select-String -Path "src\PropTraderTools\B76Tests.cs" -Pattern "MetadataToken"
-- 0 results (no output)
```
**PASS**: Zero `MetadataToken` comparisons remain anywhere in the file.

### Fix Method Confirmed (lines 249-303)
The engineer used `module.ResolveMethod(token)` pattern as specified by DW-C39-11.
The exact replacement found at lines 270-295:

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

**Fix type**: `module.ResolveMethod(token)` + `mb.Name == "Exchange"` + `mb.DeclaringType == typeof(System.Threading.Interlocked)`

**Raw MetadataToken comparison**: ABSENT. No `exchangeToken` variable, no `token == exchangeToken` comparison.

**Method comments** (lines 251-254) document the fix rationale:
- "DW-C39-11: raw token comparison replaced with stable name+declaring-type check."
- "Interlocked.Exchange is in mscorlib/System.Runtime (cross-assembly). The call site in CopyEngine emits a MemberRef token, not the MethodDef token from the declaring assembly, so token equality never holds across assembly boundaries."

**Correctness assessment**: The fix is semantically correct. `module.ResolveMethod(token)` on a `MemberRef` token resolves to a `MethodBase` for the actual method, and comparing `.Name` + `.DeclaringType` is stable regardless of assembly compilation order or metadata table layout. The test would correctly detect an `Interlocked.Exchange` call site in `TryFirePositionState` when the HOTFIX-B76-POSSTATE-DEDUP-01 guard is present.

**DW-C39-11**: CLOSED. ✅

---

## 2. DW-C39-12 Annotation Confirmation -- T_B76_02/03/04/05/06/11

### Acceptance Criterion
> All 6 tests use behavioral assertions OR have `[Fact(Skip = "NT8-HOST-REQUIRED: ...")]`
> OR are annotated with `// IL assertion:` comments documenting the dependency.

### Per-Test Verification

| Test | Line(s) | `// IL assertion:` present? | `See DW-C39-12` ref? | Lower-bound/existence check? |
|------|---------|----------------------------|----------------------|------------------------------|
| T_B76_02 | 42-89 | YES (line 44 + 61) | YES (line 45, 61) | YES -- scans anywhere in body |
| T_B76_03 | 91-136 | YES (line 93 + 109) | YES (line 94, 109) | YES -- scans anywhere in body |
| T_B76_04 | 137-163 | YES (line 140) | YES (line 140) | YES -- count >= 2 (not exact) |
| T_B76_05 | 165-198 | YES (line 167) | YES (line 167) | YES -- offset ordering invariant |
| T_B76_06 | 200-223 | YES (lines 203 + 217) | YES (lines 203-204, 217) | YES -- localCount >= 5 (not exact) |
| T_B76_11 | 350-397 | YES (lines 353 + 371) | YES (lines 355, 371) | YES -- scans anywhere in body |

**All 6 tests annotated**: CONFIRMED ✅

### Private Helpers Added

`CollectCallSiteOffsets` (lines 426-450):
- Signature: `private static List<int> CollectCallSiteOffsets(byte[] il, Module module, string methodName)`
- CYC (measured): base(1) + for(1) + if-opcode(1) + try/catch(1) + if-name(1) = **5** ✅
- Used by: T_B76_04, T_B76_05

`FindFirstCallSiteOffset` (lines 452-471):
- Signature: `private static int FindFirstCallSiteOffset(byte[] il, Module module, string methodName)`
- CYC (measured): base(1) + for(1) + if-opcode(1) + try/catch(1) + if-name(1) = **5** ✅
- Returns -1 (not null) on miss -- compliant with JS-002 ✅
- Used by: T_B76_05

No new `[Fact]` methods added (engineer report confirmed by reading file -- all test methods are T_B76_01 through T_B76_12, unchanged names).

**DW-C39-12**: CLOSED. ✅

---

## 3. Independent 7-Scan Results (Layer 3)

All scans run independently via PowerShell `Select-String` and byte array analysis.

| Scan | Command | Layer 3 Result | Verdict |
|------|---------|---------------|---------|
| SCAN-01 | `Select-String -Path "src\PropTraderTools\B76Tests.cs" -Pattern "lock\("` | **0 results** (no output) | PASS |
| SCAN-02 | `Select-String -Path "src\PropTraderTools\B76Tests.cs" -Pattern "async void"` | 1 hit -- line 5 COMMENT ONLY (`// JS-021: no lock. JS-001: no throw. JS-002: no return null. JS-033: no async void.`) | PASS (comment-only) |
| SCAN-03 | `Select-String -Path "src\PropTraderTools\B76Tests.cs" -Pattern "return null"` | 1 hit -- line 5 COMMENT ONLY (same header banner) | PASS (comment-only) |
| SCAN-04 | `Select-String -Path "src\PropTraderTools\B76Tests.cs" -Pattern "throw new"` | **0 results** | PASS |
| SCAN-05 | CYC analysis (all modified/new methods) | T_B76_08=8, CollectCallSiteOffsets=5, FindFirstCallSiteOffset=5, T_B76_04=3, T_B76_05=4, T_B76_06=2, T_B76_02=3, T_B76_03=3, T_B76_11=3 | PASS (all <= 8) |
| SCAN-06 | `$b = [System.IO.File]::ReadAllBytes('src\PropTraderTools\B76Tests.cs'); ($b \| Where-Object { $_ -gt 127 } \| Measure-Object).Count` | **0** | PASS |
| SCAN-07 | `Select-String -Path "src\PropTraderTools\B76Tests.cs" -Pattern "using NUnit\|using Microsoft\.VisualStudio"` | **0 results** | PASS |

**Additional acceptance criterion scan**:
```
Select-String -Path "src\PropTraderTools\B76Tests.cs" -Pattern "MetadataToken"
-- 0 results
```
PASS ✅

### SCAN-05 CYC Detail

**T_B76_08** (lines 255-303):
- base=1, for-loop=1, if(opcode1 || opcode2)=2, try/catch=1, if(mb!=null && Name=="Exchange" && DeclaringType==...)=3
- Total = **8** -- at the limit, not over. PASS ✅

**CollectCallSiteOffsets** (lines 429-450):
- base=1, for=1, if(opcode check)=1, try/catch=1, if(name check)=1 = **5** ✅

**FindFirstCallSiteOffset** (lines 455-471):
- base=1, for=1, if(opcode check)=1, try/catch=1, if(name check)=1 = **5** ✅

---

## 4. Layer 2 vs Layer 3 Cross-Check

| Scan | Layer 2 (engineer self-report) | Layer 3 (verifier independent) | Discrepancy? |
|------|-------------------------------|-------------------------------|--------------|
| SCAN-01 | 0 results | 0 results | NONE |
| SCAN-02 | "0 results in code (comment-only)" | 1 comment-only hit (line 5) | NONE (same finding, different phrasing) |
| SCAN-03 | "0 results in code (comment-only)" | 1 comment-only hit (line 5) | NONE (same finding) |
| SCAN-04 | 0 results | 0 results | NONE |
| SCAN-05 | T_B76_04=3, T_B76_05=4, T_B76_08=8, helpers=5 each | Same values confirmed by source read | NONE |
| SCAN-06 | 0 non-ASCII bytes | 0 | NONE |
| SCAN-07 | 0 results | 0 results | NONE |
| MetadataToken | 0 results | 0 results | NONE |

**Layer 2 fully corroborated by Layer 3. No discrepancies.**

---

## 5. Acceptance Criteria Assessment

### DW-C39-11 Acceptance Criterion (from 04-tickets.md)
> `grep -n "MetadataToken" src/PropTraderTools/B76Tests.cs` returns 0 results.

**Result**: 0 results confirmed by Layer 3 independent scan. ✅

### DW-C39-12 Acceptance Criteria (from 04-tickets.md)
> `T_B76_02/03/04/05/06/11` use behavioral assertions OR have `[Fact(Skip = "NT8-HOST-REQUIRED: ...")]` applied with documented reason.

**Result**: All 6 tests use the annotated IL-assertion pattern (Option B per ticket spec), with:
- `// IL assertion:` comments on every test
- `See DW-C39-12` references on every test
- Lower-bound/existence checks (not exact offsets or exact counts)
- Two helpers extracted to keep CYC in bounds

This is compliant with ticket spec Option B ("When adding a Roslyn version dependency note is genuinely required... add a comment on the assertion line"). ✅

### DNA Rule Check
| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no lock) | 0 `lock(` in file | PASS ✅ |
| JS-001 (no throw new) | 0 `throw new` in file | PASS ✅ |
| JS-002 (no return null) | `FindFirstCallSiteOffset` returns -1 not null; no `return null` in code | PASS ✅ |
| JS-033 (no async void) | 0 `async void` in code | PASS ✅ |
| CYC <= 8 | All methods <= 8 (max = 8 in T_B76_08) | PASS ✅ |
| ASCII-only | 0 non-ASCII bytes | PASS ✅ |
| xUnit-only | No NUnit/MSTest references | PASS ✅ |
| NT8 constraints | No production code modified (test file only) | N/A |

---

## 6. DW Item Closure

| DW Item | Ticket | Status | Evidence |
|---------|--------|--------|----------|
| DW-C39-11 | C-5 | **CLOSED** | T_B76_08: `MetadataToken` comparison removed; replaced with `module.ResolveMethod(token)` + `mb.Name == "Exchange"` + `mb.DeclaringType == typeof(System.Threading.Interlocked)`. Zero `MetadataToken` references remain in file. |
| DW-C39-12 | C-5 | **CLOSED** | T_B76_02/03/04/05/06/11: All annotated with `// IL assertion:` + `See DW-C39-12`. Assertions use lower-bound/existence checks. Two helpers (`CollectCallSiteOffsets`, `FindFirstCallSiteOffset`) extracted, both CYC=5. |

---

## Summary

**VERIFY_PASS**

- DW-C39-11: MetadataToken comparison fully replaced with stable name+declaring-type check. Zero MetadataToken references in file. Fix is semantically correct and would detect Interlocked.Exchange across assembly boundaries.
- DW-C39-12: All 6 IL assertion tests annotated per spec. Lower-bound checks in place. Two private helpers extracted at CYC=5. No fragility regressions introduced.
- All 7 independent scans: PASS (0 violations).
- Layer 2 vs Layer 3: No discrepancies.
- DNA rules: All pass.

*ptt-verifier | BWAVE-DW LaneC | Ticket C-5 | VERIFY_PASS*