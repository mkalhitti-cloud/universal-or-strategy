# B129 Ticket 1 — Independent Verification Report

**Block**: B129 — Instrument Row Redesign: Quick2t + QAll2t Buttons
**Ticket**: T1 (T1a + T1b + T1c)
**Verifier**: ptt-verifier (Phase 4b)
**Layer**: Layer 3 (independent re-run — engineer Layer 2 results NOT trusted until confirmed)
**Date**: 2026-08-11

---

## Structural Verification Results (Items a-j)

### a) Fields: `_instr2tBtn`, `_instrQAll2tBtn`, `_instrRowPanel` present; `_instrBeBtn`, `_instrQxT1`, `_instrQxBtn` absent

**Layer 3 grep result:**
```
Line 271: private Button _instr2tBtn = null;
Line 272: private Button _instrQAll2tBtn = null;
Line 273: private UniformGrid _instrRowPanel = null;
```
- `_instr2tBtn` PRESENT at line 271
- `_instrQAll2tBtn` PRESENT at line 272
- `_instrRowPanel` PRESENT at line 273
- `_instrBeBtn` ABSENT (grep returned 0 hits)
- `_instrQxT1` ABSENT (grep returned 0 hits)
- `_instrQxBtn` ABSENT (grep returned 0 hits)

**Result: PASS**

---

### b) `BuildInstrRow()`: 2 plain buttons, no RepeatButton, no spinner Grid, CYC=1

**Source (lines 1353-1377):**
- Method declared `private void BuildInstrRow()` at line 1353
- Creates `UniformGrid { Columns = 2 }` at line 1355
- Creates `_instr2tBtn` (Button, Content="Quick2t") and `_instrQAll2tBtn` (Button, Content="QAll2t")
- No RepeatButton, no Grid, no spinner, no DockPanel
- No branches (if/else/loop) -- sequential construction only
- CYC=1 confirmed

**Result: PASS**

---

### c) `Build2TargetList(int totalQty)`: present, `internal static`, formula correct, CYC=1

**Source (lines 1383-1388):**
```csharp
internal static List<(double Price, int Qty)> Build2TargetList(int totalQty)
{
    int t1Qty = (totalQty + 1) / 2;
    int t2Qty = totalQty - t1Qty;
    return new List<(double, int)> { (0.0, t1Qty), (0.0, t2Qty) };
}
```
- `internal static` confirmed
- Returns `new List<>` -- never null
- Formula: t1Qty = (totalQty + 1) / 2; t2Qty = totalQty - t1Qty
- CYC=1 -- no branches

**Result: PASS**

---

### d) `OnInstr2tClick()`: log tag `[PTT-QX-2T]` with T1= / T2= keys, calls PttQuickExit.Execute

**Source (lines 1948-1974):**
- Method signature: `private void OnInstr2tClick(object sender, RoutedEventArgs e)` at line 1948
- Guards: `if (_instrument == null) return;` and `if (_leaderAccount == null) return;`
- `NinjaTrader.Code.Output.Process("[PTT-QX-2T] button: " + ... + " T1=" + targets[0].Qty + " T2=" + targets[1].Qty, ...)` at lines 1960-1972
- `new PttQuickExit().Execute(_leaderAccount, _instrument, 4, targets);` at line 1973
- t1Ticks=4 (fixed)

**Result: PASS**

---

### e) `OnInstrQAll2tClick()`: calls `new PttGlobalQuickExit().Execute()`

**Source (lines 1979-1982):**
```csharp
private void OnInstrQAll2tClick(object sender, RoutedEventArgs e)
{
    new PttGlobalQuickExit().Execute();
}
```
- Delegates entirely to PttGlobalQuickExit.Execute()
- CYC=1, no other logic

**Result: PASS**

---

### f) `OnInstrQxClick`, `OnInstrQxUp`, `OnInstrQxDown`, `OnInstrBeClick`, `ComputeInstrSplit` ALL ABSENT

**Layer 3 grep (SCAN-06 pattern):**
```
Select-String -Pattern "_instrQxT1|_instrBeBtn|ComputeInstrSplit|OnInstrQxClick|OnInstrQxUp|OnInstrQxDown|OnInstrBeClick"
Result: No output (0 matches)
```
All 7 removed symbols confirmed absent.

**Result: PASS**

---

### g) `PttQuickExit.cs` -- `if (tNQty <= 0) continue;` guard at correct location

**Source (lines 117-125):**
```
Line 117: int tNQty =
Line 118:     (targets != null && i < targets.Count)
Line 119:         ? targets[i].Qty
Line 120:         : CalcTNQty(pos.Quantity, targetCount, i);
Line 121: (blank)
Line 122: if (tNQty <= 0)
Line 123:     continue; // B129: skip T2 when pos.Quantity==1 and t2Qty==0
Line 124: (blank)
Line 125: string ocoId_i =
```
- Guard present at lines 122-123
- Positioned AFTER tNQty assignment (line 120) and BEFORE `string ocoId_i =` (line 125)
- Comment matches spec

**Result: PASS**

---

### h) B128Tests.cs -- 5 [Fact] tests present: T_B129_01 through T_B129_05

**Layer 3 (execute_command Get-Content):**
- `T_B129_01_Build2TargetList_Even_T1EqualT2` [Fact]
- `T_B129_02_Build2TargetList_Odd_T1Heavier` [Fact]
- `T_B129_03_Build2TargetList_One_T2IsZero` [Fact]
- `T_B129_04_Build2TargetList_Large_Odd` [Fact]
- `T_B129_05_Build2TargetList_Six_BothThree` [Fact]
- Class name `B128Tests` retained
- Framework: xUnit only ([Fact] attribute, Xunit using directive)

**Result: PASS**

---

### i) No `ComputeInstrSplit` references in B128Tests.cs

Text "ComputeInstrSplit" does not appear anywhere in B128Tests.cs.

**Result: PASS**

---

### j) `Build2TargetList` test values match specification

| Test | Input | Expected T1 | Expected T2 | In Code? |
|------|-------|-------------|-------------|----------|
| T_B129_01 | 4 | 2 | 2 | YES |
| T_B129_02 | 5 | 3 | 2 | YES |
| T_B129_03 | 1 | 1 | 0 | YES |
| T_B129_04 | 7 | 4 | 3 | YES |
| T_B129_05 | 6 | 3 | 3 | YES |

All 5 test assertions match ceiling-heavy formula: `t1Qty = (totalQty + 1) / 2`

**Result: PASS**

---

## 7-Scan Results (Layer 3 Independent Run)

### SCAN-01 -- lock() check

**Commands run:**
```
Select-String TradeCopierPanel.cs -Pattern "lock\(" | Measure-Object: Count=1
  -> Line 1421: COMMENT ONLY -- "// JS-021: no lock(). JS-033: ..."

Select-String PttQuickExit.cs -Pattern "lock\(" | Measure-Object: Count=0
```

**Verdict:** TradeCopierPanel.cs has 1 hit at line 1421 in a **comment** only. No live `lock(` statement exists anywhere. PttQuickExit.cs = 0.
**SCAN-01: PASS**

---

### SCAN-02 -- async void check

**Command run:**
```
Select-String TradeCopierPanel.cs -Pattern "async void" | Measure-Object: Count=7
```

**Detail:** All 7 hits are in **comments only** (lines 1421, 1705, 1861, 2104, 2234, 2256, 2298). Pattern: "// ... not async void", "// JS-033: no async void". No live `async void` declaration exists.
**SCAN-02: PASS**

---

### SCAN-03 -- return null check

**Command run:**
```
Select-String TradeCopierPanel.cs -Pattern "return null"
Result: Lines 505, 565, 570, 574, 2040, 2050 (6 live hits, all pre-existing)
        Lines 2297, 2324, 2348, 2498, 2585, 2660 (6 in comments)
```

**Detail:** 6 live `return null` at lines 505, 565, 570, 574, 2040, 2050 -- ALL in pre-existing methods not touched by B129. `Build2TargetList` (lines 1383-1388) returns `new List<>` with zero `return null`. Not a B129 violation.
**SCAN-03: PASS**

---

### SCAN-04 -- throw new check

**Commands run:**
```
Select-String TradeCopierPanel.cs -Pattern "throw new"  -> No output (0 matches)
Select-String PttQuickExit.cs -Pattern "throw new"      -> No output (0 matches)
```

**SCAN-04: PASS**

---

### SCAN-05 -- Log tag [PTT-QX-2T]

**Command run:**
```
Select-String TradeCopierPanel.cs -Pattern "\[PTT-QX-2T\]"
Result: Line 1961: "[PTT-QX-2T] button: "
```

**Detail (source lines 1960-1972 confirmed):**
- Tag `[PTT-QX-2T]` present at line 1961 inside OnInstr2tClick
- `T1=` present at line 1967
- `T2=` present at line 1969
- Exactly 1 occurrence
- No variant tag found

**SCAN-05: PASS**

---

### SCAN-06 -- Field removals confirmed

**Command run:**
```
Select-String TradeCopierPanel.cs -Pattern "_instrQxT1|_instrBeBtn|ComputeInstrSplit|OnInstrQxClick|OnInstrQxUp|OnInstrQxDown|OnInstrBeClick"
Result: No output (0 matches)
```

All 7 B128 symbols completely absent from TradeCopierPanel.cs.
**SCAN-06: PASS**

---

### SCAN-07 -- Build + test run

**Build:**
```
dotnet build --no-incremental -> Build succeeded. 0 Warning(s) 0 Error(s). Time: 00:00:02.06
```

**Tests:**
```
dotnet test --filter "FullyQualifiedName~B128Tests"
Result: Passed! - Failed: 0, Passed: 5, Skipped: 0, Total: 5, Duration: 605 ms (net48)
```

Old ComputeInstrSplit tests absent (zero in file; would be compile error if present).
**SCAN-07: PASS**

---

## Layer 2 vs Layer 3 Comparison

| Scan | Engineer Layer 2 Claim | Verifier Layer 3 Result | Match? | Notes |
|------|------------------------|-------------------------|--------|-------|
| SCAN-01 (lock) | TradeCopierPanel code=0; PttQuickExit=0 | TradeCopierPanel=1 comment only; PttQuickExit=0 | AGREE | Engineer correctly excluded comment; raw grep returns 1 comment at line 1421 |
| SCAN-02 (async void) | TradeCopierPanel=7 comments only | 7 all comments (lines 1421,1705,1861,2104,2234,2256,2298) | AGREE | Exact count matches |
| SCAN-03 (return null) | 12 hits, all pre-existing | 12 total (6 live + 6 comment), all pre-existing | AGREE | Build2TargetList free of return null confirmed |
| SCAN-04 (throw new) | Both=0 | Both=0 | AGREE | |
| SCAN-05 ([PTT-QX-2T]) | Line 1960 | Line 1961 | AGREE | Trivial 1-line offset; multiline Output.Process, tag string starts on 1961 vs call opens at 1960 |
| SCAN-06 (removed) | All 7 absent | 0 matches (all absent) | AGREE | |
| SCAN-07 (build+tests) | 0 errors, 5/5 pass | 0 errors, 5/5 pass | AGREE | |

**Discrepancies found:** None material. The 1-line SCAN-05 offset is a formatting artifact, not a violation.

---

## DNA Rule Compliance

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no lock) | 0 live lock() in any code path | PASS |
| JS-001 (no throw) | 0 throw new in B129-touched methods | PASS |
| JS-002 (no return null) | Build2TargetList returns new List<> always | PASS |
| JS-033 (no async void) | All B129 handlers are synchronous void | PASS |
| JS-008 (mutable struct) | No struct fields introduced | N/A |
| NT8-FONTFAMILY | No FontFamily= in new WPF elements | PASS |
| NT8-HEXCOLOR | No #RRGGBB literals -- uses named brush BrushTeal | PASS |
| NT8-CREATEORDER | No CreateOrder calls in B129 code | N/A |
| NT8-DATETIME | No DateTime.Now usage in B129 code | N/A |
| ASCII-ONLY | All string literals ASCII ("Quick2t","QAll2t","[PTT-QX-2T]","T1=","T2=") | PASS |
| CYC<=8 | Build2TargetList=1, BuildInstrRow=1, OnInstr2tClick=4, OnInstrQAll2tClick=1, PttQuickExit.Execute=8 | PASS |

---

## Spec Requirements Coverage

| Req ID | Description | Verified |
|--------|-------------|---------|
| B129-REQ-01 | Replace spinner cluster with 2-button grid ("Quick2t" + "QAll2t") | UniformGrid 2-col at lines 1353-1377 |
| B129-REQ-02 | "Quick2t" fires single-account 2-target bracket exit | OnInstr2tClick at line 1948 |
| B129-REQ-03 | "QAll2t" fires all-accounts exit via PttGlobalQuickExit.Execute() | OnInstrQAll2tClick at line 1979 |
| B129-REQ-04 | Build2TargetList: ceiling-heavy split, returns List, never null | Line 1383; 5 tests confirm formula |
| B129-REQ-05 | T2qty=0 guard in PttQuickExit.Execute() | Lines 122-123 in PttQuickExit.cs |
| B129-REQ-06 | Remove all B128 spinner fields/methods; update B128Tests.cs | 7 symbols absent; 5 new tests pass |

---

## H-Criteria Cross-Check

| Check | Description | Layer 3 Result |
|-------|-------------|----------------|
| H.1 | Build 0 errors, 0 warnings | PASS |
| H.2 | 5 new tests pass; 4 old absent | PASS |
| H.3a | `_instrQxT1` removed | PASS (line 0) |
| H.3b | `_instrBeBtn` removed | PASS (line 0) |
| H.3c | `OnInstrQxUp` removed | PASS (line 0) |
| H.3d | `OnInstrQxDown` removed | PASS (line 0) |
| H.3e | `OnInstrBeClick` removed | PASS (line 0) |
| H.3f | `ComputeInstrSplit` removed | PASS (line 0) |
| H.4a | `_instr2tBtn` present | PASS (line 271) |
| H.4b | `_instrQAll2tBtn` present | PASS (line 272) |
| H.4c | `Build2TargetList` present | PASS (line 1383) |
| H.4d | `OnInstr2tClick` present | PASS (line 1948) |
| H.4e | `OnInstrQAll2tClick` present | PASS (line 1979) |
| H.5 | `tNQty <= 0` guard in PttQuickExit.cs | PASS (lines 122-123) |
| H.6 | PttGlobalQuickExit unchanged | PASS (not in touched file list; 0 diff) |
| H.7 | All 7 scans pass | PASS |

---

## Overall Result

**VERIFY_PASS**

All 7 independent Layer 3 scans pass. All 10 structural requirements (a-j) confirmed from source.
All H-criteria verified. No discrepancies between Layer 2 (engineer) and Layer 3 (verifier).
Build clean: 0 errors, 0 warnings. Tests: 5/5 B129 tests pass. All DNA rules compliant.
No violations found.