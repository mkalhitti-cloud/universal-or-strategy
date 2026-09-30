# B54-LaneA Architecture Plan — UI Live-Truth Sync (DW-B54-03 P0)

**Status**: REVIEW_PENDING
**Epic**: PTT-COPIER B54 LaneA
**Work item**: DW-B54-03 P0 — UI state desync: buttons not live with engine truth after F5 / surface create
**Spec**: `specs/002-trade-copier-spec.html` id="section-b54"
**Produced by**: ptt-architect Phase 1

---

## §1 Problem Statement

Three independent failures combine to produce the symptom (button shows COPY ON but engine is OFF):

**Root Cause A — OnLoaded never reads engine state**
Both `TradeCopierPanel.OnLoaded` and `TradeCopierWindow.OnLoaded` initialise their button visual
from a stale local `_copyEnabled` field (default `false`) rather than from `_engine.IsEnabled`.
After F5, the engine singleton is fresh (`IsEnabled = false`) and the panel is a new WPF object
(`_copyEnabled = false` too), so the values coincide by accident — but any visual-tree reuse by
NT8 WPF may carry forward the button colour from the previous panel instance. Neither surface
queries the authoritative engine state on construction.

**Root Cause B — copy_rules.xml does not persist copy-enabled state**
`SaveRules()` serialises `CopyRule` entries but does not write the global copy-enabled boolean.
On every reload, `_isCopyEnabled` starts `false` regardless of what state the user left it in.
There is no persistence path for the Copy ON/OFF decision across F5 or NT restart.

**Root Cause C — CopyEnabledChanged not fired after LoadRules**
Even if copy-enabled were restored from XML, `LoadRules()` currently does not fire
`CopyEnabledChanged` after writing `_isCopyEnabled`. Any surface already subscribed (panel
subscribed during `OnLoaded` before `LoadRules` runs) would never receive the event and would
remain visually stale.

---

## §2 Solution Overview — State Machine

```
── CopyEngine (single source of truth) ──────────────────────────────────────

  IsEnabled (public bool property, reads _isCopyEnabled volatile bool)
  _isCopyEnabled persisted in copy_rules.xml via CopyRulesContainer.CopyEnabled

  SetEnabled(bool value):             [UNCHANGED from B20]
    _isCopyEnabled = value
    CopyEnabledChanged?.Invoke(value) // all surfaces update

  LoadRules(string overridePath = null):   [MODIFIED]
    ... existing deserialization logic ...
    _isCopyEnabled = container.CopyEnabled  // NEW: restore from XML
    CopyEnabledChanged?.Invoke(_isCopyEnabled)  // NEW: snap subscribed surfaces
    _persistenceLoaded = true

  SaveRules(string overridePath = null):   [MODIFIED]
    container.CopyEnabled = _isCopyEnabled  // NEW: persist before serializing
    ... existing serialization logic ...

── TradeCopierPanel / TradeCopierWindow ─────────────────────────────────────

  OnLoaded():
    _engine.CopyEnabledChanged += ApplyCopyState  // existing B20 subscribe
    ApplyCopyState(_engine.IsEnabled)             // NEW: snap to current truth NOW

  OnCopyToggle() / OnGlobalToggle():
    _engine.SetEnabled(!_engine.IsEnabled)        // CHANGED: was direct mutation
    // event drives ApplyCopyState — no direct button mutation here

  OnCopyEnabledChanged(bool enabled):
    ApplyCopyState(enabled)                       // CHANGED: was inline mutation

  ApplyCopyState(bool enabled):                   // NEW private method
    _copyEnabled = enabled
    Dispatcher.InvokeAsync(() => {
      // Panel: if (_copyToggleBtn2 == null) return;
      button.Content    = enabled ? "<on-label>"  : "<off-label>"
      button.Background = enabled ? BrushActive   : BrushInactive
    })

  Detach() / OnWindowClosed():
    _engine.CopyEnabledChanged -= ApplyCopyState  // existing B20 unsubscribe

── Invariant ─────────────────────────────────────────────────────────────────

  For all surfaces s, at all times t:
    s.copyButton.IsGreen <-> CopyEngine.Instance.IsEnabled
  Holds after: F5, window re-open, NT cold start, LoadRules, any SetEnabled call.
```

---

## §3 CopyEngine.cs Changes

**File**: `src/PropTraderTools/CopyEngine.cs`

### A1 — Add `IsEnabled` public property

Insert after `SetEnabled` method (line ~315):

```csharp
public bool IsEnabled => _isCopyEnabled;
```

- Exposes the existing `_isCopyEnabled` volatile bool as a readable property.
- No setter; engine state changes through `SetEnabled` only.
- CYC = 1 (expression-bodied, no branches).

### A2 — Add `CopyEnabled` field to `CopyRulesContainer`

Modify `CopyRulesContainer` class (currently at line ~2571):

```csharp
public class CopyRulesContainer
{
    [XmlElement]
    public bool CopyEnabled { get; set; }   // NEW: default false (backward compat)

    [XmlElement]
    public List<CopyRuleDto> Rules { get; set; }

    public CopyRulesContainer()
    {
        Rules = new List<CopyRuleDto>();
    }
}
```

- `[XmlElement]` causes XmlSerializer to write/read `<CopyEnabled>true</CopyEnabled>` in XML.
- Default `false` means old XML files without the element deserialise safely (XmlSerializer
  leaves missing elements at their default value).
- `{ get; set; }` — standard setter. NT8-001 check: NOT an `init`-only setter. PASS.

### A3 — Modify `SaveRules()` (line ~2689)

Add one statement before the `XmlSerializer.Serialize` call:

```csharp
container.CopyEnabled = _isCopyEnabled;   // NEW: persist global toggle state
```

Existing serialization logic is unchanged. CYC: +0 branches.

### A4 — Modify `LoadRules()` (line ~2724)

Add two statements at the end of the `try` block, immediately before `_persistenceLoaded = true`:

```csharp
_isCopyEnabled = container.CopyEnabled;              // NEW: restore from XML
CopyEnabledChanged?.Invoke(_isCopyEnabled);          // NEW: snap subscribed surfaces
```

Both statements go INSIDE the existing `try` block, AFTER all rules have been added to the engine.
The `_persistenceLoaded = true` line follows on the next line (unchanged).

Signature change: add optional `string overridePath = null` parameter to both `SaveRules` and
`LoadRules` to support test injection of a temporary file path:

```csharp
private void SaveRules(string overridePath = null) { ... }
private void LoadRules(string overridePath = null) { ... }
```

Inside each method, resolve the actual path as:
```csharp
string path = overridePath ?? /* existing NT8 path resolution expression */;
```

The null-coalescing `??` adds zero formal CYC branches (McCabe metric does not count `??`).
All existing callers pass no argument and continue to work unchanged.

---

## §4 TradeCopierPanel.cs Changes

**File**: `src/PropTraderTools/TradeCopierPanel.cs`

### B1 — Add `ApplyCopyState(bool enabled)` method

Insert as a new private method (after `OnCopyEnabledChanged` method, approx. line 1341):

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

- `_copyEnabled` field write is safe: `bool` assignment is atomic on x86/x64 JIT.
- `Dispatcher.InvokeAsync` marshals to the UI thread without blocking the caller. Required because
  `CopyEnabledChanged` can fire from the NT8 init thread (when `LoadRules` calls it) or from the
  UI thread (when `SetEnabled` is called from a toggle handler). `InvokeAsync` is safe in both cases.
- Null guard on `_copyToggleBtn2` handles the NT8 WPF template quirk where the button reference
  may not be set yet during the very first `OnLoaded` invocation.
- CYC = 2 (1 base + 1 null-check branch).

### B2 — Replace `OnCopyEnabledChanged` body

Current body (line 1331–1340) directly mutates `_copyEnabled` and `_copyToggleBtn2` via
Dispatcher. Replace the entire body with a single delegate call:

```csharp
private void OnCopyEnabledChanged(bool enabled)
{
    ApplyCopyState(enabled);
}
```

- CYC = 1 (straight-line call).
- All visual logic now lives exclusively in `ApplyCopyState`.

### B3 — Add `ApplyCopyState` call in `OnLoaded`

In `OnLoaded`, immediately after the existing subscribe line (line 610):

```csharp
_engine.CopyEnabledChanged += OnCopyEnabledChanged;   // existing (B20)
ApplyCopyState(_engine.IsEnabled);                    // NEW: snap to engine truth on load
```

- This ensures the panel is correct the moment it appears, regardless of whether
  `LoadRules` has already fired `CopyEnabledChanged` or not.
- CYC impact on `OnLoaded`: +0 branches (unconditional call added).

### B4 — Replace `OnCopyToggle` body

Current body (line 1318–1326) directly mutates `_copyToggleBtn2.Content` and
`_copyToggleBtn2.Background`. Replace the mutation block with engine delegation:

```csharp
private void OnCopyToggle(object sender, RoutedEventArgs e)
{
    _engine.SetEnabled(!_engine.IsEnabled);
    // Visual update happens via CopyEnabledChanged -> OnCopyEnabledChanged -> ApplyCopyState
}
```

- Removes direct button mutation from toggle handler permanently.
- The engine fires `CopyEnabledChanged`, which calls `OnCopyEnabledChanged(enabled)`, which
  calls `ApplyCopyState(enabled)`. Single authoritative path guaranteed.
- CYC = 1 (straight-line call, no branches).

---

## §5 TradeCopierWindow.cs Changes

**File**: `src/PropTraderTools/TradeCopierWindow.cs`

### C1 — Add `ApplyCopyState(bool enabled)` method

Insert as a new private method (after `OnCopyEnabledChanged`, approx. line 661):

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

- No null guard needed for `_globalToggleBtn`: Window WPF lifecycle guarantees the control is
  initialised before `OnLoaded` fires (Window is a top-level WPF element, not a ChartTrader panel
  template). CYC = 1 (straight-line, no branches).

### C2 — Replace `OnCopyEnabledChanged` body

Current body (line 652–660) directly mutates. Replace with:

```csharp
private void OnCopyEnabledChanged(bool enabled)
{
    ApplyCopyState(enabled);
}
```

- CYC = 1.

### C3 — Add `ApplyCopyState` call in `OnLoaded`

After existing subscribe line (line 127):

```csharp
_engine.CopyEnabledChanged += OnCopyEnabledChanged;   // existing (B20)
ApplyCopyState(_engine.IsEnabled);                    // NEW: snap to engine truth on load
```

- CYC impact: +0 branches.

### C4 — Replace `OnGlobalToggle` body

Current body (line 641–647) directly mutates `_globalToggleBtn.Content` and
`_globalToggleBtn.Background`. Replace with engine delegation:

```csharp
private void OnGlobalToggle(object sender, RoutedEventArgs e)
{
    _engine.SetEnabled(!_engine.IsEnabled);
    // Visual update via CopyEnabledChanged -> OnCopyEnabledChanged -> ApplyCopyState
}
```

- CYC = 1.

---

## §6 Test Design

**File**: `tests/CopyEngineTests.cs` (append three [Fact] methods)

### Reflection reset helper (used in all three tests)

```csharp
private static void ResetPersistenceLoaded(CopyEngine engine)
{
    typeof(CopyEngine)
        .GetField("_persistenceLoaded",
                  BindingFlags.NonPublic | BindingFlags.Instance)
        ?.SetValue(engine, false);
}
```

Purpose: clears the idempotency guard so `LoadRules` executes its body in each test.

### XML construction helper

```csharp
private static string BuildRulesXml(bool copyEnabled)
{
    return $"<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
           $"<CopyRulesContainer>" +
           $"<CopyEnabled>{copyEnabled.ToString().ToLower()}</CopyEnabled>" +
           $"<Rules />" +
           $"</CopyRulesContainer>";
}
```

Note: `XmlSerializer` deserialises `<CopyEnabled>true</CopyEnabled>` as `bool` property
correctly. The root element name must match the class name exactly (`CopyRulesContainer`).

### T_B54_01 — LoadRules restores enabled=true and fires event with true

```csharp
[Fact]
public void T_B54_01_LoadRules_CopyEnabled_True_RestoresStateAndFiresEvent()
{
    var engine = CopyEngine.Instance;
    string tmpPath = Path.GetTempFileName();
    Action<bool> handler = null;
    bool? firedValue = null;
    try
    {
        File.WriteAllText(tmpPath, BuildRulesXml(true));
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
        File.Delete(tmpPath);
    }
}
```

### T_B54_02 — LoadRules restores enabled=false and fires event with false

```csharp
[Fact]
public void T_B54_02_LoadRules_CopyEnabled_False_RestoresStateAndFiresEvent()
{
    var engine = CopyEngine.Instance;
    string tmpPath = Path.GetTempFileName();
    Action<bool> handler = null;
    bool? firedValue = null;
    try
    {
        File.WriteAllText(tmpPath, BuildRulesXml(false));
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
        File.Delete(tmpPath);
    }
}
```

### T_B54_03 — SaveRules / LoadRules round-trip preserves CopyEnabled=true

```csharp
[Fact]
public void T_B54_03_SaveThenLoad_RoundTrip_PreservesEnabled()
{
    var engine = CopyEngine.Instance;
    string tmpPath = Path.GetTempFileName();
    try
    {
        engine.SetEnabled(true);
        engine.SaveRules(overridePath: tmpPath);
        engine.SetEnabled(false);    // reset in-memory state
        ResetPersistenceLoaded(engine);
        engine.LoadRules(overridePath: tmpPath);
        Assert.True(engine.IsEnabled);
    }
    finally
    {
        File.Delete(tmpPath);
    }
}
```

**Note on singleton isolation**: `CopyEngine.Instance` is a singleton. Tests run sequentially in
xUnit (same test class). Each test resets `_persistenceLoaded` before calling `LoadRules`, and
resets engine state via `SetEnabled` or `LoadRules` as needed. Tests are order-independent given
the reset pattern.

---

## §7 Invariants

From spec `id="section-b54"` (non-negotiable contract):

1. **Engine is authority.** `CopyEngine.IsEnabled` is ground truth. No surface stores its own
   authoritative copy of copy-enabled state.

2. **OnLoaded snaps to truth.** Every surface MUST call `ApplyCopyState(_engine.IsEnabled)`
   unconditionally inside `OnLoaded`, after subscribing to `CopyEnabledChanged`.

3. **Events drive visuals.** `CopyEnabledChanged` fires on every engine state change (SetEnabled)
   and after every LoadRules restore. Subscribed surfaces update via `Dispatcher.InvokeAsync`.

4. **Single visual path.** No surface ever calls button mutation code directly from a toggle
   handler. All button visual updates flow through `ApplyCopyState(bool)` exclusively. The only
   callers of `ApplyCopyState` are: `OnLoaded` (initial snap) and `OnCopyEnabledChanged` (event).

5. **Persistence round-trip.** `SaveRules()` writes `CopyEnabled` to XML. `LoadRules()` reads it
   back and fires the event. The Copy ON/OFF state survives F5 and NT cold restart.

Verification formula:
```
for all surfaces s, all times t:
  s.copyButton.IsGreen  <->  CopyEngine.Instance.IsEnabled == true
```
Holds after: F5, window re-open, NT cold start, LoadRules, any SetEnabled call, with no
user interaction required.

---

## §8 Complexity Audit

| Method | File | CYC | Status |
|---|---|---|---|
| `IsEnabled` (property) | CopyEngine.cs | 1 | PASS |
| `CopyRulesContainer.CopyEnabled` (auto-prop) | CopyEngine.cs | 1 | PASS |
| `SaveRules` (modified: +1 statement, 0 branches) | CopyEngine.cs | unchanged | PASS |
| `LoadRules` (modified: +2 statements, 0 branches) | CopyEngine.cs | unchanged | PASS |
| `ApplyCopyState(bool)` — Panel | TradeCopierPanel.cs | 2 (null guard) | PASS |
| `OnCopyEnabledChanged` — Panel (modified) | TradeCopierPanel.cs | 1 | PASS |
| `OnCopyToggle` (modified) | TradeCopierPanel.cs | 1 | PASS |
| `OnLoaded` — Panel (modified: +1 stmt) | TradeCopierPanel.cs | unchanged | PASS |
| `ApplyCopyState(bool)` — Window | TradeCopierWindow.cs | 1 | PASS |
| `OnCopyEnabledChanged` — Window (modified) | TradeCopierWindow.cs | 1 | PASS |
| `OnGlobalToggle` (modified) | TradeCopierWindow.cs | 1 | PASS |
| `OnLoaded` — Window (modified: +1 stmt) | TradeCopierWindow.cs | unchanged | PASS |
| `T_B54_01` | CopyEngineTests.cs | 1 | PASS |
| `T_B54_02` | CopyEngineTests.cs | 1 | PASS |
| `T_B54_03` | CopyEngineTests.cs | 1 | PASS |
| `ResetPersistenceLoaded` (helper) | CopyEngineTests.cs | 1 | PASS |
| `BuildRulesXml` (helper) | CopyEngineTests.cs | 1 | PASS |

All new and modified methods: CYC <= 8. No method exceeds threshold.

---

## §9 JS / NT8 Rule Compliance Table

| Rule | Description | Status | Evidence |
|---|---|---|---|
| JS-021 | No `lock()` | PASS | Zero lock calls in any new or modified method |
| JS-002 | No `return null` | PASS | All new methods are `void` or return `bool` (non-nullable) |
| JS-033 | No `async void` | PASS | `ApplyCopyState` is `private void` (synchronous); `Dispatcher.InvokeAsync` is an expression inside, does not make the containing method async. Event handlers that were already `void` are the permitted exception and are unchanged in signature. |
| NT8-001 | No `init`-only setters | PASS | `CopyRulesContainer.CopyEnabled` uses `{ get; set; }` (standard setter). No `init` keyword anywhere. |
| NT8-003 | No `volatile double/float` | PASS | `_isCopyEnabled` is `volatile bool` (existing field, not changed). No new volatile fields added. |

---

## §10 Deferred Items

The following items are explicitly OUT OF SCOPE for B54-LaneA. They remain open in the backlog.

| ID | Description | Priority | Status |
|---|---|---|---|
| DW-B54-01 | `AtmStrategyCreate` AddOn API path — NT8-055 Director research item | P0 | DEFERRED — Director research required |
| DW-B54-02 | F5-GATE-02 live ATM bracket test (Sim101 → Sim102) | P0 | BLOCKED by DW-B54-01 |
| B53-backlog DW-B54-03 | Diagnostic log for `#if NT8_ADDON_ATM` inactive state | P2 | DEFERRED — non-blocking observability aid |
| DW-BACKLOG-01 | `PttContracts.cs` FillSignal dead-code cleanup | P2 | DEFERRED — harmless, independent epic |

This lane (B54-LaneA) addresses DW-B54-03 (spec definition: UI state desync) and closes it.
None of the four items above are touched by any change in §3–§6.

---

## §11 7-Scan Contract (engineer reference)

All 7 scans must pass before `VERIFY_PASS` is declared:

| Scan | Command | Required result |
|---|---|---|
| SCAN-01 | `Select-String "lock(" src/ -Recurse -Include *.cs` | 0 results |
| SCAN-02 | `Select-String "async void " src/ -Recurse -Include *.cs` | 0 results |
| SCAN-03 | `Select-String "return null" src/ -Recurse -Include *.cs` | 0 new instances |
| SCAN-04 | `Select-String "throw new " src/ -Recurse -Include *.cs` | 0 new instances |
| SCAN-05 | `python scripts/complexity_audit.py` | All new methods CYC <= 8 |
| SCAN-06 | `dotnet build` | 0 errors (pre-existing warnings OK) |
| SCAN-07 | `dotnet test` | All [Fact] pass (baseline ~258 + 3 new = ~261 total) |

Post-scan sync: `powershell -File scripts\verify_links.ps1 -Fix`

---

*Plan status*: **REVIEW_PENDING** — awaiting ptt-plan-reviewer Phase 2 sign-off.
