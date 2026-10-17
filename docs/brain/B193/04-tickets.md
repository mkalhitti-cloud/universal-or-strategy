# B193 Tickets

**Epic**: B193 -- Formalize B193-DIRECT-1/2/3: per-panel Quick ALL + button rename
**Author**: ptt-architect
**Date**: 2026-10-06
**Plan source**: docs/brain/B193/02-architecture-plan.md (REVIEW_PASS 2026-10-06)
**Execution order**: T1 -> T2 -> T3 (sequential; each requires prior green gate)

---

## Spec Requirements Satisfied

| Ticket | Spec Requirement IDs |
|--------|---------------------|
| T1 | B193-DIRECT-2 (per-panel _quickAllT1 field formalization); B193-DIRECT-3 (button rename formalization); QUICK-LABEL-UNIT-01 (FormatQuickAllBuffer "t" suffix) |
| T2 | B193-DIRECT-3 stale assertion fix: T_B189_T4_01, T_B189_T4_02, T_B189_T4_06 (CopyEngineTests.cs); T_LABEL_01, T_LABEL_03, T_LABEL_04 (B73Tests.cs) |
| T3 | Spec sync: button label table, block history, InstrumentDefaults table, SetInstrument() AC, DW-B192-SINST-01, DW-B192-QALL-RESET-01 |

---

## T1 -- New Test File: B193Tests.cs

### File Path
`src/PropTraderTools/Tests/B193Tests.cs`

### Purpose
Formalize B193-DIRECT-2 and B193-DIRECT-3 with 10 new [Fact] tests covering:
- InstrumentDefaults.GetQuickTicks regression (4 tests, no NT8 host required)
- FormatBuffer / FormatQuickAllBuffer label string validation (4 tests, via reflection)
- TradeCopierPanel._quickAllT1 field existence (1 test, via reflection)
- ResolveQuickTicks proxy path via InstrumentDefaults (1 test)

No production .cs changes. No NT8 host required. Pure reflection + formula tests.

### Method Signatures to Implement

| Method | Signature | Return | CYC | Notes |
|--------|-----------|--------|-----|-------|
| InvokeFormatBuffer | `private static string InvokeFormatBuffer(string name, int ticks)` | string | 1 | BindingFlags.NonPublic|Static reflection into TradeCopierPanel |
| InvokeFormatQuickAllBuffer | `private static string InvokeFormatQuickAllBuffer(string name, int ticks)` | string | 1 | BindingFlags.NonPublic|Static reflection into TradeCopierPanel |
| T_B193_01 | `public void T_B193_01_GetQuickTicks_MGC_Returns10_20()` | void | 1 | [Fact]; Assert.Equal((10,20), result) |
| T_B193_02 | `public void T_B193_02_GetQuickTicks_GC_Returns10_20()` | void | 1 | [Fact]; Assert.Equal((10,20), result) |
| T_B193_03 | `public void T_B193_03_GetQuickTicks_MES_Returns4_8()` | void | 1 | [Fact]; Assert.Equal((4,8), result) |
| T_B193_04 | `public void T_B193_04_GetQuickTicks_ES_Returns4_8()` | void | 1 | [Fact]; Assert.Equal((4,8), result) |
| T_B193_05 | `public void T_B193_05_FormatBuffer_3x_10_Returns3xPlus10()` | void | 1 | [Fact]; Assert.Equal("3x +10", result) |
| T_B193_06 | `public void T_B193_06_FormatBuffer_2x_10_Returns2xPlus10()` | void | 1 | [Fact]; Assert.Equal("2x +10", result) |
| T_B193_07 | `public void T_B193_07_FormatBuffer_2xAll_10_Returns2xAllPlus10()` | void | 1 | [Fact]; Assert.Equal("2x All +10", result) |
| T_B193_08 | `public void T_B193_08_FormatQuickAllBuffer_3xAll_10_Returns3xAllPlus10t()` | void | 1 | [Fact]; Assert.Equal("3x All +10t", result); note "t" suffix via FormatQuickAllBuffer |
| T_B193_09 | `public void T_B193_09_TradeCopierPanel_HasField_quickAllT1()` | void | 1 | [Fact]; Assert.NotNull(GetField("_quickAllT1", NonPublic|Instance)) |
| T_B193_10 | `public void T_B193_10_ResolveQuickTicks_Proxy_MGC_t1_Equals10()` | void | 1 | [Fact]; Assert.Equal(10, InstrumentDefaults.GetQuickTicks("MGC").t1) |

### Complete File Content to Write

```csharp
using System;
using System.Reflection;
using Xunit;
using PropTraderTools;

// B193 formalization tests.
// Verifies per-panel _quickAllT1 field, InstrumentDefaults.GetQuickTicks,
// FormatBuffer / FormatQuickAllBuffer label strings, and ResolveQuickTicks proxy.
// No NT8 host required -- pure reflection + formula tests.
// JS-021: no lock. JS-001: no throw. JS-002: no return null. JS-033: no async void.
// ASCII-only strings. CYC<=1 per test method.
public class B193Tests
{
    // Reflection helper: invokes private static FormatBuffer(string name, int ticks)
    // Returns: name + " +" + ticks  (e.g. "3x +10")
    // Mirrors B189T4SpinnerTests.InvokeFormatBuffer pattern (CopyEngineTests.cs:10566)
    // with internal cast to string instead of returning object.
    private static string InvokeFormatBuffer(string name, int ticks)
    {
        var m = typeof(TradeCopierPanel).GetMethod(
            "FormatBuffer",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        return (string)m.Invoke(null, new object[] { name, ticks });
    }

    // Reflection helper: invokes private static FormatQuickAllBuffer(string name, int ticks)
    // Returns: name + " +" + ticks + "t"  (e.g. "3x All +10t")
    // QUICK-LABEL-UNIT-01: "t" unit suffix is appended by FormatQuickAllBuffer only.
    private static string InvokeFormatQuickAllBuffer(string name, int ticks)
    {
        var m = typeof(TradeCopierPanel).GetMethod(
            "FormatQuickAllBuffer",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        return (string)m.Invoke(null, new object[] { name, ticks });
    }

    [Fact]
    public void T_B193_01_GetQuickTicks_MGC_Returns10_20()
    {
        Assert.Equal((10, 20), InstrumentDefaults.GetQuickTicks("MGC"));
    }

    [Fact]
    public void T_B193_02_GetQuickTicks_GC_Returns10_20()
    {
        Assert.Equal((10, 20), InstrumentDefaults.GetQuickTicks("GC"));
    }

    [Fact]
    public void T_B193_03_GetQuickTicks_MES_Returns4_8()
    {
        Assert.Equal((4, 8), InstrumentDefaults.GetQuickTicks("MES"));
    }

    [Fact]
    public void T_B193_04_GetQuickTicks_ES_Returns4_8()
    {
        Assert.Equal((4, 8), InstrumentDefaults.GetQuickTicks("ES"));
    }

    [Fact]
    public void T_B193_05_FormatBuffer_3x_10_Returns3xPlus10()
    {
        Assert.Equal("3x +10", InvokeFormatBuffer("3x", 10));
    }

    [Fact]
    public void T_B193_06_FormatBuffer_2x_10_Returns2xPlus10()
    {
        Assert.Equal("2x +10", InvokeFormatBuffer("2x", 10));
    }

    [Fact]
    public void T_B193_07_FormatBuffer_2xAll_10_Returns2xAllPlus10()
    {
        Assert.Equal("2x All +10", InvokeFormatBuffer("2x All", 10));
    }

    [Fact]
    public void T_B193_08_FormatQuickAllBuffer_3xAll_10_Returns3xAllPlus10t()
    {
        Assert.Equal("3x All +10t", InvokeFormatQuickAllBuffer("3x All", 10));
    }

    [Fact]
    public void T_B193_09_TradeCopierPanel_HasField_quickAllT1()
    {
        var field = typeof(TradeCopierPanel).GetField(
            "_quickAllT1",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        Assert.NotNull(field);
    }

    [Fact]
    public void T_B193_10_ResolveQuickTicks_Proxy_MGC_t1_Equals10()
    {
        Assert.Equal(10, InstrumentDefaults.GetQuickTicks("MGC").t1);
    }
}
```

### xUnit [Fact] Test Names and Assertions

| Test Name | Method under test | Input | Expected assertion |
|-----------|-------------------|-------|--------------------|
| T_B193_01_GetQuickTicks_MGC_Returns10_20 | InstrumentDefaults.GetQuickTicks | "MGC" | Assert.Equal((10, 20), result) |
| T_B193_02_GetQuickTicks_GC_Returns10_20 | InstrumentDefaults.GetQuickTicks | "GC" | Assert.Equal((10, 20), result) |
| T_B193_03_GetQuickTicks_MES_Returns4_8 | InstrumentDefaults.GetQuickTicks | "MES" | Assert.Equal((4, 8), result) |
| T_B193_04_GetQuickTicks_ES_Returns4_8 | InstrumentDefaults.GetQuickTicks | "ES" | Assert.Equal((4, 8), result) |
| T_B193_05_FormatBuffer_3x_10_Returns3xPlus10 | InvokeFormatBuffer | ("3x", 10) | Assert.Equal("3x +10", result) |
| T_B193_06_FormatBuffer_2x_10_Returns2xPlus10 | InvokeFormatBuffer | ("2x", 10) | Assert.Equal("2x +10", result) |
| T_B193_07_FormatBuffer_2xAll_10_Returns2xAllPlus10 | InvokeFormatBuffer | ("2x All", 10) | Assert.Equal("2x All +10", result) |
| T_B193_08_FormatQuickAllBuffer_3xAll_10_Returns3xAllPlus10t | InvokeFormatQuickAllBuffer | ("3x All", 10) | Assert.Equal("3x All +10t", result) |
| T_B193_09_TradeCopierPanel_HasField_quickAllT1 | typeof(TradeCopierPanel).GetField("_quickAllT1", NonPublic|Instance) | -- | Assert.NotNull(field) |
| T_B193_10_ResolveQuickTicks_Proxy_MGC_t1_Equals10 | InstrumentDefaults.GetQuickTicks("MGC").t1 | -- | Assert.Equal(10, result) |

### JS Rule Constraints

| Rule | Constraint | Status in this ticket |
|------|-----------|----------------------|
| JS-021 | No lock() anywhere | COMPLIANT -- tests use reflection and Assert only; no shared mutable state |
| JS-001 | No throw in hot paths | COMPLIANT -- no throw statements in any test method |
| JS-002 | No return null | COMPLIANT -- helpers return string via internal cast; [Fact] methods return void |
| JS-033 | No async void (non-event-handler) | COMPLIANT -- all [Fact] methods and helpers are synchronous void / string |
| JS-066 | CYC <= 8 per method | COMPLIANT -- all 12 methods CYC=1 |
| JS-081 | ASCII-only string literals | COMPLIANT -- "3x", "2x", "2x All", "3x All", "MGC", "GC", "MES", "ES" are all 7-bit ASCII |
| NT8-DATETIME | No DateTime.Now | COMPLIANT -- no DateTime usage |
| NT8-HEX | No hardcoded hex colors | COMPLIANT -- no color strings |

### 7-Scan Checklist

```
SCAN-01 CYC: manual branch count -- each [Fact] method: CYC=1 (single return path, no branches).
             InvokeFormatBuffer: CYC=1. InvokeFormatQuickAllBuffer: CYC=1.
             All 12 methods <= 8. Command: dotnet-script cyclomatic or manual count.
             PASS criterion: zero methods > 8 branches.

SCAN-02 lock(): grep "lock(" src/PropTraderTools/Tests/B193Tests.cs
             PASS criterion: 0 matches.

SCAN-03 async void: grep "async void " src/PropTraderTools/Tests/B193Tests.cs
             PASS criterion: 0 matches.

SCAN-04 return null: grep "return null;" src/PropTraderTools/Tests/B193Tests.cs
             PASS criterion: 0 matches.

SCAN-05 throw new: grep "throw new" src/PropTraderTools/Tests/B193Tests.cs
             PASS criterion: 0 matches.

SCAN-06 ASCII: visual/grep scan of all string literals in the file.
             Strings present: "FormatBuffer", "FormatQuickAllBuffer", "_quickAllT1", "3x", "2x",
             "2x All", "3x All", "MGC", "GC", "MES", "ES" -- all 7-bit ASCII.
             PASS criterion: no Unicode codepoint > 0x7F in any string literal.

SCAN-07 JS-033: confirm no async void method signatures (non-event-handler).
             All [Fact] methods: public void (synchronous). Helpers: private static string (synchronous).
             PASS criterion: 0 async void signatures.
```

### Post-Edit Gate

```
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --no-build
```

PASS criterion: All 10 new tests PASS. Zero pre-existing test regressions. Document exact output (test count, elapsed time, pass/fail summary) in the T1 completion artifact.

### Acceptance Criteria

- [ ] File `src/PropTraderTools/Tests/B193Tests.cs` created and compiles cleanly
- [ ] All 10 [Fact] methods present with exact names as specified in the table above
- [ ] Both reflection helpers (InvokeFormatBuffer, InvokeFormatQuickAllBuffer) implemented
- [ ] All 10 tests pass: `dotnet test ... --no-build` returns 0 failures
- [ ] Zero pre-existing test regressions (full suite pass count unchanged or higher)
- [ ] All 7 scans PASS (document results in completion artifact)

---

## T2 -- Fix Stale Assertions in Existing Test Files

### Files Modified
1. `src/PropTraderTools/CopyEngineTests.cs`
2. `src/PropTraderTools/Tests/B73Tests.cs`

### Background
B193-DIRECT-3 (commit 3a098a88) renamed production button labels:
- `Quick` -> `3x`
- `Quick2t` -> `2x`
- `Quick ALL` -> `3x All`
- `QAll2t` -> `2x All`

The test files still assert the old label strings, causing test failures. T2 updates the 6 stale assertion sites (3 in CopyEngineTests.cs, 3 in B73Tests.cs) to match the production labels.

No new logic. No new branches. String literal replacements only.

### Edit Block A -- CopyEngineTests.cs (~line 10576-10582)

**Method**: `T_B189_T4_01_Quick2tSpinnerLabel_MatchesQuickT1Default`

Old (lines 10578-10581):
```csharp
            // Pure formula test: FormatBuffer("Quick2t", 4) uses same path as default _quickT1=4
            var result = (string)InvokeFormatBuffer("Quick2t", 4);
            Assert.Contains("Quick2t", result);
            Assert.Contains("+4", result);
```

New:
```csharp
            // Pure formula test: FormatBuffer("2x", 4) uses same path as default _quickT1=4
            var result = (string)InvokeFormatBuffer("2x", 4);
            Assert.Contains("2x", result);
            Assert.Contains("+4", result);
```

### Edit Block B -- CopyEngineTests.cs (~line 10584-10590)

**Method**: `T_B189_T4_02_QAll2tLabel_MatchesGlobalQuickAllT1Format`

Old (lines 10587-10589):
```csharp
            // AC14 label format: FormatBuffer("QAll2t", 4) == "QAll2t +4"
            var result = (string)InvokeFormatBuffer("QAll2t", 4);
            Assert.Equal("QAll2t +4", result);
```

New:
```csharp
            // AC14 label format: FormatBuffer("2x All", 4) == "2x All +4"
            var result = (string)InvokeFormatBuffer("2x All", 4);
            Assert.Equal("2x All +4", result);
```

### Edit Block C -- CopyEngineTests.cs (~line 10643-10652)

**Method**: `T_B189_T4_06_QuickT1Shared_OnQuickUp_UpdatesBothLabels`

Old (lines 10646-10651):
```csharp
            // AC11: same _quickT1 drives both Quick and Quick2t label strings
            int t1 = 5;
            var quickLabel = (string)InvokeFormatBuffer("Quick", t1);
            var quick2tLabel = (string)InvokeFormatBuffer("Quick2t", t1);
            Assert.Contains("+5", quickLabel);
            Assert.Contains("+5", quick2tLabel);
```

New:
```csharp
            // AC11: same _quickT1 drives both 3x and 2x label strings
            int t1 = 5;
            var quickLabel = (string)InvokeFormatBuffer("3x", t1);
            var quick2tLabel = (string)InvokeFormatBuffer("2x", t1);
            Assert.Contains("+5", quickLabel);
            Assert.Contains("+5", quick2tLabel);
```

**Also update class comment** (lines 10561-10563) if they reference old names:

Old:
```
    // B189-T4: Quick2t / QAll2t Spinners -- xUnit [Fact] tests.
    // AC11: Quick2t label driven by _quickT1. AC12: QAll2t Up increments GlobalQuickAllT1.
    // AC13: OnInstr2tClick uses _quickT1 (no hardcoded 4). AC14: OnQuickAllBufferChanged updates _instrQAll2tBtn.
```

New:
```
    // B189-T4: 2x / 2x All Spinners -- xUnit [Fact] tests.
    // AC11: 2x label driven by _quickT1. AC12: 2x All Up increments _quickAllT1.
    // AC13: OnInstr2tClick uses _quickT1 (no hardcoded 4). AC14: OnQuickAllBufferChanged updates _instrQAll2tBtn.
```

> NOTE: If the comment update introduces any ambiguity or the lines do not match exactly, skip the comment-only change and document as deferred (string literals are the mandatory fix; comments are advisory).

### Edit Block D -- B73Tests.cs (~line 183-188)

**Method**: `T_LABEL_01_FormatQuickAllBuffer_AppendsTSuffix`

Old (confirmed via Get-Content):
```csharp
            var result = (string)method.Invoke(null, new object[] { "Quick ALL", 4 })!;
            Assert.Equal("Quick ALL +4t", result);
```

New:
```csharp
            var result = (string)method.Invoke(null, new object[] { "3x All", 4 })!;
            Assert.Equal("3x All +4t", result);
```

### Edit Block E -- B73Tests.cs (~line 201-205)

**Method**: `T_LABEL_03_FormatQuickAllBuffer_ContainsTSuffix`

Old (confirmed via Get-Content):
```csharp
            var result = (string)method.Invoke(null, new object[] { "Quick ALL", 4 })!;
            Assert.Contains("t", result);
```

New:
```csharp
            var result = (string)method.Invoke(null, new object[] { "3x All", 4 })!;
            Assert.Contains("3x All", result);
            Assert.Contains("+4t", result);
```

> NOTE: The old T_LABEL_03 only asserted `Assert.Contains("t", result)` which is a weak assertion (any "t" anywhere). The new assertion is strengthened to `Assert.Contains("3x All", result)` + `Assert.Contains("+4t", result)` for correctness. This is within scope since the old assertion is stale and would pass incorrectly on any string containing "t".

### Edit Block F -- B73Tests.cs (~line 210-215)

**Method**: `T_LABEL_04_FormatQuickAllBuffer_ZeroTicks_ReturnsZeroWithTSuffix`

Old (confirmed via Get-Content):
```csharp
            var result = (string)method.Invoke(null, new object[] { "Quick ALL", 0 })!;
            Assert.Equal("Quick ALL +0t", result);
```

New:
```csharp
            var result = (string)method.Invoke(null, new object[] { "3x All", 0 })!;
            Assert.Equal("3x All +0t", result);
```

### JS Rule Constraints

| Rule | Constraint | Status in this ticket |
|------|-----------|----------------------|
| JS-021 | No lock() | COMPLIANT -- string literal replacements only; no new shared state |
| JS-066 | CYC <= 8 | COMPLIANT -- no new branches added; existing CYC unchanged |
| JS-081 | ASCII-only string literals | COMPLIANT -- replacement strings "3x", "2x", "3x All", "2x All" are 7-bit ASCII |
| JS-001 | No new throw | COMPLIANT -- no throw statements added |
| JS-002 | No new return null | COMPLIANT -- no new return null |
| JS-033 | No async void | COMPLIANT -- no change to method signatures |

### 7-Scan Checklist

```
SCAN-01 CYC: no new if/switch/ternary/loop/catch added. String literal replacements only.
             Existing CYC of T_B189_T4_01, T_B189_T4_02, T_B189_T4_06, T_LABEL_01,
             T_LABEL_03, T_LABEL_04 all remain CYC=1.
             PASS criterion: zero new branches in any edited method.

SCAN-02 lock(): grep "lock(" on each modified line range in CopyEngineTests.cs and B73Tests.cs.
             PASS criterion: 0 new lock() matches on edited lines.

SCAN-03 async void: verify no async void introduced on edited lines.
             PASS criterion: 0 new async void on edited lines.

SCAN-04 return null: verify no return null introduced on edited lines.
             PASS criterion: 0 new return null on edited lines.

SCAN-05 throw new: verify no throw new introduced on edited lines.
             PASS criterion: 0 new throw new on edited lines.

SCAN-06 ASCII: verify replacement strings are 7-bit ASCII.
             Strings: "2x", "3x", "2x All", "3x All", "2x All +4", "3x All +4t", "3x All +0t".
             PASS criterion: no codepoint > 0x7F in any replaced literal.

SCAN-07 JS-033: no change to method signatures; no async void introduced.
             PASS criterion: method signature lines for all 6 edited methods unchanged.

POST-GATE: dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --no-build
             PASS criterion: 0 failures. Named tests T_B189_T4_01, T_B189_T4_02,
             T_B189_T4_06, T_LABEL_01, T_LABEL_03, T_LABEL_04 must individually PASS.
             Document exact dotnet test output in completion artifact.
```

### Acceptance Criteria

- [ ] Edit block A applied: `InvokeFormatBuffer("Quick2t", 4)` -> `InvokeFormatBuffer("2x", 4)` and `Assert.Contains("Quick2t", result)` -> `Assert.Contains("2x", result)` in T_B189_T4_01
- [ ] Edit block B applied: `InvokeFormatBuffer("QAll2t", 4)` and `Assert.Equal("QAll2t +4", result)` -> `"2x All"` equivalents in T_B189_T4_02
- [ ] Edit block C applied: `InvokeFormatBuffer("Quick", t1)` -> `InvokeFormatBuffer("3x", t1)` and `InvokeFormatBuffer("Quick2t", t1)` -> `InvokeFormatBuffer("2x", t1)` in T_B189_T4_06
- [ ] Edit block D applied: `"Quick ALL", 4` -> `"3x All", 4` and `"Quick ALL +4t"` -> `"3x All +4t"` in T_LABEL_01
- [ ] Edit block E applied: `"Quick ALL", 4` -> `"3x All", 4`; assertion strengthened in T_LABEL_03
- [ ] Edit block F applied: `"Quick ALL", 0` -> `"3x All", 0` and `"Quick ALL +0t"` -> `"3x All +0t"` in T_LABEL_04
- [ ] `dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --no-build` returns 0 failures
- [ ] All 7 scans PASS (document results in completion artifact)
- [ ] Exact dotnet test output recorded in completion artifact (test count, elapsed, pass/fail)

---

## T3 -- Spec HTML Update

### File Modified
`specs/002-trade-copier-spec.html`

### Background
Production changes from B193-DIRECT-2 and B193-DIRECT-3 are on `main`. The spec HTML must be updated to reflect: button renames, the B193 block history row, InstrumentDefaults table state (from B192), per-panel _quickAllT1 AC, and open/closed DW cards.

### Section 1 -- Button Label Rename Table

Locate the button label reference table in the spec. Update 4 rows to reflect B193-DIRECT-3:

| Old Label (find) | New Label (replace with) |
|-----------------|--------------------------|
| `Quick` | `3x` |
| `Quick2t` | `2x` |
| `Quick ALL` | `3x All` |
| `QAll2t` | `2x All` |

> NOTE: Apply replacements only within the button label table cells. Do NOT perform a global find/replace that would alter historical references or comments describing the old names.

### Section 2 -- Block History Table

Add the following row to the block history table (insert as the newest/last entry):

```html
<tr>
  <td>B193</td>
  <td>2026-10-06</td>
  <td>Formalize B193-DIRECT-1/2/3: per-panel _quickAllT1 field, singleton removed from Quick ALL display path, button labels renamed (Quick->3x, Quick2t->2x, Quick ALL->3x All, QAll2t->2x All)</td>
</tr>
```

### Section 3 -- InstrumentDefaults Table Verification

Verify the InstrumentDefaults table contains all 5 rows (added by B192 formalization). If any row is absent, add it:

| Instrument | t1 | t2 |
|-----------|----|----|
| ES | 4 | 8 |
| NQ | 4 | 8 |
| MNQ | 4 | 8 |
| GC | 10 | 20 |
| MGC | 10 | 20 |

If the table does not exist, create it in the InstrumentDefaults section with the above rows.

### Section 4 -- SetInstrument() AC Table

In the SetInstrument() acceptance criteria table, add the following AC row:

```
AC: When SetInstrument(instr) is called, _quickAllT1 is set to
    InstrumentDefaults.GetQuickTicks(instr.MasterInstrument.Name).t1.
    The Quick ALL button (_quickAllBtn) displays
    FormatQuickAllBuffer("3x All", _quickAllT1).
    The 2x All button (_instrQAll2tBtn) displays
    FormatBuffer("2x All", _quickAllT1).
    This is independent of CopyEngine.Instance.GlobalQuickAllT1 singleton.
```

### Section 5 -- DW-B192-SINST-01 Open Card

In the open DW cards section of the spec, add:

```
DW-B192-SINST-01 (P2, OPEN): SetInstrument() in TradeCopierPanel.cs is at CYC=8
(JS-066 ceiling). Any branch addition to SetInstrument() without prior helper extraction
is an automatic P1 JS-066 violation. Engineers modifying SetInstrument() MUST extract
a helper method first.
```

### Section 6 -- DW-B192-QALL-RESET-01 Close (Conditional)

Search the spec for `DW-B192-QALL-RESET-01`. If found as OPEN:
- Change its status to CLOSED
- Add resolution note: `Closed by B193-DIRECT-2: per-panel _quickAllT1 design eliminates the cross-panel reset problem. Each panel maintains its own Quick ALL tick value.`

If the card is not found, document this in the completion artifact (not a blocking failure).

### JS Rule Constraints

| Rule | Constraint | Status in this ticket |
|------|-----------|----------------------|
| JS-081 | ASCII-only text | COMPLIANT -- all new HTML text content uses 7-bit ASCII characters only |
| All other JS rules | Not applicable to HTML files | N/A |

### 7-Scan Checklist

```
SCAN-01 CYC: N/A -- HTML file has no cyclomatic complexity.

SCAN-02 lock(): N/A -- HTML file.

SCAN-03 async void: N/A -- HTML file.

SCAN-04 return null: N/A -- HTML file.

SCAN-05 throw new: N/A -- HTML file.

SCAN-06 ASCII: visual scan of all newly added HTML text content.
             New text added: "B193", "2026-10-06", "3x", "2x", "3x All", "2x All",
             "Formalize B193-DIRECT-1/2/3", "_quickAllT1", "DW-B192-SINST-01",
             "SetInstrument()", "CYC=8" -- all 7-bit ASCII.
             PASS criterion: no codepoint > 0x7F in any newly added text node or attribute.

SCAN-07 JS-033: N/A -- HTML file.

POST-GATE: Manual review of all 6 spec sections against the Section requirements above.
             Each updated section must be reviewed and confirmed present before VERIFY_PASS.
             Document section-by-section review result in completion artifact.
```

### Acceptance Criteria

- [ ] Section 1: Button label table updated -- Quick->3x, Quick2t->2x, Quick ALL->3x All, QAll2t->2x All (within table cells only; historical references preserved)
- [ ] Section 2: B193 block history row added with correct date (2026-10-06) and description
- [ ] Section 3: InstrumentDefaults table verified to contain ES, NQ, MNQ, GC, MGC rows; missing rows added if absent
- [ ] Section 4: SetInstrument() AC row added covering per-panel _quickAllT1 initialization and button display independence from singleton
- [ ] Section 5: DW-B192-SINST-01 open card added with P2 priority and CYC=8 ceiling warning
- [ ] Section 6: DW-B192-QALL-RESET-01 -- if found OPEN, marked CLOSED with resolution note; if not found, absence documented in completion artifact
- [ ] All 7 scans PASS (document in completion artifact, N/A where applicable)
- [ ] Spec HTML remains valid (well-formed HTML; no unclosed tags introduced)

---

## Deferred Work Register (Carry-Forward)

All open DW cards from the B193 architecture plan must be acknowledged by ptt-engineer at T3 close:

| DW ID | Priority | Status | Description |
|-------|----------|--------|-------------|
| DW-B183-03 | P1 | OPEN | IsLaneEEligible CYC=8 frozen. Any branch addition without prior helper extraction = P1 JS-066 violation. (Carried from B183; unmodified by B193) |
| DW-B192-SINST-01 | P2 | OPEN | SetInstrument() in TradeCopierPanel.cs at CYC=8 ceiling. Engineers modifying SetInstrument() MUST extract a helper first. (Opened by B193; documented in spec by T3) |
| DW-B193-01 | P3 | OPEN | OnQuickAllBufferChanged (~line 1027-1029) still uses old label strings "Quick ALL"/"QAll2t". Low severity: display issue only on singleton-sink callback path since B193-DIRECT-2 moved display to per-panel. Deferred to future block. |

---

## Ticket Ordering Summary

| Order | Ticket | File(s) | Gate | Pre-condition |
|-------|--------|---------|------|---------------|
| 1 | T1 -- New B193Tests.cs | src/PropTraderTools/Tests/B193Tests.cs | `dotnet test --no-build` -- 10 new PASS | None |
| 2 | T2 -- Fix stale assertions | CopyEngineTests.cs + B73Tests.cs | `dotnet test --no-build` -- 0 failures full suite | T1 green |
| 3 | T3 -- Spec HTML update | specs/002-trade-copier-spec.html | Manual 6-section review | T2 green |

**Sequential gate rule**: T2 MUST NOT start until T1 dotnet test passes. T3 MUST NOT start until T2 dotnet test passes.

---

*Written by: ptt-architect*
*Epic: B193*
*Phase: 3 (Ticket Generation)*
*Status: TICKETS_COMPLETE*
*Plan source: docs/brain/B193/02-architecture-plan.md (REVIEW_PASS 2026-10-06)*
