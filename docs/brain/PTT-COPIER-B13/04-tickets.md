# PTT-COPIER-B13 -- Implementation Tickets
# Author: ptt-architect
# Phase: 3 (Ticket Generation)
# Source plan: docs/brain/PTT-COPIER-B13/02-architecture-plan.md (REVIEW_PASS, R2)
# Prior backlog: docs/brain/PTT-COPIER-B12/06-deferred-backlog.md
# Date: 2026-07-12

---

## Ticket 1 -- Wire GetRefPrice to MarketData.Last.Price

**ID**: DW-B12-DEFER-01
**Priority**: P2
**Files**:
  - `src/PropTraderTools/TradeCopierPanel.cs` (Wave workspace: c:\WSGTA\universal-or-strategy)
**Workspace**: Wave
**Spec Req**: specs/002-trade-copier-spec.html line 7424 (DW-B12-DEFER-01 listed as B13 target)

### Description

`GetRefPrice()` currently returns `0.0` unconditionally (B12 stub, line 756-760 of
`TradeCopierPanel.cs`). When callers (`OnTrimClick`, `OnFlattenClick`, `DispatchShortcut`
Key.T/Key.F) receive `0.0` they fall back to market-order execution, losing the Limit-order
exit path built in B12 T1.

Replace the stub body with a three-guard read from `_instrument.MarketData.Last.Price`.
The existing callers already handle `refPrice <= 0` as a market-order fallback -- no caller
changes are needed.

### Implementation

Replace the entire body of `GetRefPrice()` (lines 749-760 of TradeCopierPanel.cs).

**BEFORE (exact lines 749-760):**
```csharp
        // B12 T1 -- GetRefPrice: returns 0.0 as ref price placeholder.
        // NT8: Chart (NinjaTrader.Gui.Chart.Chart) has no BarsArray property -- that lives on
        // NinjaScriptBase (strategies/indicators). From an AddOnBase context, bar data is not
        // directly accessible via the Chart window reference. Callers (Trim/Flatten/BE limit
        // entry) receive 0.0 and must use the buffer-tick offset from current market price
        // rather than a historical close. DW-B12-DEFER-01: wire real price via MarketData.
        // CYC=1.
        private double GetRefPrice()
        {
            return 0.0;
        }
```

**AFTER:**
```csharp
        // B13 T1 -- GetRefPrice: returns last traded price via instrument.MarketData.Last.Price.
        // NT8-032: MarketData.Last is MarketDataEventArgs; .Price is the double value.
        // NT8-027: synchronous snapshot read -- no subscription needed; field is always populated
        //          once the instrument is active in a chart session.
        // Returns 0.0 on any null (instrument not set, or no data yet).
        // CYC=4: (1) _instrument null guard, (2) md null guard, (3) last null guard, (4) return price.
        private double GetRefPrice()
        {
            if (_instrument == null) return 0.0;                   // (1) guard
            var md = _instrument.MarketData;
            if (md == null)   return 0.0;                          // (2) guard
            var last = md.Last;
            if (last == null) return 0.0;                          // (3) guard
            return last.Price;                                     // (4) double
        }
```

### Method Signatures

| Method | File | Change Type | CYC |
|--------|------|-------------|-----|
| `private double GetRefPrice()` | `src/PropTraderTools/TradeCopierPanel.cs` | Body replaced | 4 |

Callers (signatures unchanged, no modification needed):
- `private void OnTrimClick(object sender, RoutedEventArgs e)` -- already handles `refPrice <= 0`
- `private void OnFlattenClick(object sender, RoutedEventArgs e)` -- already handles `refPrice <= 0`
- `private void DispatchShortcut(Key key)` -- already passes `GetRefPrice()` directly

### xUnit Test

`GetRefPrice()` is `private` and depends on `NinjaTrader.Cbi.Instrument` (NT8 runtime object
with `MarketData.Last.Price` only populated inside a live NinjaTrader session). **No xUnit
headless test is possible for this method directly.**

**Test exemption -- DW-B13-SIM-T1-01 (Sim101 gate):**
Start the PTT panel on a live Sim101 chart. Click [Trim +1] or [Flatten +1].
Confirm the Order Flow log shows an OrderType.Limit order priced at
`Last.Price +/- buffer * TickSize` rather than an OrderType.Market fallback.
This confirms the `GetRefPrice()` path returned a non-zero value and the Limit overload was invoked.

### 7-Scan Checklist

```
SCAN 1: grep -r "lock(" src/ --include="*.cs"           -> must return 0 matches
SCAN 2: grep -rn "async void " src/ --include="*.cs"    -> must return 0 matches
SCAN 3: grep -rn "return null;" src/ --include="*.cs"   -> must return 0 matches (hot paths)
SCAN 4: grep -rn "volatile double" src/ --include="*.cs" -> must return 0 matches
SCAN 5: python scripts/complexity_audit.py               -> all methods CYC <= 8
SCAN 6: dotnet build (Wave workspace c:\WSGTA\universal-or-strategy) -> 0 errors, 0 warnings
SCAN 7: dotnet test  (Wave workspace c:\WSGTA\universal-or-strategy) -> all [Fact] tests pass
```

**SCAN 4 note:** No new `volatile double` field is introduced. Existing `volatile bool` fields
(`_clickArmed`, `_clickBuy`) remain unchanged and are NT8-017 compliant.

### NT8 Constraints

| Rule | Requirement | This ticket |
|------|-------------|-------------|
| NT8-032 | `MarketData.Last` is `MarketDataEventArgs`; use `.Price` not cast to double | PASS -- `last.Price` used |
| NT8-027 | Synchronous snapshot read from `AddOnBase` -- confirm field availability | PASS -- `.Last.Price` is always populated once instrument is active |
| NT8-033 | `Chart.BarsArray` does not exist from AddOn context | PASS -- not using BarsArray |
| NT8-003 | No `volatile double` | PASS -- `GetRefPrice` returns a value-type `double`; no new volatile field |
| NT8-001 | No `{ get; init; }` | PASS -- no new properties |

### Acceptance Criteria

1. `GetRefPrice()` body contains the three null guards and `return last.Price` as written above.
2. `dotnet build` (Wave workspace) completes with 0 errors and 0 warnings.
3. SCAN 1-4 all return 0 matches on the modified file.
4. SCAN 5 shows `GetRefPrice` CYC = 4.
5. Sim101 gate DW-B13-SIM-T1-01: [Trim +N] / [Flatten +N] buttons issue OrderType.Limit orders
   instead of market fallback when `Last.Price` is non-zero.

---

## Ticket 2 -- ATR Fraction Spinner Startup Sync

**ID**: DW-B12-DEFER-02
**Priority**: P3
**Files**:
  - `src/PropTraderTools/TradeCopierPanel.cs` (Wave workspace: c:\WSGTA\universal-or-strategy) -- append 2 calls to `OnLoaded`
  - `src/PropTraderTools/CopyEngineTests.cs` (Wave workspace: c:\WSGTA\universal-or-strategy) -- add 1 [Fact] test
**Workspace**: Wave
**Spec Req**: specs/002-trade-copier-spec.html line 7424 (DW-B12-DEFER-02 listed as B13 target)

### Description

At construction, `TradeCopierPanel` sets `_atrFraction = 0.75` and `_maxRiskDollars = 200.0`.
`AtrSizingEngine` starts with `_atrFraction = 1.0` (default) and `_maxRiskDollars = 150.0`
(default). `NotifyAtrFractionChanged()` and `NotifyRiskChanged()` are never called during
initialization, so the engine uses wrong defaults until the user manually touches either spinner.

Fix: append `NotifyRiskChanged();` and `NotifyAtrFractionChanged();` to the end of `OnLoaded()`
after the existing `LoadAtmTemplates()` call (line 338).

The CopyEngine's `UpdateAtrFraction` and `UpdateMaxRisk` methods are already null-guarded
against `_atrEngine == null`, so these calls are safe even if the engine is not yet attached.

### Implementation

#### Change 1 -- Append 2 lines to `OnLoaded` in `TradeCopierPanel.cs`

**Location**: `OnLoaded` method, line 323. Append AFTER line 338 (`LoadAtmTemplates();`).

**BEFORE (lines 323-340, the entire OnLoaded body):**
```csharp
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            _engine.PositionStateChanged += OnPositionStateChanged;
            _engine.PendingBeFired       += OnPendingBeFiredDispatch;
            _followerItems.Clear();
            if (Account.All == null) return;
            foreach (var acc in Account.All)
            {
                _followerItems.Add(new FollowerItem { Account = acc, IsSelected = false });
                acc.AccountItemUpdate += OnAccountItemUpdate;
            }
            if (_followersDropDown != null)
                _followersDropDown.ItemsSource = _followerItems;
            UpdateDropDownHeader();
            LoadAtmTemplates();
        }
```

**AFTER (append 3 lines before the closing brace):**
```csharp
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            _engine.PositionStateChanged += OnPositionStateChanged;
            _engine.PendingBeFired       += OnPendingBeFiredDispatch;
            _followerItems.Clear();
            if (Account.All == null) return;
            foreach (var acc in Account.All)
            {
                _followerItems.Add(new FollowerItem { Account = acc, IsSelected = false });
                acc.AccountItemUpdate += OnAccountItemUpdate;
            }
            if (_followersDropDown != null)
                _followersDropDown.ItemsSource = _followerItems;
            UpdateDropDownHeader();
            LoadAtmTemplates();
            // B13 T2: push initial panel values to AtrSizingEngine at startup.
            // CopyEngine.UpdateAtrFraction / UpdateMaxRisk are null-guarded;
            // if _atrEngine is null (not yet attached) they are silent no-ops.
            NotifyRiskChanged();
            NotifyAtrFractionChanged();
        }
```

**Note on `if (Account.All == null) return;` early-exit:** The two new calls are appended AFTER
`LoadAtmTemplates()`, which is also after the early-return guard. If `Account.All` is null the
early return fires before reaching the new lines -- this is acceptable: if NT8 has no accounts
yet, the engine also has no instrument data and the sync is meaningless. When the panel
re-initializes with a valid account, `NotifyRiskChanged` and `NotifyAtrFractionChanged` will fire.

#### Change 2 -- Add [Fact] test to `CopyEngineTests.cs`

Add the following test to the existing test class in `CopyEngineTests.cs`. Place it adjacent
to the `UpdateMaxRisk_SetsAtrEngineMaxRiskDollars_ReflectsInSubsequentSizing` test (which
follows the same Arrange/Act/Assert pattern).

```csharp
[Fact]
public void UpdateAtrFraction_ForwardsToEngine_WhenEngineSet()
{
    // Arrange: engine constructed with testContracts=5; _atrFraction default is 1.0
    var engine = new AtrSizingEngine(testContracts: 5);
    CopyEngine.Instance.SetAtrEngine(engine, enabled: true);

    // Act: push fraction 0.5 through the wiring chain
    CopyEngine.Instance.UpdateAtrFraction(0.5);

    // Assert: GetSuggestedQty returns engine's testContracts value (5) confirming
    // the engine is active and the UpdateAtrFraction call reached it without
    // throwing or short-circuiting.
    // If SetAtrEngine were not called, _atrEnabled = false and qty = 1 (fallback).
    int qty = CopyEngine.Instance.GetSuggestedQty(null);
    Assert.Equal(5, qty);

    // Teardown
    CopyEngine.Instance.SetAtrEngine(null, enabled: false);
}
```

### Method Signatures

| Method | File | Change Type | CYC |
|--------|------|-------------|-----|
| `private void OnLoaded(object sender, RoutedEventArgs e)` | `src/PropTraderTools/TradeCopierPanel.cs` | 2 lines appended | unchanged (no new branches) |
| `[Fact] public void UpdateAtrFraction_ForwardsToEngine_WhenEngineSet()` | `src/PropTraderTools/CopyEngineTests.cs` | New test method | 1 |

Supporting methods called (no change to their signatures):
- `private void NotifyRiskChanged()` -- already exists in TradeCopierPanel.cs
- `private void NotifyAtrFractionChanged()` -- already exists in TradeCopierPanel.cs
- `public void UpdateAtrFraction(double fraction)` -- already exists in CopyEngine.cs (READ ONLY)
- `public void SetAtrEngine(AtrSizingEngine engine, bool enabled)` -- already exists in CopyEngine.cs (READ ONLY)
- `public int GetSuggestedQty(Instrument instrument)` -- already exists in CopyEngine.cs (READ ONLY)

### xUnit Test

```csharp
[Fact]
public void UpdateAtrFraction_ForwardsToEngine_WhenEngineSet()
{
    // Arrange
    var engine = new AtrSizingEngine(testContracts: 5);
    CopyEngine.Instance.SetAtrEngine(engine, enabled: true);

    // Act
    CopyEngine.Instance.UpdateAtrFraction(0.5);

    // Assert: enabled-engine path returns testContracts (5), not fallback (1)
    int qty = CopyEngine.Instance.GetSuggestedQty(null);
    Assert.Equal(5, qty);

    // Teardown
    CopyEngine.Instance.SetAtrEngine(null, enabled: false);
}
```

**Why this assertion is meaningful:**
- Path A (this test): `SetAtrEngine(engine, enabled: true)` -> `GetSuggestedQty` returns `5`
- Path B (disabled): `SetAtrEngine(null, enabled: false)` -> `GetSuggestedQty` returns `1` (fallback)
- `Assert.Equal(5, qty)` distinguishes path A from path B conclusively.

### 7-Scan Checklist

```
SCAN 1: grep -r "lock(" src/ --include="*.cs"           -> must return 0 matches
SCAN 2: grep -rn "async void " src/ --include="*.cs"    -> must return 0 matches
SCAN 3: grep -rn "return null;" src/ --include="*.cs"   -> must return 0 matches (hot paths)
SCAN 4: grep -rn "volatile double" src/ --include="*.cs" -> must return 0 matches
SCAN 5: python scripts/complexity_audit.py               -> all methods CYC <= 8
SCAN 6: dotnet build (Wave workspace c:\WSGTA\universal-or-strategy) -> 0 errors, 0 warnings
SCAN 7: dotnet test  (Wave workspace c:\WSGTA\universal-or-strategy) -> all [Fact] tests pass
         including UpdateAtrFraction_ForwardsToEngine_WhenEngineSet
```

### NT8 Constraints

| Rule | Requirement | This ticket |
|------|-------------|-------------|
| NT8-003 | No `volatile double` | PASS -- no new volatile fields; `_atrFraction` and `_maxRiskDollars` are plain `double`, UI-thread-only |
| NT8-018 | No `lock()` | PASS -- no lock in call chain; `UpdateAtrFraction` uses existing null-guard pattern |
| NT8-019 | No `async void` | PASS -- `OnLoaded` is `private void`; no async keyword |
| NT8-001 | No `{ get; init; }` | PASS -- no new properties |

### Acceptance Criteria

1. `OnLoaded()` body ends with `NotifyRiskChanged();` then `NotifyAtrFractionChanged();` as the
   last two statements before the closing brace.
2. `UpdateAtrFraction_ForwardsToEngine_WhenEngineSet` [Fact] is present in `CopyEngineTests.cs`.
3. `dotnet test` passes all tests including the new [Fact].
4. `dotnet build` completes with 0 errors, 0 warnings.
5. SCAN 1-4 all return 0 matches on modified files.
6. SCAN 5 shows `OnLoaded` CYC unchanged (two straight-line appended calls introduce no branches).

---

## Ticket 3 -- Docs and Comment Fix (NT8-031)

**ID**: DW-B12-DEFER-03
**Priority**: P3
**Files**:
  - `src/PropTraderTools/TradeCopierPanel.cs` (Wave workspace: c:\WSGTA\universal-or-strategy) -- comment-only fix at line 802
  - `docs/standards/NT8_COMPILER_RULES.md` (Director workspace: c:\WSGTA\universal-or-strategy-director) -- add NT8-031 rule entry
**Workspace**: Both (Wave for comment; Director for docs)
**Spec Req**: B12 backlog DW-B12-DEFER-03 (OPEN entering B13); source: B12 ticket-review WARN-01,
             ticket-1-verification WARN-01, ticket-3-verification OBS-01

### Description

`TradeCopierPanel.cs` line 802 contains an incorrect comment:

```
// NT8-003: no Math.Clamp (banned in .NET 4.8). Math.Max/Min used instead.
```

NT8-003 bans `volatile double`. The absence of `Math.Clamp` is a separate concern: it was
not added to .NET until Standard 2.1 / Core 2.0, and NinjaTrader 8 targets .NET Framework 4.8
which predates it. The comment misattributes the reason, leading future engineers to look up
NT8-003 (volatile ban) when they need to understand the Math.Clamp constraint.

This ticket:
1. Corrects the comment text in `TradeCopierPanel.cs` at line 802.
2. Adds a new NT8-031 rule entry to `docs/standards/NT8_COMPILER_RULES.md` documenting the
   correct cause and safe workaround.

**No logic, methods, fields, UI, or CopyEngine changes are made in this ticket.**

### Implementation

#### Change 1 -- Fix comment in TradeCopierPanel.cs (Wave workspace)

**Location**: `TradeCopierPanel.cs`, line 802 (inside `OnTightenStop` method comment block).

**BEFORE (line 802, exact text):**
```csharp
        // NT8-003: no Math.Clamp (banned in .NET 4.8). Math.Max/Min used instead.
```

**AFTER:**
```csharp
        // NT8-031: no Math.Clamp (.NET 4.8 version constraint -- not the NT8-003 volatile ban).
        //          Use Math.Max/Math.Min as manual clamp: value < min ? min : value > max ? max : value.
```

**Context (surrounding lines, for orientation -- do NOT change these):**
```
801: // B10 T3 -- OnTightenStop: tighten stop button click handler.
802: // NT8-003: no Math.Clamp (banned in .NET 4.8). Math.Max/Min used instead.  <-- CHANGE THIS LINE
803: // JS-021: no lock -- _engine.TightenStop iterates ConcurrentBag (lock-free).
```

Only line 802 changes. Lines 801 and 803 are unchanged.

#### Change 2 -- Add NT8-031 rule entry to NT8_COMPILER_RULES.md (Director workspace)

**Location**: `docs/standards/NT8_COMPILER_RULES.md`
**Action**: Add NT8-031 to the INDEX TABLE and append a rule section.

**INDEX TABLE row to add** (append after the last existing rule row):
```
| NT8-031 | Math.Clamp absent -- .NET 4.8 version constraint | Math.Clamp(v,min,max) | Math.Max(min,Math.Min(max,v)) |
```

**Rule section to append** (add after last existing rule entry):

```markdown
## NT8-031

**ERROR**: `'Math' does not contain a definition for 'Clamp'`
  (or: `The best overloaded method match for 'System.Math.Clamp(...)' has some invalid arguments`)

**CAUSE**: `Math.Clamp` was added in .NET Standard 2.1 and .NET Core 2.0.
  NinjaTrader 8 targets .NET Framework 4.8, which does NOT include `Math.Clamp`.
  This is a .NET version constraint, NOT the NT8-003 volatile double ban.
  Comments citing "NT8-003" as the reason for missing Math.Clamp are incorrect.

**BANNED**: `Math.Clamp(value, min, max)`

**SAFE**: Manual ternary clamp:
  `value < min ? min : value > max ? max : value`
  Or equivalently: `Math.Max(min, Math.Min(max, value))`

**SCAN**: `grep -r "Math.Clamp" src/ --include="*.cs"`

**NOTE**: NT8-003 bans `volatile double` (cross-thread mutable double fields).
  NT8-031 documents Math.Clamp absence. These are distinct rules.
  If a comment says "NT8-003: no Math.Clamp" -- that comment is wrong; update it to NT8-031.
```

### Method Signatures

No method signatures change. This ticket modifies comment text and documentation only.

| File | Change Type | Lines Modified |
|------|-------------|---------------|
| `src/PropTraderTools/TradeCopierPanel.cs` | Comment text only (line 802) | 1 line |
| `docs/standards/NT8_COMPILER_RULES.md` | Append INDEX row + rule section | N/A (append) |

### xUnit Test

**No new [Fact] required.** This is a docs/comment-only change. There is no observable
runtime behavior change to assert. Verification is by code review:

- ptt-verifier confirms line 802 now reads `NT8-031` not `NT8-003`.
- ptt-verifier confirms `NT8_COMPILER_RULES.md` contains a section headed `## NT8-031`.
- ptt-verifier confirms the NT8-031 rule entry names `Math.Clamp` as BANNED and
  `Math.Max(min, Math.Min(max, value))` as SAFE.

### 7-Scan Checklist

```
SCAN 1: grep -r "lock(" src/ --include="*.cs"           -> must return 0 matches
SCAN 2: grep -rn "async void " src/ --include="*.cs"    -> must return 0 matches
SCAN 3: grep -rn "return null;" src/ --include="*.cs"   -> must return 0 matches (hot paths)
SCAN 4: grep -rn "volatile double" src/ --include="*.cs" -> must return 0 matches
SCAN 5: python scripts/complexity_audit.py               -> all methods CYC <= 8 (unchanged)
SCAN 6: dotnet build (Wave workspace c:\WSGTA\universal-or-strategy) -> 0 errors, 0 warnings
SCAN 7: dotnet test  (Wave workspace c:\WSGTA\universal-or-strategy) -> all [Fact] tests pass
         (comment change does not affect any existing test)
```

**SCAN 1-4 note**: Comment-only edit in TradeCopierPanel.cs introduces no new code patterns.
SCAN 4 specifically: the line being changed is a comment, not a `volatile double` declaration.

### NT8 Constraints

| Rule | Requirement | This ticket |
|------|-------------|-------------|
| NT8-003 | No `volatile double` | PASS -- the comment is being corrected FROM "NT8-003" TO "NT8-031"; no volatile double introduced |
| NT8-031 | `Math.Clamp` absent in .NET 4.8 -- use manual ternary | PASS -- the rule entry being created documents this correctly; existing code already uses Math.Max/Min |
| NT8-001 | No `{ get; init; }` | PASS -- no new properties |

### Acceptance Criteria

1. `TradeCopierPanel.cs` line 802 reads:
   `// NT8-031: no Math.Clamp (.NET 4.8 version constraint -- not the NT8-003 volatile ban).`
   (The second continuation line on 803 replaces the old `// JS-021: no lock` comment --
   **CORRECTION**: do NOT displace the `// JS-021` comment. Insert the second line of the NT8-031
   comment as an additional line, or keep it as a single-line replacement of line 802 only.
   The simplest correct edit is the single-line replacement of line 802.)
2. `docs/standards/NT8_COMPILER_RULES.md` contains a section `## NT8-031` with `Math.Clamp`
   as BANNED and `Math.Max(min, Math.Min(max, value))` as SAFE.
3. `docs/standards/NT8_COMPILER_RULES.md` INDEX TABLE contains a row for NT8-031.
4. `dotnet build` (Wave workspace) completes with 0 errors, 0 warnings.
5. All existing `[Fact]` tests continue to pass (comment change has no runtime effect).

---

## Ticket Execution Order

| Order | Ticket | File(s) | Dependency |
|-------|--------|---------|------------|
| 1st | T1 (GetRefPrice) | TradeCopierPanel.cs | None -- independent |
| 2nd | T2 (Startup Sync) | TradeCopierPanel.cs + CopyEngineTests.cs | None -- independent; build after T1 |
| 3rd | T3 (Docs+Comment) | TradeCopierPanel.cs (comment) + NT8_COMPILER_RULES.md | None -- independent |

T1 and T2 both modify `TradeCopierPanel.cs`. Execute them sequentially (T1 first, T2 second)
and build after each to confirm clean compile before proceeding.

---

## Cross-File Scan (post-all-tickets)

After all three tickets are implemented, run the full 7-scan against the complete Wave workspace:

```powershell
# SCAN 1: no lock() anywhere in src/
grep -r "lock(" c:\WSGTA\universal-or-strategy\src\ --include="*.cs"

# SCAN 2: no async void anywhere
grep -rn "async void " c:\WSGTA\universal-or-strategy\src\ --include="*.cs"

# SCAN 3: no return null on hot paths
grep -rn "return null;" c:\WSGTA\universal-or-strategy\src\ --include="*.cs"

# SCAN 4: no volatile double fields
grep -rn "volatile double" c:\WSGTA\universal-or-strategy\src\ --include="*.cs"

# SCAN 5: complexity check
python c:\WSGTA\universal-or-strategy\scripts\complexity_audit.py

# SCAN 6: build
dotnet build c:\WSGTA\universal-or-strategy

# SCAN 7: tests
dotnet test c:\WSGTA\universal-or-strategy
```

All 7 scans must pass before ptt-verifier sign-off.

TICKETS_COMPLETE
