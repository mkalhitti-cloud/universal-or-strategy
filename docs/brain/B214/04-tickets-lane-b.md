# B214 Lane B -- Tickets
## Phase 3 -- Ticket Generation (ptt-architect)
## Status: TICKETS_COMPLETE

Source plan: `docs/brain/B214/02-architecture-plan-lane-b.md` (REVIEW_PASS)
Pipeline: SINGLE-PIPELINE. Execute T1 before T2. T2 has no dependency on T1.

---

## TICKET T1: TradeCopierPanel.cs -- Label Casing + Zero Suppression + "t" Suffix

**Spec Requirements:** UI-1, UI-2, UI-3
**File:** `src/PropTraderTools/TradeCopierPanel.cs`
**New tests file:** `src/PropTraderTools/Tests/B214Tests.cs` (create if absent)

---

### Context (read before touching any code)

TradeCopierPanel.cs contains two private static formatting helpers and several callsites that pass string literals for button labels. Three independent cosmetic/UX bugs must be fixed together in this ticket:

- **UI-1:** Button labels show "ALL" (all-caps). The correct casing is "All" (title case). Affects 7 string literals at callsites -- no method body changes required for UI-1 alone.
- **UI-2 + UI-3:** `FormatBuffer` currently returns `"Trim +0"` at zero ticks (ugly) and omits the "t" tick-unit suffix on all values (ambiguous). The correct behaviors are: zero ticks → return the label name only (suppress "+0"); non-zero ticks → append "t" unit suffix (e.g. `"Trim +5t"`, `"Ask -3t"`).
- `FormatGlobalBeBuffer` already has a correct zero-suppress branch; it just needs the "t" suffix appended to its positive and negative paths.

**UI-2 and UI-3 must be applied together** in a single edit pass. Applying only one produces the defect artifact `"0t"` (FormatGlobalBeBuffer would show "BE All 0t" if only "t" suffix is added without the zero branch).

---

### Change A: UI-1 -- String literal casing at 7 callsites

No method body changes. Edit only the string literals at these exact lines:

| Line | Current string literal | Replace with |
|------|------------------------|--------------|
| 1048 | `"BE ALL"` | `"BE All"` |
| 1059 | `"Quick ALL"` | `"Quick All"` |
| 1131 | `"BE ALL"` | `"BE All"` |
| 1170 | `"Ask-ALL"` | `"Ask-All"` |
| 1187 | `"Bid-ALL"` | `"Bid-All"` |
| 1513 | `"Ask-ALL"` | `"Ask-All"` |
| 1570 | `"Bid-ALL"` | `"Bid-All"` |

**Do NOT change any of the following** (they are already correct):
- `"3x All"` at lines 536, 1151, 1949, 1959
- `"2x All"` at lines 538, 1268, 1951, 1961
- `"QAll2t"` at line 1061 (abbreviation, not a display word)
- `"BE"` (single word, no "All" suffix) at lines 1121, 1591

---

### Change B: UI-2 + UI-3 -- FormatBuffer body (lines 1326-1331)

**Method location:** Lines 1328-1331 in `src/PropTraderTools/TradeCopierPanel.cs`

**Method signature (unchanged):**
```csharp
private static string FormatBuffer(string name, int ticks)
```

**Current body (lines 1329-1331, CYC=1):**
```csharp
{
    return name + " +" + ticks;
}
```

**Replace body with (CYC=3):**
```csharp
{
    if (ticks == 0)
        return name;
    if (ticks > 0)
        return name + " +" + ticks + "t";
    return name + " " + ticks + "t"; // negative: int.ToString() auto-includes "-"
}
```

**Replace the comment block at lines 1326-1327 with:**
```csharp
// B214 Lane B: added zero-suppress and "t" suffix. CYC=3.
// FormatBuffer("Trim", 0)  -> "Trim"    (zero: no suffix, UI-2)
// FormatBuffer("Trim", 5)  -> "Trim +5t" (positive, UI-3)
// FormatBuffer("Ask", -3)  -> "Ask -3t"  (negative: int.ToString auto-includes "-", UI-3)
```

**CYC analysis:** 3 paths: `ticks==0` (return name), `ticks>0` (return positive format), fall-through (return negative format). CYC = 1 base + 2 decision points = 3. Compliant (limit 8).

---

### Change C: UI-2 + UI-3 -- FormatGlobalBeBuffer body (lines 1345-1352)

**Method location:** Lines 1345-1352 in `src/PropTraderTools/TradeCopierPanel.cs`

**Method signature (unchanged):**
```csharp
private static string FormatGlobalBeBuffer(string name, int ticks)
```

**Current body (lines 1346-1352, CYC=3):**
```csharp
{
    if (ticks == 0)
        return name;
    if (ticks > 0)
        return name + " +" + ticks;
    return name + " " + ticks; // int.ToString() of negative auto-includes "-"
}
```

**Replace body with (CYC=3, structure unchanged -- only append "t" to non-zero paths):**
```csharp
{
    if (ticks == 0)
        return name;
    if (ticks > 0)
        return name + " +" + ticks + "t";
    return name + " " + ticks + "t"; // negative: int.ToString() auto-includes "-"
}
```

**Replace the comment block at lines 1341-1344 with:**
```csharp
// B214 Lane B: appended "t" unit suffix to positive and negative paths (UI-3).
// Zero path unchanged (UI-2 zero suppression already correct).
// FormatGlobalBeBuffer("BE All", 0)  -> "BE All"    (zero: name only)
// FormatGlobalBeBuffer("BE All", 3)  -> "BE All +3t"
// FormatGlobalBeBuffer("BE All", -3) -> "BE All -3t"
// CYC=3 (unchanged structure).
```

**CYC analysis:** Structure unchanged from current body. CYC remains 3. Compliant (limit 8).

---

### Existing Tests to Update

These tests assert on the OLD string formats and will FAIL after Changes B and C. The engineer MUST update them before committing.

**File: `src/PropTraderTools/CopyEngineTests.cs`**

| Line | Test name | Current `Assert.Equal` | Update to |
|------|-----------|------------------------|-----------|
| 10494 | `T_B189_T2_03_FormatGlobalBeBuffer_NegativeBuffer_NoPlus` | `Assert.Equal("BE -3", result)` | `Assert.Equal("BE -3t", result)` |
| 10514 | `T_B189_T2_05_FormatGlobalBeBuffer_Positive_WithPlus` | `Assert.Equal("BE +3", result)` | `Assert.Equal("BE +3t", result)` |
| 10504 | `T_B189_T2_04_FormatGlobalBeBuffer_Zero_NameOnly` | `Assert.Equal("BE", result)` | **No change needed** (zero path unchanged) |

**File: `src/PropTraderTools/Tests/B73Tests.cs`** *(gitignored -- open directly in the NT8 project folder)*

These two tests assert on positive `FormatGlobalBeBuffer` returns without the "t" suffix and must be updated:

| Test method | Current assertion | Update to |
|-------------|------------------|-----------|
| `T_BUF_BE_01_FormatGlobalBeBuffer_WithPositiveTicks_ReturnsCorrectLabel` | positive label without "t" | append "t" to expected string |
| `T_LABEL_02_FormatGlobalBeBuffer_WithFiveTicks_ReturnsCorrectLabel` | positive label without "t" | append "t" to expected string |

The zero-ticks test `T_BUF_BE_02_FormatGlobalBeBuffer_ZeroTicks_ReturnsNameOnly` requires no change (zero path returns name only -- unchanged).

---

### New xUnit Tests to Add

Add all six tests to `src/PropTraderTools/Tests/B214Tests.cs`. Create the file if it does not exist.

`FormatBuffer` and `FormatGlobalBeBuffer` are `private static` -- access via Reflection as shown.

```csharp
using System;
using System.Reflection;
using Xunit;

namespace PropTraderTools.Tests
{
    // B214 Lane B -- T1 tests for FormatBuffer and FormatGlobalBeBuffer.
    public class B214T1Tests
    {
        private static MethodInfo GetFormatBuffer() =>
            typeof(TradeCopierPanel)
                .GetMethod("FormatBuffer", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("FormatBuffer method not found");

        private static MethodInfo GetFormatGlobalBeBuffer() =>
            typeof(TradeCopierPanel)
                .GetMethod("FormatGlobalBeBuffer", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("FormatGlobalBeBuffer method not found");

        // UI-2: zero ticks suppresses the "+0" artifact -- returns label name only.
        [Fact]
        public void T_B214_T1_01_FormatBuffer_ZeroTicks_ReturnsNameOnly()
        {
            var result = (string)GetFormatBuffer().Invoke(null, new object[] { "Trim", 0 });
            Assert.Equal("Trim", result);
        }

        // UI-3: positive ticks appends "t" suffix.
        [Fact]
        public void T_B214_T1_02_FormatBuffer_PositiveTicks_AppendsTSuffix()
        {
            var result = (string)GetFormatBuffer().Invoke(null, new object[] { "Trim", 5 });
            Assert.Equal("Trim +5t", result);
        }

        // UI-3: negative ticks appends "t" suffix (int.ToString auto-includes "-").
        [Fact]
        public void T_B214_T1_03_FormatBuffer_NegativeTicks_AppendsTSuffix()
        {
            var result = (string)GetFormatBuffer().Invoke(null, new object[] { "Ask", -3 });
            Assert.Equal("Ask -3t", result);
        }

        // UI-3: FormatGlobalBeBuffer positive ticks appends "t" suffix.
        [Fact]
        public void T_B214_T1_04_FormatGlobalBeBuffer_PositiveTicks_AppendsTSuffix()
        {
            var result = (string)GetFormatGlobalBeBuffer().Invoke(null, new object[] { "BE All", 3 });
            Assert.Equal("BE All +3t", result);
        }

        // UI-3: FormatGlobalBeBuffer negative ticks appends "t" suffix.
        [Fact]
        public void T_B214_T1_05_FormatGlobalBeBuffer_NegativeTicks_AppendsTSuffix()
        {
            var result = (string)GetFormatGlobalBeBuffer().Invoke(null, new object[] { "BE All", -3 });
            Assert.Equal("BE All -3t", result);
        }

        // UI-2: FormatGlobalBeBuffer zero ticks returns name only (no "0t" artifact).
        [Fact]
        public void T_B214_T1_06_FormatGlobalBeBuffer_ZeroTicks_ReturnsNameOnly()
        {
            var result = (string)GetFormatGlobalBeBuffer().Invoke(null, new object[] { "BE All", 0 });
            Assert.Equal("BE All", result);
        }
    }
}
```

---

### 7-Scan Checklist (Engineer Contract -- MANDATORY before VERIFY_PASS)

Run every scan before pushing. All 7 must PASS. A single FAIL = do not push.

**SCAN-1: P0 Jane Street Violations**
```powershell
# Zero results required in all modified lines:
grep -n "lock(" src/PropTraderTools/TradeCopierPanel.cs
grep -n "async void " src/PropTraderTools/TradeCopierPanel.cs
grep -n "return null;" src/PropTraderTools/TradeCopierPanel.cs
```
Expected: 0 matches in lines you modified.
Rationale: `FormatBuffer` and `FormatGlobalBeBuffer` are pure static helpers. No shared state, no async, no nullable return. All paths return a string literal or the `name` parameter (non-null at all callsites where `name` is a string literal).

**SCAN-2: dotnet test -- zero new failures**
```powershell
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --no-build -v minimal 2>&1 | Select-String "Failed|Error|Passed"
```
Expected: The 3 updated existing tests (`T_B189_T2_03`, `T_B189_T2_05`, updated B73 tests) pass. The 6 new `T_B214_T1_*` tests pass. Pre-existing failure count unchanged (24 pre-existing failures are allowed to remain -- do not fix or disturb them).

**SCAN-3: CYC <= 8 for all modified methods**
Verify by inspection (count decision points):
- `FormatBuffer` after change: 2 `if` statements = CYC 3. ✓
- `FormatGlobalBeBuffer` after change: 2 `if` statements = CYC 3 (unchanged). ✓
All other methods listed in the callsite table are untouched; their CYC is unchanged.

**SCAN-4: ASCII-only compliance**
New string content introduced: `"t"` (0x74), `"All"` (0x41 0x6C 0x6C), arithmetic operator `+` (0x2B), space (0x20).
```powershell
# Confirm no non-ASCII bytes introduced in the changed lines:
[System.IO.File]::ReadAllBytes("src/PropTraderTools/TradeCopierPanel.cs") |
    Where-Object { $_ -gt 0x7E } | Measure-Object
```
Expected: count unchanged from pre-edit baseline (no new high bytes introduced by this ticket).

**SCAN-5: Symbol existence -- all modified methods present with correct signatures**
After edit, confirm these exact signatures exist in the file:
```
private static string FormatBuffer(string name, int ticks)
private static string FormatGlobalBeBuffer(string name, int ticks)
```
```powershell
Select-String "private static string FormatBuffer" src/PropTraderTools/TradeCopierPanel.cs
Select-String "private static string FormatGlobalBeBuffer" src/PropTraderTools/TradeCopierPanel.cs
```
Expected: 1 match each. No signature renames. No overloads added.

**SCAN-6: Functional correctness per acceptance criteria**

Verify by running the 6 new `T_B214_T1_*` tests, then confirm visually after F5:

| AC | Expected result |
|----|-----------------|
| AC-1 | "BE All" button label (not "BE ALL") |
| AC-2 | "Quick All" button label (not "Quick ALL") |
| AC-3 | "Ask-All" button label (not "Ask-ALL") |
| AC-4 | "Bid-All" button label (not "Bid-ALL") |
| AC-5 | Trim button at 0 buffer shows "Trim" (not "Trim +0" or "Trim +0t") |
| AC-6 | Flatten button at 0 buffer shows "Flatten" (not "Flatten +0") |
| AC-7 | Trim button at 5 ticks shows "Trim +5t" |
| AC-8 | BE button at 3 ticks shows "BE +3t" |
| AC-9 | BE All button at 3 ticks shows "BE All +3t" |
| AC-10 | BE All button at zero shows "BE All" (no "0t") |
| AC-11 | `T_B189_T2_03` and `T_B189_T2_05` updated assertions pass |
| AC-12 | `T_B214_T1_01` through `T_B214_T1_06` all pass |

**SCAN-7: Diff hygiene**

```powershell
# Single-hunk ban: changes span multiple discrete hunks -- must NOT be a single full-file replace.
# Expected: >= 3 hunks (7 callsite edits + Change B body + Change C body).
git diff HEAD -- src/PropTraderTools/TradeCopierPanel.cs | Select-String "^@@" | Measure-Object

# DIFF GUARD: total diff must be < 10,000 characters in src/
powershell -File .\scripts\verify_pr_hygiene.ps1

# Guardian symbols script:
powershell -File .\scripts\verify_critical_symbols.ps1

# ptt-sync hard-link re-sync (mandatory after every .cs edit):
powershell -File .\scripts\ptt-sync.ps1
```
Expected: `(git diff | Select-String "^@@").Count -ge 3`. DIFF GUARD PASS. Guardian PASS. ptt-sync PASS.

---

### Acceptance Criteria (T1)

| AC | Criterion | Verification method |
|----|-----------|---------------------|
| AC-1 | "BE All" button label after F5 | Visual inspection |
| AC-2 | "Quick All" button label after F5 | Visual inspection |
| AC-3 | "Ask-All" button label after F5 | Visual inspection |
| AC-4 | "Bid-All" button label after F5 | Visual inspection |
| AC-5 | Trim at 0 ticks shows "Trim" (not "Trim +0t") | `T_B214_T1_01` |
| AC-6 | Flatten at 0 ticks shows "Flatten" (not "Flatten +0") | `T_B214_T1_01` (same code path) |
| AC-7 | Trim at 5 ticks shows "Trim +5t" | `T_B214_T1_02` |
| AC-8 | BE at 3 ticks shows "BE +3t" | `T_B214_T1_04` (same method, "BE" label) |
| AC-9 | BE All at 3 ticks shows "BE All +3t" | `T_B214_T1_04` |
| AC-10 | BE All at 0 shows "BE All" (no "0t") | `T_B214_T1_06` |
| AC-11 | `T_B189_T2_03` and `T_B189_T2_05` updated and pass | `dotnet test` |
| AC-12 | `T_B214_T1_01` through `T_B214_T1_06` all pass | `dotnet test` |

---
---

## TICKET T2: PttGlobalBreakEven.cs -- Buffer Cap Raise (UI-4 + UI-5)

**Spec Requirements:** UI-4 (IncrementBuffer ceiling 10 → 50), UI-5 (DecrementBuffer floor -10 → -50)
**File:** `src/PropTraderTools/Features/PttGlobalBreakEven.cs`
**New tests:** Add to `src/PropTraderTools/Tests/B214Tests.cs` (use the same file as T1, or create if absent)

**Sequencing:** T2 has no dependency on T1. It may be executed in parallel or after T1.

---

### Context (read before touching any code)

`PttGlobalBreakEven.cs` is a 30-line thin buffer-holder class. It owns a `private volatile int _globalBeBuffer` field and exposes `IncrementBuffer()` / `DecrementBuffer()` for the panel +/- spin UI buttons. Both methods contain a single integer literal that gates the cap:

- `IncrementBuffer` at line 18 guards: `if (_globalBeBuffer < 10)` -- this is the ceiling.
- `DecrementBuffer` at line 25 guards: `if (_globalBeBuffer > -10)` -- this is the floor.

Both literals must be changed together (UI-4 and UI-5) to maintain symmetric bounds. Raising only one would cause asymmetric behavior: the buffer could spin further in one direction than the other.

**Cap constants location:** The cap values `10` and `-10` are integer literals embedded directly in the method bodies at lines 18 and 25. They do NOT live in `PttContracts.cs`. No constant or field needs to be added -- inline comment is sufficient.

---

### Change A: UI-4 -- IncrementBuffer ceiling (line 18)

**File:** `src/PropTraderTools/Features/PttGlobalBreakEven.cs`
**Method signature (unchanged):** `internal void IncrementBuffer()`

**Current line 18:**
```csharp
            if (_globalBeBuffer < 10)
```

**Replace with:**
```csharp
            if (_globalBeBuffer < 50) // B214 Lane B: raised from 10 to 50
```

The rest of `IncrementBuffer` (lines 16-21) is unchanged.

---

### Change B: UI-5 -- DecrementBuffer floor (line 25)

**File:** `src/PropTraderTools/Features/PttGlobalBreakEven.cs`
**Method signature (unchanged):** `internal void DecrementBuffer()`

**Current line 25:**
```csharp
            if (_globalBeBuffer > -10)
```

**Replace with:**
```csharp
            if (_globalBeBuffer > -50) // B214 Lane B: raised from -10 to -50
```

The rest of `DecrementBuffer` (lines 23-28) is unchanged.

---

### Complete Post-Edit File State

After both changes the complete file must look exactly like this (verified by diff -- do not touch any other line):

```csharp
// src/PropTraderTools/Features/PttGlobalBreakEven.cs
// B180 DW-B180-BE-SWAP-ALL: thin buffer holder only.
// Execute(int) removed -- use PttBeSwapAll.Execute(bufferTicks) instead.
// Keeps GlobalBeBuffer, IncrementBuffer, DecrementBuffer for panel +/- spin UI.
// CYC=2 (IncrementBuffer), CYC=2 (DecrementBuffer).
// JS-021: no lock (volatile int). JS-001: no throw. ASCII-only.

namespace PropTraderTools
{
    internal sealed class PttGlobalBreakEven
    {
        private volatile int _globalBeBuffer = 0;

        internal int GlobalBeBuffer => _globalBeBuffer;

        internal void IncrementBuffer()
        {
            if (_globalBeBuffer < 50) // B214 Lane B: raised from 10 to 50
                _globalBeBuffer++;
            CopyEngine.Instance.RaiseBeBufferChanged(_globalBeBuffer);
        }

        internal void DecrementBuffer()
        {
            if (_globalBeBuffer > -50) // B214 Lane B: raised from -10 to -50
                _globalBeBuffer--;
            CopyEngine.Instance.RaiseBeBufferChanged(_globalBeBuffer);
        }
    }
}
```

---

### Existing Tests to Update

Two tests in `src/PropTraderTools/Tests/B74LaneCTests.cs` assert the old cap values. This file is **gitignored** -- the agent tool will return "Access denied". You must open it directly in the NinjaTrader project folder.

**File path (open directly):** `src/PropTraderTools/Tests/B74LaneCTests.cs`

| Line (approx) | Test method | Current assertion | Update to |
|---------------|-------------|------------------|-----------|
| ~69 | `GlobalBeBuffer_ReflectionSet_AtCeiling_ReturnsTen` | `Assert.Equal(10, gbe.GlobalBeBuffer)` | `Assert.Equal(50, gbe.GlobalBeBuffer)` |
| ~84 | `GlobalBeBuffer_ReflectionSet_AtFloor_ReturnsNegTen` | `Assert.Equal(-10, gbe.GlobalBeBuffer)` | `Assert.Equal(-50, gbe.GlobalBeBuffer)` |

These tests set `_globalBeBuffer` to the cap value via Reflection and call the method, then assert the value did not move. The only change needed is the `Assert.Equal` expected value; the Reflection setup code is unchanged.

---

### New xUnit Tests to Add

Add all three tests to `src/PropTraderTools/Tests/B214Tests.cs`. If T1 already created this file, append a new class `B214T2Tests` to it.

`PttGlobalBreakEven` is `internal sealed`. Access `_globalBeBuffer` via Reflection as shown.

```csharp
// B214 Lane B -- T2 tests for PttGlobalBreakEven cap raise.
public class B214T2Tests
{
    private static System.Reflection.FieldInfo GetBufferField() =>
        typeof(PttGlobalBreakEven)
            .GetField("_globalBeBuffer", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("_globalBeBuffer field not found");

    // UI-4: IncrementBuffer at new ceiling 50 does not exceed 50.
    [Fact]
    public void T_B214_T2_01_IncrementBuffer_AtNewCeiling_DoesNotExceed50()
    {
        var gbe = new PttGlobalBreakEven();
        GetBufferField().SetValue(gbe, 50);
        gbe.IncrementBuffer();
        Assert.Equal(50, gbe.GlobalBeBuffer);
    }

    // UI-5: DecrementBuffer at new floor -50 does not go below -50.
    [Fact]
    public void T_B214_T2_02_DecrementBuffer_AtNewFloor_DoesNotGoBelow50()
    {
        var gbe = new PttGlobalBreakEven();
        GetBufferField().SetValue(gbe, -50);
        gbe.DecrementBuffer();
        Assert.Equal(-50, gbe.GlobalBeBuffer);
    }

    // UI-4: IncrementBuffer from 49 reaches 50 (old cap 10 would have blocked at 10).
    [Fact]
    public void T_B214_T2_03_IncrementBuffer_From49_Reaches50()
    {
        var gbe = new PttGlobalBreakEven();
        GetBufferField().SetValue(gbe, 49);
        gbe.IncrementBuffer();
        Assert.Equal(50, gbe.GlobalBeBuffer);
    }
}
```

**Note on `CopyEngine.Instance.RaiseBeBufferChanged`:** `IncrementBuffer` and `DecrementBuffer` call this relay after updating the field. In the test context `CopyEngine.Instance` may be null. If the test runner throws a `NullReferenceException` from `RaiseBeBufferChanged`, wrap the call in a try-catch or initialize a test-mode `CopyEngine` singleton as done in the existing `B74LaneCTests.cs` tests. Check how those tests handle it and replicate the pattern.

---

### 7-Scan Checklist (Engineer Contract -- MANDATORY before VERIFY_PASS)

**SCAN-1: P0 Jane Street Violations**
```powershell
grep -n "lock(" src/PropTraderTools/Features/PttGlobalBreakEven.cs
grep -n "async void " src/PropTraderTools/Features/PttGlobalBreakEven.cs
grep -n "return null;" src/PropTraderTools/Features/PttGlobalBreakEven.cs
```
Expected: 0 matches. Rationale: `_globalBeBuffer` is `volatile int`. No lock anywhere. Both methods are synchronous `void`. No nullable return.

**SCAN-2: dotnet test -- zero new failures**
```powershell
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --no-build -v minimal 2>&1 | Select-String "Failed|Error|Passed"
```
Expected: The 2 updated B74LaneCTests assertions (`ReturnsTen` → expect 50, `ReturnsNegTen` → expect -50) pass. The 3 new `T_B214_T2_*` tests pass. Pre-existing failure count unchanged.

**SCAN-3: CYC <= 8 for all modified methods**
Verify by inspection:
- `IncrementBuffer`: 1 `if` statement = CYC 2 (unchanged). ✓
- `DecrementBuffer`: 1 `if` statement = CYC 2 (unchanged). ✓
No other methods modified.

**SCAN-4: ASCII-only compliance**
No string literals changed. Literals `50` and `-50` are integer values, not strings. The inline comments use ASCII characters only: `B214 Lane B: raised from 10 to 50` (all 0x20-0x7E).
```powershell
[System.IO.File]::ReadAllBytes("src/PropTraderTools/Features/PttGlobalBreakEven.cs") |
    Where-Object { $_ -gt 0x7E } | Measure-Object
```
Expected: count 0.

**SCAN-5: Symbol existence -- all modified methods present with correct signatures**
```powershell
Select-String "internal void IncrementBuffer\(\)" src/PropTraderTools/Features/PttGlobalBreakEven.cs
Select-String "internal void DecrementBuffer\(\)" src/PropTraderTools/Features/PttGlobalBreakEven.cs
Select-String "internal int GlobalBeBuffer" src/PropTraderTools/Features/PttGlobalBreakEven.cs
```
Expected: 1 match each. No renamed symbols. No new overloads.

**SCAN-6: Functional correctness per acceptance criteria**

| AC | Expected result |
|----|-----------------|
| AC-1 | BE All buffer spinner increments from 10 to 50 without stopping at 10 |
| AC-2 | BE All buffer spinner decrements from -10 to -50 without stopping at -10 |
| AC-3 | Stops exactly at 50 (does not exceed) |
| AC-4 | Stops exactly at -50 (does not go below) |
| AC-5 | B74LaneCTests.cs assertions updated to 50 / -50 and pass |
| AC-6 | `T_B214_T2_01` through `T_B214_T2_03` all pass |

Verify AC-1 and AC-2 by SIM: after F5, click BE All + button and observe spinner increments past 10 all the way to 50.

**SCAN-7: Diff hygiene**
```powershell
# Two discrete one-line edits in a 30-line file -- must NOT be a single-hunk full-file replace.
git diff HEAD -- src/PropTraderTools/Features/PttGlobalBreakEven.cs | Select-String "^@@" | Measure-Object
# Expected count: 1 or 2 (two close edits may share a hunk; that is fine -- the ban is on
# a full-file replacement commit, not on hunks merging due to proximity).
# Verify it is NOT a full-file replace by checking hunk header line counts:
git diff HEAD -- src/PropTraderTools/Features/PttGlobalBreakEven.cs | Select-String "^@@"
# Example acceptable: "@@ -16,7 +16,7 @@" and "@@ -23,7 +23,7 @@"

# DIFF GUARD:
powershell -File .\scripts\verify_pr_hygiene.ps1

# Guardian:
powershell -File .\scripts\verify_critical_symbols.ps1

# ptt-sync:
powershell -File .\scripts\ptt-sync.ps1
```
Expected: DIFF GUARD PASS (2 one-character edits = well under 10,000 char limit). Guardian PASS. ptt-sync PASS.

---

### Acceptance Criteria (T2)

| AC | Criterion | Verification method |
|----|-----------|---------------------|
| AC-1 | BE All buffer increments from 10 to 50 without stopping at 10 | SIM inspection after F5 |
| AC-2 | BE All buffer decrements from -10 to -50 without stopping at -10 | SIM inspection after F5 |
| AC-3 | Buffer stops at exactly 50 (does not exceed 50) | `T_B214_T2_01` |
| AC-4 | Buffer stops at exactly -50 (does not go below -50) | `T_B214_T2_02` |
| AC-5 | B74LaneCTests.cs `ReturnsTen`/`ReturnsNegTen` updated to 50/-50 and pass | `dotnet test` |
| AC-6 | `T_B214_T2_01` through `T_B214_T2_03` all pass | `dotnet test` |

---

## Summary

| Ticket | File | Changes | CYC delta | Existing tests updated | New tests |
|--------|------|---------|-----------|------------------------|-----------|
| T1 | `TradeCopierPanel.cs` | 7 string literal callsites + 2 method bodies | FormatBuffer: 1→3; others: 0 | 5 (CopyEngineTests: 2; B73Tests: 2; zero-test: no change) | 6 (`T_B214_T1_01..06`) |
| T2 | `PttGlobalBreakEven.cs` | 2 integer literals | 0 | 2 (B74LaneCTests, gitignored -- open directly) | 3 (`T_B214_T2_01..03`) |

**Pipeline:** SINGLE-PIPELINE. Execute T1 then T2 (T2 has no dependency on T1).
**No new files in src/. No new classes in src/. No new methods in src/.**
**All new tests go into `src/PropTraderTools/Tests/B214Tests.cs`.**

---

*Tickets produced by ptt-architect (B214 Lane B, Phase 3).*
*Source plan: `docs/brain/B214/02-architecture-plan-lane-b.md` (REVIEW_PASS).*
