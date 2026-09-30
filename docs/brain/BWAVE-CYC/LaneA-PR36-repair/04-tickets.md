# BWAVE-CYC Lane A PR #36 Repair -- Implementation Tickets

**Epic**: BWAVE-CYC Lane A Repair (PR #36 blockers)
**Plan**: 02-architecture-plan.md (REVIEW_PASS)
**Branch**: feature/bwave-cyc-lane-a (HEAD: 2270c544)
**Date**: 2026-09-03
**Architect**: ptt-architect
**Status**: TICKETS_COMPLETE

---

## Mandatory Execution Order

A-1 --> A-2 --> A-3 --> A-4 (confirm) --> A-5 (confirm) --> A-6

**Hard constraint**: A-2 MUST complete before A-6 (A-2 removes the misplaced
`FindPositionForInstrument` tests from `CopyEngineTests.cs`; A-6 adds
`TryFindPositionForInstrument` -- if A-6 runs first, line offsets are wrong and
the old tests in the removed block cause a second compilation error).

---

## TICKET A-1

**Ticket ID**: A-1
**Category**: DNA
**Spec requirement IDs**: CodeRabbit CR36-3 (ASCII violation -- buffered button arrows) + Greptile P2 (Unicode escape in string literals)
**File(s) affected**: `src/PropTraderTools/TradeCopierPanel.cs`
**Confirmed line numbers**: 1147, 1153, 1184, 1190, 1226, 1232, 1265, 1271, 1311, 1317, 1350, 1356

**Method signature(s) affected**:
- `BuildBufferedButtonsRow()` -- private, no parameters, return void (the method that creates all 6 arrow-button clusters in the inline block ~lines 1130-1375)

**old_text / new_text pairs** (12 replacements, all in `BuildBufferedButtonsRow`):

| # | Line | old_text | new_text |
|---|------|----------|----------|
| 1 | 1147 | `                Content = "\u25B2",` | `                Content = "^",` |
| 2 | 1153 | `                Content = "\u25BC",` | `                Content = "v",` |
| 3 | 1184 | `                Content = "\u25B2",` | `                Content = "^",` |
| 4 | 1190 | `                Content = "\u25BC",` | `                Content = "v",` |
| 5 | 1226 | `                Content = "\u25B2",` | `                Content = "^",` |
| 6 | 1232 | `                Content = "\u25BC",` | `                Content = "v",` |
| 7 | 1265 | `                Content = "\u25B2",` | `                Content = "^",` |
| 8 | 1271 | `                Content = "\u25BC",` | `                Content = "v",` |
| 9 | 1311 | `                Content = "\u25B2",` | `                Content = "^",` |
| 10 | 1317 | `                Content = "\u25BC",` | `                Content = "v",` |
| 11 | 1350 | `                Content = "\u25B2",` | `                Content = "^",` |
| 12 | 1356 | `                Content = "\u25BC",` | `                Content = "v",` |

**Implementation note**: Because all 12 lines share the same two patterns, the
engineer MAY use `search_and_replace` with `use_regex: false`, replacing
`Content = "\u25B2",` globally with `Content = "^",` -- BUT only within the
line-number range 1130-1400 to avoid touching the pre-existing Director-waivers
at lines 1815, 1831-1832, 1888, 2344, 2350, 2380-2390, 3159, 3164, 3202, 3207.
Alternatively apply 12 targeted `apply_diff` edits. Either approach is acceptable.

**Exact current text at lines 1145-1156 (representative sample; all 12 are identical pattern)**:
```csharp
            var trimUp = new System.Windows.Controls.Primitives.RepeatButton
            {
                Content = "\u25B2",
                Width = 18,
                Height = 12,
            };
            var trimDn = new System.Windows.Controls.Primitives.RepeatButton
            {
                Content = "\u25BC",
                Width = 18,
                Height = 12,
            };
```

**Exact replacement text (same block)**:
```csharp
            var trimUp = new System.Windows.Controls.Primitives.RepeatButton
            {
                Content = "^",
                Width = 18,
                Height = 12,
            };
            var trimDn = new System.Windows.Controls.Primitives.RepeatButton
            {
                Content = "v",
                Width = 18,
                Height = 12,
            };
```

**Rationale**: V12 DNA ASCII-Only Compliance (AGENTS.md §2) -- "NEVER use Unicode, emoji, or curly quotes in C# string literals." `\u25B2` (BLACK UP-POINTING TRIANGLE) and `\u25BC` (BLACK DOWN-POINTING TRIANGLE) are non-ASCII. Replacement characters `"^"` and `"v"` are semantically equivalent directional indicators and are pure ASCII. Pre-existing lines 1781-3207 are covered by Director waiver; do NOT touch those.

**xUnit [Fact] test names affected**: None -- no test references these content strings directly.

**JS rule constraints**:
- JS-001: No throw introduced. OK.
- JS-002: No return null introduced. OK.
- JS-021: No lock introduced. OK.
- JS-033: No async void introduced. OK.

**NT8 constraints**: `TradeCopierPanel.cs` contains NT8 UI code. All NT8 UI updates run on the WPF dispatcher. The `Content` property assignments here are in a builder method called from `Dispatcher.InvokeAsync`-wrapped initialization -- no direct NT8 API involvement in the string replacement itself.

**7-scan checklist**:
- [ ] SCAN-01: `lock()` -- 0 after this change (no lock added or removed by A-1)
- [ ] SCAN-02: `async void` -- 0 after this change (no async void added)
- [ ] SCAN-03: `return null` -- 0 new after this change (no null return added)
- [ ] SCAN-04: `throw new` -- 0 new after this change (no throw added)
- [ ] SCAN-05: build -- 0 errors after this change
- [ ] SCAN-06: ASCII -- 0 non-ASCII in lines 1130-1400 after this change:
  ```powershell
  Select-String -Path src\PropTraderTools\TradeCopierPanel.cs -Pattern "\\u25B[23]" | Where-Object { $_.LineNumber -ge 1130 -and $_.LineNumber -le 1400 }
  # Expected: 0 results
  ```
- [ ] SCAN-07: `dotnet test` -- 0 new failures after this change

---

## TICKET A-2

**Ticket ID**: A-2
**Category**: MECHANICAL
**Spec requirement IDs**: CodeRabbit CR36-1 (CHANGES_REQUESTED -- misplaced TA-R9 block causes CS0103 compile errors in `BwaveCycTaR7HelperTests`) + CodeRabbit CR36-2 (partial -- vacuous assertion in `TryCancelOrders_ShouldNotThrow_WhenStaleListIsEmpty` inside `BwaveCycTaR7HelperTests` is eliminated by this block removal)
**File(s) affected**: `src/PropTraderTools/CopyEngineTests.cs`
**Confirmed line numbers**: 7181-7395 (inclusive -- the entire misplaced TA-R9 block)

**Method signature(s) affected**: All methods inside `BwaveCycTaR7HelperTests` from line 7181 to 7395 are removed. No method in the kept code is altered. The enclosing class `BwaveCycTaR7HelperTests` retains its closing brace at what will become the new line 7181 area (line 7396 = new first kept line after removal).

**old_text** (lines 7181-7395, to be deleted in full -- shown with start/end anchors):

Start of block (line 7181):
```csharp

        // =====================================================================
        // TA-R9: New helper tests (ticket R9 -- CCN reduction extractions)
        // =====================================================================

        // IsFollowerByName helper tests

        [Fact]
        public void IsFollowerByName_ShouldReturnFalse_WhenFollowerAccountNamesIsNull()
```

End of block (line 7395, closing brace of last method in block):
```csharp
            Assert.Equal("instr", parms[1].Name);
        }
```

Immediately following line 7396 (which becomes new 7181 after deletion):
```csharp

        // =====================================================================
        // TA-R10: GetFollowerMultiplier + BuildAtmModeMap (DtoToRule/RuleToDto helpers)
        // =====================================================================
```

**new_text**: Empty -- the entire block lines 7181-7395 is deleted. No replacement. The empty line 7180 (closing brace of the previous test method) connects directly to the TA-R10 comment block previously at 7397.

**Note on CR36-2 partial resolution**: `TryCancelOrders_ShouldNotThrow_WhenStaleListIsEmpty` (located at approximately lines 7352-7361 within this removal block) contains the vacuous `Record.Exception`/inner-try-catch pattern identified by CodeRabbit CR36-2. Because this entire block is deleted by A-2, that instance of the CR36-2 finding is eliminated here. The remaining CR36-2 instance (`T_R9_09_TryCancelOrders_EmptyList_DoesNotThrow` in `BwaveCycLaneAR9Tests.cs`) is resolved by Ticket A-3.

**Implementation note**: Use `apply_diff` with `start_line: 7181` targeting the full block. The
SEARCH text must be verbatim lines 7181-7395. Because this is a large block (~215 lines), the
engineer should verify using:
```powershell
(Get-Content src\PropTraderTools\CopyEngineTests.cs)[7180].Trim()
# Expected: "// ===================================================================== " (TA-R9 header)
(Get-Content src\PropTraderTools\CopyEngineTests.cs)[7395].Trim()
# Expected: "}" (closing brace of FindPositionForInstrument_ShouldReturnNull_WhenInstrumentIsNull)
```

**Rationale**: `BwaveCycTaR7HelperTests` does not have a `_engine` field or `GetField` helper method. The TA-R9 tests inside this class reference both (`_engine.SetEnabled`, `GetField("_rules")`), causing `CS0103` compilation errors. The canonical TA-R9 tests belong in `BwaveCycLaneAR9Tests.cs` and already exist there. The misplaced block is a duplicate introduced by a bad merge.

**xUnit [Fact] test names affected** (removed from `CopyEngineTests.cs` -- all are present canonically in `BwaveCycLaneAR9Tests.cs`):
- `IsFollowerByName_ShouldReturnFalse_WhenFollowerAccountNamesIsNull` (removed)
- `IsFollowerByName_ShouldReturnFalse_WhenAccountNameDoesNotMatch` (removed)
- `IsFollowerByName_ShouldReturnTrue_WhenAccountNameMatches` (removed)
- `IsOrderForInstrument_ShouldReturnFalse_WhenOrderIsNull` (removed)
- `IsSnapshotBlocked_ShouldReturnFalse_WhenSnapshotIsNull` (removed)
- `TryCancelOrders_ShouldNotThrow_WhenStaleListIsEmpty` (removed from CopyEngineTests.cs; canonical copy exists in BwaveCycLaneAR9Tests.cs as T_R9_09)
- `FindPositionForInstrument_MethodExists_WithCorrectSignature` (removed)
- `FindPositionForInstrument_ShouldReturnNull_WhenInstrumentIsNull` (removed)

**JS rule constraints**:
- JS-021: No lock introduced. OK.
- JS-001: No throw introduced. OK.
- JS-033: No async void introduced. OK.

**NT8 constraints**: None -- test/non-NT8 file.

**7-scan checklist**:
- [ ] SCAN-01: `lock()` -- 0 after this change (block contained no lock; removal only improves)
- [ ] SCAN-02: `async void` -- 0 after this change
- [ ] SCAN-03: `return null` -- 0 new after this change (removal only)
- [ ] SCAN-04: `throw new` -- 0 new after this change (removal only)
- [ ] SCAN-05: build -- 0 errors after this change (CS0103 errors for `_engine` / `GetField` references are eliminated)
- [ ] SCAN-06: ASCII -- 0 non-ASCII after this change (removal only)
- [ ] SCAN-07: `dotnet test` -- 0 new failures after this change (removed tests had CS0103 and would not compile; removal cannot increase failure count)

---

## TICKET A-3

**Ticket ID**: A-3
**Category**: MECHANICAL
**Spec requirement IDs**: CodeRabbit CR36-2 (vacuous assertion -- inner try/catch swallows exception inside `Record.Exception`, remaining instance after A-2 partial resolution) + Cubic confidence=10
**File(s) affected**: `src/PropTraderTools/Tests/BwaveCycLaneAR9Tests.cs` -- lines 146-154

**Note on scope**: CR36-2 has two instances. The instance in `CopyEngineTests.cs` (`TryCancelOrders_ShouldNotThrow_WhenStaleListIsEmpty` inside `BwaveCycTaR7HelperTests`) is eliminated by Ticket A-2 (it falls within the A-2 removal block lines 7181-7395). A-3 addresses only the remaining instance in `BwaveCycLaneAR9Tests.cs`.

**Confirmed line numbers**:
- `BwaveCycLaneAR9Tests.cs` lines 146-154

**Method signature(s) affected**:
- `T_R9_09_TryCancelOrders_EmptyList_DoesNotThrow` in `BwaveCycLaneAR9Tests` (BwaveCycLaneAR9Tests.cs)

---

### A-3 Instance: BwaveCycLaneAR9Tests.cs

**old_text** (exact current lines 146-154):
```csharp
            var ex = Record.Exception(() =>
            {
                try
                {
                    mi.Invoke(null, new object[] { (Account)null, stale });
                }
                catch (TargetInvocationException) { }
            });
            Assert.Null(ex);
```

**new_text**:
```csharp
            var ex = Record.Exception(() =>
                mi.Invoke(null, new object[] { (Account)null, stale })
            );
            Assert.Null(ex);
```

**Rationale**: `Record.Exception(lambda)` is designed to catch any exception thrown by the lambda and return it (or null if no exception). When an inner `try/catch(TargetInvocationException){}` silently swallows the exception, `Record.Exception` always sees a clean exit, making `Assert.Null(ex)` trivially pass whether or not `mi.Invoke` throws. Removing the inner try/catch gives `Record.Exception` direct visibility into the invocation. `TryCancelOrders` with a null account and empty stale list never calls `acc.Cancel` (the list is empty, so the foreach body never executes), meaning no exception propagates through `mi.Invoke` -- the assertion is substantive, not vacuous, after the fix.

**xUnit [Fact] test names affected**:
- `T_R9_09_TryCancelOrders_EmptyList_DoesNotThrow` (body rewritten -- still present in BwaveCycLaneAR9Tests.cs)

**JS rule constraints**:
- JS-021: No lock introduced. OK.
- JS-001: No throw introduced. OK.
- JS-033: No async void introduced. OK.

**NT8 constraints**: None -- test/non-NT8 file.

**7-scan checklist**:
- [ ] SCAN-01: `lock()` -- 0 after this change
- [ ] SCAN-02: `async void` -- 0 after this change
- [ ] SCAN-03: `return null` -- 0 new after this change
- [ ] SCAN-04: `throw new` -- 0 new after this change
- [ ] SCAN-05: build -- 0 errors after this change
- [ ] SCAN-06: ASCII -- 0 non-ASCII after this change
- [ ] SCAN-07: `dotnet test` -- 0 new failures after this change; verify:
  ```powershell
  Select-String -Path src\PropTraderTools\Tests\BwaveCycLaneAR9Tests.cs -Pattern "TargetInvocationException"
  # Expected: 0 results (CopyEngineTests.cs instance is already removed by A-2)
  ```

---

## TICKET A-4

**Ticket ID**: A-4
**Category**: MECHANICAL -- CONFIRMED-NOOP
**Spec requirement IDs**: CodeFactor FAILURE (original PR submission -- SA1507 consecutive blank lines, SA1508 closing brace preceded by blank line)
**File(s) affected**: `src/PropTraderTools/CopyEngineTests.cs`
**Confirmed line numbers**: N/A -- no lines to change

**Method signature(s) affected**: None.

**old_text**: N/A

**new_text**: N/A

### Assessment: Already Fixed

The CSharpier format-pass commit `2270c544` resolved all SA1507 and SA1508 violations in `CopyEngineTests.cs`. The CodeFactor FAILURE was raised against a pre-`2270c544` commit. The current HEAD is clean.

### Engineer Action (mandatory confirmation)

Run the following scan and record output in the ticket completion report:

```powershell
$content = Get-Content src\PropTraderTools\CopyEngineTests.cs
$prev = $false
$sa1507 = 0
for ($i = 0; $i -lt $content.Length; $i++) {
    $blank = ($content[$i].Trim() -eq "")
    if ($blank -and $prev) { $sa1507++ }
    $prev = $blank
}
Write-Host "SA1507 violations: $sa1507"

$sa1508 = 0
for ($i = 1; $i -lt $content.Length; $i++) {
    if ($content[$i-1].Trim() -eq "" -and $content[$i].Trim() -eq "}") { $sa1508++ }
}
Write-Host "SA1508 violations: $sa1508"
```

**Expected output**: `SA1507 violations: 0` and `SA1508 violations: 0`

Document the result as:
```
A-4 CONFIRMED-ALREADY-FIXED
SA1507 violations: 0
SA1508 violations: 0
Resolved by CSharpier commit 2270c544.
No source edit required.
```

**Rationale**: The CodeFactor check that failed applied to a pre-format commit. Current HEAD passes.

**xUnit [Fact] test names affected**: None.

**JS rule constraints**: N/A -- no code change.

**NT8 constraints**: None -- test/non-NT8 file.

**7-scan checklist**:
- [ ] SCAN-01: `lock()` -- 0 (no change; unchanged baseline)
- [ ] SCAN-02: `async void` -- 0 (no change)
- [ ] SCAN-03: `return null` -- 0 new (no change)
- [ ] SCAN-04: `throw new` -- 0 new (no change)
- [ ] SCAN-05: build -- 0 errors (no source edit; build status unchanged)
- [ ] SCAN-06: ASCII -- 0 non-ASCII (no change)
- [ ] SCAN-07: `dotnet test` -- 0 new failures (no change)

---

## TICKET A-5

**Ticket ID**: A-5
**Category**: LOGIC-BUG -- CONFIRMED-NOOP
**Spec requirement IDs**: Greptile P2 + Cubic confidence=10 + CodeRabbit CHANGES_REQUESTED (teal button background regression -- `BuildArrowCluster` unconditionally sets `Background = mainBackground` on teal-bordered buttons)
**File(s) affected**: `src/PropTraderTools/TradeCopierPanel.cs`
**Confirmed line numbers**: N/A -- no lines to change

**Method signature(s) affected**: None.

**old_text**: N/A

**new_text**: N/A

### Assessment: Already Fixed (Method Does Not Exist)

The CodeRabbit/Greptile finding targeted `BuildArrowCluster`, which was extracted during LaneC R11. However, LaneC remediation subsequently replaced the data-driven loop + `BuildArrowCluster` with a full inline `BuildBufferedButtonsRow` (~270 lines, inline). `BuildArrowCluster` does NOT exist in the current HEAD.

Current state of the four teal buttons (inline in `BuildBufferedButtonsRow`):
- `_beBtn2` (BE): `BorderBrush = BrushTeal, Foreground = BrushTeal, BorderThickness = new Thickness(2)` -- **no `Background` property set** (correct)
- `_globalBeBtn2` (BE ALL): same pattern -- **no `Background` set** (correct)
- `_quickBtn` (Quick): same pattern -- **no `Background` set** (correct)
- `_quickAllBtn` (Quick ALL): same pattern -- **no `Background` set** (correct)
- `_trimBtn2` (Trim): `Background = BrushInactive` -- correct (non-teal button)
- `_flattenBtn2` (Flatten): `Background = BrushInactive` -- correct (non-teal button)

The `NTButtonStyle` default applies to the teal buttons; `BrushInactive` is NOT incorrectly assigned.

### Engineer Action (mandatory confirmation)

```powershell
Select-String -Path src\PropTraderTools\TradeCopierPanel.cs -Pattern "BuildArrowCluster"
# Expected: 0 results (method does not exist)
```

Document the result as:
```
A-5 CONFIRMED-ALREADY-FIXED
BuildArrowCluster: 0 occurrences found
Method replaced by inline BuildBufferedButtonsRow in LaneC remediation.
Teal buttons (_beBtn2, _globalBeBtn2, _quickBtn, _quickAllBtn) have no Background set.
No source edit required.
```

**Rationale**: The bug reported (unconditional `Background = mainBackground` on teal buttons) was introduced by `BuildArrowCluster` and subsequently eliminated when that method was inlined away. Current HEAD does not exhibit the bug.

**xUnit [Fact] test names affected**: None.

**JS rule constraints**: N/A -- no code change.

**NT8 constraints**: None -- this is WPF UI code with no NT8 API call at issue.

**7-scan checklist**:
- [ ] SCAN-01: `lock()` -- 0 (no change)
- [ ] SCAN-02: `async void` -- 0 (no change)
- [ ] SCAN-03: `return null` -- 0 new (no change)
- [ ] SCAN-04: `throw new` -- 0 new (no change)
- [ ] SCAN-05: build -- 0 errors (no source edit)
- [ ] SCAN-06: ASCII -- 0 non-ASCII (no change)
- [ ] SCAN-07: `dotnet test` -- 0 new failures (no change)

---

## TICKET A-6

**Ticket ID**: A-6
**Category**: LOGIC-BUG
**Spec requirement IDs**: Greptile P0 (JS-002 violation -- `FindPositionForInstrument` returned null for missing value; method was also lost from HEAD between TA-R9 verify commit 68a1c1c4 and CSharpier/LaneC merge 2270c544)
**File(s) affected**:
1. `src/PropTraderTools/CopyEngine.cs` -- ADD new method after line 1131
2. `src/PropTraderTools/Tests/BwaveCycLaneAR9Tests.cs` -- UPDATE tests T_R9_10 (lines 159-166) and T_R9_11 (lines 168-176)

**Confirmed line numbers**:
- `CopyEngine.cs`: Insert after line 1131 (after closing `}` of `FindBePosition`)
- `BwaveCycLaneAR9Tests.cs`: lines 159-166 (T_R9_10), lines 168-176 (T_R9_11)

**Note on A-2 interaction**: `CopyEngineTests.cs` lines 7364-7395 (within the A-2 removal block) also reference `FindPositionForInstrument`. Those are eliminated by A-2. A-6 does NOT add or update anything in `CopyEngineTests.cs` -- only `CopyEngine.cs` and `BwaveCycLaneAR9Tests.cs`.

---

### A-6 Part 1: ADD method to CopyEngine.cs

**Location**: Insert after line 1131 (after `FindBePosition` closing brace), before the `SubmitBeStopOrder` comment block that begins at line 1133.

**Current text at line 1131-1133** (anchor context):
```csharp
            return null;
        }

        // BWAVE-CYC TB-T3: SubmitBeStopOrder -- submit the StopMarket order via acc.CreateOrder + Submit.
```

**old_text** (lines 1131-1133, exact anchor for insertion):
```csharp
            return null;
        }

        // BWAVE-CYC TB-T3: SubmitBeStopOrder -- submit the StopMarket order via acc.CreateOrder + Submit.
```

**new_text** (insert new method between the `}` and the TB-T3 comment):
```csharp
            return null;
        }

        // BWAVE-CYC TA-R9 (restored): TryFindPositionForInstrument -- locate open position for acc+instr.
        // JS-002: bool + out parameter replaces null return (original FindPositionForInstrument pattern).
        // JS-021: acc.Positions is NT8 read-only collection -- no lock needed.
        // JS-001: no throw. JS-033: synchronous. ASCII-only.
        // CYC=3: base(1) + foreach(1) + inner null-guard(1).
        private static bool TryFindPositionForInstrument(
            Account acc,
            NinjaTrader.Cbi.Instrument instr,
            out NinjaTrader.Cbi.Position pos
        )
        {
            pos = null;
            if (acc == null || instr == null) // (1)
                return false;
            foreach (NinjaTrader.Cbi.Position p in acc.Positions) // (2)
                if (p.Instrument != null && p.Instrument.FullName == instr.FullName) // (3)
                {
                    pos = p;
                    return true;
                }
            return false;
        }

        // BWAVE-CYC TB-T3: SubmitBeStopOrder -- submit the StopMarket order via acc.CreateOrder + Submit.
```

**Post-insertion verification**:
```powershell
# Confirm new method present
Select-String -Path src\PropTraderTools\CopyEngine.cs -Pattern "TryFindPositionForInstrument"
# Expected: 1+ result

# Confirm old null-returning name is gone from production code
Select-String -Path src\PropTraderTools\CopyEngine.cs -Pattern "FindPositionForInstrument"
# Expected: 0 results (TryFindPositionForInstrument does NOT match this pattern)
```

---

### A-6 Part 2: UPDATE T_R9_10 in BwaveCycLaneAR9Tests.cs

**Method**: `T_R9_10_FindPositionForInstrument_MethodExists_PrivateStatic` -- lines 159-166

**old_text** (exact current lines 159-166):
```csharp
        [Fact]
        public void T_R9_10_FindPositionForInstrument_MethodExists_PrivateStatic()
        {
            var mi = GetStaticMethod("FindPositionForInstrument");
            Assert.NotNull(mi);
            Assert.True(mi.IsStatic);
            Assert.Equal(2, mi.GetParameters().Length);
        }
```

**new_text**:
```csharp
        [Fact]
        public void T_R9_10_TryFindPositionForInstrument_MethodExists_PrivateStatic()
        {
            var mi = GetStaticMethod("TryFindPositionForInstrument");
            Assert.NotNull(mi);
            Assert.True(mi.IsStatic);
            Assert.Equal(typeof(bool), mi.ReturnType);
            Assert.Equal(3, mi.GetParameters().Length);
        }
```

**Changes**:
- Method name: `T_R9_10_FindPositionForInstrument_MethodExists_PrivateStatic` --> `T_R9_10_TryFindPositionForInstrument_MethodExists_PrivateStatic`
- Lookup string: `"FindPositionForInstrument"` --> `"TryFindPositionForInstrument"`
- New assertion: `Assert.Equal(typeof(bool), mi.ReturnType)` (was absent; now required because the new method returns bool)
- Parameter count: `Assert.Equal(2, ...)` --> `Assert.Equal(3, ...)` (acc, instr, out pos)

---

### A-6 Part 3: UPDATE T_R9_11 in BwaveCycLaneAR9Tests.cs

**Method**: `T_R9_11_FindPositionForInstrument_ParameterNames` -- lines 168-176

**old_text** (exact current lines 168-176):
```csharp
        [Fact]
        public void T_R9_11_FindPositionForInstrument_ParameterNames()
        {
            var mi = GetStaticMethod("FindPositionForInstrument");
            Assert.NotNull(mi);
            var parms = mi.GetParameters();
            Assert.Equal("acc", parms[0].Name);
            Assert.Equal("instr", parms[1].Name);
        }
```

**new_text**:
```csharp
        [Fact]
        public void T_R9_11_TryFindPositionForInstrument_ParameterNames()
        {
            var mi = GetStaticMethod("TryFindPositionForInstrument");
            Assert.NotNull(mi);
            var parms = mi.GetParameters();
            Assert.Equal("acc", parms[0].Name);
            Assert.Equal("instr", parms[1].Name);
            Assert.Equal("pos", parms[2].Name);
            Assert.True(parms[2].IsOut, "Third parameter must be out Position");
        }
```

**Changes**:
- Method name: `T_R9_11_FindPositionForInstrument_ParameterNames` --> `T_R9_11_TryFindPositionForInstrument_ParameterNames`
- Lookup string: `"FindPositionForInstrument"` --> `"TryFindPositionForInstrument"`
- New assertions: `parms[2].Name == "pos"` and `parms[2].IsOut == true`

---

**A-6 combined post-change verification**:
```powershell
# 1. Method exists in CopyEngine.cs
Select-String -Path src\PropTraderTools\CopyEngine.cs -Pattern "TryFindPositionForInstrument"
# Expected: 1+ result (method declaration)

# 2. Old null-returning name absent from production code
Select-String -Path src\PropTraderTools\CopyEngine.cs -Pattern "FindPositionForInstrument"
# Expected: 0 results

# 3. Tests updated (2 occurrences in test file)
Select-String -Path src\PropTraderTools\Tests\BwaveCycLaneAR9Tests.cs -Pattern "TryFindPositionForInstrument"
# Expected: 2 results (T_R9_10, T_R9_11)

# 4. Old name absent from test file
Select-String -Path src\PropTraderTools\Tests\BwaveCycLaneAR9Tests.cs -Pattern '"FindPositionForInstrument"'
# Expected: 0 results
```

**Rationale**: JS-002 forbids `return null` for missing values. The original `FindPositionForInstrument` returned `null` when no matching position was found. The replacement `TryFindPositionForInstrument` uses the bool + out pattern (standard .NET TryXxx idiom), which makes the absence/presence of a result explicit at the call site. The method was lost during the `2270c544` merge; re-introducing it with the corrected signature closes both the JS-002 violation and the compilation error in the test file (`Assert.NotNull(mi)` would fail at runtime because the method no longer existed).

**xUnit [Fact] test names affected**:
- `T_R9_10_FindPositionForInstrument_MethodExists_PrivateStatic` -- RENAMED to `T_R9_10_TryFindPositionForInstrument_MethodExists_PrivateStatic`
- `T_R9_11_FindPositionForInstrument_ParameterNames` -- RENAMED to `T_R9_11_TryFindPositionForInstrument_ParameterNames`

**JS rule constraints**:
- JS-002: `TryFindPositionForInstrument` uses `bool` return + `out` parameter. No `return null` in the method's return statement. The `pos = null` assignment before early-return is a parameter initialization, not a null-returning design. COMPLIANT.
- JS-021: `acc.Positions` is an NT8 read-only thread-safe collection; no lock required. COMPLIANT.
- JS-001: No throw. `if (acc == null || instr == null) return false` -- no exception thrown. COMPLIANT.
- JS-033: Method is synchronous (`private static bool`). No `async void`. COMPLIANT.
- CYC=3: base(1) + null-guard branch(1) + foreach(1). Within CYC <= 8 mandate.

**NT8 constraints**:
- `acc.Positions` -- NT8 `Account.Positions` is an `AccountPositionCollection`, available in AddOnBase scope. Read-only enumeration; no Submit/Cancel/CreateOrder involved. Confirmed in `NT8_FULL_REFERENCE.md` and `NT8_ADDON_KNOWLEDGE.md`.
- `NinjaTrader.Cbi.Position` -- NT8 type; available as `out` parameter type without any AddOnBase restriction.
- No `AtmStrategyCreate`, `AtmStrategyChangeStopTarget`, or `Account.Change` involvement.
- `out NinjaTrader.Cbi.Position pos` -- standard .NET parameter binding. No NT8 API constraint applies to the parameter declaration itself.

**7-scan checklist**:
- [ ] SCAN-01: `lock()` -- 0 after this change (new method uses no lock; NT8 `acc.Positions` is read-only enumerable)
- [ ] SCAN-02: `async void` -- 0 after this change (method is synchronous `private static bool`)
- [ ] SCAN-03: `return null` -- 0 NEW after this change (`TryFindPositionForInstrument` has no `return null`; `pos = null` is parameter init before `return false`; `FindBePosition` pre-existing `return null` at line 1130 is unchanged baseline)
- [ ] SCAN-04: `throw new` -- 0 new after this change (new method has no throw)
- [ ] SCAN-05: build -- 0 errors after this change (method exists; test assertions match new 3-param bool signature)
- [ ] SCAN-06: ASCII -- 0 non-ASCII after this change (all identifiers and strings in new method are ASCII-only; comment text is ASCII-only)
- [ ] SCAN-07: `dotnet test` -- 0 NEW failures after this change; T_R9_10 and T_R9_11 now succeed (method present with correct signature); pre-existing 80 NT8-runtime failures unchanged

---

## Global 7-Scan After ALL Tickets Applied

Run after A-1 through A-6 are complete:

### SCAN-01: lock() -- Zero

```powershell
Select-String -Path src\PropTraderTools\CopyEngine.cs, src\PropTraderTools\TradeCopierPanel.cs, src\PropTraderTools\CopyEngineTests.cs, src\PropTraderTools\Tests\BwaveCycLaneAR9Tests.cs -Pattern "lock\s*\(" | Where-Object { $_.Line.Trim() -notmatch "^//" }
```
**Expected**: 0 results.

### SCAN-02: async void -- Zero (production files only)

```powershell
Select-String -Path src\PropTraderTools\CopyEngine.cs, src\PropTraderTools\TradeCopierPanel.cs -Pattern "async void " | Where-Object { $_.Line.Trim() -notmatch "^//" }
```
**Expected**: 0 results.

### SCAN-03: return null -- 0 NEW

```powershell
Select-String -Path src\PropTraderTools\CopyEngine.cs -Pattern "return null" | Where-Object { $_.Line.Trim() -notmatch "^//" } | Measure-Object | Select-Object Count
```
**Expected**: Count unchanged from pre-repair baseline. `TryFindPositionForInstrument` does NOT use `return null`.

### SCAN-04: throw new -- 0 NEW

```powershell
Select-String -Path src\PropTraderTools\CopyEngine.cs, src\PropTraderTools\TradeCopierPanel.cs -Pattern "throw new " | Where-Object { $_.Line.Trim() -notmatch "^//" }
```
**Expected**: Pre-existing count only (2 in other files not modified by this repair). Zero in files modified by A-1 through A-6.

### SCAN-05: build -- 0 errors

```powershell
dotnet build src\PropTraderTools\PropTraderTools.csproj 2>&1 | Select-String "error|Error" | Where-Object { $_ -notmatch "0 Error" }
```
**Expected**: 0 errors, 0 new warnings.

### SCAN-06: ASCII -- 0 non-ASCII in repair scope

```powershell
Select-String -Path src\PropTraderTools\TradeCopierPanel.cs -Pattern "\\u25B[23]" | Where-Object { $_.LineNumber -ge 1130 -and $_.LineNumber -le 1400 }
```
**Expected**: 0 results.

### SCAN-07: dotnet test -- 0 NEW failures

```powershell
dotnet test src\PropTraderTools\PropTraderTools.csproj --no-build 2>&1 | Select-String "Failed:"
```
**Expected**: Failed count = 80 (pre-existing accepted NT8-runtime failures). No new failures from A-1 through A-6.

---

## Component Summary

| Ticket | Category | File(s) | Type | Action |
|--------|----------|---------|------|--------|
| A-1 | DNA | TradeCopierPanel.cs | 12x string replace | `\u25B2` -> `"^"`, `\u25BC` -> `"v"` at lines 1147-1356 |
| A-2 | MECHANICAL | CopyEngineTests.cs | Block delete | Remove lines 7181-7395 (TA-R9 misplaced block) |
| A-3 | MECHANICAL | BwaveCycLaneAR9Tests.cs | Body rewrite (1 test) | Remove inner try/catch from Record.Exception lambda (CopyEngineTests.cs instance eliminated by A-2) |
| A-4 | MECHANICAL-NOOP | CopyEngineTests.cs | Confirm + document | SA1507/SA1508 already fixed by 2270c544 |
| A-5 | LOGIC-BUG-NOOP | TradeCopierPanel.cs | Confirm + document | BuildArrowCluster does not exist; inline rewrite fixed it |
| A-6 | LOGIC-BUG | CopyEngine.cs, BwaveCycLaneAR9Tests.cs | Add method + rename 2 tests | Add TryFindPositionForInstrument; update T_R9_10, T_R9_11 |

**Total source files touched**: 4
- `src/PropTraderTools/TradeCopierPanel.cs` (A-1)
- `src/PropTraderTools/CopyEngineTests.cs` (A-2)
- `src/PropTraderTools/Tests/BwaveCycLaneAR9Tests.cs` (A-3, A-6)
- `src/PropTraderTools/CopyEngine.cs` (A-6)

**No .cs files written by architect** (protocol mandate).
