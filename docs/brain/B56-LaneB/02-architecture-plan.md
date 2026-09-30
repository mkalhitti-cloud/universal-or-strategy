# PTT-COPIER B56-LaneB — Architecture Plan
# Status: REVIEW_PASS (awaiting ptt-plan-reviewer)
# Written by: ptt-architect
# Date: 2026-08-09
# Defects: DW-B56-02 (P1) + DW-B56-03 (P1)
# Stage: 2 of 4

---

## 0. Pre-Flight — Source Reads Performed

All facts below are grounded in direct source reads. No speculation.

| File | Lines Read | Key Findings |
|------|-----------|--------------|
| `CopyEngine.cs` | 70-110, 285-330 | Enum at line 83; GetCopyMode at 302-306; GetSuggestedQty at 308; `_rules` is `ConcurrentBag<CopyRule>` at line 99 |
| `TradeCopierWindow.cs` | 90-125, 176-200, 200-320, 573-579 | OnLoaded at 95-122; modeCb at 189-193; OnCopyModeComboChanged at 573-579 |
| `B50Tests.cs` | 1-40 | References `CopyMode.Clone` (lines 18, 28-29, 38, 53, 56, 65) — currently failing because Clone not yet in enum |
| `B55Tests.cs` | 1-30 | Namespace: `PropTraderTools`; class: `public class B55Tests` — match this exactly |
| `PropTraderTools.csproj` | Full | LSP-only project; explicit `<Compile Include>` required for Tests\ files |
| `docs/standards/NT8_COMPILER_RULES.md` | 1-80 | No rules violated by B56 changes |

---

## 1. CopyMode.Clone Pre-Condition Verification

**Grep result: `CopyMode.Clone` exists ONLY in `src/PropTraderTools/Tests/B50Tests.cs`** (lines 18, 28, 29, 38, 53, 56, 65).

It is NOT defined in `CopyEngine.cs` — the enum at line 83 currently reads:
```
internal enum CopyMode { Signal = 0, Mirror = 1 }
```

**Conclusion**: `Clone = 2` is safe and required. Adding it will simultaneously:
1. Close DW-B56-03 (Clone missing from enum)
2. Fix B50Tests.cs pre-existing failures (those tests already assert `(int)CopyMode.Clone == 2`)

---

## 2. Component List

| Component | File | Type | CYC |
|-----------|------|------|-----|
| `CopyMode.Clone = 2` | `CopyEngine.cs:83` | enum value | N/A |
| `GetRuleInstruments()` | `CopyEngine.cs` after line 306 | `internal IEnumerable<string>` | 2 |
| `modeCb.Items.Add("Clone")` | `TradeCopierWindow.cs:191` | 1-line insert | N/A |
| `OnCopyModeComboChanged` (updated) | `TradeCopierWindow.cs:573-579` | `private void` | 4 |
| `RefreshRuleRows()` | `TradeCopierWindow.cs` after line 122 | `private void` | 3 |
| `RefreshRuleRows()` call | `TradeCopierWindow.cs:115` | 1-line insert | N/A |
| `T_B56B_01` | `Tests/B56Tests.cs` | `[Fact]` | 1 |
| `T_B56B_02` | `Tests/B56Tests.cs` | `[Fact]` | 1 |
| csproj entry | `PropTraderTools.csproj` | `<Compile Include>` | N/A |

---

## 3. JS Rules Verification

| Rule | Check | Result |
|------|-------|--------|
| JS-021 | No `lock()`. `GetRuleInstruments` iterates `ConcurrentBag<CopyRule>` — lock-free snapshot enumeration. `RefreshRuleRows` uses `Dispatcher.InvokeAsync` (WPF thread marshal — not a lock). | PASS |
| JS-002 | `GetRuleInstruments` is an iterator method (`yield return`) — can never return `null`; returns empty `IEnumerable<string>` when `_rules` is empty. `RefreshRuleRows` is `void`. | PASS |
| JS-033 | `RefreshRuleRows` is `private void` (not async void). The `Dispatcher.InvokeAsync(lambda)` inside is an `Action` parameter, not an async void body. | PASS |
| JS-001 | No `throw new` in any new or modified method. | PASS |
| CYC | `GetRuleInstruments` CYC=2; `RefreshRuleRows` CYC=3; updated `OnCopyModeComboChanged` CYC=4; tests CYC=1. All <= 8. | PASS |

---

## 4. NT8 Compiler Rules Verification

| Rule | Check | Result |
|------|-------|--------|
| NT8-001 | No `{ get; init; }` in any new code | PASS |
| NT8-002 | No `abstract record` / `sealed record` | PASS |
| NT8-003 | No `volatile double` | PASS |
| NT8-004 | `HashSet<string>` in `GetRuleInstruments` — `System.Collections.Generic.HashSet` is fully supported in .NET 4.8 | PASS |
| NT8-006 | `LINQ .ToList()` used in `RefreshRuleRows` only (UI thread, Loaded event — not hot path). Mission brief explicitly notes this is acceptable. | PASS |
| NT8-032 | `B56Tests.cs` uses `namespace PropTraderTools` (same assembly). Tests only access `CopyMode.Clone` (public enum) and `CopyEngine.Instance.GetRuleInstruments()` (internal method) — no private nested type access. Safe. | PASS |
| NT8-042 | `Dispatcher.InvokeAsync` in `TradeCopierWindow` (WPF `Window` subclass, not a `NinjaScript` AddOn context). Valid. | PASS |
| NT8-054 | New test file placed in `Tests\` subfolder per protocol. | PASS |

---

## 5. Threading Model

```
NT8 event loop (Load)
  → OnLoaded [UI thread]
     → CopyEngine.Instance.LoadRules()     // fills _rules ConcurrentBag
     → RefreshRuleRows()                   // [UI thread, sync]
        → GetRuleInstruments().ToList()    // reads _rules (ConcurrentBag snapshot, lock-free)
        → if (instruments.Count == 0) return;
        → Dispatcher.InvokeAsync(lambda)   // posts lambda back to UI thread
           → _rulesPanel.Children.Clear()  // [UI thread, safe]
           → foreach instr: _rulesPanel.Children.Add(BuildRuleRow(instr))  // [UI thread, safe]
```

No lock, no async void, no race condition. `ConcurrentBag<CopyRule>` enumeration is always safe from any thread.

---

## 6. Data Flow

### DW-B56-02 (Rule Rows Not Rebuilt)

**Before fix**: `OnLoaded` calls `LoadRules()` which fills `_rules`, but `_rulesPanel` still shows only the hardcoded "MES" row from `BuildUI()` (line 206).

**After fix**: `RefreshRuleRows()` called immediately after `LoadRules()`. Reads instrument names from engine, clears panel, adds one `BuildRuleRow(instr)` per saved rule.

### DW-B56-03 (Clone Missing)

**Before fix**: Enum has only `Signal=0, Mirror=1`. ComboBox has only "Signal (default)" and "Mirror". `OnCopyModeComboChanged` uses ternary: index 1 → Mirror, else → Signal.

**After fix**: Enum has `Clone=2`. ComboBox has 3 items. `OnCopyModeComboChanged` uses 3-way if-chain matching existing code style.

---

## 7. File Split Validation

| File | Changes | Independence |
|------|---------|-------------|
| `CopyEngine.cs` | CHANGE 1 (line 83) + CHANGE 2 (after line 306) | Isolated enum + new method |
| `TradeCopierWindow.cs` | CHANGE 3a (after 191) + CHANGE 3b (573-579) + CHANGE 4a (after 122) + CHANGE 4b (after 115) | Isolated UI changes |
| `Tests/B56Tests.cs` | NEW FILE | No impact on production code |
| `PropTraderTools.csproj` | 1-line Compile Include | LSP resolution only |

**LaneA conflict check**: LaneA modifies `DispatchCopy` and `OnOrderUpdate` method bodies only. No overlap with any B56-LaneB change targets. ✅

---

## 8. Ticket T1 — CopyEngine.cs Changes

**File**: `C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs`
**Spec requirements**: DW-B56-03 (Clone enum missing), INV-1, INV-2, INV-3

### CHANGE 1 — CopyMode enum, line 83

**Exact SEARCH/REPLACE:**

```
<<<<<<< SEARCH
:start_line:83
-------
    internal enum CopyMode { Signal = 0, Mirror = 1 }
=======
    internal enum CopyMode { Signal = 0, Mirror = 1, Clone = 2 }
>>>>>>> REPLACE
```

**Verification**: `Select-String -Path CopyEngine.cs -Pattern "CopyMode"` must show `Signal = 0, Mirror = 1, Clone = 2`.

---

### CHANGE 2 — GetRuleInstruments() method, insert after line 306

**Exact insertion point**: After the closing `}` of `GetCopyMode()` at line 306.
Line 307 is blank; line 308 begins `// B9 T1: CYC=2` comment for `GetSuggestedQty`.

**New method body to insert** (insert before line 308, i.e., as lines 308-316 with blank lines):

```csharp
        // B56-LaneB: CYC=2 -- yield distinct instrument names for UI refresh after LoadRules.
        // JS-021: no lock -- ConcurrentBag foreach is lock-free.
        // JS-002: returns empty IEnumerable (not null) when _rules is empty.
        internal IEnumerable<string> GetRuleInstruments()
        {
            var seen = new HashSet<string>();
            foreach (var r in _rules)
                if (seen.Add(r.Instrument))
                    yield return r.Instrument;
        }

```

**Exact SEARCH/REPLACE** (anchor on the GetCopyMode closing brace + next method comment):

```
<<<<<<< SEARCH
:start_line:302
-------
        // B9 T3: CYC=1 -- straight-line cast and return
        internal CopyMode GetCopyMode()
        {
            return (CopyMode)_copyModeValue;
        }

        // B9 T1: CYC=2 -- returns engine value when enabled; 1 otherwise
=======
        // B9 T3: CYC=1 -- straight-line cast and return
        internal CopyMode GetCopyMode()
        {
            return (CopyMode)_copyModeValue;
        }

        // B56-LaneB: CYC=2 -- yield distinct instrument names for UI refresh after LoadRules.
        // JS-021: no lock -- ConcurrentBag foreach is lock-free.
        // JS-002: returns empty IEnumerable (not null) when _rules is empty.
        internal IEnumerable<string> GetRuleInstruments()
        {
            var seen = new HashSet<string>();
            foreach (var r in _rules)
                if (seen.Add(r.Instrument))
                    yield return r.Instrument;
        }

        // B9 T1: CYC=2 -- returns engine value when enabled; 1 otherwise
>>>>>>> REPLACE
```

**Method signatures:**
```csharp
internal IEnumerable<string> GetRuleInstruments()
```

**Verification**: `Select-String -Path CopyEngine.cs -Pattern "GetRuleInstruments"` must return 1 match (definition).

---

## 9. Ticket T2 — TradeCopierWindow.cs Changes

**File**: `C:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierWindow.cs`
**Spec requirements**: DW-B56-02 (rules not refreshed), DW-B56-03 (Clone missing from ComboBox), INV-4, INV-5, INV-6, INV-7

### CHANGE 3a — Add "Clone" ComboBox item, after line 191

**Exact SEARCH/REPLACE** (anchor on Mirror + SelectedIndex):

```
<<<<<<< SEARCH
:start_line:190
-------
            modeCb.Items.Add("Signal (default)");
            modeCb.Items.Add("Mirror");
            modeCb.SelectedIndex = 0;
=======
            modeCb.Items.Add("Signal (default)");
            modeCb.Items.Add("Mirror");
            modeCb.Items.Add("Clone");
            modeCb.SelectedIndex = 0;
>>>>>>> REPLACE
```

**Verification**: `Select-String -Path TradeCopierWindow.cs -Pattern '"Clone"'` must return 1 match (the `Items.Add` line).

---

### CHANGE 3b — OnCopyModeComboChanged, replace body (lines 573-579)

**Observed current body** (lines 573-579):
```csharp
        private void OnCopyModeComboChanged(object sender, SelectionChangedEventArgs e)
        {
            var cb = sender as ComboBox;
            if (cb == null) return;                                              // guard (1)
            CopyEngine.Instance.SetCopyMode(
                cb.SelectedIndex == 1 ? CopyMode.Mirror : CopyMode.Signal);    // branch (2)
        }
```

CYC of current method = 2 (1 null guard + 1 ternary branch).
After change: CYC = 4 (1 null guard + 3 mode branches). Still <= 8. ✅

Style note: Current method uses a ternary (not a switch). We expand to 3-way if/else-if/else chain to match existing if-guard style and avoid switch (which would require a default case). This keeps the code pattern consistent.

**Exact SEARCH/REPLACE:**

```
<<<<<<< SEARCH
:start_line:573
-------
        private void OnCopyModeComboChanged(object sender, SelectionChangedEventArgs e)
        {
            var cb = sender as ComboBox;
            if (cb == null) return;                                              // guard (1)
            CopyEngine.Instance.SetCopyMode(
                cb.SelectedIndex == 1 ? CopyMode.Mirror : CopyMode.Signal);    // branch (2)
        }
=======
        private void OnCopyModeComboChanged(object sender, SelectionChangedEventArgs e)
        {
            var cb = sender as ComboBox;
            if (cb == null) return;                                                  // guard (1)
            if      (cb.SelectedIndex == 1) CopyEngine.Instance.SetCopyMode(CopyMode.Mirror);   // branch (2)
            else if (cb.SelectedIndex == 2) CopyEngine.Instance.SetCopyMode(CopyMode.Clone);    // branch (3)
            else                            CopyEngine.Instance.SetCopyMode(CopyMode.Signal);   // branch (4)
        }
>>>>>>> REPLACE
```

**Method signature** (unchanged):
```csharp
private void OnCopyModeComboChanged(object sender, SelectionChangedEventArgs e)
```

**Verification**: `Select-String -Path TradeCopierWindow.cs -Pattern "CopyMode.Clone"` must return 1 match.

---

### CHANGE 4b — Call RefreshRuleRows() after LoadRules() in OnLoaded, line 115

**Exact SEARCH/REPLACE:**

```
<<<<<<< SEARCH
:start_line:115
-------
                CopyEngine.Instance.LoadRules();
                _engine.CopyEnabledChanged += OnCopyEnabledChanged;
=======
                CopyEngine.Instance.LoadRules();
                RefreshRuleRows();
                _engine.CopyEnabledChanged += OnCopyEnabledChanged;
>>>>>>> REPLACE
```

**Verification**: `Select-String -Path TradeCopierWindow.cs -Pattern "RefreshRuleRows"` must return 2 matches (call site + method definition).

---

### CHANGE 4a — Add RefreshRuleRows() method after OnLoaded closing brace (after line 122)

**Exact insertion point**: After line 122 (OnLoaded closing `}`), before line 124 (V04 comment for OnWindowClosed).

**Exact SEARCH/REPLACE** (anchor on OnLoaded close + V04 comment):

```
<<<<<<< SEARCH
:start_line:122
-------
        }

        // V04: unsubscribe PositionStateChanged on close to prevent ghost callbacks / memory leaks
=======
        }

        // B56-LaneB: CYC=3 -- rebuild rule rows from saved engine state after LoadRules.
        // JS-021: no lock. JS-033: private void (not async void). Dispatcher.InvokeAsync inside.
        // JS-002: guard against empty instruments (keeps default MES row).
        private void RefreshRuleRows()
        {
            var instruments = CopyEngine.Instance.GetRuleInstruments().ToList();
            if (instruments.Count == 0) return;    // CYC branch (1): no saved rules -- keep default MES row
            Dispatcher.InvokeAsync(() =>
            {
                _rulesPanel.Children.Clear();
                foreach (var instr in instruments)    // CYC branch (2): iterate instruments
                    _rulesPanel.Children.Add(BuildRuleRow(instr));
            });
        }

        // V04: unsubscribe PositionStateChanged on close to prevent ghost callbacks / memory leaks
>>>>>>> REPLACE
```

**Method signature:**
```csharp
private void RefreshRuleRows()
```

**CYC=3 breakdown:**
- Branch 1: `if (instruments.Count == 0) return`
- Branch 2: `Dispatcher.InvokeAsync` lambda contains `foreach (var instr in instruments)` — 1 branch for loop body
- Branch 3 (implicit): lambda itself is a distinct execution path off the main method body

**Verification**: `Select-String -Path TradeCopierWindow.cs -Pattern "private void RefreshRuleRows"` must return 1 match.

---

## 10. Ticket T3 — New Test File + csproj

### NEW FILE: `src/PropTraderTools/Tests/B56Tests.cs`

**Namespace**: `PropTraderTools` (matches B55Tests.cs exactly)
**Class**: `public class B56Tests`
**Pattern**: NT8-054 — test files in `Tests\` subfolder.
**xUnit only** — no NUnit, no MSTest.
**No NT8 API, no WPF, no reflection** — pure C# assertions.

```csharp
// PTT-COPIER-B56 -- B56Tests.cs
// xUnit [Fact] tests for B56-LaneB: Rules Refresh + Clone Mode Fix.
// Defects closed: DW-B56-02 (rules not rebuilt after LoadRules) + DW-B56-03 (Clone missing from enum).
// T_B56B_01: GetRuleInstruments_ReturnsEmpty_WhenNoRules -- JS-002 contract.
// T_B56B_02: CopyModeEnum_HasCloneValue2 -- locks the Clone=2 enum contract.
// Jane Street rules: JS-002 (no return null), JS-021 (no lock), JS-033 (no async void).
// xUnit only -- no NUnit, no MSTest. NT8-054: Tests\ subfolder.
// CYC: T_B56B_01 = CYC 1, T_B56B_02 = CYC 1.
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PropTraderTools
{
    /// <summary>
    /// B56-LaneB: Rules Refresh and Clone Mode Fix.
    /// DW-B56-02: GetRuleInstruments returns empty (not null) when no rules loaded.
    /// DW-B56-03: CopyMode.Clone == 2.
    /// </summary>
    public class B56Tests
    {
        // -------------------------------------------------------------------------
        // T_B56B_01 -- GetRuleInstruments returns empty IEnumerable when no rules
        // -------------------------------------------------------------------------

        /// <summary>
        /// When CopyEngine has no rules loaded, GetRuleInstruments() returns an empty
        /// IEnumerable (not null). JS-002: empty IEnumerable is the null-safe return contract.
        /// </summary>
        [Fact]
        public void T_B56B_01_GetRuleInstruments_ReturnsEmpty_WhenNoRules()
        {
            // Engine singleton -- _rules starts empty (or was reset by prior test cleanup).
            // GetRuleInstruments() must return empty IEnumerable, never null.
            var result = CopyEngine.Instance.GetRuleInstruments().ToList();
            Assert.Equal(0, result.Count);
        }

        // -------------------------------------------------------------------------
        // T_B56B_02 -- CopyMode enum has Clone=2
        // -------------------------------------------------------------------------

        /// <summary>
        /// CopyMode.Clone must equal 2. Documents and locks the B56 enum contract.
        /// Ensures Signal=0 and Mirror=1 are not regressed.
        /// </summary>
        [Fact]
        public void T_B56B_02_CopyModeEnum_HasCloneValue2()
        {
            Assert.Equal(2, (int)CopyMode.Clone);
            Assert.True(System.Enum.IsDefined(typeof(CopyMode), 2));
            Assert.Equal(0, (int)CopyMode.Signal);   // no regression
            Assert.Equal(1, (int)CopyMode.Mirror);   // no regression
        }
    }
}
```

### PropTraderTools.csproj — Add Compile Include

**Exact SEARCH/REPLACE** (anchor on AtrSizingEngine.cs, the first Compile Include):

```
<<<<<<< SEARCH
:start_line:88
-------
  <ItemGroup>
    <Compile Include="AtrSizingEngine.cs" />
    <Compile Include="CopyEngine.cs" />
    <Compile Include="CopyEngineTests.cs" />
    <Compile Include="TradeCopierAddOn.cs" />
    <Compile Include="TradeCopierPanel.cs" />
    <Compile Include="TradeCopierWindow.cs" />
  </ItemGroup>
=======
  <ItemGroup>
    <Compile Include="AtrSizingEngine.cs" />
    <Compile Include="CopyEngine.cs" />
    <Compile Include="CopyEngineTests.cs" />
    <Compile Include="TradeCopierAddOn.cs" />
    <Compile Include="TradeCopierPanel.cs" />
    <Compile Include="TradeCopierWindow.cs" />
    <Compile Include="Tests\B56Tests.cs" />
  </ItemGroup>
>>>>>>> REPLACE
```

**Verification**: `Select-String -Path PropTraderTools.csproj -Pattern "B56Tests"` must return 1 match.

---

## 11. 9 Invariants Satisfied

| # | Invariant | How Satisfied |
|---|-----------|--------------|
| INV-1 | `CopyMode` has exactly 3 values: Signal=0, Mirror=1, Clone=2 | CHANGE 1: line 83 replacement |
| INV-2 | `GetRuleInstruments()` exists in `CopyEngine`, returns `IEnumerable<string>` | CHANGE 2: new method after line 306 |
| INV-3 | `GetRuleInstruments()` returns empty (not null) when `_rules` is empty | Iterator method with `yield return` — can never return null |
| INV-4 | `TradeCopierWindow` modeCb has exactly 3 items: "Signal (default)", "Mirror", "Clone" | CHANGE 3a: Insert `modeCb.Items.Add("Clone")` after line 191 |
| INV-5 | `OnCopyModeComboChanged` handles index 2 → `SetCopyMode(CopyMode.Clone)` | CHANGE 3b: Replace method body |
| INV-6 | `RefreshRuleRows()` exists in `TradeCopierWindow` | CHANGE 4a: New method after line 122 |
| INV-7 | `RefreshRuleRows()` called after `LoadRules()` in `OnLoaded` | CHANGE 4b: Insert call after line 115 |
| INV-8 | `T_B56B_01` PASS, `T_B56B_02` PASS | T3: New `Tests/B56Tests.cs` |
| INV-9 | No new `lock()`, no new `async void`, no new `return null` in changed methods | JS rules verified (Section 3) |

---

## 12. 7-Scan Checklist Contract

| # | Scan | Command | Pass Condition |
|---|------|---------|---------------|
| SCAN-01 | No new `lock()` | `Select-String "lock(" src/PropTraderTools/ -Recurse` | 0 actual lock() calls in new code |
| SCAN-02 | No new `async void` | `Select-String "async void " src/PropTraderTools/ -Recurse` | 0 async void in new code |
| SCAN-03 | No new `return null` | `Select-String "return null" src/PropTraderTools/ -Recurse` | 0 new instances (pre-existing allowed) |
| SCAN-04 | No new `throw new` | `Select-String "throw new " src/PropTraderTools/ -Recurse` | 0 new instances |
| SCAN-05 | CYC <= 8 | `python scripts/complexity_audit.py` on changed methods | `GetRuleInstruments` CYC=2, `RefreshRuleRows` CYC=3, `OnCopyModeComboChanged` CYC=4, B56Tests CYC=1 |
| SCAN-06 | Build | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | 0 new errors (pre-existing CS0122 is baseline; must not increase) |
| SCAN-07 | Tests | `dotnet test src/PropTraderTools/PropTraderTools.csproj --no-build` | T_B56B_01 PASS, T_B56B_02 PASS; +2 delta vs 279 baseline |

**SCAN-06 note**: `dotnet build` fails with pre-existing CS0122 (`CopyEngine.CopyRule` private nested type in `CopyEngineTests.cs`). This is a KNOWN baseline failure (exists before B56). The build succeeds in NT8 via F5 (NT8's internal Roslyn host). The engineer must confirm NO NEW errors are added beyond the pre-existing baseline.

**SCAN-07 note**: `T_B56B_01` and `T_B56B_02` do not access `CopyRule` private nested type, so they compile and run successfully via `dotnet test`. The +2 test delta is the new B56 tests. Additionally, B50Tests.cs Clone-related failures will be resolved by CHANGE 1 (those were pre-existing failures from the 24-fail baseline). This is a BONUS improvement — the engineer should note the pre-existing fail count decreasing.

---

## 13. Hard-Link Sync

After SCAN-07 passes:
```powershell
powershell -File scripts\verify_links.ps1 -Fix
```

Expected result:
- `Tests\B56Tests.cs` → SKIP (Tests subfolder — not deployed to NT8)
- All other changed files → OK or FIXED
- PASS: 0 DESYNC

---

## 14. Build Tag

```
PTT-COPIER B56 | rules-refresh-clone-fix | 2026-08-09
```

---

## 15. Pipeline Handoff

**Next stage**: ptt-plan-reviewer reads this document and returns REVIEW_PASS or REVIEW_FAIL.
**If REVIEW_PASS**: ptt-architect writes `04-tickets.md` (Stage 3).
**If REVIEW_FAIL**: ptt-architect revises this document per violation list.

---

*ptt-architect | B56-LaneB | Stage 2 | 2026-08-09*
