# B214 Lane B -- Architecture Plan
## Phase 1 -- Architecture (ptt-architect)
## Status: PLAN_COMPLETE

---

## LANE-SPLIT GATE RESULT: SINGLE-PIPELINE

### Gate Questions and Answers

| Question | Answer | Rationale |
|----------|--------|-----------|
| Q1: Are all fixes in the same method or within 50 lines of each other? | NO | UI-1 label changes are scattered across TradeCopierPanel.cs (callsites at lines 1048, 1059, 1131, 1170, 1187, 1513, 1570 -- span > 500 lines). UI-4+5 are in a separate file entirely. |
| Q2: Does Fix B design depend on Fix A final design? | YES | UI-2 (zero suppression) and UI-3 ("t" suffix) both modify the same two methods (FormatBuffer and FormatGlobalBeBuffer) and must be implemented together to avoid the "0t" artifact. Their combined output contract depends on both being applied simultaneously. |
| Q3: Does each fix have standalone value if the other is blocked? | YES | UI-1 (cosmetic casing), UI-2+3 (UX label quality), UI-4+5 (functional cap raise) each deliver independent user-visible improvements. |
| Q4: Does each fix have an independent SIM verification path? | YES | UI-1 verified by visual button label inspection post-F5. UI-2+3 verified by spinning buffers and observing label changes. UI-4+5 verified by spinning BE All buffer past 10. |

**Gate decision: Q1=NO but Q2=YES -- SINGLE-PIPELINE required. LANES-APPROVED requires NO on BOTH Q1 AND Q2.**

**Ticket consolidation: UI-4 and UI-5 collapse into T2 (same file, same class, 7 lines apart, no standalone value when separated).**

---

## Component List

| Component | File | Scope |
|-----------|------|-------|
| TradeCopierPanel | `src/PropTraderTools/TradeCopierPanel.cs` | UI-1, UI-2, UI-3 |
| PttGlobalBreakEven | `src/PropTraderTools/Features/PttGlobalBreakEven.cs` | UI-4+5 |

**No new files. No new classes. No new methods.**

---

## Ticket Structure (SINGLE-PIPELINE)

```
T1: TradeCopierPanel.cs  -- UI-1 + UI-2 + UI-3  (label casing + zero suppress + t suffix)
T2: PttGlobalBreakEven.cs -- UI-4+5              (buffer cap raise 10 -> 50)
```

Sequencing: T1 before T2. T2 has no dependency on T1.

---

## Ticket T1: TradeCopierPanel.cs -- Label Casing + "t" Suffix + Zero Suppression

### Spec Requirements Satisfied
- UI-1: Button label casing -- "All" not "ALL"
- UI-2: BE / Be All show "0t" at zero instead of nothing (zero suppression)
- UI-3: "t" suffix on ALL buffer numbers

### File
`src/PropTraderTools/TradeCopierPanel.cs`

---

### Change A: UI-1 -- Label casing -- "ALL" -> "All" at 7 callsites

All changes are string literal substitutions at callsites. No method bodies change for UI-1 alone.

| Line | Current | After |
|------|---------|-------|
| 1048 | `FormatGlobalBeBuffer("BE ALL", newBuffer)` | `FormatGlobalBeBuffer("BE All", newBuffer)` |
| 1059 | `FormatQuickAllBuffer("Quick ALL", newT1)` | `FormatQuickAllBuffer("Quick All", newT1)` |
| 1131 | `FormatGlobalBeBuffer("BE ALL", CopyEngine.Instance.GlobalBe.GlobalBeBuffer)` | `FormatGlobalBeBuffer("BE All", ...)` |
| 1170 | `FormatBuffer("Ask-ALL", _askBuffer)` | `FormatBuffer("Ask-All", _askBuffer)` |
| 1187 | `FormatBuffer("Bid-ALL", _bidBuffer)` | `FormatBuffer("Bid-All", _bidBuffer)` |
| 1513 | `FormatBuffer("Ask-ALL", _askBuffer)` (UpdateAskLabels) | `FormatBuffer("Ask-All", _askBuffer)` |
| 1570 | `FormatBuffer("Bid-ALL", _bidBuffer)` (UpdateBidLabels) | `FormatBuffer("Bid-All", _bidBuffer)` |

**Already correct (no change needed):**
- "3x All" (lines 536, 1151, 1949, 1959) -- already title case
- "2x All" (lines 538, 1268, 1951, 1961) -- already title case
- "QAll2t" (line 1061) -- abbreviation, not a display word

---

### Change B: UI-2 + UI-3 -- FormatBuffer body (lines 1328-1331)

**Method signature (unchanged):**
```csharp
private static string FormatBuffer(string name, int ticks)
```

**Current body (CYC=1):**
```csharp
{
    return name + " +" + ticks;
}
```

**New body (CYC=3):**
```csharp
{
    if (ticks == 0)
        return name;
    if (ticks > 0)
        return name + " +" + ticks + "t";
    return name + " " + ticks + "t"; // negative: int.ToString() auto-includes "-"
}
```

**JS compliance:**
- JS-021: no lock. Pure static function, no shared state. ✅
- JS-001: no throw. All paths return string. ✅
- JS-002: never returns null (returns string in all paths). ✅
- ASCII-only: "t" = 0x74. ✅
- CYC=3 (<= 8). ✅

**Comment update at definition site:**
```
// B214 Lane B: added zero-suppress and "t" suffix.
// FormatBuffer("Trim", 0)  -> "Trim"    (zero: no suffix, UI-2)
// FormatBuffer("Trim", 5)  -> "Trim +5t" (positive, UI-3)
// FormatBuffer("Ask", -3)  -> "Ask -3t"  (negative: int.ToString auto-includes "-", UI-3)
// CYC=3 (zero branch, positive branch, negative path).
```

---

### Change C: UI-2 + UI-3 -- FormatGlobalBeBuffer body (lines 1345-1352)

**Method signature (unchanged):**
```csharp
private static string FormatGlobalBeBuffer(string name, int ticks)
```

**Current body (CYC=3):**
```csharp
{
    if (ticks == 0)
        return name;
    if (ticks > 0)
        return name + " +" + ticks;
    return name + " " + ticks;
}
```

**New body (CYC=3, structure unchanged -- only appends "t" to positive and negative paths):**
```csharp
{
    if (ticks == 0)
        return name;
    if (ticks > 0)
        return name + " +" + ticks + "t";
    return name + " " + ticks + "t"; // negative: int.ToString() auto-includes "-"
}
```

**JS compliance:**
- JS-021: no lock. Pure static function. ✅
- JS-001: no throw. ✅
- JS-002: never returns null. ✅
- ASCII-only: "t" = 0x74. ✅
- CYC=3 (unchanged). ✅

**Comment update at definition site:**
```
// B214 Lane B: appended "t" unit suffix to positive and negative paths (UI-3).
// Zero path unchanged (UI-2 zero suppression already correct).
// FormatGlobalBeBuffer("BE All", 0)  -> "BE All"    (zero: name only)
// FormatGlobalBeBuffer("BE All", 3)  -> "BE All +3t"
// FormatGlobalBeBuffer("BE All", -3) -> "BE All -3t"
// CYC=3 (unchanged structure).
```

---

### T1 Method Signatures

No new methods. All changes are to existing private static helpers and string literal callsites.

```csharp
// Modified (body change):
private static string FormatBuffer(string name, int ticks)         // CYC: 1 -> 3
private static string FormatGlobalBeBuffer(string name, int ticks) // CYC: 3 -> 3 (unchanged)

// Unmodified (callsite string literal update only):
private static string FormatQuickAllBuffer(string name, int ticks) // CYC=1, unchanged
private void UpdateAskLabels()                                      // CYC=3, unchanged
private void UpdateBidLabels()                                      // CYC=3, unchanged
private void OnGlobalBeBufferChanged(int newBuffer)                // CYC=2, unchanged
private void OnQuickAllBufferChanged(int newT1)                    // CYC=2, unchanged
private void BuildBufferedButtonsRow(StackPanel root)              // CYC=2, unchanged
```

---

### T1 Affected Existing Tests (MUST UPDATE)

| Test Class | Test Name | Current Assert | New Assert |
|------------|-----------|---------------|------------|
| CopyEngineTests.cs (B189 T2) | T_B189_T2_03_FormatGlobalBeBuffer_NegativeBuffer_NoPlus | "BE -3" | "BE -3t" |
| CopyEngineTests.cs (B189 T2) | T_B189_T2_04_FormatGlobalBeBuffer_Zero_NameOnly | "BE" | "BE" (unchanged) |
| CopyEngineTests.cs (B189 T2) | T_B189_T2_05_FormatGlobalBeBuffer_Positive_WithPlus | "BE +3" | "BE +3t" |
| Tests/B73Tests.cs | T_BUF_BE_01_FormatGlobalBeBuffer_WithPositiveTicks_ReturnsCorrectLabel | positive label without t | add "t" |
| Tests/B73Tests.cs | T_LABEL_02_FormatGlobalBeBuffer_WithFiveTicks_ReturnsCorrectLabel | positive label without t | add "t" |
| Tests/B73Tests.cs | T_BUF_BE_02_FormatGlobalBeBuffer_ZeroTicks_ReturnsNameOnly | name only | unchanged |

---

### T1 New xUnit Tests (add to B214Tests.cs)

```csharp
// FormatBuffer new behavior -- UI-2 (zero) + UI-3 ("t" suffix)

[Fact]
public void T_B214_T1_01_FormatBuffer_ZeroTicks_ReturnsNameOnly()
// FormatBuffer("Trim", 0) returns "Trim" (zero suppress -- no "+0t")
// Assert.Equal("Trim", result)

[Fact]
public void T_B214_T1_02_FormatBuffer_PositiveTicks_AppendsTSuffix()
// FormatBuffer("Trim", 5) returns "Trim +5t"
// Assert.Equal("Trim +5t", result)

[Fact]
public void T_B214_T1_03_FormatBuffer_NegativeTicks_AppendsTSuffix()
// FormatBuffer("Ask", -3) returns "Ask -3t"
// Assert.Equal("Ask -3t", result)

[Fact]
public void T_B214_T1_04_FormatGlobalBeBuffer_PositiveTicks_AppendsTSuffix()
// FormatGlobalBeBuffer("BE All", 3) returns "BE All +3t"
// Assert.Equal("BE All +3t", result)

[Fact]
public void T_B214_T1_05_FormatGlobalBeBuffer_NegativeTicks_AppendsTSuffix()
// FormatGlobalBeBuffer("BE All", -3) returns "BE All -3t"
// Assert.Equal("BE All -3t", result)

[Fact]
public void T_B214_T1_06_FormatGlobalBeBuffer_ZeroTicks_ReturnsNameOnly()
// FormatGlobalBeBuffer("BE All", 0) returns "BE All" (no suffix)
// Assert.Equal("BE All", result)
```

Note: FormatBuffer and FormatGlobalBeBuffer are `private static` -- use Reflection to invoke:
```csharp
var method = typeof(TradeCopierPanel)
    .GetMethod("FormatBuffer", BindingFlags.NonPublic | BindingFlags.Static);
var result = (string)method.Invoke(null, new object[] { "Trim", 5 });
```

---

### T1 SCAN-01..07 Checklist

- SCAN-01 (lock ban): No lock() in any modified line. FormatBuffer and FormatGlobalBeBuffer are pure static methods with no shared state. ✅
- SCAN-02 (async void ban): No async keyword on any modified method. ✅
- SCAN-03 (return null ban in non-null context): FormatBuffer and FormatGlobalBeBuffer return string literals in all paths. "return name" returns the (string) parameter, which is always non-null at callsites (literals). ✅
- SCAN-04 (DateTime.Now ban): No time operations. ✅
- SCAN-05 (ASCII-only): All new string content is ASCII. "t"=0x74, "All"=0x41 0x6C 0x6C. No Unicode. ✅
- SCAN-06 (CYC <= 8): FormatBuffer: CYC 1→3. FormatGlobalBeBuffer: CYC=3 (unchanged). All other methods untouched. All <= 8. ✅
- SCAN-07 (single-hunk ban + Guardian script): Changes are spread across multiple distinct hunks (7 callsite changes + 2 method body changes). Not a single-hunk full-file replace. Run `verify_critical_symbols.ps1` after edit. ✅

---

### T1 Acceptance Criteria

| AC | Criterion |
|----|-----------|
| AC-1 | BE All button label shows "BE All" (not "BE ALL") after F5 |
| AC-2 | Quick All button label shows "Quick All" (not "Quick ALL") |
| AC-3 | Ask-All button label shows "Ask-All" (not "Ask-ALL") |
| AC-4 | Bid-All button label shows "Bid-All" (not "Bid-ALL") |
| AC-5 | Trim button at zero buffer shows "Trim" (not "Trim +0" or "Trim +0t") |
| AC-6 | Flatten button at zero buffer shows "Flatten" (not "Flatten +0") |
| AC-7 | Trim button at 5 ticks shows "Trim +5t" |
| AC-8 | BE button at 3 ticks shows "BE +3t" |
| AC-9 | BE All button at 3 ticks shows "BE All +3t" |
| AC-10 | BE All button at zero shows "BE All" (no "0t") |
| AC-11 | Existing tests T_B189_T2_03 and T_B189_T2_05 updated and pass |
| AC-12 | New T1 tests T_B214_T1_01..06 all pass |

---

## Ticket T2: PttGlobalBreakEven.cs -- Buffer Cap Raise (UI-4 + UI-5 collapsed)

### Spec Requirements Satisfied
- UI-4: Be All buffer cap raised from 10 to 50
- UI-5: Be All buffer cap raised from 10 to 50 (second enforcement site)

**Collapsed rationale:** Both changes are in the same file, same class (PttGlobalBreakEven), 7 lines apart (lines 18 and 25). There is no standalone value in splitting them -- IncrementBuffer ceiling and DecrementBuffer floor are always changed together to maintain symmetric bounds.

### File
`src/PropTraderTools/Features/PttGlobalBreakEven.cs`

---

### Change A: UI-4 -- IncrementBuffer ceiling (line 18)

**Current:**
```csharp
internal void IncrementBuffer()
{
    if (_globalBeBuffer < 10)
        _globalBeBuffer++;
    CopyEngine.Instance.RaiseBeBufferChanged(_globalBeBuffer);
}
```

**New:**
```csharp
internal void IncrementBuffer()
{
    if (_globalBeBuffer < 50) // B214 Lane B: raised from 10 to 50
        _globalBeBuffer++;
    CopyEngine.Instance.RaiseBeBufferChanged(_globalBeBuffer);
}
```

### Change B: UI-5 -- DecrementBuffer floor (line 25)

**Current:**
```csharp
internal void DecrementBuffer()
{
    if (_globalBeBuffer > -10)
        _globalBeBuffer--;
    CopyEngine.Instance.RaiseBeBufferChanged(_globalBeBuffer);
}
```

**New:**
```csharp
internal void DecrementBuffer()
{
    if (_globalBeBuffer > -50) // B214 Lane B: raised from -10 to -50
        _globalBeBuffer--;
    CopyEngine.Instance.RaiseBeBufferChanged(_globalBeBuffer);
}
```

---

### T2 Method Signatures

No new methods. Both changes are one-literal edits in existing methods.

```csharp
internal void IncrementBuffer() // CYC=2, unchanged
internal void DecrementBuffer() // CYC=2, unchanged
```

---

### T2 Affected Existing Tests (MUST UPDATE)

| Test Class | Test Name | Current Assert | New Assert |
|------------|-----------|---------------|------------|
| Tests/B74LaneCTests.cs | GlobalBeBuffer_ReflectionSet_AtCeiling_ReturnsTen | 10 | 50 |
| Tests/B74LaneCTests.cs | GlobalBeBuffer_ReflectionSet_AtFloor_ReturnsNegTen | -10 | -50 |

Note: B74LaneCTests.cs is gitignored (read returns Access denied). Engineer must open this file directly in the NT8 project to update the test assertions. The tests set the private field via Reflection then check the cap guard -- the cap guard in the methods is what we're changing.

---

### T2 New xUnit Tests (add to B214Tests.cs)

```csharp
[Fact]
public void T_B214_T2_01_IncrementBuffer_AtNewCeiling_DoesNotExceed50()
// Arrange: Reflection-set _globalBeBuffer = 50
// Act: gbe.IncrementBuffer()
// Assert: gbe.GlobalBeBuffer == 50 (stayed at ceiling)

[Fact]
public void T_B214_T2_02_DecrementBuffer_AtNewFloor_DoesNotGoBelow_50()
// Arrange: Reflection-set _globalBeBuffer = -50
// Act: gbe.DecrementBuffer()
// Assert: gbe.GlobalBeBuffer == -50 (stayed at floor)

[Fact]
public void T_B214_T2_03_IncrementBuffer_From49_Reaches50()
// Arrange: Reflection-set _globalBeBuffer = 49
// Act: gbe.IncrementBuffer()
// Assert: gbe.GlobalBeBuffer == 50
```

Pattern (PttGlobalBreakEven is internal -- use Reflection for _globalBeBuffer field):
```csharp
var gbe = new PttGlobalBreakEven();
var fi = typeof(PttGlobalBreakEven)
    .GetField("_globalBeBuffer", BindingFlags.NonPublic | BindingFlags.Instance);
fi.SetValue(gbe, 50);
gbe.IncrementBuffer();
Assert.Equal(50, gbe.GlobalBeBuffer);
```

---

### T2 SCAN-01..07 Checklist

- SCAN-01 (lock ban): `_globalBeBuffer` is `volatile int`. No lock anywhere. ✅
- SCAN-02 (async void ban): No async in IncrementBuffer or DecrementBuffer. ✅
- SCAN-03 (return null ban): Both methods are void. ✅
- SCAN-04 (DateTime.Now ban): No time operations. ✅
- SCAN-05 (ASCII-only): No string changes. Literals 50 and -50 are integers. ✅
- SCAN-06 (CYC <= 8): IncrementBuffer CYC=2 (unchanged). DecrementBuffer CYC=2 (unchanged). ✅
- SCAN-07 (single-hunk ban + Guardian): 2 discrete one-line edits in a 30-line file. NOT a single-hunk full-file replace. Run `verify_critical_symbols.ps1` after edit. ✅

---

### T2 Acceptance Criteria

| AC | Criterion |
|----|-----------|
| AC-1 | BE All buffer spinner increments from 10 to 50 without stopping at 10 |
| AC-2 | BE All buffer spinner decrements from -10 to -50 without stopping at -10 |
| AC-3 | BE All buffer stops at exactly 50 (does not exceed) |
| AC-4 | BE All buffer stops at exactly -50 (does not go below) |
| AC-5 | Existing cap tests in B74LaneCTests.cs updated to expect 50/-50 and pass |
| AC-6 | New T2 tests T_B214_T2_01..03 all pass |

---

## NinjaTrader 8 API Usage

**None.** No NT8 API is called in any modified method. All changes are:
- String literal substitutions in WPF UI-thread-only contexts
- Pure static string formatting method bodies
- Integer literal changes in a thin buffer-holder class

---

## Threading Model

| Change | Thread Context | Dispatcher.InvokeAsync required? |
|--------|---------------|----------------------------------|
| FormatBuffer callsites in BuildBufferedButtonsRow | UI constructor (UI thread) | No |
| FormatBuffer callsites in UpdateAskLabels/UpdateBidLabels | Called from WPF handlers (UI thread) | No |
| FormatBuffer callsite in OnQuickAllBufferChanged | Already in Dispatcher.InvokeAsync lambda | No (already wrapped) |
| FormatGlobalBeBuffer callsite in OnGlobalBeBufferChanged | Already in Dispatcher.InvokeAsync lambda | No (already wrapped) |
| PttGlobalBreakEven.IncrementBuffer/DecrementBuffer | Called from WPF RoutedEventHandlers (UI thread) | No |

---

## Jane Street Compliance Notes Per Change

| Change | JS Rule | Status |
|--------|---------|--------|
| FormatBuffer body change | JS-021 (lock ban) | PASS -- pure static, no shared state |
| FormatBuffer body change | JS-001 (no throw in hot path) | PASS -- no throw |
| FormatBuffer body change | JS-002 (no return null) | PASS -- returns string literals |
| FormatBuffer body change | JS-066 (CYC <= 8) | PASS -- CYC=3 |
| String literal callsites | ASCII-only | PASS -- "All" = 0x41 0x6C 0x6C |
| PttGlobalBreakEven cap | JS-021 (lock ban) | PASS -- volatile int, no lock |
| PttGlobalBreakEven cap | JS-033 (no async void) | PASS -- synchronous void |
| FormatGlobalBeBuffer body | JS-066 (CYC <= 8) | PASS -- CYC=3 (unchanged) |

---

## Deferred Backlog Impact

No open deferred items from B214/06-deferred-backlog.md are addressed or affected by these changes.

Open items carry forward unchanged:
- DW-B214-01 (CYC counting methodology) -- not affected
- DW-B214-02 (24 pre-existing test failures) -- not affected; new tests T_B214_T1_* and T_B214_T2_* are in new B214Tests.cs and do not touch pre-existing failures

---

## Summary

| Ticket | File | Changes | CYC Impact | Tests Updated | New Tests |
|--------|------|---------|------------|---------------|-----------|
| T1 | TradeCopierPanel.cs | 7 string literal callsites + 2 method bodies | FormatBuffer: 1→3, others unchanged | 5 existing | 6 new |
| T2 | PttGlobalBreakEven.cs | 2 integer literals | Unchanged (CYC=2) | 2 existing | 3 new |

---

*Architecture plan produced by ptt-architect (B214 Lane B, Phase 1).*
*All 8 required sequential thinking thoughts completed before writing this file.*
