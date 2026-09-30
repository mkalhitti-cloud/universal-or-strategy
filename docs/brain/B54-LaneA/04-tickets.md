# B54-LaneA Tickets — UI Live-Truth Sync (DW-B54-03 P0)

**Status**: TICKETS_COMPLETE
**Epic**: PTT-COPIER B54 LaneA
**Work item**: DW-B54-03 P0 — UI state desync: buttons not live with engine truth after F5 / surface create
**Architecture plan**: `docs/brain/B54-LaneA/02-architecture-plan.md` (REVIEW_PASS)
**Produced by**: ptt-architect Phase 3

---

## T1 — B54-LaneA-T1: UI Live-Truth Sync (DW-B54-03 P0)

### 1. Spec Requirement IDs

| ID | Priority | Description |
|---|---|---|
| DW-B54-03 | P0 | UI state desync: copy-enabled button does not reflect engine truth after F5 or surface create |

Root causes closed by this ticket:
- **Root Cause A**: `OnLoaded` never reads engine state — both surfaces initialise button from stale local `_copyEnabled` field instead of `_engine.IsEnabled`.
- **Root Cause B**: `copy_rules.xml` does not persist the copy-enabled boolean — `_isCopyEnabled` resets to `false` on every F5/reload.
- **Root Cause C**: `CopyEnabledChanged` not fired after `LoadRules` — surfaces already subscribed at load time never receive the restored value.

---

### 2. Files Modified

| File | Change type |
|---|---|
| `C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs` | Modify existing + add property + extend DTO |
| `C:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs` | Add new method + modify 3 existing methods |
| `C:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierWindow.cs` | Add new method + modify 3 existing methods |
| `C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs` | Add 3 new [Fact] tests + 2 test helpers |

> **Engineer note**: All four files MUST be edited in a single commit. They form one atomic
> unit — the tests verify the engine changes, and the UI changes depend on `IsEnabled` being
> exposed. No partial commit is acceptable.

---

### 3. Method Signatures (exact)

#### 3.1 CopyEngine.cs

**A1 — Add `IsEnabled` public property** (insert after `SetEnabled`, line ~315)

```csharp
public bool IsEnabled => _isCopyEnabled;
```

- Expression-bodied read-only property. No setter.
- Exposes existing `volatile bool _isCopyEnabled` field as public API.
- CYC = 1. Zero branches.
- NT8-001 check: this is a property, not an init-only setter. PASS.

---

**A2 — Add `CopyEnabled` field to `CopyRulesContainer`** (class body, lines ~2571–2574)

```csharp
[XmlElement]
public bool CopyEnabled { get; set; }
```

Full class after change:

```csharp
public class CopyRulesContainer
{
    [XmlElement]
    public bool CopyEnabled { get; set; }   // NEW — default false (backward compat)

    [XmlElement]
    public List<CopyRuleDto> Rules { get; set; }

    public CopyRulesContainer()
    {
        Rules = new List<CopyRuleDto>();
    }
}
```

- `{ get; set; }` standard setter. NOT `init`. NT8-001: PASS.
- Default `false` ensures old XML without `<CopyEnabled>` element deserialises safely (XmlSerializer
  leaves missing elements at the type default).
- No new `volatile` fields. NT8-003: PASS.

---

**A3 — Modify `SaveRules(string overridePath = null)`** (line ~2698–2700)

Add the following statement **BEFORE** the `XmlSerializer` constructor call and AFTER
`container.Rules` has been populated:

```csharp
container.CopyEnabled = _isCopyEnabled;   // NEW: persist global toggle state
```

Full edit-point context (engineer to locate by surrounding lines):

```csharp
// --- existing code ---
var container = new CopyRulesContainer();
foreach (var rule in _rules)
    container.Rules.Add(RuleToDto(rule));

// >>> INSERT HERE <<<
container.CopyEnabled = _isCopyEnabled;

// --- existing code continues ---
var serializer = new XmlSerializer(typeof(CopyRulesContainer));
```

- +1 assignment statement. +0 CYC branches.
- `overridePath` parameter added with default `null`. Resolve actual path as:
  ```csharp
  string path = overridePath ?? /* existing NT8 path resolution expression */;
  ```
  Existing callers pass no argument and continue to work unchanged.

---

**A4 — Modify `LoadRules(string overridePath = null)`** (line ~2741–2744)

Add two statements at the end of the `try` block, **AFTER** all rules have been added to the
engine, **BEFORE** `_persistenceLoaded = true`:

```csharp
// --- existing code ---
foreach (var dto in container.Rules)
    _rules.Add(DtoToRule(dto));

// >>> INSERT HERE <<<
_isCopyEnabled = container.CopyEnabled;              // NEW: restore from XML
CopyEnabledChanged?.Invoke(_isCopyEnabled);          // NEW: snap subscribed surfaces

// --- existing code continues ---
_persistenceLoaded = true;
```

- `overridePath` parameter added with default `null` (same pattern as `SaveRules`).
- +2 statements inside existing `try` block. +0 CYC branches.
- `?.Invoke` fires the event only when at least one surface is subscribed. Safe to call when
  zero surfaces are subscribed (no NullReferenceException).

---

#### 3.2 TradeCopierPanel.cs

**B1 — Add `ApplyCopyState(bool enabled)`** (new private method, insert near line 1331)

```csharp
private void ApplyCopyState(bool enabled)
{
    _copyEnabled = enabled;
    Dispatcher.InvokeAsync(() =>
    {
        if (_copyToggleBtn2 == null) return;
        _copyToggleBtn2.Content    = enabled ? "\u25CF COPY ON" : "\u25CF COPY OFF";
        _copyToggleBtn2.Background = enabled ? BrushActive : BrushInactive;
    });
}
```

- JS-021: no `lock()`. PASS.
- JS-033: method is `private void` (synchronous). `Dispatcher.InvokeAsync` is called inside but
  does NOT make the method `async`. PASS.
- JS-002: no `return null`. The lambda `return;` is a `void` early-exit guard, not a null return. PASS.
- `bool` assignment to `_copyEnabled` is atomic on x86/x64 JIT. No lock required.
- Null guard on `_copyToggleBtn2` handles NT8 WPF template quirk during first `OnLoaded`.
- CYC = 2 (1 base + 1 null-check branch in lambda).

---

**B2 — Replace `OnCopyEnabledChanged` body** (lines 1331–1340)

Replace entire method body with:

```csharp
private void OnCopyEnabledChanged(bool enabled)
{
    ApplyCopyState(enabled);
}
```

- Replaces previous inline `Dispatcher.InvokeAsync` + direct button mutation.
- CYC = 1 (straight-line delegate call).
- All visual logic now lives exclusively in `ApplyCopyState`.

---

**B3 — Modify `OnLoaded`** (line ~610)

Add one unconditional call immediately after the existing subscribe line:

```csharp
// EXISTING (do not remove or move):
_engine.CopyEnabledChanged += OnCopyEnabledChanged;

// NEW — add immediately after the line above:
ApplyCopyState(_engine.IsEnabled);
```

- Snaps button to engine truth the moment the panel appears in the visual tree.
- +0 CYC branches to `OnLoaded`.

---

**B4 — Replace `OnCopyToggle` body** (lines 1319–1326)

Replace entire method body with engine delegation:

```csharp
private void OnCopyToggle(object sender, RoutedEventArgs e)
{
    _engine.SetEnabled(!_engine.IsEnabled);
    // Visual update: CopyEnabledChanged -> OnCopyEnabledChanged -> ApplyCopyState
}
```

- Removes ALL direct button mutation from this handler permanently.
- Engine fires `CopyEnabledChanged` → `OnCopyEnabledChanged(enabled)` → `ApplyCopyState(enabled)`.
- CYC = 1.

---

#### 3.3 TradeCopierWindow.cs

**C1 — Add `ApplyCopyState(bool enabled)`** (new private method, insert near line 652)

```csharp
private void ApplyCopyState(bool enabled)
{
    _copyEnabled = enabled;
    Dispatcher.InvokeAsync(() =>
    {
        _globalToggleBtn.Content    = enabled ? "Copy All ON" : "Copy All OFF";
        _globalToggleBtn.Background = enabled ? WBrushActive  : WBrushInactive;
    });
}
```

- No null guard needed: Window WPF lifecycle guarantees `_globalToggleBtn` is initialised before
  `OnLoaded` fires (Window is a top-level WPF element, not a ChartTrader panel template).
- JS-021: no `lock()`. PASS.
- JS-033: `private void`, not async. PASS.
- CYC = 1 (straight-line, no branches).

---

**C2 — Replace `OnCopyEnabledChanged` body** (lines 652–660)

```csharp
private void OnCopyEnabledChanged(bool enabled)
{
    ApplyCopyState(enabled);
}
```

- CYC = 1.

---

**C3 — Modify `OnLoaded`** (line ~127)

```csharp
// EXISTING (do not remove or move):
_engine.CopyEnabledChanged += OnCopyEnabledChanged;

// NEW — add immediately after:
ApplyCopyState(_engine.IsEnabled);
```

- +0 CYC branches.

---

**C4 — Replace `OnGlobalToggle` body** (lines 641–647)

```csharp
private void OnGlobalToggle(object sender, RoutedEventArgs e)
{
    _engine.SetEnabled(!_engine.IsEnabled);
    // Visual update: CopyEnabledChanged -> OnCopyEnabledChanged -> ApplyCopyState
}
```

- Removes ALL direct button mutation from this handler.
- CYC = 1.

---

#### 3.4 CopyEngineTests.cs — new [Fact] tests

Add the following private helper methods and three [Fact] methods to the existing test class.

**Test helpers** (private, not test methods):

```csharp
private static void ResetPersistenceLoaded(CopyEngine engine)
{
    typeof(CopyEngine)
        .GetField("_persistenceLoaded",
                  BindingFlags.NonPublic | BindingFlags.Instance)
        ?.SetValue(engine, false);
}

private static string BuildRulesXml(bool copyEnabled)
{
    return "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
           "<CopyRulesContainer>" +
           "<CopyEnabled>" + copyEnabled.ToString().ToLower() + "</CopyEnabled>" +
           "<Rules />" +
           "</CopyRulesContainer>";
}
```

- `ResetPersistenceLoaded` uses Reflection to clear the idempotency guard so `LoadRules` executes
  its body in each test.
- `BuildRulesXml` constructs minimal valid XML matching `CopyRulesContainer` schema.
- Root element name MUST be `CopyRulesContainer` (must match class name exactly for `XmlSerializer`).
- CYC = 1 each.

---

**[Fact] T_B54_01** — LoadRules with `CopyEnabled=true` restores engine state and fires event

```csharp
[Fact]
public void T_B54_01_LoadRules_CopyEnabledTrue_EngineIsEnabledTrueAndEventFires()
{
    var engine = CopyEngine.Instance;
    string tmpPath = System.IO.Path.GetTempFileName();
    Action<bool> handler = null;
    bool? firedValue = null;
    try
    {
        System.IO.File.WriteAllText(tmpPath, BuildRulesXml(true));
        ResetPersistenceLoaded(engine);
        handler = v => firedValue = v;
        engine.CopyEnabledChanged += handler;
        engine.LoadRules(overridePath: tmpPath);
        Assert.True(engine.IsEnabled);
        Assert.True(firedValue == true);
    }
    finally
    {
        if (handler != null) engine.CopyEnabledChanged -= handler;
        System.IO.File.Delete(tmpPath);
    }
}
```

**Asserts**:
1. `engine.IsEnabled == true` — `_isCopyEnabled` was restored from XML.
2. `firedValue == true` — `CopyEnabledChanged` fired with the correct value.

---

**[Fact] T_B54_02** — LoadRules with `CopyEnabled=false` restores engine state and fires event

```csharp
[Fact]
public void T_B54_02_LoadRules_CopyEnabledFalse_EngineIsEnabledFalseAndEventFires()
{
    var engine = CopyEngine.Instance;
    string tmpPath = System.IO.Path.GetTempFileName();
    Action<bool> handler = null;
    bool? firedValue = null;
    try
    {
        System.IO.File.WriteAllText(tmpPath, BuildRulesXml(false));
        ResetPersistenceLoaded(engine);
        handler = v => firedValue = v;
        engine.CopyEnabledChanged += handler;
        engine.LoadRules(overridePath: tmpPath);
        Assert.False(engine.IsEnabled);
        Assert.True(firedValue == false);
    }
    finally
    {
        if (handler != null) engine.CopyEnabledChanged -= handler;
        System.IO.File.Delete(tmpPath);
    }
}
```

**Asserts**:
1. `engine.IsEnabled == false` — restored from XML `<CopyEnabled>false</CopyEnabled>`.
2. `firedValue == false` — event fired with correct `false` value.

---

**[Fact] T_B54_03** — SaveRules + LoadRules round-trip preserves `CopyEnabled=true`

```csharp
[Fact]
public void T_B54_03_SaveThenLoadRules_RoundTripPreservesCopyEnabled()
{
    var engine = CopyEngine.Instance;
    string tmpPath = System.IO.Path.GetTempFileName();
    try
    {
        engine.SetEnabled(true);
        engine.SaveRules(overridePath: tmpPath);
        engine.SetEnabled(false);        // reset in-memory state
        ResetPersistenceLoaded(engine);
        engine.LoadRules(overridePath: tmpPath);
        Assert.True(engine.IsEnabled);
    }
    finally
    {
        System.IO.File.Delete(tmpPath);
    }
}
```

**Asserts**:
1. After `SetEnabled(true)` + `SaveRules` + `SetEnabled(false)` + `LoadRules`, `IsEnabled == true`.
2. Confirms the full persist/restore round-trip for the `CopyEnabled` XML element.

---

### 4. Exact Current Code Locations

Engineer must open these precise locations for each edit:

#### CopyEngine.cs

| Change | Location | Action |
|---|---|---|
| `IsEnabled` property | After line 315 (after `SetEnabled` method, before `SetDailyCapFloor`) | INSERT new property |
| `CopyEnabled` in `CopyRulesContainer` | Lines 2571–2574 (class body) | INSERT `[XmlElement] public bool CopyEnabled { get; set; }` as first member |
| `SaveRules` patch | Lines 2698–2700 (after `container.Rules.Add(...)` loop, before `var serializer = new XmlSerializer...`) | INSERT `container.CopyEnabled = _isCopyEnabled;` |
| `LoadRules` patch | Lines 2741–2744 (after `_rules.Add(DtoToRule(dto))` loop, before `_persistenceLoaded = true`) | INSERT 2 statements |
| `SaveRules` signature | Existing `private void SaveRules(` declaration | ADD `string overridePath = null` parameter |
| `LoadRules` signature | Existing `private void LoadRules(` declaration | ADD `string overridePath = null` parameter |
| Path resolution in both | Inside `SaveRules` and `LoadRules` body, where path string is first assigned | REPLACE raw path expression with `overridePath ?? <existing expression>` |

#### TradeCopierPanel.cs

| Change | Location | Action |
|---|---|---|
| `ApplyCopyState` | New method, insert before or after `OnCopyEnabledChanged` (~line 1331) | INSERT new private method |
| `OnCopyEnabledChanged` body | Lines 1331–1340 | REPLACE body with `ApplyCopyState(enabled);` |
| `OnLoaded` patch | Line 610, after `_engine.CopyEnabledChanged += OnCopyEnabledChanged;` | INSERT `ApplyCopyState(_engine.IsEnabled);` |
| `OnCopyToggle` body | Lines 1319–1326 | REPLACE body with `_engine.SetEnabled(!_engine.IsEnabled);` + comment |

#### TradeCopierWindow.cs

| Change | Location | Action |
|---|---|---|
| `ApplyCopyState` | New method, insert before or after `OnCopyEnabledChanged` (~line 652) | INSERT new private method |
| `OnCopyEnabledChanged` body | Lines 652–660 | REPLACE body with `ApplyCopyState(enabled);` |
| `OnLoaded` patch | Line 127, after `_engine.CopyEnabledChanged += OnCopyEnabledChanged;` | INSERT `ApplyCopyState(_engine.IsEnabled);` |
| `OnGlobalToggle` body | Lines 641–647 | REPLACE body with `_engine.SetEnabled(!_engine.IsEnabled);` + comment |

---

### 5. JS Rule Constraints

| Rule | Description | Applies to | Enforcement |
|---|---|---|---|
| JS-021 | No `lock()` anywhere in new/modified code | All 4 files | SCAN-01 |
| JS-002 | No `return null` in new code (use guard-return `void` pattern or null-conditional `?.`) | All 4 files | SCAN-03 |
| JS-033 | No `async void` in new code (event handlers that were already `void` are pre-existing — do not change their signatures) | All 4 files | SCAN-02 |
| NT8-001 | No `init`-only setters — use `{ get; set; }` not `{ get; init; }` | `CopyRulesContainer.CopyEnabled` | Code review |
| NT8-003 | No `volatile double` or `volatile float` — `_isCopyEnabled` is `volatile bool` (pre-existing, not changed) | `CopyEngine.cs` | Code review |

---

### 6. 7-Scan Checklist

Engineer MUST run all 7 scans and report results in `ticket-1-completion.md`.

```
SCAN-01: Select-String "lock("       src\ -Recurse -Include *.cs
         REQUIRED: 0 results
         JS-021 enforcement

SCAN-02: Select-String "async void " src\ -Recurse -Include *.cs
         REQUIRED: 0 results
         JS-033 enforcement

SCAN-03: Select-String "return null" src\ -Recurse -Include *.cs
         REQUIRED: 0 NEW instances in files modified by this ticket
         (report any pre-existing baseline count separately)

SCAN-04: Select-String "throw new "  src\ -Recurse -Include *.cs
         REQUIRED: 0 NEW instances in files modified by this ticket
         (report any pre-existing baseline count separately)

SCAN-05: python scripts/complexity_audit.py
         REQUIRED: all new and modified methods CYC <= 8
         Expected: ApplyCopyState(Panel)=2, all others=1

SCAN-06: dotnet build
         REQUIRED: 0 errors (pre-existing warnings are acceptable — do not fix
         pre-existing warnings as that would constitute scope creep)

SCAN-07: dotnet test
         REQUIRED: all [Fact] pass
         Expected total: baseline count + 3 new tests (T_B54_01, T_B54_02, T_B54_03)
```

Post-scan hard-link sync (mandatory after all scans pass):
```powershell
powershell -File scripts\verify_links.ps1 -Fix
```

---

### 7. xUnit [Fact] Names

| Test method | Asserts |
|---|---|
| `T_B54_01_LoadRules_CopyEnabledTrue_EngineIsEnabledTrueAndEventFires()` | `engine.IsEnabled == true` after loading XML with `CopyEnabled=true`; `CopyEnabledChanged` fired with `true` |
| `T_B54_02_LoadRules_CopyEnabledFalse_EngineIsEnabledFalseAndEventFires()` | `engine.IsEnabled == false` after loading XML with `CopyEnabled=false`; `CopyEnabledChanged` fired with `false` |
| `T_B54_03_SaveThenLoadRules_RoundTripPreservesCopyEnabled()` | Full round-trip: `SetEnabled(true)` → `SaveRules` → `SetEnabled(false)` → `LoadRules` → `IsEnabled == true` |

Test isolation pattern: call `ResetPersistenceLoaded(engine)` via Reflection before each
`LoadRules` call to clear the idempotency guard. Tests run sequentially in xUnit (same test class).

---

### 8. Invariants (verifier must confirm)

The following invariants are non-negotiable post-conditions. Verifier must confirm each one
explicitly in `ticket-1-verification.md`:

| # | Invariant | How to verify |
|---|---|---|
| INV-1 | After `LoadRules(copyEnabled: true)`: `engine.IsEnabled == true` | T_B54_01 passes |
| INV-2 | After `LoadRules(copyEnabled: false)`: `engine.IsEnabled == false` | T_B54_02 passes |
| INV-3 | After F5 cycle (`SaveRules` + `LoadRules`): enabled state restored | T_B54_03 passes |
| INV-4 | Button colour path is: `SetEnabled` → `CopyEnabledChanged` → `ApplyCopyState` → `Dispatcher.InvokeAsync` → button | Code review: no handler directly mutates button |
| INV-5 | No surface ever calls `ApplyCopyState` from a toggle handler directly | Code review: only callers are `OnLoaded` and `OnCopyEnabledChanged` |
| INV-6 | `OnGlobalToggle` contains no direct button mutation after this change | Code review: method body is `_engine.SetEnabled(...)` only |
| INV-7 | `OnCopyToggle` contains no direct button mutation after this change | Code review: method body is `_engine.SetEnabled(...)` only |
| INV-8 | `IsEnabled` property exposes `_isCopyEnabled` read-only, no setter | Code review: expression-bodied `=> _isCopyEnabled` |
| INV-9 | `CopyRulesContainer.CopyEnabled` uses `{ get; set; }` (not `init`) | NT8-001 compliance |

Global invariant (holds after F5, window re-open, NT cold start, LoadRules, any SetEnabled call):
```
for all surfaces s, at all times t:
    s.copyButton.IsGreen  <->  CopyEngine.Instance.IsEnabled == true
```

---

### 9. Complexity Audit Reference

| Method | File | CYC | Threshold | Status |
|---|---|---|---|---|
| `IsEnabled` (property) | CopyEngine.cs | 1 | 8 | PASS |
| `CopyRulesContainer.CopyEnabled` (auto-prop) | CopyEngine.cs | 1 | 8 | PASS |
| `SaveRules` (modified: +1 stmt, +0 branches) | CopyEngine.cs | unchanged | 8 | PASS |
| `LoadRules` (modified: +2 stmts, +0 branches) | CopyEngine.cs | unchanged | 8 | PASS |
| `ApplyCopyState(bool)` — Panel | TradeCopierPanel.cs | 2 | 8 | PASS |
| `OnCopyEnabledChanged` — Panel (modified) | TradeCopierPanel.cs | 1 | 8 | PASS |
| `OnCopyToggle` (modified) | TradeCopierPanel.cs | 1 | 8 | PASS |
| `OnLoaded` — Panel (modified: +1 stmt) | TradeCopierPanel.cs | unchanged | 8 | PASS |
| `ApplyCopyState(bool)` — Window | TradeCopierWindow.cs | 1 | 8 | PASS |
| `OnCopyEnabledChanged` — Window (modified) | TradeCopierWindow.cs | 1 | 8 | PASS |
| `OnGlobalToggle` (modified) | TradeCopierWindow.cs | 1 | 8 | PASS |
| `OnLoaded` — Window (modified: +1 stmt) | TradeCopierWindow.cs | unchanged | 8 | PASS |
| `T_B54_01` | CopyEngineTests.cs | 1 | 8 | PASS |
| `T_B54_02` | CopyEngineTests.cs | 1 | 8 | PASS |
| `T_B54_03` | CopyEngineTests.cs | 1 | 8 | PASS |
| `ResetPersistenceLoaded` (helper) | CopyEngineTests.cs | 1 | 8 | PASS |
| `BuildRulesXml` (helper) | CopyEngineTests.cs | 1 | 8 | PASS |

All new and modified methods: CYC <= 8. No method exceeds threshold.

---

### 10. Out-of-Scope Items (do not touch)

The following items are explicitly deferred. Engineer MUST NOT touch them:

| ID | Description |
|---|---|
| DW-B54-01 | `AtmStrategyCreate` AddOn API path — Director research required |
| DW-B54-02 | F5-GATE-02 live ATM bracket test (blocked by DW-B54-01) |
| B53-backlog DW-B54-03 | Diagnostic log for `#if NT8_ADDON_ATM` inactive state |
| DW-BACKLOG-01 | `PttContracts.cs` FillSignal dead-code cleanup |

If any pre-existing warning or unrelated error is encountered during work: **stop, report to Director,
do not fix inline** (No Scope Creep Protocol, `.bob/rules/` §11).

---

*Ticket status*: **TICKETS_COMPLETE**
