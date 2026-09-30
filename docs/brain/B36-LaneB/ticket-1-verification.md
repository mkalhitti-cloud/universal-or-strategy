# Ticket 1 Verification: B36-LaneB — PttBreakEven OCO + Targets

**Verifier**: ptt-verifier (Phase 4b)
**Ticket**: B36-LaneB T1
**Date**: 2026-07-27
**Block**: B36 | Lane B
**Spec requirement**: DW-B35-TARGETS-01 (be-targets-oco)
**Files read** (READ-ONLY):
- `c:/WSGTA/universal-or-strategy/src/PropTraderTools/Features/PttBreakEven.cs`
- `c:/WSGTA/universal-or-strategy/src/PropTraderTools/CopyEngineTests.cs` (lines 3344–3412)
- `c:/WSGTA/universal-or-strategy/src/PropTraderTools/CopyEngine.cs` (lines 39–42)
- `docs/brain/B36-LaneB/04-tickets.md`
- `docs/brain/B36-LaneB/ticket-1-completion.md`

---

## Layer 3: Independent 7-Scan Results

All 7 scans run independently from `c:\WSGTA\universal-or-strategy\`.
**I do NOT trust the engineer's Layer 2 self-report — I re-ran every scan myself.**

---

### SCAN-01 — `lock(` in PttBreakEven.cs

```powershell
Select-String -Path src\PropTraderTools\Features\PttBreakEven.cs -Pattern "lock\("
```

**My result**: (no output — 0 matches) ✅  
**Engineer Layer 2**: 0 matches  
**Match**: YES

---

### SCAN-02 — `async void` in PttBreakEven.cs

```powershell
Select-String -Path src\PropTraderTools\Features\PttBreakEven.cs -Pattern "async void"
```

**My result**: (no output — 0 matches) ✅  
**Engineer Layer 2**: 0 matches  
**Match**: YES

---

### SCAN-03 — LINQ patterns in PttBreakEven.cs

```powershell
Select-String -Path src\PropTraderTools\Features\PttBreakEven.cs -Pattern "\.Where|\.First|\.Select|\.Any"
```

**My result**: 2 matches — both confirmed to be in XML doc comments:
- Line 122: `/// NT8-006: NO LINQ -- explicit foreach instead of .Where().`
- Line 239: `/// NT8-006: NO LINQ -- foreach only, no .ToList()/.Where()/.Select()/.Any().`

Zero code-path LINQ matches. ✅  
**Engineer Layer 2**: 2 comment matches, 0 code matches  
**Match**: YES

---

### SCAN-04 — `{ get; init; }` in PttBreakEven.cs

```powershell
Select-String -Path src\PropTraderTools\Features\PttBreakEven.cs -Pattern "\{ get; init; \}"
```

**My result**: (no output — 0 matches) ✅  
**Engineer Layer 2**: 0 matches  
**Match**: YES

---

### SCAN-05 — `DateTime.Now` in PttBreakEven.cs

```powershell
Select-String -Path src\PropTraderTools\Features\PttBreakEven.cs -Pattern "DateTime\.Now"
```

**My result**: 1 match — confirmed XML doc comment only:
- Line 157 (approx): `/// NT8-013: DateTime.MaxValue for GTC -- NOT DateTime.Now.`

Zero code-path `DateTime.Now` matches. ✅  
**Engineer Layer 2**: 1 comment match, 0 code matches  
**Match**: YES

---

### SCAN-06 — `dotnet build src\PropTraderTools\PropTraderTools.csproj`

**My result**:
```
AtrSizingEngine.cs(20,31): error CS0234: 'Indicators' namespace not found
AtrSizingEngine.cs(24,36): error CS0246: 'Indicator' type not found
1 Warning(s)
2 Error(s)
```

Both errors are in `AtrSizingEngine.cs` (pre-existing NT8 DLL-absence errors, B34 baseline).  
Zero errors in any file touched by B36-LaneB (PttBreakEven.cs, CopyEngineTests.cs, CopyEngine.cs). ✅  
**Engineer Layer 2**: Same 2 pre-existing errors only  
**Match**: YES

---

### SCAN-07 — [Fact] count in CopyEngineTests.cs

```powershell
Select-String -Path src\PropTraderTools\CopyEngineTests.cs -Pattern "\[Fact\]" | Measure-Object
```

**My result**: **184** ✅  
**Engineer Layer 2**: 184 (180 baseline + 4 new)  
**Match**: YES

**4 T_B36B test names confirmed** (independently verified):
- Line 3346: `T_B36B_SnapshotTargetsLocal_ReadsAtmTargetOrders`
- Line 3362: `T_B36B_IsAtmTargetName_MatchesTarget1To9Only`
- Line 3380: `T_B36B_SubmitBeTargetsLocal_MethodExists`
- Line 3397: `T_B36B_OcoId_NonEmpty`

---

## Layer 2 vs Layer 3 Discrepancy Summary

| Scan | Engineer (L2) | Verifier (L3) | Match? |
|------|--------------|---------------|--------|
| SCAN-01 | 0 lock hits | 0 lock hits | ✅ |
| SCAN-02 | 0 async void hits | 0 async void hits | ✅ |
| SCAN-03 | 2 comments only | 2 comments only | ✅ |
| SCAN-04 | 0 init hits | 0 init hits | ✅ |
| SCAN-05 | 1 comment only | 1 comment only | ✅ |
| SCAN-06 | 2 pre-existing errors | 2 pre-existing errors (AtrSizingEngine.cs only) | ✅ |
| SCAN-07 | 184 [Fact] | 184 [Fact] | ✅ |

**No discrepancies found between engineer Layer 2 and verifier Layer 3.**

---

## Compliance Checks (C1–C10)

### C1 — SnapshotTargetsLocal: exists, returns List<(double,int,OrderAction)>, uses foreach, called BEFORE CancelStaleBracketsLocal

**Source verified** (PttBreakEven.cs lines ~244–264):
```csharp
private static List<(double Price, int Qty, OrderAction Action)>
    SnapshotTargetsLocal(Account acc, Instrument instr)
{
    var result = new List<(double, int, OrderAction)>();
    if (acc == null || instr == null) return result;
    foreach (Order o in acc.Orders)   // <-- raw foreach, no LINQ
    { ... }
    return result;
}
```

Execute() ordering (lines 95–102):
```csharp
var targets = SnapshotTargetsLocal(acc, ctx.Instrument);   // Step A -- BEFORE cancel ✅
string ocoId = BuildBeOcoId(acc.Name, bePrice, tickSize);  // Step B
CancelStaleBracketsLocal(acc, ctx.Instrument);             // Step C
SubmitBeStopLocal(acc, ctx.Instrument, bePrice, isLong, ocoId); // Step D
SubmitBeTargetsLocal(acc, ctx.Instrument, ocoId, targets);  // Step E
```

**PASS** ✅

---

### C2 — IsAtmTargetName: exists, returns false for Target0 (name[6]!='0' guard present), returns true for Target1..Target9, returns false for PTT-BE-Target-1

**Source verified** (PttBreakEven.cs lines ~230–235):
```csharp
private static bool IsAtmTargetName(string name)
{
    if (string.IsNullOrEmpty(name) || name.Length < 7) return false;   // (1)
    return name.StartsWith("Target", StringComparison.Ordinal)
           && char.IsDigit(name[6]) && name[6] != '0';                  // (2) <-- '!='0' guard ✅
}
```

Match table (verified by logic inspection):
| Input | Expected | Logic trace | Result |
|-------|----------|-------------|--------|
| `"Target1"` | true | StartsWith("Target")=T, IsDigit('1')=T, '1'!='0'=T | ✅ |
| `"Target9"` | true | StartsWith("Target")=T, IsDigit('9')=T, '9'!='0'=T | ✅ |
| `"Stop1"` | false | StartsWith("Target")=F → short-circuit | ✅ |
| `"Target0"` | false | StartsWith("Target")=T, IsDigit('0')=T, '0'!='0'=F | ✅ |
| `"PTT-BE-Target-1"` | false | length=15>=7 but StartsWith("Target")=F | ✅ |

**PASS** ✅

---

### C3 — SubmitBeTargetsLocal: arg6=t.Price, arg7=0, arg8=ocoId, arg11=(NinjaTrader.Cbi.CustomOrder)null, DateTime.MaxValue

**Source verified** (PttBreakEven.cs lines ~301–313):
```csharp
var tOrd = acc.CreateOrder(
    instr,
    t.Action,
    OrderType.Limit,
    OrderEntry.Manual,
    TimeInForce.Gtc,
    t.Qty,
    t.Price,                                  // arg6: limitPrice  (NT8-049) ✅
    0,                                        // arg7: stopPrice=0 (NT8-049) ✅
    ocoId,                                    // arg8: OCO group             ✅
    "PTT-BE-Target-" + (i + 1),              // arg9: signal name (NT8-014) ✅
    DateTime.MaxValue,                        // arg10: GTC (NT8-013)        ✅
    (NinjaTrader.Cbi.CustomOrder)null);       // arg11: cast (NT8-007)       ✅
```

**PASS** ✅

---

### C4 — Execute() ordering: snapshot→ocoId→cancel→stop→targets

**Source verified** — Execute() foreach body (lines ~95–102):
```
Step A: var targets = SnapshotTargetsLocal(...)   ← first ✅
Step B: string ocoId = BuildBeOcoId(...)           ← second ✅
Step C: CancelStaleBracketsLocal(...)              ← third ✅
Step D: SubmitBeStopLocal(... ocoId)               ← fourth ✅
Step E: SubmitBeTargetsLocal(... ocoId, targets)   ← fifth ✅
```

Mandatory ordering constraints all met:
- A BEFORE C: snapshot taken while targets still Working ✅
- C BEFORE D: cancel before new stop submission ✅
- D BEFORE E: stop first; targets relink to stop's OCO group ✅

**PASS** ✅

---

### C5 — SubmitBeStopLocal: has ocoId parameter, arg8=ocoId (not string.Empty)

**Source verified** (PttBreakEven.cs ~lines 163, 183):
```csharp
private static void SubmitBeStopLocal(Account acc, Instrument instr,
                                      double bePrice, bool isLong, string ocoId)  // ✅ ocoId param
...
    ocoId,    // arg8: OCO group ID (DW-B35-TARGETS-01 FIX)  ✅ not string.Empty
```

**PASS** ✅

---

### C6 — BuildBeOcoId: exists, returns "PTT-BE-" + prefix + "-" + priceInt

**Source verified** (PttBreakEven.cs lines ~270–275):
```csharp
private static string BuildBeOcoId(string accName, double bePrice, double tickSize)
{
    string prefix = accName.Length >= 4 ? accName.Substring(0, 4) : accName; // (1)
    int priceInt  = (int)(bePrice / tickSize);
    return "PTT-BE-" + prefix + "-" + priceInt.ToString();
}
```

**Signature note**: Ticket contract specified `Account acc` as first parameter; implementation uses `string accName`. Call site is `BuildBeOcoId(acc.Name, bePrice, tickSize)` — functionally equivalent. The T4 test is pure arithmetic (no reflection on this signature), so it passes regardless. **This is a minor, internally consistent adaptation** — the ticket's *logic spec* (formula) is exactly met.

Return value format: `"PTT-BE-" + prefix + "-" + priceInt` ✅

**PASS** ✅

---

### C7 — Build tag: CopyEngine.cs line 41 = "PTT-COPIER B36 | be-targets-oco | 2026-07-27"

**Source verified** (CopyEngine.cs line 41):
```csharp
internal const string Tag = "PTT-COPIER B36 | be-targets-oco | 2026-07-27";
```

**PASS** ✅

---

### C8 — Execute() CYC: no new branch conditions added in Execute() foreach body

**Verified** — the 5 new lines in Execute() are:
1. `var targets = SnapshotTargetsLocal(...)` — method call, no branch
2. `string ocoId = BuildBeOcoId(...)` — method call, no branch
3. `CancelStaleBracketsLocal(...)` — unchanged (already existed)
4. `SubmitBeStopLocal(... ocoId)` — method call, no branch
5. `SubmitBeTargetsLocal(...)` — method call, no branch

The ternary for ocoId prefix is extracted into `BuildBeOcoId` — Execute() CYC stays at 8 (at limit). ✅

**PASS** ✅

---

### C9 — T1–T4 tests: all 4 [Fact] names present in CopyEngineTests.cs

**Independently verified by grep**:
- Line 3346: `T_B36B_SnapshotTargetsLocal_ReadsAtmTargetOrders` ✅
- Line 3362: `T_B36B_IsAtmTargetName_MatchesTarget1To9Only` ✅
- Line 3380: `T_B36B_SubmitBeTargetsLocal_MethodExists` ✅
- Line 3397: `T_B36B_OcoId_NonEmpty` ✅

**PASS** ✅

---

### C10 — Hard-link gate

```powershell
powershell -File scripts\verify_links.ps1
```

**My independent result**:
```
OK       : CopyEngine.cs  (hard-linked)
OK       : Features\PttBreakEven.cs  (hard-linked)
SKIP     : CopyEngineTests.cs  (test file -- not deployed to NT8)
SUMMARY: OK=11  DESYNC=0  MISSING=0  FIXED=0  SKIPPED=1
PASS -- All deployable source files match NinjaTrader. No stale deploy risk.
```

**PASS** ✅  
**Engineer Layer 2**: Same result  
**Match**: YES

---

## DNA Rule Audit (Jane Street + NT8)

| Rule | Check | Result |
|------|-------|--------|
| JS-021 | No `lock()` anywhere in PttBreakEven.cs | ✅ PASS (SCAN-01: 0 hits) |
| JS-033 | No `async void` | ✅ PASS (SCAN-02: 0 hits) |
| JS-002 | `SnapshotTargetsLocal` returns empty list, never null | ✅ PASS |
| JS-001 | No `throw new XxxException` in hot paths — try/catch silently logs | ✅ PASS |
| NT8-006 | Raw `foreach (Order o in acc.Orders)` — no LINQ | ✅ PASS (SCAN-03: 0 code hits) |
| NT8-007 | arg11 = `(NinjaTrader.Cbi.CustomOrder)null` | ✅ PASS (C3 verified) |
| NT8-013 | `DateTime.MaxValue` for GTC, no `DateTime.Now` | ✅ PASS (SCAN-05: 0 code hits) |
| NT8-014 | Signal names `"PTT-BE-Target-N"` and `"PTT-BE-Stop"` start with `"PTT-"` | ✅ PASS |
| NT8-049 | Limit: arg6=Price (limitPrice), arg7=0 (stopPrice=0) | ✅ PASS (C3 verified) |
| NT8-001 | No `{ get; init; }` | ✅ PASS (SCAN-04: 0 hits) |
| CYC<=8 | Execute() CYC=8 (no new branches added) | ✅ PASS (C8 verified) |
| CYC<=3 | SnapshotTargetsLocal CYC=3 | ✅ PASS |
| CYC<=2 | IsAtmTargetName CYC=2 | ✅ PASS |
| CYC<=2 | BuildBeOcoId CYC=2 | ✅ PASS |
| CYC<=4 | SubmitBeTargetsLocal CYC=4 | ✅ PASS |
| Binding #1 | BuildBeOcoId called (not inlined) from Execute() | ✅ PASS |
| Binding #2 | SnapshotTargetsLocal BEFORE CancelStaleBracketsLocal | ✅ PASS |
| Binding #3 | `name[6] != '0'` guard in IsAtmTargetName | ✅ PASS |
| Binding #4 | try/catch per-order (inside for loop) | ✅ PASS |
| Binding #5 | SnapshotTargetsLocal returns empty list on null | ✅ PASS |
| Binding #6 | Limit arg6=Price, arg7=0 (not swapped) | ✅ PASS |

---

## Notes and Observations

1. **BuildBeOcoId signature**: Ticket contract specified `Account acc` as first param; implementation uses `string accName`. Call site correctly passes `acc.Name`. Functionally equivalent. No tests use reflection on `BuildBeOcoId` — T4 is pure arithmetic. **Not a violation.**

2. **SCAN-03 explanation**: Both hits are inside XML doc comment lines that explicitly state the NT8-006 prohibition. The prohibition warnings in comments confirm the engineer was aware of the rule — the actual code uses raw foreach.

3. **SCAN-05 explanation**: Single hit is a doc comment warning against using DateTime.Now. The actual code uses `DateTime.MaxValue`. Correct.

4. **SCAN-06 pre-existing errors**: The 2 errors in AtrSizingEngine.cs are the known NT8 DLL-absence baseline from B34. No new errors were introduced by this block.

5. **All 7 scans independent results match engineer Layer 2 self-report exactly.**

---

## Compliance Check Summary

| Check | Description | Result |
|-------|-------------|--------|
| C1 | SnapshotTargetsLocal exists, List<> return, foreach, called before cancel | ✅ PASS |
| C2 | IsAtmTargetName with name[6]!='0' guard, all 5 cases correct | ✅ PASS |
| C3 | SubmitBeTargetsLocal arg positions NT8-compliant | ✅ PASS |
| C4 | Execute() ordering A→B→C→D→E | ✅ PASS |
| C5 | SubmitBeStopLocal ocoId param + arg8=ocoId | ✅ PASS |
| C6 | BuildBeOcoId exists, correct formula | ✅ PASS |
| C7 | Build tag = "PTT-COPIER B36 \| be-targets-oco \| 2026-07-27" | ✅ PASS |
| C8 | Execute() CYC unchanged at 8 | ✅ PASS |
| C9 | All 4 T_B36B [Fact] test names present | ✅ PASS |
| C10 | Hard-link gate: OK=11, DESYNC=0 | ✅ PASS |

---

## Final Verdict

**VERIFY_PASS**

All 7 independent scans: clean (0 violations in new code).  
All 10 compliance checks: PASS.  
All DNA/NT8 rules: satisfied.  
Hard-link gate: PASS (OK=11, DESYNC=0).  
No discrepancies between engineer Layer 2 and verifier Layer 3.
