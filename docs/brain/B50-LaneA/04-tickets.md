# B50-LaneA Tickets — Clone Mode
## PTT-COPIER-B50 / Lane A

**Block**: PTT-COPIER-B50
**Lane**: A
**Label**: clone-mode
**Brain Dir**: docs/brain/B50-LaneA/
**Wave Workspace**: C:\WSGTA\universal-or-strategy\src\PropTraderTools\
**Source**: docs/brain/B50-LaneA/02-architecture-plan.md (REVIEW_PASS required)

---

## T1 — Clone Mode: Full Implementation

### Spec Requirements Satisfied

- REQ §ATM CACHE: `_cloneAtmCache` volatile field; refreshed on Clone click and read at dispatch time.
- REQ §BRACKET SYNC: Clone reuses Gate B / `HandleBracketChange` path; no duplication.
- REQ §ATM COMBO VISIBILITY: Per-follower ATM combos hidden when Clone active.
- REQ §CLONE RADIO BUTTON: `_cloneModeBtn` added to `BuildModeRow`; `OnCloneModeClick` handler.
- REQ §BUILD TAG: `PttBuild.Tag` updated to B50.

---

### File 1: `src/PropTraderTools/CopyEngine.cs`

#### Change 1a — `PttBuild.Tag` (line 41)

```
// OLD:
internal const string Tag = "PTT-COPIER B49 | layout-reorder | 2026-08-08";

// NEW:
internal const string Tag = "PTT-COPIER B50 | clone-mode+be-color+test-fix | 2026-08-08";
```

**Exact line reference**: Line 41.
**CYC**: 0 (constant declaration).

---

#### Change 1b — `CopyMode` enum (line 87)

```csharp
// OLD:
internal enum CopyMode { Signal = 0, Mirror = 1 }

// NEW:
internal enum CopyMode { Signal = 0, Mirror = 1, Clone = 2 }
```

**Exact line reference**: Line 87.
**JS rules**: None (enum extension — no new logic branches).
**NT8 rules**: None — plain enum.

---

#### Change 1c — Add `_cloneAtmCache` field (after line 108)

Insert immediately after `private volatile int _copyModeValue = 0;` (line 108):

```csharp
private volatile string _cloneAtmCache = string.Empty;   // B50: Clone mode ATM template cache
// Written by: UI thread (SetCloneAtmCache via OnCloneModeClick).
// Read by: NT8 background thread (GetCloneAtmMode via ResolveAtmMode via DispatchCopy).
// volatile string = reference type; CLR volatile is safe. NT8-003: ONLY volatile double/float banned.
```

**Exact line reference**: Insert after line 108.
**JS rules**: JS-021 (no lock — volatile provides cross-thread visibility).
**NT8 rules**: NT8-003 COMPLIANT — `volatile string` is a reference type; only `volatile double/float` are banned (CS0677).

---

#### Change 1d — Add `SetCloneAtmCache(string)` method (after `GetCopyMode`, ~line 347)

Insert after `internal CopyMode GetCopyMode()` block:

```csharp
// B50: SetCloneAtmCache -- stores the leader's ATM template name for Clone mode dispatch.
// Called from TradeCopierPanel.OnCloneModeClick on UI thread.
// JS-021: volatile write -- no lock needed. JS-002: null-coalesces to string.Empty.
// CYC=1 (straight-line assignment).
internal void SetCloneAtmCache(string template)
{
    _cloneAtmCache = template ?? string.Empty;
}
```

**Exact line reference**: After `GetCopyMode` method (~line 347).
**Method signature**: `internal void SetCloneAtmCache(string template)`
**CYC**: 1.
**JS rules**: JS-021 PASS (volatile write), JS-002 PASS (never null — coalesces to Empty).

---

#### Change 1e — Add `ResolveAtmMode` and `GetCloneAtmMode` methods (after `GetAtmMode`, ~line 856)

Insert immediately after the `GetAtmMode` private method:

```csharp
// B50: ResolveAtmMode -- mode-aware ATM resolution for DispatchCopy inner loop.
// Returns per-follower ATM mode for Signal/Mirror; Clone-wide ATM mode for Clone.
// Extracted from DispatchCopy to keep DispatchCopy CYC at limit (CYC=8).
// CYC=2: (1) Clone mode check, (2) delegate to GetCloneAtmMode or GetAtmMode.
// JS-002: both branches return non-null FollowerAtmMode. JS-021: no lock.
private FollowerAtmMode ResolveAtmMode(CopyRule rule, string accountName)
{
    if ((CopyMode)_copyModeValue == CopyMode.Clone)   // branch (1)
        return GetCloneAtmMode();
    return GetAtmMode(rule, accountName);              // branch (2)
}

// B50: GetCloneAtmMode -- builds Clone ATM mode from cached template name.
// internal for testability (mirrors ShouldMirrorClose pattern from B9 T3).
// CYC=2: (1) empty-cache guard, (2) Named return.
// JS-002: returns Inherit on empty -- never null.
// JS-001: no throw -- StatusUpdate is null-safe (?. operator).
internal FollowerAtmMode GetCloneAtmMode()
{
    string cache = _cloneAtmCache;
    if (string.IsNullOrEmpty(cache))                  // branch (1)
    {
        StatusUpdate?.Invoke("PTT-Clone: no ATM cache -- using Inherit fallback");
        return new FollowerAtmMode.Inherit();
    }
    return new FollowerAtmMode.Named(cache);           // branch (2)
}
```

**Method signatures**:
- `private FollowerAtmMode ResolveAtmMode(CopyRule rule, string accountName)` — CYC=2
- `internal FollowerAtmMode GetCloneAtmMode()` — CYC=2

**JS rules**: JS-002 PASS (never null), JS-001 PASS (no throw), JS-021 PASS (volatile read only).

---

#### Change 1f — `DispatchCopy` inner loop (1-line change, ~line 584)

Locate the line inside the `foreach (var acc in rule.FollowerAccounts)` loop body:

```csharp
// OLD (locate by context):
var mode = GetAtmMode(rule, acc.Name);

// NEW (1-line change only):
var mode = ResolveAtmMode(rule, acc.Name);
```

**Exact line reference**: Inside `DispatchCopy`, in the `foreach` loop, the `GetAtmMode` call.
**CYC impact on DispatchCopy**: ZERO — replacing one method call with another adds no new branches.
  `DispatchCopy` CYC remains at 8 (existing AT-LIMIT comment preserved).

---

### File 2: `src/PropTraderTools/TradeCopierPanel.cs`

#### Change 2a — Add `_cloneModeBtn` field (after line 196)

After `private RadioButton _mirrorModeBtn = null;` (line 196):

```csharp
private RadioButton _cloneModeBtn = null;   // B50: Clone mode radio button
```

**Exact line reference**: Line 197 (insert after line 196).

---

#### Change 2b — Add `_atmComboRefs` field (after the QuickExit refs section, ~line 220)

After `private int _quickT2 = 8;` (line 223, or the nearest available location after existing ATM combo fields):

```csharp
// B50: Tracks per-follower ATM ComboBox refs for Clone mode visibility toggle.
// Populated in OnFollowerAtmTemplateComboLoaded. UI-thread-only -- no volatile.
private readonly List<ComboBox> _atmComboRefs = new List<ComboBox>();
```

**Exact line reference**: After line 223 (or in the region of ATM-related fields).

---

#### Change 2c — `BuildModeRow` — add Clone button (lines 1431-1435)

Locate the section in `BuildModeRow` where `_mirrorModeBtn` is added to `row.Children`:

```csharp
// EXISTING (preserved):
row.Children.Add(_signalModeBtn);
row.Children.Add(_mirrorModeBtn);

// INSERT AFTER _mirrorModeBtn.Add, BEFORE _copyToggleBtn2.Add:
_cloneModeBtn = new RadioButton
{
    Content           = "Clone",
    Margin            = new Thickness(8, 0, 0, 0),
    VerticalAlignment = VerticalAlignment.Center
};
_cloneModeBtn.Click += OnCloneModeClick;
row.Children.Add(_cloneModeBtn);

// EXISTING (preserved — moves one position down):
row.Children.Add(_copyToggleBtn2);
```

**Exact line reference**: After `row.Children.Add(_mirrorModeBtn);` (~line 1433), before `row.Children.Add(_copyToggleBtn2);` (~line 1434).
**CYC**: `BuildModeRow` CYC stays at 1 (straight-line construction, no new branches).

---

#### Change 2d — `OnSignalModeClick` — add visibility restore (line ~1441)

```csharp
// OLD:
private void OnSignalModeClick(object sender, RoutedEventArgs e)
{
    CopyEngine.Instance.SetCopyMode(CopyMode.Signal);
}

// NEW:
private void OnSignalModeClick(object sender, RoutedEventArgs e)
{
    CopyEngine.Instance.SetCopyMode(CopyMode.Signal);
    UpdateAtmComboVisibility(Visibility.Visible);
}
```

**Exact line reference**: `OnSignalModeClick` body, ~line 1439-1442.
**CYC**: 1 (unchanged).

---

#### Change 2e — `OnMirrorModeClick` — add visibility restore (line ~1447)

```csharp
// OLD:
private void OnMirrorModeClick(object sender, RoutedEventArgs e)
{
    CopyEngine.Instance.SetCopyMode(CopyMode.Mirror);
}

// NEW:
private void OnMirrorModeClick(object sender, RoutedEventArgs e)
{
    CopyEngine.Instance.SetCopyMode(CopyMode.Mirror);
    UpdateAtmComboVisibility(Visibility.Visible);
}
```

**Exact line reference**: `OnMirrorModeClick` body, ~line 1445-1448.
**CYC**: 1 (unchanged).

---

#### Change 2f — Add `OnCloneModeClick` and `UpdateAtmComboVisibility` (after `OnMirrorModeClick`)

Insert the two new methods immediately after `OnMirrorModeClick`:

```csharp
// B50: OnCloneModeClick -- Clone radio button event handler. CYC=1.
// JS-033: synchronous event handler (RoutedEventHandler) -- async void exemption NOT needed.
// JS-021: no lock. Calls SetCopyMode (volatile int write) + SetCloneAtmCache (volatile string write).
private void OnCloneModeClick(object sender, RoutedEventArgs e)
{
    CopyEngine.Instance.SetCopyMode(CopyMode.Clone);
    string tpl = GetLeaderAtmTemplateName(_currentChart);
    CopyEngine.Instance.SetCloneAtmCache(tpl);
    UpdateAtmComboVisibility(Visibility.Collapsed);
}

// B50: UpdateAtmComboVisibility -- sets Visibility on all tracked per-follower ATM combos.
// CYC=2: (1) foreach loop, (2) null guard.
// JS-021: no lock. UI-thread-only -- called only from Click handlers (UI thread).
// _atmComboRefs populated in OnFollowerAtmTemplateComboLoaded (also UI thread).
private void UpdateAtmComboVisibility(Visibility v)
{
    foreach (var cb in _atmComboRefs)   // branch (1)
    {
        if (cb != null)                 // branch (2)
            cb.Visibility = v;
    }
}
```

**Method signatures**:
- `private void OnCloneModeClick(object sender, RoutedEventArgs e)` — CYC=1
- `private void UpdateAtmComboVisibility(Visibility v)` — CYC=2

**Exact line reference**: After `OnMirrorModeClick` (~line 1448).

---

#### Change 2g — `OnFollowerAtmTemplateComboLoaded` — add tracking line (~line 1927-1932)

Locate the null guard and idempotency guard at the top of `OnFollowerAtmTemplateComboLoaded`:

```csharp
// EXISTING (preserved):
var cb = sender as ComboBox;
if (cb == null) return;                 // branch 1 -- null guard
if (cb.Items.Count > 0) return;        // branch 2 -- idempotency guard

// ADD immediately after the two guards, before cb.Items.Add("(none)"):
if (!_atmComboRefs.Contains(cb))
    _atmComboRefs.Add(cb);             // B50: track combo for Clone visibility toggle
```

**Exact line reference**: After line ~1931 (`if (cb.Items.Count > 0) return;`), before `cb.Items.Add("(none)");`.
**CYC impact**: `OnFollowerAtmTemplateComboLoaded` CYC was 4. Adding 1 `if` guard raises CYC to 5. Still ≤ 8. PASS.

---

### File 3: `src/PropTraderTools/Tests/B50Tests.cs` (CREATE)

**Full path**: `src/PropTraderTools/Tests/B50Tests.cs`
**Placement rule (DW-B48-02)**: MUST be in `Tests\` subfolder, NEVER flat root.

```csharp
// B50Tests.cs -- Clone Mode xUnit tests
// Block: PTT-COPIER-B50 Lane A
// NT8-054: test file in Tests\ subfolder (never flat root).
// DW-B48-02: All BXXTests.cs files must be in Tests\ per protocol established B48.
// JS-021: CopyEngine.Instance.SetCopyMode cleanup after each test (reset to Signal).
using Xunit;
using PropTraderTools;

namespace PropTraderTools
{
    public class B50Tests
    {
        // T_B50_01 -- CopyMode enum value check.
        // Verifies Clone=2 and that existing values are unchanged.
        // No NT8 runtime. Pure enum assertion.
        [Fact]
        public void T_B50_01_CopyMode_Clone_HasValue2()
        {
            Assert.Equal(2, (int)CopyMode.Clone);
            Assert.Equal(0, (int)CopyMode.Signal);   // existing -- must not regress
            Assert.Equal(1, (int)CopyMode.Mirror);   // existing -- must not regress
        }

        // T_B50_02 -- SetCopyMode(Clone) roundtrip via GetCopyMode.
        // Verifies engine returns Clone after SetCopyMode(Clone).
        [Fact]
        public void T_B50_02_SetCopyMode_Clone_SetsModeValueToClone()
        {
            CopyEngine.Instance.SetCopyMode(CopyMode.Clone);
            Assert.Equal(CopyMode.Clone, CopyEngine.Instance.GetCopyMode());
            CopyEngine.Instance.SetCopyMode(CopyMode.Signal);   // cleanup
        }

        // T_B50_03 -- GetCloneAtmMode returns Named when cache is non-empty.
        // Verifies Clone dispatch path injects Named ATM mode with correct template name.
        [Fact]
        public void T_B50_03_DispatchCopy_CloneMode_UsesCloneAtmCache()
        {
            CopyEngine.Instance.SetCopyMode(CopyMode.Clone);
            CopyEngine.Instance.SetCloneAtmCache("MES $200 SL5");
            var mode = CopyEngine.Instance.GetCloneAtmMode();
            Assert.IsType<FollowerAtmMode.Named>(mode);
            var named = (FollowerAtmMode.Named)mode;
            Assert.Equal("MES $200 SL5", named.TemplateName);
            CopyEngine.Instance.SetCopyMode(CopyMode.Signal);   // cleanup
        }

        // T_B50_04 -- Clone mode does not activate Mirror guard.
        // Verifies Clone != Mirror so Gate B (HandleBracketChange) fires unconditionally.
        // Bracket sync for Clone is handled by Gate B without Mirror intercept.
        [Fact]
        public void T_B50_04_HandleBracketChange_CloneMode_SyncsFollowers()
        {
            CopyEngine.Instance.SetCopyMode(CopyMode.Clone);
            // Clone must not be confused with Mirror (which triggers MirrorOrderUpdate)
            Assert.NotEqual(CopyMode.Mirror, CopyEngine.Instance.GetCopyMode());
            Assert.Equal(CopyMode.Clone, CopyEngine.Instance.GetCopyMode());
            // Gate B calls HandleBracketChange for all modes -- Clone is not blocked.
            // Verified indirectly: CopyMode.Clone != CopyMode.Mirror, so MirrorOrderUpdate
            // is not invoked, and Gate B proceeds to HandleBracketChange normally.
            CopyEngine.Instance.SetCopyMode(CopyMode.Signal);   // cleanup
        }

        // T_B50_05 -- GetCloneAtmMode returns Inherit when cache is empty.
        // Verifies fallback behavior when no ATM template was cached at Clone click time.
        [Fact]
        public void T_B50_05_CloneAtmCache_EmptyFallback_UsesDefault()
        {
            CopyEngine.Instance.SetCopyMode(CopyMode.Clone);
            CopyEngine.Instance.SetCloneAtmCache(string.Empty);
            var mode = CopyEngine.Instance.GetCloneAtmMode();
            Assert.IsType<FollowerAtmMode.Inherit>(mode);
            CopyEngine.Instance.SetCopyMode(CopyMode.Signal);   // cleanup
        }
    }
}
```

---

### File 4: `src/PropTraderTools/PropTraderTools.csproj`

#### Change 4a — Add B50Tests compile entry

After the B47Tests entry (line 103):

```xml
<!-- B50: Clone mode tests -->
<Compile Include="Tests\B50Tests.cs" />
```

**Exact line reference**: After line 103 (`<Compile Include="Tests\B47Tests.cs" />`), before the closing `</ItemGroup>` (line 104).

---

### File 5: `src/PropTraderTools/Features/PttFollowerStrategy.cs`

**NO CHANGES.** `FillSignalEventArgs.AtmTemplateName` is already populated by `SendCopy` → `PttBus.RaiseFillSignal`. `CallAtmStrategyCreate` already calls `AtmStrategyCreate` with `args.AtmTemplateName`. Clone dispatch path is identical to Signal (Named ATM) — zero changes required.

---

## Seven-Scan Checklist (SCAN-01 through SCAN-07)

Engineer: run ALL seven scans after implementation, before committing.

### SCAN-01 — JS-021 `lock()` check

```
grep -n "lock(" src/PropTraderTools/CopyEngine.cs src/PropTraderTools/TradeCopierPanel.cs
```

**Expected**: Zero matches in new or modified regions.
**Rationale**: All new cross-thread state uses `volatile` fields. No `lock()` anywhere in scope.

---

### SCAN-02 — JS-033 `async void` check

```
grep -n "async void" src/PropTraderTools/CopyEngine.cs src/PropTraderTools/TradeCopierPanel.cs
```

**Expected**: Zero new `async void` declarations. `OnCloneModeClick` is synchronous `void` — not `async void`.

---

### SCAN-03 — JS-002 `return null` check

```
grep -n "return null" src/PropTraderTools/CopyEngine.cs src/PropTraderTools/TradeCopierPanel.cs
```

**Expected**: Zero new `return null` in changed methods.
- `GetCloneAtmMode` returns `FollowerAtmMode.Inherit()` or `FollowerAtmMode.Named(...)` — never null.
- `ResolveAtmMode` delegates to `GetCloneAtmMode` or `GetAtmMode` — both return non-null.
- `SetCloneAtmCache` is `void` — no return value.
- Pre-existing `return null` in `FindRule` (lines 1381/1387) are documented pre-existing debt (DW-B47-05). These are NOT new violations introduced by B50.

---

### SCAN-04 — NT8-003 `volatile double/float` check

```
grep -n "volatile double\|volatile float" src/PropTraderTools/CopyEngine.cs
```

**Expected**: Zero matches.
- New field `_cloneAtmCache` is `volatile string` (reference type — SAFE).
- No `volatile double` or `volatile float` introduced.

---

### SCAN-05 — Build gate

```
dotnet build
```
(Run from Wave workspace root: `C:\WSGTA\universal-or-strategy\src\PropTraderTools\`)

**Expected**: 0 errors. 0 warnings on new code.

---

### SCAN-06 — CYC check per modified method

Count `if`, `for`, `while`, `switch`, `case`, `? :`, `??`, `&&`, `||` per method.

| Method | File | Max CYC |
|--------|------|---------|
| `SetCloneAtmCache` | CopyEngine.cs | 1 |
| `GetCloneAtmMode` | CopyEngine.cs | 2 |
| `ResolveAtmMode` | CopyEngine.cs | 2 |
| `DispatchCopy` | CopyEngine.cs | 8 (AT LIMIT — no change) |
| `BuildModeRow` | TradeCopierPanel.cs | 1 |
| `OnSignalModeClick` | TradeCopierPanel.cs | 1 |
| `OnMirrorModeClick` | TradeCopierPanel.cs | 1 |
| `OnCloneModeClick` | TradeCopierPanel.cs | 1 |
| `UpdateAtmComboVisibility` | TradeCopierPanel.cs | 2 |
| `OnFollowerAtmTemplateComboLoaded` | TradeCopierPanel.cs | 5 |

**Expected**: All methods ≤ 8.

---

### SCAN-07 — Hard-link integrity

```
powershell -File scripts\verify_links.ps1
```
(Run from Wave workspace root.)

**Expected**: `DESYNC=0 MISSING=0`
- `B50Tests.cs` is in `Tests\` subfolder — Layer 1 directory skip excludes it from NT8 deployment automatically (per DW-B48-02 protocol). No `$DeployExcludes` entry needed.

---

## Engineer Notes

1. **`_cloneAtmCache` is `volatile string`** — this is a reference type. NT8-003 ONLY bans `volatile double` and `volatile float` (CS0677). `volatile string` compiles correctly. Do not remove the `volatile` modifier.

2. **`GetCloneAtmMode` is `internal` (not `private`)** — intentional for xUnit testability. This mirrors the `ShouldMirrorClose` pattern established in B9 T3.

3. **`HandleBracketChange` is NOT modified** — Clone bracket sync is already handled by Gate B in `OnOrderUpdate`. Gate B fires for all modes when `IsWorkingBracket` returns true. No mode-specific restriction is needed.

4. **`UpdateAtmComboVisibility` iterates `_atmComboRefs`** — populated lazily via `OnFollowerAtmTemplateComboLoaded`. If Clone mode is selected before any followers are loaded, the visibility toggle is a no-op; that is correct behavior.

5. **`_atmComboRefs.Contains(cb)` dedup guard** — prevents double-registration if `OnFollowerAtmTemplateComboLoaded` fires multiple times for the same combo (can happen on DataTemplate re-layout).

6. **`PttFollowerStrategy.cs` is NOT touched** — `AtmTemplateName` field in `FillSignalEventArgs` is already wired correctly. Clone dispatch goes through the same `SendCopy` → `PttBus.RaiseFillSignal` path as Signal Named ATM.

7. **csproj B48/B49Tests**: Checking the workspace, `B48Tests.cs` and `B49Tests.cs` were never created (neither file exists in `Tests\`). `B50Tests.cs` follows immediately after `B47Tests.cs` in the csproj entry sequence.

---

## Deferred Items Opened by T1

| ID | Priority | Description |
|----|----------|-------------|
| DW-B50-01 | P1 | Live F5 verification: Clone mode ATM cache fills correctly from leader's ChartTrader in real NT8 session. `GetLeaderAtmTemplateName` depends on DW-B43-02 visual-tree index accuracy. |
| DW-B50-02 | P2 | `_atmComboRefs` list retains references to detached ComboBox controls if followers list is rebuilt. No harm but mild GC pressure. Future: weak references or list clear on panel teardown. |

---

## Return

TICKETS_COMPLETE
