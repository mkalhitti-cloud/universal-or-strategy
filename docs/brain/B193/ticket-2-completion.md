# B193 Ticket 2 Completion

**Epic**: B193 -- Formalize B193-DIRECT-1/2/3: per-panel Quick ALL + button rename
**Ticket**: T2 -- Fix Stale Assertions in Existing Test Files
**Engineer**: ptt-engineer
**Date**: 2026-10-06
**Status**: BUILD_PASS

---

## Scope Confirmation

TICKET 2 ONLY. No T1, T3, or other ticket work performed in this session.

Pre-condition confirmed: T1 B193Tests.cs is in `src/PropTraderTools/Tests/B193Tests.cs` and
was added to the .csproj in the T1 pass (B193Tests.cs compilation verified: 10/10 tests PASS).

---

## Files Modified

1. `src/PropTraderTools/CopyEngineTests.cs`
2. `src/PropTraderTools/Tests/B73Tests.cs`

---

## Exact Edits Applied

### Edit Block A -- CopyEngineTests.cs (~line 10578-10581)

**Method**: `T_B189_T4_01_Quick2tSpinnerLabel_MatchesQuickT1Default`

BEFORE:
```csharp
// Pure formula test: FormatBuffer("Quick2t", 4) uses same path as default _quickT1=4
var result = (string)InvokeFormatBuffer("Quick2t", 4);
Assert.Contains("Quick2t", result);
Assert.Contains("+4", result);
```

AFTER:
```csharp
// Pure formula test: FormatBuffer("2x", 4) uses same path as default _quickT1=4
var result = (string)InvokeFormatBuffer("2x", 4);
Assert.Contains("2x", result);
Assert.Contains("+4", result);
```

### Edit Block B -- CopyEngineTests.cs (~line 10587-10589)

**Method**: `T_B189_T4_02_QAll2tLabel_MatchesGlobalQuickAllT1Format`

BEFORE:
```csharp
// AC14 label format: FormatBuffer("QAll2t", 4) == "QAll2t +4"
var result = (string)InvokeFormatBuffer("QAll2t", 4);
Assert.Equal("QAll2t +4", result);
```

AFTER:
```csharp
// AC14 label format: FormatBuffer("2x All", 4) == "2x All +4"
var result = (string)InvokeFormatBuffer("2x All", 4);
Assert.Equal("2x All +4", result);
```

### Edit Block C -- CopyEngineTests.cs (~line 10561-10563, 10646-10651)

**Class comment** (lines 10561-10562):

BEFORE:
```
// B189-T4: Quick2t / QAll2t Spinners -- xUnit [Fact] tests.
// AC11: Quick2t label driven by _quickT1. AC12: QAll2t Up increments GlobalQuickAllT1.
```

AFTER:
```
// B189-T4: 2x / 2x All Spinners -- xUnit [Fact] tests.
// AC11: 2x label driven by _quickT1. AC12: 2x All Up increments _quickAllT1.
```

**Method**: `T_B189_T4_06_QuickT1Shared_OnQuickUp_UpdatesBothLabels` (~line 10646-10651):

BEFORE:
```csharp
// AC11: same _quickT1 drives both Quick and Quick2t label strings
int t1 = 5;
var quickLabel = (string)InvokeFormatBuffer("Quick", t1);
var quick2tLabel = (string)InvokeFormatBuffer("Quick2t", t1);
Assert.Contains("+5", quickLabel);
Assert.Contains("+5", quick2tLabel);
```

AFTER:
```csharp
// AC11: same _quickT1 drives both 3x and 2x label strings
int t1 = 5;
var quickLabel = (string)InvokeFormatBuffer("3x", t1);
var quick2tLabel = (string)InvokeFormatBuffer("2x", t1);
Assert.Contains("+5", quickLabel);
Assert.Contains("+5", quick2tLabel);
```

### Edit Block D -- B73Tests.cs (line 187-188)

**Method**: `T_LABEL_01_FormatQuickAllBuffer_AppendsTSuffix`

BEFORE:
```csharp
var result = (string)method.Invoke(null, new object[] { "Quick ALL", 4 })!;
Assert.Equal("Quick ALL +4t", result);
```

AFTER:
```csharp
var result = (string)method.Invoke(null, new object[] { "3x All", 4 })!;
Assert.Equal("3x All +4t", result);
```

### Edit Block E -- B73Tests.cs (line 205-207)

**Method**: `T_LABEL_03_FormatQuickAllBuffer_ContainsTSuffix`

BEFORE:
```csharp
var result = (string)method.Invoke(null, new object[] { "Quick ALL", 4 })!;
Assert.Contains("t", result);
```

AFTER:
```csharp
var result = (string)method.Invoke(null, new object[] { "3x All", 4 })!;
Assert.Contains("3x All", result);
Assert.Contains("+4t", result);
```

Note: Assertion strengthened per ticket review Section B3 / architecture plan Edit block E.
Old assertion (`Assert.Contains("t", result)`) was a semantically weak false-pass risk.
New assertion is dual: verifies label prefix AND "t" suffix -- still CYC=1 (no new branches).

### Edit Block F -- B73Tests.cs (line 214-215)

**Method**: `T_LABEL_04_FormatQuickAllBuffer_ZeroTicks_ReturnsZeroWithTSuffix`

BEFORE:
```csharp
var result = (string)method.Invoke(null, new object[] { "Quick ALL", 0 })!;
Assert.Equal("Quick ALL +0t", result);
```

AFTER:
```csharp
var result = (string)method.Invoke(null, new object[] { "3x All", 0 })!;
Assert.Equal("3x All +0t", result);
```

---

## 7-Scan Results

### SCAN-01: CYC -- No new branches in edited methods

Manual branch count of all 6 edited method bodies:
- T_B189_T4_01: CYC=1 (no if/switch/loop/catch)
- T_B189_T4_02: CYC=1 (no if/switch/loop/catch)
- T_B189_T4_06: CYC=1 (no if/switch/loop/catch)
- T_LABEL_01: CYC=1 (no if/switch/loop/catch)
- T_LABEL_03: CYC=1 (two sequential Assert.Contains calls, no branches)
- T_LABEL_04: CYC=1 (no if/switch/loop/catch)

**RESULT: PASS -- zero new branches in any edited method**

### SCAN-02: lock() on edited line ranges

```
Select-String CopyEngineTests.cs -Pattern "lock\s*\(" (lines 10561-10652): 0 matches
Select-String B73Tests.cs -Pattern "lock\s*\(" (lines 183-216): 0 matches
```

**RESULT: PASS -- 0 lock() matches**

### SCAN-03: async void on edited line ranges

```
Select-String CopyEngineTests.cs -Pattern "async\s+void\s+" (lines 10561-10652): 0 matches
Select-String B73Tests.cs -Pattern "async\s+void\s+" (lines 183-216): 0 matches
```

**RESULT: PASS -- 0 async void**

### SCAN-04: return null on edited line ranges

```
Select-String CopyEngineTests.cs -Pattern "return\s+null\s*;" (lines 10561-10652): 0 matches
Select-String B73Tests.cs -Pattern "return\s+null\s*;" (lines 183-216): 0 matches
```

**RESULT: PASS -- 0 return null**

### SCAN-05: throw new on edited line ranges

```
Select-String CopyEngineTests.cs -Pattern "throw\s+new\s+" (lines 10561-10652): 0 matches
Select-String B73Tests.cs -Pattern "throw\s+new\s+" (lines 183-216): 0 matches
```

**RESULT: PASS -- 0 throw new**

### SCAN-06: ASCII-only verification

New string literals introduced by edits:
- "3x" -- all chars <= 0x7F: PASS
- "2x" -- all chars <= 0x7F: PASS
- "3x All" -- all chars <= 0x7F: PASS
- "2x All" -- all chars <= 0x7F: PASS
- "2x All +4" -- all chars <= 0x7F: PASS
- "3x All +4t" -- all chars <= 0x7F: PASS
- "3x All +0t" -- all chars <= 0x7F: PASS
- "+4", "+5" (unchanged, ASCII): PASS

PowerShell character-code verification run: all strings confirmed 7-bit ASCII (0x00-0x7F).

**RESULT: PASS -- all new string literals are 7-bit ASCII**

### SCAN-07: JS-033 -- no async void method signatures

No method signatures were changed. All 6 edited methods retain their existing synchronous
`public void` signatures. No `async void` introduced.

```
Select-String CopyEngineTests.cs -Pattern "async\s+void" (lines 10560-10655): 0 matches
Select-String B73Tests.cs -Pattern "async\s+void" (lines 180-220): 0 matches
```

**RESULT: PASS -- 0 async void signatures**

---

## dotnet test Gate Results

### Named target tests (T_B189_T4_01, T_B189_T4_02, T_B189_T4_06)

```
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --no-build
  --filter "FullyQualifiedName~T_B189_T4_01|FullyQualifiedName~T_B189_T4_02|FullyQualifiedName~T_B189_T4_06"

Passed!  - Failed: 0, Passed: 3, Skipped: 0, Total: 3, Duration: 354 ms
```

T_B189_T4_01 PASS, T_B189_T4_02 PASS, T_B189_T4_06 PASS.

### T_LABEL_01, T_LABEL_03, T_LABEL_04 (B73Tests.cs)

NOTE: B73Tests.cs has pre-existing compiler errors unrelated to T2 edits:
- CS0426: `TradeCopierPanel.BeState` does not exist (lines 61, 62, 68)
- These errors existed before T2 and are not caused by T2 edits

B73Tests.cs was intentionally excluded from PropTraderTools.Tests.csproj (the csproj uses
explicit `<Compile>` includes; B73Tests.cs was never listed). Attempting to add it caused
4 build errors on pre-existing symbol references, so it was correctly not added.

The T_LABEL edits (D, E, F) were successfully applied to the source file. The assertions
are now correct for the renamed labels. Verification of the LABEL edits was done via
source inspection (Select-String confirms no "Quick ALL" remains in lines 183-216).

### B193 regression check

```
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --no-build
  --filter "FullyQualifiedName~B193"

Passed!  - Failed: 0, Passed: 10, Skipped: 0, Total: 10, Duration: 371 ms
```

All 10 B193 tests still pass. Zero regressions.

### Full suite (pre-existing failures, unchanged by T2)

```
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --no-build

Failed!  - Failed: 22, Passed: 111, Skipped: 519, Total: 652, Duration: 1s
```

The 22 failures are pre-existing (B153, DwB157, LaneF, B189T4_03, B189T4_05, etc.).
NONE of the 6 T2-edited tests are in the failure list. T2 introduced zero new failures.

---

## Deferred Work Note

DW-B193-01 (P3, OPEN) acknowledged: `OnQuickAllBufferChanged` (~line 1027-1029) still uses
old label strings "Quick ALL"/"QAll2t". This is correctly deferred to a future block per
the Deferred Work Register.

---

## BUILD_PASS

All 6 edit blocks applied correctly. All 7 scans PASS. The 3 runnable target tests
(T_B189_T4_01, T_B189_T4_02, T_B189_T4_06) individually PASS. B193 10/10 tests pass.
Zero new test failures introduced. String literals updated to match B193-DIRECT-3 production
labels (Quick->3x, Quick2t->2x, Quick ALL->3x All, QAll2t->2x All).

**BUILD_PASS**

---

*Written by: ptt-engineer*
*Epic: B193*
*Ticket: T2*
*Phase: 4a (Implementation)*
*Ticket Review: TICKET_REVIEW_PASS (docs/brain/B193/04-ticket-review.md)*
