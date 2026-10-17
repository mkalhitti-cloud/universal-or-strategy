# B193 Architecture Plan

**Epic**: B193 -- Formalize B193-DIRECT-1/2/3: per-panel Quick ALL + button rename
**Author**: ptt-architect
**Date**: 2026-10-06
**Status**: PLAN_COMPLETE
**Preceding backlog**: docs/brain/B191/06-deferred-backlog.md

---

## LANE-SPLIT GATE RESULT

| Question | Answer | Rationale |
|----------|--------|-----------|
| Q1. Same method or within 50 lines? | NO | T1 is a new file; T2 edits different existing test files; T3 is HTML |
| Q2. Fix B design depends on Fix A final design? | NO | T2 string replacements are independent of T1's test structure |
| Q3. Each fix has standalone value if the other is blocked? | YES | T1 adds new B193 coverage; T2 unbreaks existing suite |
| Q4. Each fix has an independent SIM verification path? | YES | T1: dotnet test B193Tests; T2: dotnet test full suite |

**LANE-SPLIT GATE RESULT: LANES-APPROVED**

Execution order: **single sequential pipeline T1 -> T2 -> T3** per Director spec
(tickets share .csproj context and sequential green-gate verification is mandatory).

---

## Epic Summary

B193 is a **formalization epic**. All production .cs changes are already on `main` via:
- **B193-DIRECT-2** (4a63f693): Per-panel `_quickAllT1` field; singleton removed from display path
- **B193-DIRECT-3** (3a098a88): Label rename -- Quick->3x, Quick2t->2x, Quick ALL->3x All, QAll2t->2x All

No new production .cs files are authored by ptt-engineer in this pipeline.
The pipeline formalizes: **tests** (T1), **stale assertion fixes** (T2), **spec update** (T3).

---

## Component Inventory

| Component | File | Status |
|-----------|------|--------|
| B193Tests (new test class) | src/PropTraderTools/Tests/B193Tests.cs | NEW -- T1 |
| B189T4SpinnerTests assertion fixes | src/PropTraderTools/CopyEngineTests.cs | EDIT -- T2 |
| B73LabelTests assertion fixes | src/PropTraderTools/Tests/B73Tests.cs | EDIT -- T2 |
| Spec HTML update | specs/002-trade-copier-spec.html | EDIT -- T3 |

---

## Production Code Reference (already on main, no edits)

### TradeCopierPanel.cs -- key signatures

```csharp
// Line 260 -- per-panel field added by B193-DIRECT-2
private int _quickAllT1 = 4; // default MES: 4t

// Line 1259 -- private static helper (unchanged)
private static string FormatBuffer(string name, int ticks)
// Returns: name + " +" + ticks  (e.g. "3x +10")

// Line 1267 -- private static helper (unchanged, appends "t")
private static string FormatQuickAllBuffer(string name, int ticks)
// Returns: name + " +" + ticks + "t"  (e.g. "3x All +10t")

// Lines 1761-1778 -- per-panel Up/Down spinner handlers (B193-DIRECT-2)
// OnQuickAllUp: _quickAllT1 = Math.Max(1, Math.Min(_quickAllT1 + 1, 100)); updates labels
// OnQuickAllDown: _quickAllT1 = Math.Max(1, Math.Min(_quickAllT1 - 1, 100)); updates labels
// Both use "3x All" / "2x All" label strings (B193-DIRECT-3 applied)
```

### PttGlobalQuickExit.cs -- ResolveQuickTicks (already uses InstrumentDefaults)

```csharp
// Line 289-294 -- B193-DIRECT-2: singleton removed from this path
private static (int t1, int t2) ResolveQuickTicks(Instrument instr)
{
    return InstrumentDefaults.GetQuickTicks(
        instr?.MasterInstrument?.Name ?? string.Empty
    );
}
```

### InstrumentDefaults.GetQuickTicks (already on main from B192)

```csharp
// Static method, no NT8 host required
// Signature: public static (int t1, int t2) GetQuickTicks(string instrumentName)
// Known returns: MGC->(10,20), GC->(10,20), MES->(4,8), ES->(4,8)
// Default (unknown/null): (4, 8)
```

---

## T1 -- New Test File: B193Tests.cs

### File path
`src/PropTraderTools/Tests/B193Tests.cs`

### Test class structure

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
    // Mirrors B189T4SpinnerTests.InvokeFormatBuffer pattern in CopyEngineTests.cs
    private static string InvokeFormatBuffer(string name, int ticks)
    {
        var m = typeof(TradeCopierPanel).GetMethod(
            "FormatBuffer",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        return (string)m.Invoke(null, new object[] { name, ticks });
    }

    // Reflection helper: invokes private static FormatQuickAllBuffer(string name, int ticks)
    // Appends "t" unit suffix (QUICK-LABEL-UNIT-01).
    private static string InvokeFormatQuickAllBuffer(string name, int ticks)
    {
        var m = typeof(TradeCopierPanel).GetMethod(
            "FormatQuickAllBuffer",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        return (string)m.Invoke(null, new object[] { name, ticks });
    }

    [Fact] public void T_B193_01_GetQuickTicks_MGC_Returns10_20() => ...
    [Fact] public void T_B193_02_GetQuickTicks_GC_Returns10_20() => ...
    [Fact] public void T_B193_03_GetQuickTicks_MES_Returns4_8() => ...
    [Fact] public void T_B193_04_GetQuickTicks_ES_Returns4_8() => ...
    [Fact] public void T_B193_05_FormatBuffer_3x_10_Returns3xPlus10() => ...
    [Fact] public void T_B193_06_FormatBuffer_2x_10_Returns2xPlus10() => ...
    [Fact] public void T_B193_07_FormatBuffer_2xAll_10_Returns2xAllPlus10() => ...
    [Fact] public void T_B193_08_FormatQuickAllBuffer_3xAll_10_Returns3xAllPlus10t() => ...
    [Fact] public void T_B193_09_TradeCopierPanel_HasField_quickAllT1() => ...
    [Fact] public void T_B193_10_ResolveQuickTicks_Proxy_MGC_t1_Equals10() => ...
}
```

### Method signatures (all CYC=1)

| Method | Signature | Return | Notes |
|--------|-----------|--------|-------|
| InvokeFormatBuffer | private static string InvokeFormatBuffer(string name, int ticks) | string | Reflection via BindingFlags.NonPublic\|Static |
| InvokeFormatQuickAllBuffer | private static string InvokeFormatQuickAllBuffer(string name, int ticks) | string | Reflection via BindingFlags.NonPublic\|Static |

### Test assertions (exact)

| Test ID | Method under test | Input | Expected |
|---------|-------------------|-------|----------|
| T_B193_01 | InstrumentDefaults.GetQuickTicks | "MGC" | (10, 20) |
| T_B193_02 | InstrumentDefaults.GetQuickTicks | "GC" | (10, 20) |
| T_B193_03 | InstrumentDefaults.GetQuickTicks | "MES" | (4, 8) |
| T_B193_04 | InstrumentDefaults.GetQuickTicks | "ES" | (4, 8) |
| T_B193_05 | InvokeFormatBuffer | ("3x", 10) | "3x +10" |
| T_B193_06 | InvokeFormatBuffer | ("2x", 10) | "2x +10" |
| T_B193_07 | InvokeFormatBuffer | ("2x All", 10) | "2x All +10" |
| T_B193_08 | InvokeFormatQuickAllBuffer | ("3x All", 10) | "3x All +10t" |
| T_B193_09 | typeof(TradeCopierPanel).GetField("_quickAllT1", NonPublic\|Instance) | -- | field != null |
| T_B193_10 | InstrumentDefaults.GetQuickTicks("MGC").t1 | -- | == 10 |

### Post-edit gate
`dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --no-build`
All 10 new tests must pass. Zero pre-existing test regressions.

---

## T2 -- Stale Assertion Fixes

### Scope
Fix test assertions that use old button-label strings.
Production renames already committed by B193-DIRECT-3:
- `Quick` -> `3x`
- `Quick2t` -> `2x`
- `Quick ALL` -> `3x All`
- `QAll2t` -> `2x All`

### File 1: src/PropTraderTools/CopyEngineTests.cs

**Edit block A -- T_B189_T4_01 (~line 10578-10581)**

Old:
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

**Edit block B -- T_B189_T4_02 (~line 10587-10590)**

Old:
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

**Edit block C -- T_B189_T4_06 (~line 10646-10652)**

Old:
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

Also update the test comments at lines 10562-10563 if they reference old label names:
- Line 10562: "AC11: Quick2t label driven by _quickT1" -> "AC11: 2x label driven by _quickT1"
- Line 10563: "AC12: QAll2t Up increments GlobalQuickAllT1" -> "AC12: 2x All Up increments _quickAllT1"

### File 2: src/PropTraderTools/Tests/B73Tests.cs

> NOTE: This file is in the Tests/ directory. Access via CLI `type` or `Get-Content` may be needed
> if read_file is blocked by .bobignore. Use `Get-Content src/PropTraderTools/Tests/B73Tests.cs`
> to verify exact lines before edit.

**Edit block D -- T_LABEL_01 (~line 183-188)**

Old:
```csharp
var result = (string)method.Invoke(null, new object[] { "Quick ALL", 4 })!;
Assert.Equal("Quick ALL +4t", result);
```

New:
```csharp
var result = (string)method.Invoke(null, new object[] { "3x All", 4 })!;
Assert.Equal("3x All +4t", result);
```

**Edit block E -- T_LABEL_03 (~line 201-205)**

Old:
```csharp
var result = (string)method.Invoke(null, new object[] { "Quick ALL", 4 })!;
Assert.Contains("Quick ALL", result);
Assert.Contains("+4t", result);
```

New:
```csharp
var result = (string)method.Invoke(null, new object[] { "3x All", 4 })!;
Assert.Contains("3x All", result);
Assert.Contains("+4t", result);
```

**Edit block F -- T_LABEL_04 (~line 210-215)**

Old:
```csharp
var result = (string)method.Invoke(null, new object[] { "Quick ALL", 0 })!;
Assert.Equal("Quick ALL +0t", result);
```

New:
```csharp
var result = (string)method.Invoke(null, new object[] { "3x All", 0 })!;
Assert.Equal("3x All +0t", result);
```

### Post-edit gate
`dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --no-build`
Zero test failures. T_B189_T4_01, T_B189_T4_02, T_B189_T4_06 must PASS.
T_LABEL_01, T_LABEL_03, T_LABEL_04 in B73Tests.cs must PASS.

---

## T3 -- Spec HTML Update

### File: specs/002-trade-copier-spec.html

#### Section 1 -- Button label table

Find the button label table. Update 4 rows:

| Old Label | New Label |
|-----------|-----------|
| Quick | 3x |
| Quick2t | 2x |
| Quick ALL | 3x All |
| QAll2t | 2x All |

#### Section 2 -- Block history table

Add B193 row:

| Block | Date | Description |
|-------|------|-------------|
| B193 | 2026-10-06 | Formalize B193-DIRECT-1/2/3: per-panel _quickAllT1 field, singleton removed from Quick ALL display path, button labels renamed (Quick->3x, Quick2t->2x, Quick ALL->3x All, QAll2t->2x All) |

#### Section 3 -- InstrumentDefaults table

Verify the following rows are present (from B192 formalization):
- ES: (4, 8)
- NQ: (4, 8)
- MNQ: (4, 8)
- GC: (10, 20)
- MGC: (10, 20)

If any row is missing, add it.

#### Section 4 -- SetInstrument() AC table

Add AC for per-panel _quickAllT1 behavior:

> AC: When SetInstrument(instr) is called, _quickAllT1 is set to InstrumentDefaults.GetQuickTicks(instr.MasterInstrument.Name).t1. The Quick ALL button (_quickAllBtn) displays FormatQuickAllBuffer("3x All", _quickAllT1). The 2x All button (_instrQAll2tBtn) displays FormatBuffer("2x All", _quickAllT1). This is independent of CopyEngine.Instance.GlobalQuickAllT1 singleton.

#### Section 5 -- DW-B192-SINST-01 open card

Add in the open DW cards section:

> DW-B192-SINST-01 (P2, OPEN): SetInstrument() in TradeCopierPanel.cs is at CYC=8 (JS-066 ceiling). Any branch addition to SetInstrument() without prior helper extraction is an automatic P1 JS-066 violation. Engineers modifying SetInstrument() MUST extract a helper method first.

#### Section 6 -- DW-B192-QALL-RESET-01

If this card exists in the spec as OPEN, mark it CLOSED with resolution note:
> Closed by B193-DIRECT-2: per-panel _quickAllT1 design eliminates the cross-panel reset problem. Each panel maintains its own Quick ALL tick value.

---

## Threading Model

No new threading patterns introduced in this epic.

Existing patterns (already on main) remain:
- `TradeCopierPanel.OnQuickAllUp/Down`: `Dispatcher.InvokeAsync` wraps button label updates (lines 1763-1778)
- `TradeCopierPanel.SetInstrument()`: `Dispatcher.InvokeAsync` wraps all label updates (line ~515-525)
- `PttGlobalQuickExit.ResolveQuickTicks`: synchronous static, no dispatcher needed
- `InstrumentDefaults.GetQuickTicks`: pure static function, thread-safe

No `lock()` anywhere. No `ConcurrentQueue` required. No `async void`. JS-021 compliant.

---

## JS Rule Constraints per Ticket

### T1 (B193Tests.cs)
| Rule | Constraint | Applies |
|------|-----------|---------|
| JS-021 | No lock() | Tests use reflection and Assert only -- no shared state |
| JS-001 | No throw in hot paths | [Fact] tests have no throw statements |
| JS-002 | No return null | Test methods return void; helpers return string via cast |
| JS-033 | No async void | All [Fact] methods are synchronous void |
| JS-066 | CYC <= 8 | All test methods: CYC=1. Helpers: CYC=1. |
| ASCII | ASCII-only strings | "3x", "2x", "2x All", "3x All", "MGC", "GC", "MES", "ES" -- all ASCII |

### T2 (assertion string fixes)
| Rule | Constraint | Applies |
|------|-----------|---------|
| JS-021 | No lock() | String literal replacements only |
| JS-066 | CYC <= 8 | No new branches added |
| ASCII | ASCII-only | New strings "3x", "2x", "3x All", "2x All" -- ASCII |

### T3 (spec HTML)
| Rule | Constraint | Applies |
|------|-----------|---------|
| ASCII | ASCII-only text | No Unicode characters in spec HTML strings |
| N/A | All other JS rules do not apply to HTML files | -- |

---

## 7-Scan Checklist Contract (per ticket)

### T1 -- B193Tests.cs

```
SCAN-01 CYC: dotnet-script manual count -- each [Fact] method CYC=1. InvokeFormatBuffer CYC=1. InvokeFormatQuickAllBuffer CYC=1. Total: all <= 8. PASS required.
SCAN-02 lock(): grep "lock(" src/PropTraderTools/Tests/B193Tests.cs -- must return 0 matches.
SCAN-03 async void: grep "async void " src/PropTraderTools/Tests/B193Tests.cs -- must return 0 matches.
SCAN-04 return null: grep "return null;" src/PropTraderTools/Tests/B193Tests.cs -- must return 0 matches.
SCAN-05 throw new: grep "throw new" src/PropTraderTools/Tests/B193Tests.cs -- must return 0 matches.
SCAN-06 ASCII: no Unicode, emoji, or curly quotes in any string literal.
SCAN-07 JS-033: confirm no async void method signatures (non-event-handler). All [Fact] methods: synchronous void. PASS.
```

### T2 -- CopyEngineTests.cs + B73Tests.cs

```
SCAN-01 CYC: no new branches added. Existing CYC unchanged. PASS if no new if/switch/ternary/loop.
SCAN-02 lock(): grep "lock(" on edited lines -- must return 0 new matches.
SCAN-03 async void: no new async void introduced. PASS.
SCAN-04 return null: no new return null. PASS.
SCAN-05 throw new: no new throw new. PASS.
SCAN-06 ASCII: verify replaced strings "3x", "2x", "3x All", "2x All" are ASCII. PASS.
SCAN-07 JS-033: no change to async method signatures. PASS.
POST-GATE: dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --no-build -- 0 failures.
```

### T3 -- specs/002-trade-copier-spec.html

```
SCAN-01 CYC: N/A (HTML file).
SCAN-02 lock(): N/A.
SCAN-03 async void: N/A.
SCAN-04 return null: N/A.
SCAN-05 throw new: N/A.
SCAN-06 ASCII: verify no Unicode in newly added text strings.
SCAN-07 JS-033: N/A.
POST-GATE: manual review of spec sections 1-6 against Section 5 requirements above.
```

---

## Deferred Work Register (Carry-Forward + New)

### Carried Forward from B191

| DW ID | Priority | Description | Status |
|-------|----------|-------------|--------|
| DW-B183-03 | P1 | IsLaneEEligible CYC=8 frozen. Any branch addition to IsLaneEEligible without prior helper extraction is a P1 JS-066 violation. | OPEN (B183-B193, unmodified) |

### New Items Opened by B193

| DW ID | Priority | Description | Status |
|-------|----------|-------------|--------|
| DW-B192-SINST-01 | P2 | SetInstrument() in TradeCopierPanel.cs is at CYC=8 (JS-066 ceiling). Engineers modifying SetInstrument() MUST extract a helper method first. Blocks only future branch additions. | OPEN |
| DW-B193-01 | P3 | OnQuickAllBufferChanged (TradeCopierPanel.cs line 1027-1029) still uses old label strings "Quick ALL" and "QAll2t". The per-panel Up/Down path (lines 1764-1778) correctly uses "3x All"/"2x All". OnQuickAllBufferChanged is the singleton-sink callback path -- since B193-DIRECT-2 moved display to per-panel, this method may be vestigial or rarely triggered. Low severity: visual label shows old name only on singleton path. | OPEN |

---

## Pre-Flight Violations Summary

Zero violations found. All 9 sequential thoughts completed cleanly.

| Check | Result |
|-------|--------|
| lock() anywhere | NONE |
| async void (non-event) | NONE |
| return null | NONE |
| throw new XxxException | NONE |
| DateTime.Now | NONE |
| Hardcoded hex | NONE |
| FontFamily | NONE |
| Non-ASCII strings | NONE |
| CYC > 8 | NONE |

---

## Ticket Ordering Summary

| Order | Ticket | File(s) | Gate |
|-------|--------|---------|------|
| 1 | T1 -- New B193Tests.cs | src/PropTraderTools/Tests/B193Tests.cs | dotnet test -- 10 new tests PASS |
| 2 | T2 -- Fix stale assertions | CopyEngineTests.cs + B73Tests.cs | dotnet test -- 0 failures full suite |
| 3 | T3 -- Spec HTML update | specs/002-trade-copier-spec.html | Manual spec review -- 6 sections updated |

---

*Written by: ptt-architect*
*Epic: B193*
*Phase: 1 (Architecture)*
*Status: PLAN_COMPLETE*
*Supersedes: n/a (first plan for this epic)*
