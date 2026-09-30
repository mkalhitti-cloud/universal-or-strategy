# PTT-COPIER-B1 Implementation Tickets

**Status:** TICKETS_COMPLETE
**Spec:** specs/002-trade-copier-spec.html
**Plan:** docs/brain/PTT-COPIER-B1/02-architecture-plan.md (REVIEW_PASS)
**Date:** 2026-07-06

These tickets are written for the engineer who will implement the C# files in the Wave workspace.
No code is written here. The engineer writes all `.cs` files in:
  `c:\WSGTA\universal-or-strategy\src\PropTraderTools\`

Ticket dependency order: T1 first, then T2 and T3 in parallel.

---

# Ticket T1 -- CopyEngine.cs

## Target file
`c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs`

## Dependencies
None (standalone). All other tickets depend on this one. Build T1 first.

---

## Structs to implement

All three structs are declared as `private readonly struct` nested inside the `CopyEngine` class.
Nesting them inside the class prevents any external instantiation -- illegal states are structurally
unrepresentable from outside the engine.

---

### CopyRule

**Declaration:**
```
private readonly struct CopyRule
```

**Fields:**
```
public readonly string    Instrument;
public readonly Account   MasterAccount;
public readonly Account[] FollowerAccounts;
```

**Private constructor:**
```
private CopyRule(string instrument, Account master, Account[] followers)
```
Parameters: `string instrument`, `Account master`, `Account[] followers`

**Static factory:**
```
public static CopyRule Create(string instrument, Account master, Account[] followers)
    => new CopyRule(instrument, master, followers);
```

**JS rules that apply:**
- JS-003: A rule with no instrument or no master account is an illegal state.
  The private constructor makes it impossible to construct one outside the engine.
- JS-008: All three fields are `readonly`. The struct cannot be mutated after `Create()` returns.
- JS-010: Constructor is `private`. External callers must go through the `static Create()` factory.

---

### CopySignal

**Declaration:**
```
private readonly struct CopySignal
```

**Fields:**
```
public readonly OrderAction Action;
public readonly OrderType   Type;
public readonly int         Quantity;
public readonly double      LimitPrice;
public readonly string      OrderId;
```

**Private constructor:**
```
private CopySignal(OrderAction action, OrderType type, int qty, double limitPrice, string orderId)
```
Parameters: `OrderAction action`, `OrderType type`, `int qty`, `double limitPrice`, `string orderId`

**Static factory:**
```
public static CopySignal Create(OrderAction action, OrderType type, int qty,
                                double limitPrice, string orderId)
    => new CopySignal(action, type, qty, limitPrice, orderId);
```

**JS rules that apply:**
- JS-001: The struct is a plain data carrier. It does not throw. `SendCopy` returns bool to
  signal failure -- no exceptions propagate from this struct.
- JS-008: All five fields are `readonly`. Immutable after creation.
- JS-010: Constructor is `private`. The `static Create()` factory is the only entry point.

---

### TrimSignal

**Declaration:**
```
private readonly struct TrimSignal
```

**Fields:**
```
public readonly DateTime UtcTime;
public readonly string   Instrument;
```

**NO qty field -- illegal state unrepresentable by design (JS-003).**
Each account independently reads its own live position via `account.Positions[instrument].Quantity`
and computes `ceil(qty/2)`. Carrying a shared qty here would allow quantity synchronization
across accounts -- which is an illegal state. TrimSignal cannot express that state.

**Private constructor:**
```
private TrimSignal(string instrument)
```
Parameters: `string instrument`
Implementation note: `UtcTime = DateTime.UtcNow;` inside the constructor. Never `DateTime.Now` (SCAN-06).

**Static factory:**
```
public static TrimSignal Create(string instrument) => new TrimSignal(instrument);
```

**JS rules that apply:**
- JS-003: No qty field. Quantity sync across accounts is an illegal state. TrimSignal enforces
  this structurally -- the type literally cannot carry a qty.
- JS-008: Both fields are `readonly`. `UtcTime` is set once at construction. Immutable.
- JS-010: Constructor is `private`. External code calls `TrimSignal.Create(instrument)` only.

---

## Methods to implement

---

### 1. `private void OnOrderUpdate(object sender, OrderEventArgs e)`

**Purpose:** 4-gate chain that filters all NT order events and dispatches copy signals to
each follower account that passes the daily cap check.

**Implementation notes:**
- Gate 1: `if (!_isCopyEnabled) return;` -- volatile read, no lock needed (JS-023).
- Gate 2a: `if (order.Account != _rule.MasterAccount) return;` -- reject follower-originated events.
- Gate 2b: `if (order.Instrument.FullName != _rule.Instrument) return;` -- reject wrong instrument.
- Gate 3a: `if (order.OrderState != OrderState.Submitted) return;` -- only copy on Submitted state.
- Gate 3b: check `order.OrderType == OrderType.Market` and `order.OrderType == OrderType.Limit`;
  if neither is true, `return;` -- this structurally excludes stop and stop-limit orders.
- Gate 4: `if (IsDedup(order.Id.ToString())) return;` -- reject duplicate event for same orderId.
- After all 4 gates pass: build `CopySignal` via `CopySignal.Create(...)`.
- Loop over `_rule.FollowerAccounts`. For each follower: call `PassesDailyCapCheck(follower)`;
  if it returns false, `continue`. Otherwise call `SendCopy(follower, in signal)`.
- If `SendCopy` returns true, fire `StatusUpdate?.Invoke(...)` with a status string.
  Status string must be ASCII-only (SCAN-02). Use `+` concatenation, not interpolation with Unicode.
- Method signature note: the plan shows `public` in §4.1 but the access is implementation-internal.
  Use `private` -- it is registered via `Account.All.OrderUpdate += OnOrderUpdate` as a delegate,
  not called directly by external code.
- NEVER use `lock()` anywhere in this method (SCAN-01, SCAN-07, JS-021).
- NEVER use `DateTime.Now` (SCAN-06).

**JS rules:** JS-001 (no throw, early returns), JS-021 (no lock), JS-023 (_isCopyEnabled volatile),
JS-025 (_dedupCache ConcurrentDictionary), SCAN-01, SCAN-06, SCAN-07.

---

### 2. `private bool SendCopy(Account follower, in CopySignal signal)`

**Purpose:** Submit one copy order to a follower account via NT's `CreateOrder` + `Submit()` API.
Returns `true` on success, `false` on any failure. Never throws.

**Implementation notes:**
- Call `follower.CreateOrder(signal.Action, signal.Type, signal.Quantity, signal.LimitPrice,
  "PTT-Copy", ...)` to build the order. The name parameter MUST be `"PTT-Copy"` (SCAN-05).
- Wrap the `CreateOrder` and `.Submit()` calls in a try/catch. On exception, return `false`.
  Do NOT rethrow. Do NOT use `lock()`.
- The `in` keyword on `signal` is required -- `CopySignal` is passed by readonly reference
  to avoid struct copy overhead on the hot path.
- Use `signal.LimitPrice` for limit orders, `0` or the NT-required value for market orders.
  Refer to the NT8 `CreateOrder` API for the correct overload.
- Return `true` only when `Submit()` completes without exception.
- NEVER use `DateTime.Now` (SCAN-06).
- NEVER use `lock()` (SCAN-01, JS-021).

**JS rules:** JS-001 (returns bool, no throw), SCAN-05 ("PTT-Copy" prefix mandatory),
SCAN-01 (no lock), SCAN-06 (no DateTime.Now).

---

### 3. `internal void Trim(Instrument instrument)`

**Purpose:** For every account scoped to the given instrument, close approximately half the
open position using a market order named "PTT-Trim".

**Implementation notes:**
- Call `AllAccounts(instrument)` to get the account set. Do not enumerate all accounts globally.
- For each account: read `account.Positions[instrument].Quantity` (the live position qty).
- If `qty == 0`, skip (account is flat). Early continue, do not submit.
- Compute half qty: `int half = (int)Math.Ceiling(Math.Abs(qty) / 2.0);`
- Determine exit direction: if `qty > 0` (long), action is `OrderAction.Sell`.
  If `qty < 0` (short), action is `OrderAction.Buy`.
- Submit a market order: `account.CreateOrder(exitAction, OrderType.Market, half, 0, "PTT-Trim", ...)`
  followed by `.Submit()`. Order name MUST be `"PTT-Trim"` (SCAN-05).
- Each account computes its own half independently. TrimSignal carries no qty -- do NOT pass qty
  between accounts. This is JS-003 in action.
- Wrap each `CreateOrder`/`Submit()` in a try/catch. On exception, continue to the next account.
  Do NOT rethrow (JS-001).
- NEVER use `lock()` (SCAN-01, JS-021). NEVER use `DateTime.Now` (SCAN-06).

**JS rules:** JS-001, JS-003 (each account computes its own half independently),
SCAN-01, SCAN-05 ("PTT-Trim"), SCAN-06.

---

### 4. `internal void Flatten(Instrument instrument)`

**Purpose:** For every account scoped to the given instrument, close the entire open position
using a single market order named "PTT-Flatten".

**Implementation notes:**
- Call `AllAccounts(instrument)` to get the account set.
- For each account: read `account.Positions[instrument].Quantity`.
- If `qty == 0`, skip. Early continue.
- Use full `Math.Abs(qty)` as the order quantity.
- Determine exit direction: `qty > 0` -> `OrderAction.Sell`; `qty < 0` -> `OrderAction.Buy`.
- Submit a market order named `"PTT-Flatten"` (SCAN-05).
- Wrap each `CreateOrder`/`Submit()` in a try/catch. On exception, continue.
- NEVER use `lock()` (SCAN-01). NEVER use `DateTime.Now` (SCAN-06).

**JS rules:** JS-001, SCAN-01, SCAN-05 ("PTT-Flatten"), SCAN-06.

---

### 5. `internal void CancelPendingEntries(Instrument instrument)`

**Purpose:** Cancel all working non-bracket, non-PTT entry orders for every account scoped
to the given instrument.

**Implementation notes:**
- Call `AllAccounts(instrument)` to get the account set.
- For each account: iterate `account.Orders`.
- For each order: skip if `order.OrderState != OrderState.Working`.
- For each order: skip if `IsBracketLeg(order)` returns `true`.
  This is the structural safety gate -- bracket legs and PTT- prefixed orders can never be
  cancelled by this method.
- If both filters pass: call `order.Cancel()`. Wrap in try/catch; on exception, continue.
- NEVER use `lock()` (SCAN-01). NEVER use `DateTime.Now` (SCAN-06).
- NEVER cancel an order with a "PTT-" name -- `IsBracketLeg` Layer 2 catches this,
  but as belt-and-suspenders verify the guard is called on every iteration.

**JS rules:** JS-001, JS-003 (IsBracketLeg makes bracket cancellation unrepresentable),
SCAN-01, SCAN-06.

---

### 6. `private bool IsDedup(string orderId)`

**Purpose:** Returns `true` if the given orderId was seen within the last 10 seconds (duplicate
NT order event). Prevents the same order from being copied more than once.

**Implementation notes:**
- The dedup window is 10 seconds expressed in 100-nanosecond ticks: `10_000_000L * 10L`.
- Step 1 -- prune stale entries: iterate `_dedupCache.Keys`. For each key, read the stored ticks
  value. If `DateTime.UtcNow.Ticks - storedTicks > 10_000_000L * 10L`, call `_dedupCache.TryRemove(key, out _)`.
  Use `TryRemove`, never `Remove` + lock.
- Step 2 -- attempt add: call `_dedupCache.TryAdd(orderId, DateTime.UtcNow.Ticks)`.
  If `TryAdd` returns `false` (key already exists and is still fresh), this IS a duplicate: return `true`.
  If `TryAdd` returns `true` (key was absent), this is a new event: return `false`.
- All operations use `ConcurrentDictionary` built-in methods. No `lock()` needed.
- NEVER use `DateTime.Now` -- all ticks must come from `DateTime.UtcNow.Ticks` (SCAN-06).

**JS rules:** JS-021 (no lock), JS-025 (ConcurrentDictionary), SCAN-01, SCAN-06.

---

### 7. `internal void SetEnabled(bool enabled)`

**Purpose:** Set the copy-enabled state atomically and fire a status notification. Called by
both TradeCopierPanel and TradeCopierWindow toggle buttons.

**Implementation notes:**
- Set `_isCopyEnabled = enabled;`. This is a `volatile bool` write -- atomic, no lock needed.
- Fire `StatusUpdate?.Invoke("Copy " + (enabled ? "ON" : "OFF"));`
  Note: use `?.Invoke()` not direct invocation -- if no subscribers, this is a safe no-op.
- The status string must be ASCII-only (SCAN-02).
- This method is called from the WPF UI thread (button click). It is safe to read/write `_isCopyEnabled`
  here without synchronization because the `volatile` keyword guarantees visibility to the
  NT strategy thread reading it in Gate 1.
- NEVER use `lock()` (SCAN-01, JS-021).

**JS rules:** JS-023 (volatile write is the atomic primitive here), SCAN-01, SCAN-02.

---

### 8. `private IEnumerable<Account> AllAccounts(Instrument instrument)`

**Purpose:** Return the master account plus all follower accounts for the rule matching the
given instrument. This is the instrument fence.

**Implementation notes:**
- Compare `instrument.FullName` against `_rule.Instrument` (the rule's stored instrument name string).
- If no match (including if `_rule` is uninitialized), return `Enumerable.Empty<Account>()`.
  Do NOT return null.
- If match: use `yield return _rule.MasterAccount;` then iterate `_rule.FollowerAccounts` and
  `yield return` each one. Or build a single flat list and return it. Either approach is acceptable.
- This method is the ONLY place that produces an account set for Trim, Flatten, and CancelPendingEntries.
  Never enumerate accounts directly in those methods -- always go through AllAccounts.
- MES trim must never see MNQ accounts. The instrument fence here is the structural guarantee.
- NEVER use `lock()`. No shared state is modified in this method.

**JS rules:** JS-003 (instrument fence -- wrong-instrument accounts are unrepresentable in output).

---

### 9. `private bool IsBracketLeg(Order order)`

**Purpose:** Returns `true` if the order is a bracket leg or a PTT-originated order that must
not be cancelled by `CancelPendingEntries`.

**Implementation notes:**
- Implement exactly the 3-layer guard from the architecture plan Section 6:
- Layer 1: `if (order.FromEntrySignal != null) return true;`
  Rationale: NT's ATM engine stamps bracket legs with `FromEntrySignal`. This is structural proof.
- Layer 2: `if (order.Name.StartsWith("PTT-")) return true;`
  Rationale: Our own PTT- orders (PTT-Flatten, PTT-Trim in Working state) must never be
  self-cancelled. The prefix is the safety net.
- Layer 3: `if (order.Name.StartsWith("Stop") || order.Name.StartsWith("Target")) return true;`
  Rationale: Belt-and-suspenders for ATM-named orders where `FromEntrySignal` may be null.
- Return `false` if none of the three layers match. Order is a plain entry -- safe to cancel.
- This method must have exactly 3 conditional guards and nothing else. CYC = 4. Meets CYC <= 8.
- NEVER use `lock()`. Read-only on `order`.

**JS rules:** JS-003 (3-layer guard makes accidental bracket cancellation unrepresentable).

---

## Fields to declare

All fields are declared inside `public sealed class CopyEngine`.

```
private volatile bool _isCopyEnabled;
```
- Declared `volatile`. Enables atomic read in `OnOrderUpdate` (NT thread) and atomic write in
  `SetEnabled` (UI thread) without a lock. JS-023, SCAN-01.

```
private readonly ConcurrentDictionary<string, long> _dedupCache = new ConcurrentDictionary<string, long>();
```
- Lock-free dictionary. Key = orderId string, Value = `DateTime.UtcNow.Ticks` at first-seen.
  JS-025, SCAN-01.

```
private CopyRule _rule;
```
- The single active rule. Set by `Initialize(CopyRule rule)`. Read by gate chain and AllAccounts.
  Block 1 supports one rule. (The plan uses `_rule` not a list -- use this.)

```
private static readonly CopyEngine _instance = new CopyEngine();
```
- Eagerly-initialized singleton. Thread-safe by CLR guarantee (static field initializers run once).

```
public static CopyEngine Instance => _instance;
```
- Public accessor. No backing field needed when using `=>` expression body.

```
public event Action<string> StatusUpdate;
```
- Fired from the NT strategy thread by `OnOrderUpdate` (via SetEnabled status strings) and by
  `SetEnabled`. Subscribers (TradeCopierWindow, TradeCopierPanel) must dispatch to UI thread via
  `Dispatcher.InvokeAsync` in their handler. The event itself is not dispatched here.

---

## xUnit tests to write

Test file location: `tests/` directory in Wave workspace (same project as other V12 tests).
Use `[Fact]` only. `Assert.Equal`, `Assert.True`, `Assert.False`. Never NUnit or MSTest.

```
[Fact] Gate1_DisabledEngine_ReturnsBeforeCopy()
[Fact] Gate2_WrongAccount_ReturnsBeforeCopy()
[Fact] Gate2_WrongInstrument_ReturnsBeforeCopy()
[Fact] Gate3_NotSubmitted_ReturnsBeforeCopy()
[Fact] Gate3_StopOrder_ReturnsBeforeCopy()
[Fact] Gate4_DuplicateOrderId_ReturnsBeforeCopy()
[Fact] IsDedup_FreshEntry_ReturnsFalse()
[Fact] IsDedup_SameIdWithinTtl_ReturnsTrue()
[Fact] IsDedup_SameIdAfterTtlExpiry_ReturnsFalse()
[Fact] IsBracketLeg_FromEntrySignalNotNull_ReturnsTrue()
[Fact] IsBracketLeg_PttPrefix_ReturnsTrue()
[Fact] IsBracketLeg_StopPrefix_ReturnsTrue()
[Fact] IsBracketLeg_TargetPrefix_ReturnsTrue()
[Fact] IsBracketLeg_RegularOrder_ReturnsFalse()
[Fact] TrimSignal_HasNoQtyField_StructuralVerification()
[Fact] CopySignal_Create_AllFieldsAssigned()
[Fact] SetEnabled_VolatileWrite_ReflectedImmediately()
```

---

## 7-Scan checklist

Run ALL scans from the Wave workspace root (`c:\WSGTA\universal-or-strategy`) before reporting
BUILD_PASS. Every scan must return 0 results for this file.

**SCAN-01 -- No `lock()` (grep):**
```
grep -r "lock(" src/PropTraderTools/CopyEngine.cs
```
Must return 0 results. If any match: remove the lock() and replace with ConcurrentDictionary or volatile.

**SCAN-02 -- ASCII-only:**
```
Get-Content src/PropTraderTools/CopyEngine.cs | Where-Object {$_ -match '[^\x00-\x7F]'}
```
Must return 0 results. If any match: remove the non-ASCII character and replace with ASCII equivalent.

**SCAN-03 -- No FontFamily:**
```
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "FontFamily"
```
Must return 0 results. CopyEngine.cs has no UI code -- this should trivially pass.

**SCAN-04 -- No hardcoded hex colors:**
```
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "#[0-9A-Fa-f]{6}"
```
Must return 0 results. CopyEngine.cs has no UI code -- this should trivially pass.

**SCAN-05 -- PTT- prefix on all CreateOrder calls:**
Manually verify: every `CreateOrder(...)` call in CopyEngine.cs passes an order name that starts
with "PTT-". Expected names: "PTT-Copy", "PTT-Trim", "PTT-Flatten".
0 violations = all CreateOrder name params start with "PTT-".

**SCAN-06 -- No `DateTime.Now`:**
```
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "DateTime\.Now[^U]"
```
Must return 0 results. All timestamps must use `DateTime.UtcNow` or `DateTime.UtcNow.Ticks`.

**SCAN-07 -- No `lock()` (regex, belt-and-suspenders):**
```
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "\block\s*\("
```
Must return 0 results. Intentional duplicate of SCAN-01.

---

# Ticket T2 -- TradeCopierPanel.cs

## Target file
`c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs`

## Dependencies
- T1 (CopyEngine.cs) must be written and compiling before T2 is started.
  T2 calls `CopyEngine.Instance` directly.

---

## Class declaration

```
public sealed class TradeCopierPanel : NTWindow
```

Note: `TradeCopierPanel` is a ChartTrader row extension injected into the NT ChartTrader panel.
It is NOT a standalone window. The correct NT8 base class may be `AddOnControl` or a
ChartTrader-specific base -- consult NT8 Add-On SDK for the exact ChartTrader row extension
base class. The plan uses "ChartTrader row extension" and "AddOnControl" interchangeably.
Use whichever base class NT8's ChartTrader injection mechanism requires.

---

## Methods to implement

---

### 1. `protected override void OnInitialize()`

**Purpose:** Wire up the panel to the singleton engine and the chart's current instrument.

**Implementation notes:**
- Get the engine reference: `CopyEngine.Instance` (no separate instance, singleton only).
- Bind to the chart instrument: read `ChartControl.Instrument` and store it in a private field
  (`_currentInstrument` or similar). This field is used by OnTrim, OnFlatten, OnCancel.
- Subscribe to `CopyEngine.Instance.StatusUpdate += OnStatusUpdate` to receive status lines.
- Call `BuildUI()`.
- Do NOT call `CopyEngine.Initialize()` here. The engine is already live at Add-On startup.
  The Panel only binds to it -- it does not own the engine lifecycle.

---

### 2. `protected override void OnDestroyed()`

**Purpose:** Clean up the panel's bindings without touching the engine lifecycle.

**Implementation notes:**
- Unsubscribe from the StatusUpdate event: `CopyEngine.Instance.StatusUpdate -= OnStatusUpdate`.
- Clear the stored `_currentInstrument` reference.
- Do NOT call `CopyEngine.Shutdown()`. The engine must keep running after this panel is closed.
  Other panel instances and the Window are unaffected.
- This method must never call `SetEnabled(false)`. Closing a chart does not disable copying.

---

### 3. `private void BuildUI()`

**Purpose:** Construct the two-row ChartTrader UI for this panel.

**EXACT XAML/WPF structure required:**

**Row 1 -- Copy toggle button:**
- Full-width Button.
- `Style="{DynamicResource NTButtonStyle}"` (SCAN-04).
- Initial text: `"Copy OFF"` when `_isCopyEnabled` is false; `"Copying ON"` when true.
- Click handler: `OnToggle`.
- Store reference in `_toggleButton` field to allow text update from `OnToggle`.

**Row 2 -- Three equal-width action buttons:**
- Three buttons laid out horizontally with equal column widths (use `Grid` with 3 `ColumnDefinition` Width="*").
- Button 1: text = `"Trim 1/2  S+T"`, click handler = `OnTrim`, `Style="{DynamicResource NTButtonStyle}"`.
- Button 2: text = `"Flatten  S+F"`, click handler = `OnFlatten`, `Style="{DynamicResource NTButtonStyle}"`.
- Button 3: text = `"Cancel  S+C"`, click handler = `OnCancel`, `Style="{DynamicResource NTButtonStyle}"`.
- Trim button: `IsEnabled = false` when the current instrument's position qty == 0.
- Flatten button: `IsEnabled = false` when the current instrument's position qty == 0.
- Cancel button: `IsEnabled = false` when there are no working entry orders for the current instrument.
  Working entry order = `order.OrderState == OrderState.Working && !IsBracketLeg(order)`.
  The panel does not call `IsBracketLeg` directly -- query `CopyEngine.Instance` or perform
  the same check inline (no lock, read-only on accounts).
- Store button references in `_trimButton`, `_flattenButton`, `_cancelButton` fields to allow
  `IsEnabled` toggling.

**Row 3 (below Row 2) -- Account selectors:**
- Leader ComboBox: `Style="{DynamicResource AccountComboBoxStyle}"`. Bound to the leader account.
  Label: "Leader" (TextBlock).
- Follower checklist ComboBox: shows all available accounts with checkboxes.
  `Style="{DynamicResource AccountComboBoxStyle}"` or equivalent NT account selector style.
  Label: "Followers" (TextBlock).

**Status line:**
- `TextBlock` below account selectors.
- `Foreground="{DynamicResource {x:Static NTBrushes.SubtleBrushKey}}"` (or equivalent NTBrushes key).
- Store reference in `_statusLine` field. Updated by `OnStatusUpdate`.

**Keyboard shortcuts -- MANDATORY:**
Register on the panel's `InputBindings` collection inside `BuildUI()`:
```
InputBindings.Add(new KeyBinding(new RelayCommand(OnTrim),    Key.T, ModifierKeys.Shift));
InputBindings.Add(new KeyBinding(new RelayCommand(OnFlatten), Key.F, ModifierKeys.Shift));
InputBindings.Add(new KeyBinding(new RelayCommand(OnCancel),  Key.C, ModifierKeys.Shift));
```
Shift+T -> OnTrim, Shift+F -> OnFlatten, Shift+C -> OnCancel.
Use whatever `ICommand` wrapper NT8 provides (e.g., `ActionCommand`, `DelegateCommand`).

**Color and font rules:**
- All `Foreground` and `Background` references: use `NTBrushes.*` resource keys (SCAN-04).
- No `FontFamily` attribute anywhere in this file (SCAN-03).
- No `#RRGGBB` hex color literals anywhere (SCAN-04).

---

### 4. `private void OnToggle(object sender, RoutedEventArgs e)`

**Purpose:** Toggle the copy-enabled state and update the button text immediately.

**Implementation notes:**
- Read the current state: store `_isCopyEnabled` locally (read from `CopyEngine.Instance` state
  or track it in a private field).
- Call `CopyEngine.Instance.SetEnabled(!currentState)`.
- Update `_toggleButton.Content = newState ? "Copying ON" : "Copy OFF";`
- No logic beyond the toggle call and button text update. All business logic is in CopyEngine.
- This method runs on the WPF UI thread (button click). No Dispatcher needed here.

---

### 5. `private void OnTrim(object sender, RoutedEventArgs e)`

**Purpose:** Delegate trim command to the engine for the chart's current instrument.

**Implementation notes:**
- Call `CopyEngine.Instance.Trim(_currentInstrument);`
- Single line. No other logic in this handler.

---

### 6. `private void OnFlatten(object sender, RoutedEventArgs e)`

**Purpose:** Delegate flatten command to the engine for the chart's current instrument.

**Implementation notes:**
- Call `CopyEngine.Instance.Flatten(_currentInstrument);`
- Single line. No other logic in this handler.

---

### 7. `private void OnCancel(object sender, RoutedEventArgs e)`

**Purpose:** Delegate cancel-pending-entries command to the engine for the chart's current instrument.

**Implementation notes:**
- Call `CopyEngine.Instance.CancelPendingEntries(_currentInstrument);`
- Single line. No other logic in this handler.

---

### 8. `private void OnStatusUpdate(string line)`

**Purpose:** Receive a status string from the engine (fired on the NT strategy thread) and
display it on the UI status line.

**Implementation notes:**
- This handler is called from the NT strategy thread. It MUST dispatch to the WPF UI thread.
- Use `Dispatcher.InvokeAsync(() => _statusLine.Text = line);`
  (Replace the entire text for a single-line status display.)
- Do NOT call `_statusLine.Text = line;` directly without dispatch. This will throw a
  cross-thread exception at runtime.
- The `Dispatcher` here is the WPF dispatcher obtained from `Application.Current.Dispatcher`
  or from the `_statusLine.Dispatcher` property.
- NEVER use `lock()` here (SCAN-01). The Dispatcher queue is thread-safe.

---

## NT-native UI rules -- SCAN-03/SCAN-04 enforcement

The engineer MUST NOT do any of the following in TradeCopierPanel.cs:

- **No `FontFamily="..."` attribute** anywhere in XAML inline or code-behind (SCAN-03).
  Rationale: NT8 has its own WPF theme. Overriding FontFamily breaks the 100% NT-native
  appearance pillar. Inherit -- never override.
- **No hardcoded hex colors** like `"#1E1E1E"` or `Brushes.FromArgb(...)` with literal values (SCAN-04).
  Rationale: NT's dark/light theme switch changes all colors. Hardcoded hex breaks in the
  opposite theme.
- **No `Border.BorderBrush` with a literal color value.**
  Use `"{DynamicResource {x:Static NTBrushes.BorderBrushKey}}"` or equivalent.
- **All color references must use `NTBrushes.*` resource keys** (e.g., `NTBrushes.SubtleBrushKey`,
  `NTBrushes.Accent1BrushKey`, etc.). These update automatically with the NT theme.
- **`AccountComboBoxStyle` for both account ComboBoxes** (leader and followers).
  `Style="{DynamicResource AccountComboBoxStyle}"`.
- **`NTButtonStyle` for all buttons** (toggle, trim, flatten, cancel).
  `Style="{DynamicResource NTButtonStyle}"`.

---

## xUnit tests to write

```
[Fact] OnToggle_CallsSetEnabled_WithFlippedState()
[Fact] OnTrim_CallsTrimWithChartInstrument()
[Fact] OnFlatten_CallsFlattenWithChartInstrument()
[Fact] OnCancel_CallsCancelPendingEntriesWithChartInstrument()
[Fact] OnDestroyed_DoesNotShutdownEngine()
```

---

## 7-Scan checklist

Run ALL scans from `c:\WSGTA\universal-or-strategy` before reporting BUILD_PASS.

**SCAN-01 -- No `lock()` (grep):**
```
grep -r "lock(" src/PropTraderTools/TradeCopierPanel.cs
```
Must return 0 results.

**SCAN-02 -- ASCII-only:**
```
Get-Content src/PropTraderTools/TradeCopierPanel.cs | Where-Object {$_ -match '[^\x00-\x7F]'}
```
Must return 0 results.

**SCAN-03 -- No FontFamily:**
```
Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern "FontFamily"
```
Must return 0 results. No FontFamily override anywhere in this file.

**SCAN-04 -- No hardcoded hex colors:**
```
Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern "#[0-9A-Fa-f]{6}"
```
Must return 0 results.

**SCAN-05 -- PTT- prefix on all CreateOrder calls:**
TradeCopierPanel.cs MUST contain zero `CreateOrder` calls. The panel is a pure UI surface --
all order submission goes through `CopyEngine`. If any `CreateOrder` call appears in this file,
it is a P0 violation. Verify manually: 0 CreateOrder calls expected.

**SCAN-06 -- No `DateTime.Now`:**
```
Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern "DateTime\.Now[^U]"
```
Must return 0 results.

**SCAN-07 -- No `lock()` (regex, belt-and-suspenders):**
```
Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern "\block\s*\("
```
Must return 0 results.

---

# Ticket T3 -- TradeCopierWindow.cs

## Target file
`c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierWindow.cs`

## Dependencies
- T1 (CopyEngine.cs) must be written and compiling before T3 is started.
- T2 and T3 are mutually independent. They can be built in parallel once T1 is complete.

---

## Class declaration

```
public sealed class TradeCopierWindow : NTWindow
```

`NTWindow` is the NT8-provided base class for Add-On windows that appear in the NT Control Center
menu. NT provides the window chrome, docking, title bar, and menu registration. The implementation
owns only the content area.

Note on NT8 registration: The window is registered in NT's Add-On system so it appears in the
Control Center menu. Refer to NT8 NTWindow documentation for the correct `[NinjaScriptProperty]`
attribute or Add-On registration mechanism.

---

## Methods to implement

---

### 1. `protected override void OnInitialize()`

**Purpose:** Wire up the window to the singleton engine and subscribe to status events.

**Implementation notes:**
- Get the engine: `CopyEngine.Instance` (same singleton that TradeCopierPanel uses).
- Subscribe: `CopyEngine.Instance.StatusUpdate += OnStatusUpdate`.
- Call `BuildUI()`.
- Do NOT call `CopyEngine.Initialize()`. The engine lifecycle is owned by the Add-On entry point,
  not by this window.
- No `lock()`. No `DateTime.Now`.

---

### 2. `private void BuildUI()`

**Purpose:** Construct the entire window content area.

**EXACT structure required:**

**Global enable button (top row):**
- Full-width Button.
- `Style="{DynamicResource NTButtonStyle}"`.
- Initial text: `"Copy All OFF"` / `"Copy All ON"` based on current engine state.
- Click handler: `OnGlobalToggle`.
- Store reference in `_globalToggleButton`.

**Per-rule rows (one row per `CopyRule`):**
For Block 1 there is exactly one rule. The UI should be designed for one row but written in
a loop over `CopyEngine.Instance` rules to allow future expansion.
Each row contains:
- Instrument `TextBlock` (static label, shows rule instrument name).
- Leader `ComboBox` with `Style="{DynamicResource AccountComboBoxStyle}"`.
- Followers checklist `ComboBox` with `Style="{DynamicResource AccountComboBoxStyle}"`.
- `[1/2]` Trim button: `Style="{DynamicResource NTButtonStyle}"`, click handler `OnRuleTrim`.
  Store button tag or row index to identify which rule's instrument to pass.
- `[=]` Flatten button: `Style="{DynamicResource NTButtonStyle}"`, click handler `OnRuleFlatten`.
- `[X]` Cancel button: `Style="{DynamicResource NTButtonStyle}"`, click handler `OnRuleCancel`.
- `[ON]` Per-rule toggle button: `Style="{DynamicResource NTButtonStyle}"`.

**[+ Add Rule] button:**
- `Style="{DynamicResource NTButtonStyle}"`.
- For Block 1: `IsEnabled = false`. Present in the UI but disabled.
  Multi-rule support is deferred to Block 2.

**Status log:**
- A `ScrollViewer` containing a `StackPanel` (`_logPanel`) of `TextBlock` items.
- New log lines are added to the TOP of `_logPanel` (newest at top, oldest at bottom).
- Maximum 50 lines. When a 51st line would be added, remove the last child of `_logPanel`
  (oldest entry) before inserting the new one at index 0.
- All `TextBlock` items in the log: `Foreground="{DynamicResource {x:Static NTBrushes.SubtleBrushKey}}"`.
- No FontFamily override (SCAN-03). No hex colors (SCAN-04).

**All colors and fonts:**
- `NTBrushes.*` for all color references (SCAN-04).
- `NTButtonStyle` for all buttons (SCAN-04).
- `AccountComboBoxStyle` for all account ComboBoxes (SCAN-04).
- No `FontFamily` attribute anywhere (SCAN-03).

---

### 3. `private void OnGlobalToggle(object sender, RoutedEventArgs e)`

**Purpose:** Toggle copy-enabled state globally via the engine.

**Implementation notes:**
- Read current state (track in a private `_isEnabled` field or read from engine).
- Call `CopyEngine.Instance.SetEnabled(!_isEnabled);`
- Update `_globalToggleButton.Content = newState ? "Copy All ON" : "Copy All OFF";`
- Single responsibility: toggle + button text. No other logic.

---

### 4. `private void OnRuleTrim(object sender, RoutedEventArgs e)`

**Purpose:** Trim the position for the rule associated with the clicked button.

**Implementation notes:**
- Identify which rule's instrument to use. Use the button's `Tag` property set during `BuildUI()`
  to store the `Instrument` reference for that row.
  Example: `var instrument = (Instrument)((Button)sender).Tag;`
- Call `CopyEngine.Instance.Trim(instrument);`
- No other logic.

---

### 5. `private void OnRuleFlatten(object sender, RoutedEventArgs e)`

**Purpose:** Flatten the position for the rule associated with the clicked button.

**Implementation notes:**
- Same Tag-based instrument lookup as `OnRuleTrim`.
- Call `CopyEngine.Instance.Flatten(instrument);`

---

### 6. `private void OnRuleCancel(object sender, RoutedEventArgs e)`

**Purpose:** Cancel pending entries for the rule associated with the clicked button.

**Implementation notes:**
- Same Tag-based instrument lookup as `OnRuleTrim`.
- Call `CopyEngine.Instance.CancelPendingEntries(instrument);`

---

### 7. `private void OnStatusUpdate(string line)`

**Purpose:** Receive a status string from the engine (fired on the NT strategy thread) and
prepend it to the scrollable log.

**Implementation notes:**
- This handler is called from the NT strategy thread. It MUST dispatch to the WPF UI thread.
- Full dispatch pattern:
  ```
  Dispatcher.InvokeAsync(() =>
  {
      string timestamp = DateTime.UtcNow.ToString("HH:mm:ss");
      var tb = new TextBlock { Text = timestamp + "  " + line };
      tb.Foreground = (Brush)FindResource(NTBrushes.SubtleBrushKey);
      _logPanel.Children.Insert(0, tb);
      if (_logPanel.Children.Count > 50)
          _logPanel.Children.RemoveAt(50);
  });
  ```
- Timestamp format: `"HH:mm:ss"` using `DateTime.UtcNow` (SCAN-06). Never `DateTime.Now`.
- Newest log entry at index 0 (top of the StackPanel). Oldest at bottom.
- Max 50 lines enforced by removing at index 50 after insert.
- NEVER call `_logPanel.Children.Insert(...)` directly without `Dispatcher.InvokeAsync`.
- NEVER use `lock()` (SCAN-01). The Dispatcher queue is thread-safe.
- The `Foreground` assignment uses `FindResource(NTBrushes.SubtleBrushKey)` -- no hex (SCAN-04).

**JS rules:** SCAN-01 (no lock), SCAN-04 (NTBrushes, no hex), SCAN-06 (DateTime.UtcNow).

---

## NT-native UI rules

Same constraints as T2. The engineer MUST NOT do any of the following in TradeCopierWindow.cs:

- **No `FontFamily="..."` attribute** anywhere (SCAN-03).
- **No hardcoded hex colors** like `"#1E1E1E"` or raw `Color.FromArgb(...)` literals (SCAN-04).
- **No `Border.BorderBrush` with a literal color value.**
- **All color references must use `NTBrushes.*` resource keys.**
- **`AccountComboBoxStyle` for all account ComboBoxes.**
- **`NTButtonStyle` for all buttons** (global toggle, per-rule 1/2, =, X, ON, + Add Rule).

---

## xUnit tests to write

```
[Fact] OnStatusUpdate_DispatchesToUiThread()
[Fact] OnInitialize_SubscribesToStatusUpdateEvent()
[Fact] OnStatusUpdate_AppendsLineToLog()
[Fact] GlobalToggle_CallsSetEnabled()
```

---

## 7-Scan checklist

Run ALL scans from `c:\WSGTA\universal-or-strategy` before reporting BUILD_PASS.

**SCAN-01 -- No `lock()` (grep):**
```
grep -r "lock(" src/PropTraderTools/TradeCopierWindow.cs
```
Must return 0 results.

**SCAN-02 -- ASCII-only:**
```
Get-Content src/PropTraderTools/TradeCopierWindow.cs | Where-Object {$_ -match '[^\x00-\x7F]'}
```
Must return 0 results.

**SCAN-03 -- No FontFamily:**
```
Select-String -Path src/PropTraderTools/TradeCopierWindow.cs -Pattern "FontFamily"
```
Must return 0 results.

**SCAN-04 -- No hardcoded hex colors:**
```
Select-String -Path src/PropTraderTools/TradeCopierWindow.cs -Pattern "#[0-9A-Fa-f]{6}"
```
Must return 0 results.

**SCAN-05 -- PTT- prefix on all CreateOrder calls:**
TradeCopierWindow.cs MUST contain zero `CreateOrder` calls. The window is a pure UI surface.
All order submission routes through `CopyEngine`. If any `CreateOrder` call appears in this file,
it is a P0 violation. Verify manually: 0 CreateOrder calls expected.

**SCAN-06 -- No `DateTime.Now`:**
```
Select-String -Path src/PropTraderTools/TradeCopierWindow.cs -Pattern "DateTime\.Now[^U]"
```
Must return 0 results. `OnStatusUpdate` uses `DateTime.UtcNow.ToString("HH:mm:ss")` -- this passes.

**SCAN-07 -- No `lock()` (regex, belt-and-suspenders):**
```
Select-String -Path src/PropTraderTools/TradeCopierWindow.cs -Pattern "\block\s*\("
```
Must return 0 results.

---

# Cross-Ticket Wiring Verification

After T2 and T3 are both written, the engineer must verify the following integration points
before reporting the epic complete.

- [ ] `CopyEngine.StatusUpdate` event is subscribed by BOTH TradeCopierPanel (in `OnInitialize`)
  AND TradeCopierWindow (in `OnInitialize`). Both surfaces must independently subscribe and
  independently unsubscribe (Panel in `OnDestroyed`, Window in its own cleanup).

- [ ] Both surfaces call `CopyEngine.Instance` -- the same singleton object.
  There must be exactly ONE `CopyEngine` instance in the process. Verify by searching for
  `new CopyEngine(` in all three files -- it must appear exactly once (in the static field
  initializer `_instance = new CopyEngine()`).

- [ ] `SetEnabled()` from either surface is reflected immediately in both surfaces' button text.
  This works because `SetEnabled` fires `StatusUpdate` which both surfaces listen to.
  The Panel and Window each update their own button text in their own `OnStatusUpdate` handler.
  Alternatively (and simpler): each surface updates its button text locally in its own toggle
  handler after calling `SetEnabled`. Either approach is acceptable. What is NOT acceptable:
  one surface updating the other surface's button text directly (they are mutually independent).

- [ ] Trim, Flatten, and Cancel commands from any surface (Panel buttons, Panel keyboard shortcuts,
  Window per-rule buttons) all route through `CopyEngine.Instance.Trim(...)`,
  `CopyEngine.Instance.Flatten(...)`, and `CopyEngine.Instance.CancelPendingEntries(...)`.
  No direct `account.CreateOrder(...)` calls appear in TradeCopierPanel.cs or TradeCopierWindow.cs.
  Verify: `grep -r "CreateOrder" src/PropTraderTools/TradeCopierPanel.cs` returns 0.
  Verify: `grep -r "CreateOrder" src/PropTraderTools/TradeCopierWindow.cs` returns 0.
