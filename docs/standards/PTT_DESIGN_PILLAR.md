# PTT Design Pillar — "Live Map"
**Established:** B7
**Applies to:** All PTT surfaces — TradeCopierPanel, TradeCopierWindow, all future blocks, all future features

---

## Core Principle

**Every pixel on every surface is a function of live system state — not of user action history.**

The ChartTrader Panel and the Trade Copier Window are not two UIs.
They are two viewports into the same live system.
The source of truth is CopyEngine and NT account state.
The UI renders that truth. Nothing more.

Nothing says "click me to find out."
Nothing is grey because we forgot to enable it.
Nothing is green because the user last clicked ON.
Every control reflects the exact state of the system it acts on, right now, from any surface.

---

## The Four Layers

### Layer 1 — Labels are state

Control text reflects live state — always. Never a placeholder. Never a prompt.

| Good | Bad | Why bad |
|---|---|---|
| `"2 selected"` | `"Select followers..."` | Prompt tells you nothing about current state |
| `"Copy ON"` / `"Copy OFF"` | `"Copy"` | Ambiguous — is it on or off? |
| `"MES: 4 long \| +$120"` | `"Ready"` | Ready for what? Show the actual state |
| `"Rule: MES enabled"` | `"Status"` | Label restates the control name, not the state |
| `"0 selected"` | `""` (blank) | Blank is not a state — zero is a state |

### Layer 2 — Color is state

Color is never decorative. Every color carries exactly one meaning and is used consistently
across all surfaces. When the same state occurs in the Panel and the Window, the same color
appears on both.

**Button background semantic map:**

| Color | Semantic | Applied to |
|---|---|---|
| Green | Active / safe positive action available | Copy ON, BE (when position open) |
| Amber | Partial / caution action available | Trim (when position open) |
| Red | Destructive action available | Flatten, Cancel (when position or entries exist) |
| Dark grey | Action not currently meaningful | Any button when its target state does not exist |

**Text / foreground semantic map:**

| Color | Semantic | Applied to |
|---|---|---|
| Green text | Positive numeric value | P&L positive (`+$120`), long position count |
| Red text | Negative numeric value | P&L negative (`-$80`), short position count |
| White/bright text | Armed / selection active | Followers dropdown when 1+ selected |
| Dim/grey text | Disarmed / nothing selected | Followers dropdown when 0 selected, secondary labels |

**Color constants — defined once as `static readonly SolidColorBrush`, used everywhere:**

```csharp
// Semantic brushes -- match spec HTML CSS variables exactly
// --green: #22c55e  --red: #ef4444  --amber: #f59e0b  --dim: #4b5563  --raised: #111520
private static readonly SolidColorBrush BrushActive   = new SolidColorBrush(Color.FromRgb(34,  197, 94));  // green
private static readonly SolidColorBrush BrushDanger   = new SolidColorBrush(Color.FromRgb(239, 68,  68));  // red
private static readonly SolidColorBrush BrushCaution  = new SolidColorBrush(Color.FromRgb(245, 158, 11));  // amber
private static readonly SolidColorBrush BrushInactive = new SolidColorBrush(Color.FromRgb(55,  65,  81));  // dark grey
private static readonly SolidColorBrush BrushPositive = new SolidColorBrush(Color.FromRgb(34,  197, 94));  // green (text)
private static readonly SolidColorBrush BrushNegative = new SolidColorBrush(Color.FromRgb(239, 68,  68));  // red (text)
private static readonly SolidColorBrush BrushDim      = new SolidColorBrush(Color.FromRgb(107, 114, 128)); // grey (text)
```

These RGB values intentionally match the spec HTML CSS variables so the spec document
and the live UI speak the same visual language.

### Layer 3 — Enabled state is state

A grey button is information. It tells the user: the system state this action would operate
on does not currently exist. The user does not need to check the position panel — the button
color IS the position panel for the purpose of this action.

**Rules:**

- A button is active-colored (green/amber/red) only when its action is currently meaningful
- A button is dark grey when its action has no current target
- This is evaluated live, not set once at click time
- The evaluation re-runs on every relevant state change event

**State gates per button:**

| Button | Active condition | Inactive condition |
|---|---|---|
| Copy ON/OFF | always interactive — state is ON or OFF, both are valid | never grey — toggle is always meaningful |
| Trim | open position exists on this instrument | no position — grey |
| Flatten | open position exists on this instrument | no position — grey |
| Cancel | working entry orders exist on this instrument | no working entries — grey |
| BE | open position exists on this instrument | no position — grey |
| Apply Rule | always interactive — wires a rule, valid at any time | never grey |

**Wiring:**
CopyEngine fires `PositionStateChanged(instrumentName, hasOpenPosition, hasWorkingEntries)`
from its existing `OnOrderUpdate` handler. All surfaces subscribe and call
`UpdateButtonColors(hasPosition, hasEntries)` via `Dispatcher.InvokeAsync`. One event,
any number of listening surfaces.

### Layer 4 — One system, any surface

The Panel and the Window are both live views of the same CopyEngine state.

- Toggle Copy ON from the Panel -- the Window button goes green simultaneously
- A position opens -- both Panel and Window buttons transition from grey to colored simultaneously
- A fill closes the position -- both surfaces go grey simultaneously
- The user never needs to check one surface to know the state of the other

This is not a synchronization problem. It is a rendering problem. Both surfaces subscribe to
the same CopyEngine events and re-render on receipt. CopyEngine does not know or care how many
surfaces are listening.

**Architectural rule:**
> No PTT surface may hold UI state that is not derivable from CopyEngine state plus NT account state.
> A button's color, a label's text, a count's value — all are computed from live data, never stored
> as a local UI variable that can drift from reality.

---

## Applied Examples

### Copy toggle — full button color
```
[  Copy ON  ]   green background -- engine is copying
[  Copy OFF ]   dark grey background -- engine is paused
```
Label AND color both carry the state. Double signal. Unambiguous at a glance.

### Flatten / Cancel — red only when live
```
Position open:    [ Flatten ]  red background -- action is available and destructive
No position:      [ Flatten ]  dark grey background -- no target, action not meaningful
```
The color is not a warning. It is a readout: "there is something here to flatten."

### Trim / BE — amber / green only when live
```
Position open:    [ Trim 1/2 ]  amber background -- partial exit available
No position:      [ Trim 1/2 ]  dark grey background -- nothing to trim

Position open:    [ BE ]        green background -- move stop is meaningful
No position:      [ BE ]        dark grey background -- no stop to move
```

### Followers dropdown — foreground reflects armed state
```
0 accounts checked:   "0 selected"   dim text -- rule cannot fire
2 accounts checked:   "2 selected"   bright text -- rule is armed
```

### Status line — P&L text color
```
"MES: 4 long | +$120"   +$120 in green text
"MES: 2 short | -$80"   -$80 in red text
"No position"            dim text -- no active state
```

---

## NT8 Implementation Rules

### Color assignment
- `Button.Background` = semantic `SolidColorBrush` — set in `UpdateButtonColors()`
- `TextBlock.Foreground` = semantic `SolidColorBrush` or `SetResourceReference` for neutral/dim
- Never `SetResourceReference` for semantic colors — NT theme should NOT override green/red signals
- Always `SetResourceReference` for neutral colors — NT theme should control them

### JS-008: brush.Freeze() requirement (MANDATORY)

All `static readonly SolidColorBrush` semantic constants MUST call `.Freeze()` after construction.
`Freeze()` makes the brush immutable and thread-safe — required for `Dispatcher.InvokeAsync` callbacks
that set `Button.Background` from the CopyEngine order-update thread.

```csharp
// Helper used by all PTT files that declare semantic brush constants
private static SolidColorBrush MakeBrush(byte r, byte g, byte b)
{
    var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
    brush.Freeze(); // immutable + thread-safe (JS-008, WPF threading rule)
    return brush;
}

// Usage -- all semantic brush constants use MakeBrush():
private static readonly SolidColorBrush BrushActive   = MakeBrush(34,  197, 94);  // green
private static readonly SolidColorBrush BrushDanger   = MakeBrush(239, 68,  68);  // red
private static readonly SolidColorBrush BrushCaution  = MakeBrush(245, 158, 11);  // amber
private static readonly SolidColorBrush BrushInactive = MakeBrush(55,  65,  81);  // dark grey
private static readonly SolidColorBrush BrushPositive = MakeBrush(34,  197, 94);  // green (text)
private static readonly SolidColorBrush BrushNegative = MakeBrush(239, 68,  68);  // red (text)
private static readonly SolidColorBrush BrushDim      = MakeBrush(107, 114, 128); // grey (text)
```

**Why Freeze() is mandatory (not optional):**
- WPF throws `InvalidOperationException` if you assign an unfrozen brush from a non-UI thread
- CopyEngine fires `PositionStateChanged` from the NT order-update thread (not UI thread)
- `Dispatcher.InvokeAsync` marshals the callback to the UI thread, but the brush object
  is captured in the closure — it must already be frozen at capture time
- `static readonly` + `Freeze()` = zero allocation on re-render + full thread safety

**ptt-verifier scan rule (SCAN-08 — unfrozen brushes):**
Any `new SolidColorBrush(...)` that does NOT immediately call `.Freeze()` is a VERIFY_FAIL.
The only exception: brushes created AND consumed on the UI thread in a single synchronous call.

---

### JS-003: PositionState readonly struct (MANDATORY)

The `PositionStateChanged` event signature uses TWO bool parameters. Two anonymous bools are
a value object — they must be a `readonly struct` so the type system prevents misuse (JS-003).

```csharp
// In CopyEngine.cs -- define this struct (additive, outside CopyEngine class)
public readonly struct PositionState
{
    public bool HasOpenPosition  { get; init; }
    public bool HasWorkingEntries { get; init; }
}

// Corrected event signature (replace Action<string, bool, bool>):
public event Action<string, PositionState> PositionStateChanged;

// Fire site (in CopyEngine.OnOrderUpdate -- additive):
PositionStateChanged?.Invoke(instrumentName, new PositionState
{
    HasOpenPosition   = hasPos,
    HasWorkingEntries = hasEntries
});

// Handler in TradeCopierPanel / TradeCopierWindow:
private void OnPositionStateChanged(string instr, PositionState state)
{
    if (_instrument == null || _instrument.FullName != instr) return;
    Dispatcher.InvokeAsync(() => UpdateButtonColors(state.HasOpenPosition, state.HasWorkingEntries));
}
```

**Why struct, not two loose bools:**
- Two `bool` parameters in a callback can be silently transposed by any caller
- A named struct makes each field's meaning unambiguous at the call site
- `readonly struct` = no defensive copy overhead, zero allocation on fire

---

### Event pattern
```csharp
// In CopyEngine -- fired from OnOrderUpdate (existing handler, additive only)
// NOTE: uses PositionState struct (JS-003), not raw bool pair
public event Action<string, PositionState> PositionStateChanged;

// In TradeCopierPanel / TradeCopierWindow -- subscribe in constructor / Loaded
_engine.PositionStateChanged += OnPositionStateChanged;

// Handler -- always via Dispatcher.InvokeAsync (off-thread callback)
private void OnPositionStateChanged(string instr, PositionState state)
{
    if (_instrument == null || _instrument.FullName != instr) return;
    Dispatcher.InvokeAsync(() => UpdateButtonColors(state.HasOpenPosition, state.HasWorkingEntries));
}
```

### Scan compliance
- `Color.FromRgb(r, g, b)` is NOT a string literal — SCAN-04 passes
- No `FontFamily` — SCAN-03 passes
- No `lock()` — SCAN-01 passes
- `SolidColorBrush` constructed once via `MakeBrush()` as `static readonly` — zero allocation on re-render
- All brushes call `.Freeze()` via `MakeBrush()` — WPF thread safety + JS-008 compliance (SCAN-08)

---

## Implementation Checklist (per control added)

- [ ] Does the label show current state, not a prompt?
- [ ] Does the button color reflect live system state?
- [ ] Is the button grey when its action has no current target?
- [ ] Is `FontFamily`/`FontSize` explicitly set? (must be NO -- inherit only)
- [ ] Are neutral colors via `SetResourceReference`? (no literals)
- [ ] Are semantic colors (`BrushActive`, `BrushDanger`, etc.) from the constant set?
- [ ] Is `NTButtonStyle` applied as base style, with `Background` overridden per state?
- [ ] Is color update happening via `Dispatcher.InvokeAsync`?
- [ ] Does this surface subscribe to `PositionStateChanged`?
- [ ] If CopyEnabled changes on one surface, does the other surface reflect it?

---

## Scheduled Implementation (B7 pipeline)

The following code changes implement this pillar. They are queued for the B7 nt-builder run.

### B7-F1: Button color coding (Layer 2 + Layer 3)

**Files:** `TradeCopierPanel.cs`, `TradeCopierWindow.cs`

Changes:
- Add private `static SolidColorBrush MakeBrush(byte r, byte g, byte b)` helper (calls `.Freeze()`)
- Add 7 `static readonly SolidColorBrush` constants via `MakeBrush()` — all frozen at class init (JS-008)
- Add `UpdateButtonColors(bool hasPosition, bool hasEntries)` method
  - Copy toggle: green if `_copyEnabled`, dark grey if not
  - Trim/Flatten/BE: active color if `hasPosition`, dark grey if not
  - Cancel: danger color if `hasEntries`, dark grey if not
- Call `UpdateButtonColors(false, false)` at end of `BuildUI()` — initial state is grey
- Subscribe to `_engine.PositionStateChanged` in constructor / Loaded
- Unsubscribe in `Detach()` / `OnClosed()`
- Handler signature: `OnPositionStateChanged(string instr, PositionState state)` — JS-003

### B7-F1 dependency: CopyEngine types + event (Layer 3 + Layer 4)

**File:** `CopyEngine.cs`

Changes (additive only — no existing logic touched):
- Add `public readonly struct PositionState` outside `CopyEngine` class (JS-003)
  - `bool HasOpenPosition { get; init; }`
  - `bool HasWorkingEntries { get; init; }`
- Add `public abstract record FollowerAtmMode` outside `CopyEngine` class (JS-003)
  - `sealed record Inherit()`, `sealed record Market()`, `sealed record Named(string)`
  - Private base constructor (JS-010)
- Add `public event Action<string, PositionState> PositionStateChanged` — typed struct, not raw bool pair
- In existing `OnOrderUpdate`: after processing, evaluate position/entry state and fire event
- Fire on: Filled, PartFilled, Cancelled, Rejected — any state that changes position truth
- Add `ImmutableDictionary<string, FollowerAtmMode> FollowerAtmTemplates { get; init; }` to `CopyRule`
  - Default: `ImmutableDictionary<string, FollowerAtmMode>.Empty` (JS-009)
  - B7: field exists, dictionary always empty, zero behavior change

### B7-F2: Status strip with live P&L (Layer 1 + Layer 2)

**Files:** `TradeCopierPanel.cs`, `TradeCopierWindow.cs`

Changes:
- Status line parses for `+` / `-` prefix on numeric segment
- Sets `_statusText.Foreground` to `BrushPositive` / `BrushNegative` / `BrushDim` accordingly
- Followers dropdown header: sets `Foreground` to bright when count > 0, dim when 0

---

## Relation to NT8 Constraints

| Pillar rule | NT8 constraint satisfied |
|---|---|
| No `FontFamily` set | SCAN-03 automatic |
| No `#RRGGBB` string literals | SCAN-04 automatic (`Color.FromRgb` is not a string) |
| `SetResourceReference` for neutral colors | Survives NT dark/light theme switch |
| Semantic colors hardcoded (not resource refs) | Green stays green regardless of NT theme |
| `Dispatcher.InvokeAsync` for all color updates | Off-thread callback safety |
| `static readonly` brushes via `MakeBrush()` | Zero allocation in hot path |
| All brushes `.Freeze()`d via `MakeBrush()` | JS-008 — WPF thread safety, SCAN-08 |
| `PositionState` readonly struct on event | JS-003 — named value object, no anonymous bool pair |
| `PositionStateChanged` event (no lock) | JS-021 compliant |
