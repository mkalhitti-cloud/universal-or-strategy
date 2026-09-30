# NT8-COMPILER-RULES — NinjaTrader 8 NinjaScript Compiler Constraints
# Version: 1.1
# Source: PTT Trade Copier blocks B1-B18 (hard compiler errors, runtime crashes, confirmed workarounds)
# Audience: AGENTS ONLY — every rule here was discovered by hitting the actual NT8 Roslyn compiler
# Format: NT8-NNN | SEVERITY | ONE-LINE BAN | DO / DONT | FIX
# Update protocol: append a new rule block whenever a new compiler error or runtime crash is
#   confirmed in NT8. NEVER delete a rule. Update STATUS field only.
# Gate: agents touching ANY .cs file in src/PropTraderTools/ MUST read this file first.

---

## HOW TO READ THIS FILE

Each rule has:
  NT8-NNN   — rule ID, stable forever
  SEVERITY  — P0 = instant build break / crash | P1 = silent wrong behaviour | P2 = style/risk
  CONFIRMED — block where the error was first hit (B1..B10)
  ERROR     — exact CS-code or runtime message produced by the WRONG pattern
  BANNED    — the exact C# construct to never write
  SAFE      — the exact replacement that compiles and works in NT8
  SCAN      — grep/regex to detect violations before committing

---

## CATEGORY: C# LANGUAGE FEATURES NOT SUPPORTED IN NT8 ROSLYN

### NT8-001 | P0 | `{ get; init; }` IS BANNED
CONFIRMED: B7, B8 (CS0518 + CS8341)
ERROR: CS0518 "Predefined type 'System.Runtime.CompilerServices.IsExternalInit' is not defined or imported"
       CS8341 "Only auto-implemented properties can have initializers."
CAUSE: `init` accessor requires C# 9 + .NET 5+ System.Runtime.CompilerServices.IsExternalInit.
       NT8 uses .NET Framework 4.8 with a pre-C#9 Roslyn build. IsExternalInit does not exist.

BANNED:
  internal Account Foo { get; init; }
  public bool Bar { get; init; }

SAFE:
  // Option A: private set + explicit constructor
  internal Account Foo { get; private set; }
  internal Foo(Account foo) { Foo = foo; }

  // Option B: readonly field (for structs where mutability is truly banned)
  internal readonly Account Foo;

SCAN: `\{\s*get;\s*init;\s*\}`

---

### NT8-002 | P0 | `abstract record` / `sealed record` IS BANNED
CONFIRMED: B8 (CS0518 on positional record constructor)
ERROR: CS0518 "IsExternalInit not defined" — positional record constructors synthesise
       compiler-generated init properties internally even when you don't write `init` yourself.
CAUSE: Any `record` with positional parameters generates IsExternalInit usage under the hood.
       NT8's Roslyn version does not have this type.

BANNED:
  public abstract record FollowerAtmMode { ... }
  public sealed record Inherit() : FollowerAtmMode;
  public sealed record Named(string TemplateName) : FollowerAtmMode;

SAFE:
  // Replace abstract record with abstract class + explicit constructors
  public abstract class FollowerAtmMode
  {
      private FollowerAtmMode() { }                 // prevent external subclassing
      public sealed class Inherit : FollowerAtmMode { public Inherit() : base() { } }
      public sealed class Market  : FollowerAtmMode { public Market()  : base() { } }
      public sealed class Named   : FollowerAtmMode
      {
          public string TemplateName { get; private set; }
          public Named(string name) : base() { TemplateName = name; }
      }
  }

NOTE: The nested-class pattern preserves the discriminated union semantics of records.
      Pattern-matching with `is Named n` still works.

SCAN: `\babstract\s+record\b|\bsealed\s+record\b`

---

### NT8-003 | P0 | `volatile double` IS BANNED
CONFIRMED: B8/B9 (CS0677)
ERROR: CS0677 "The volatile modifier cannot be used with fields of type 'double'"
CAUSE: CLR volatile is restricted to reference types and integers up to 32 bits.
       `double` is 64-bit — not allowed.

BANNED:
  private volatile double _lastAtr = 0.0;

SAFE:
  // Remove volatile. Add comment explaining thread-safety reasoning.
  // In NT8 (64-bit process on x64), double reads/writes are naturally atomic on
  // aligned memory. For guaranteed visibility, use Interlocked or a lock-free
  // approach if strict happens-before is needed.
  private double _lastAtr = 0.0;   // written by OnBarUpdate, read by GetSuggestedQty — x64 atomic

  // If strict visibility is required (cross-core):
  private long _lastAtrBits = 0L;   // volatile long wrapping double bits
  // write: Interlocked.Exchange(ref _lastAtrBits, BitConverter.DoubleToInt64Bits(value));
  // read:  BitConverter.Int64BitsToDouble(Interlocked.Read(ref _lastAtrBits));

SCAN: `volatile\s+double`

---

### NT8-004 | P0 | `System.Collections.Immutable` IS NOT AVAILABLE IN NT8 NINJASCRIPT
CONFIRMED: B8 (CS0246 when NinjaScript compiler resolves types)
ERROR: CS0246 "The type or namespace name 'ImmutableDictionary' could not be found"
CAUSE: NT8's NinjaScript compiler does NOT include System.Collections.Immutable in its
       reference assembly set. It is available in the Linting.csproj (dotnet build passes)
       but the NT8 in-process compiler rejects it at Script compile time.

BANNED (in files deployed to NT8 AddOns folder):
  using System.Collections.Immutable;
  ImmutableDictionary<string, Foo> _map = ImmutableDictionary<string, Foo>.Empty;

SAFE:
  // Use Dictionary<K,V> written ONCE at construction time (logically immutable).
  // Document the single-writer contract in a comment.
  private readonly Dictionary<string, FollowerAtmMode> _atmTemplates
      = new Dictionary<string, FollowerAtmMode>();
  // Populated only in constructor/factory. Never mutated after construction.

  // For copy-on-mutate (SetItem equivalent):
  private Dictionary<K,V> CopyWith(Dictionary<K,V> src, K key, V val)
  {
      var next = new Dictionary<K,V>(src);
      next[key] = val;
      return next;
  }

SCAN: `ImmutableDictionary|System\.Collections\.Immutable`

---

### NT8-005 | P0 | `readonly struct` WITH AUTO-PROPERTY `{ get; private set; }` IS BANNED
CONFIRMED: B8 (CS8341)
ERROR: CS8341 "Auto-implemented properties of readonly structs must have a getter but no setter."
CAUSE: A `readonly struct` cannot have a private setter on auto-properties — the setter
       would allow mutation after construction which violates struct readonly contract.

BANNED:
  internal readonly struct FollowerBinding
  {
      internal Account FollowerAccount { get; private set; }   // CS8341
  }

SAFE:
  // Option A: readonly field (simplest)
  internal readonly struct FollowerBinding
  {
      internal readonly Account FollowerAccount;
      internal FollowerBinding(Account acc) { FollowerAccount = acc; }
  }

  // Option B: drop readonly from struct, keep private set
  internal struct FollowerBinding
  {
      internal Account FollowerAccount { get; private set; }
      internal FollowerBinding(Account acc) { FollowerAccount = acc; }
  }

SCAN: `readonly\s+struct` (then check each property for `private set`)

---

### NT8-006 | P1 | `ConcurrentBag<T>.Any()` REQUIRES EXPLICIT `using System.Linq`
CONFIRMED: B8
ERROR: CS1061 "'ConcurrentBag<T>' does not contain a definition for 'Any'"
CAUSE: `Any()` is a LINQ extension method. In NT8 NinjaScript files, `using System.Linq`
       is not auto-included — it must be explicitly declared.

BANNED:
  if (_rules.Any()) { ... }   // without `using System.Linq` at file top

SAFE:
  // Add at file top:
  using System.Linq;
  // Then .Any(), .FirstOrDefault(), .Where() etc. all work.

  // Or avoid LINQ entirely (zero-allocation):
  if (_rules.Count > 0) { ... }   // for List/ConcurrentBag where Count is O(1)

SCAN: `\.Any\(\)` (verify `using System.Linq` is present in the same file)

---

## CATEGORY: NT8 API CONSTRAINTS

### NT8-007 | P0 | `Account.CreateOrder` — ARGUMENT 12 IS `CustomOrder`, NOT `string`
CONFIRMED: B8 (CS1503 argument type mismatch)
ERROR: CS1503 "Argument 12: cannot convert from 'string' to 'NinjaTrader.Cbi.CustomOrder'"
CAUSE: The 12-argument overload of Account.CreateOrder takes a CustomOrder object as the
       final argument, not a plain string. The ATM strategy name is a SEPARATE parameter
       at position 11 (zero-indexed 10).

BANNED:
  acc.CreateOrder(instr, action, type, entry, tif, qty, 0, stop,
                  null, "PTT-Copy", DateTime.MaxValue, "MyAtmTemplate");  // last arg: string

SAFE:
  acc.CreateOrder(instr, action, type, entry, tif, qty, 0, stop,
                  null, "PTT-Copy", DateTime.MaxValue,
                  (NinjaTrader.Cbi.CustomOrder)null);   // last arg: cast null to CustomOrder

  // Signature reference (12 args, zero-indexed):
  //  0  Instrument instrument
  //  1  OrderAction orderAction
  //  2  OrderType orderType
  //  3  OrderEntry orderEntry
  //  4  TimeInForce timeInForce
  //  5  int quantity
  //  6  double limitPrice
  //  7  double stopPrice
  //  8  string oco
  //  9  string signalName          -- must start with "PTT-"
  // 10  DateTime gtd
  // 11  CustomOrder customOrder    -- pass (CustomOrder)null

SCAN: `acc\.CreateOrder\(` — verify last arg is `(NinjaTrader.Cbi.CustomOrder)null` not a string

---

### NT8-008 | P0 | `Chart.ChartControl` PROPERTY DOES NOT EXIST
CONFIRMED: B8 (CS1061)
ERROR: CS1061 "'Chart' does not contain a definition for 'ChartControl'"
CAUSE: The NT8 Chart window class does not expose a `ChartControl` property directly.
       ChartControl is a child in the visual tree, not a named property.

BANNED:
  var cc = chart.ChartControl;

SAFE:
  // Walk the visual tree to find ChartControl
  var cc = FindVisualChild<ChartControl>(chart);
  // FindVisualChild<T> is the depth-first helper already in TradeCopierAddOn.cs

SCAN: `chart\.ChartControl`

---

### NT8-009 | P0 | `ChartControl.GetValueByY()` DOES NOT EXIST IN THIS NT8 VERSION
CONFIRMED: B8 (CS1061)
ERROR: CS1061 "'ChartControl' does not contain a definition for 'GetValueByY'"
CAUSE: This method is documented in some NT8 API references but is absent in the actual
       NT8 version used by PTT. Call site must be stubbed.

BANNED:
  double price = chartControl.GetValueByY(mouseY);

SAFE:
  // Stub to 0.0 and document the deferred work item
  double price = 0.0;   // STUB: GetValueByY not available in this NT8 build (DW-B8-04)
  // Implement via pixel-to-price conversion using chart scale if needed in future.

SCAN: `\.GetValueByY\(`

---

### NT8-010 | P0 | `State.SetDefaults` IN INDICATOR SUBCLASS MUST BE FULLY QUALIFIED
CONFIRMED: B9 (CS0103 ambiguous name)
ERROR: CS0103 "The name 'SetDefaults' does not exist in the current context" OR
       ambiguous reference between NinjaScript State enum and local usage.
CAUSE: Inside a class that inherits from `Indicator`, bare `State.SetDefaults` is
       ambiguous — the compiler cannot resolve whether `State` means the enum or
       something else in scope.

BANNED (inside Indicator subclass OnStateChange):
  if (State == State.SetDefaults) { ... }   // may fail depending on using directives

SAFE:
  if (State == NinjaTrader.NinjaScript.State.SetDefaults) { ... }
  if (State == NinjaTrader.NinjaScript.State.DataLoaded)  { ... }
  if (State == NinjaTrader.NinjaScript.State.Terminated)  { ... }

SCAN: `\bState\.(SetDefaults|DataLoaded|Terminated|Configure)\b` — verify full namespace present

---

### NT8-011 | P0 | `Add(ATR(Period))` IN INDICATOR `OnStateChange` DataLoaded IS INVALID
CONFIRMED: B9 (runtime NullReferenceException / no data)
ERROR: No compiler error — silently produces wrong output. ATR values are always 0 or NaN.
CAUSE: In a headless/detached Indicator (not attached to chart bars via NinjaScripts.Add),
       `Add(ATR(Period))` registers a data series but BarsArray is not wired. The ATR
       indicator is never fed price data so it never calculates.

BANNED (in headless Indicator managed by AddOn):
  protected override void OnStateChange()
  {
      if (State == State.DataLoaded)
          Add(ATR(Period));   // WRONG: Add() for sub-indicators requires chart bar wiring
  }

SAFE:
  // In OnBarUpdate, call the ATR indicator directly on the current bar:
  protected override void OnBarUpdate()
  {
      double atrValue = ATR(Period)[0];   // Correct: called per-bar on current BarsArray
      _lastAtr = atrValue;
  }

SCAN: `Add\(ATR\(` inside `OnStateChange`

---

### NT8-012 | P1 | `FrameworkElementFactory` CANNOT ADD `ColumnDefinitions` BEFORE INSTANTIATION
CONFIRMED: B10-UI-01 (runtime — columns not created, layout broken)
ERROR: No compiler error — columns silently never appear. All cells collapse to column 0.
CAUSE: `FrameworkElementFactory` builds a template that is only instantiated when a
       DataTemplate is applied. `ColumnDefinitions` are a run-time collection — they
       cannot be populated via `FrameworkElementFactory` calls at template-definition time.

BANNED:
  var gridFactory = new FrameworkElementFactory(typeof(Grid));
  gridFactory.AddHandler(FrameworkElement.LoadedEvent, ...);
  // trying to add ColumnDefinitions here — no API exists on FrameworkElementFactory for this

SAFE:
  // Use the Loaded event on the instantiated Grid to add ColumnDefinitions at runtime:
  var gridFactory = new FrameworkElementFactory(typeof(Grid));
  gridFactory.AddHandler(FrameworkElement.LoadedEvent,
      new RoutedEventHandler(OnRowGridLoaded));

  private void OnRowGridLoaded(object sender, RoutedEventArgs e)
  {
      var grid = (Grid)sender;
      if (grid.ColumnDefinitions.Count > 0) return;   // idempotency guard
      grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
      grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
      // ... add remaining columns
      // then set Grid.Column attached property on each child:
      var child = (FrameworkElement)grid.Children[0];
      Grid.SetColumn(child, 0);
  }

SCAN: `FrameworkElementFactory.*Grid` — verify Loaded event handler present, not inline column adds

---

### NT8-013 | P0 | `DateTime.Now` FOR ORDER EXPIRY IS WRONG
CONFIRMED: B1 (confirmed as pattern; always use MaxValue for GTC orders in NT8)
ERROR: No compiler error — order expires immediately or in unexpected ways at broker.
CAUSE: NT8 order GTC (good-til-cancelled) requires `DateTime.MaxValue` as the expiry
       parameter to Account.CreateOrder. Using `DateTime.Now` or any real timestamp
       submits a day-order that expires after that moment.

BANNED:
  acc.CreateOrder(..., DateTime.Now, ...);
  acc.CreateOrder(..., DateTime.UtcNow, ...);
  acc.CreateOrder(..., DateTime.Now.AddDays(1), ...);

SAFE:
  acc.CreateOrder(..., DateTime.MaxValue, ...);   // GTC — never expires

SCAN: `DateTime\.Now[^U]|DateTime\.UtcNow` inside CreateOrder call

---

### NT8-014 | P1 | SIGNAL NAME IN `CreateOrder` MUST START WITH `"PTT-"`
CONFIRMED: B7 (functional constraint — orders without prefix are not tracked by CopyEngine)
ERROR: No compiler error — order placed but CopyEngine.OnOrderUpdate never routes it
       because the signal name filter (`order.Name.StartsWith("PTT-")`) misses it.

BANNED:
  acc.CreateOrder(..., "Copy", ...);
  acc.CreateOrder(..., "Stop", ...);
  acc.CreateOrder(..., "", ...);

SAFE:
  acc.CreateOrder(..., "PTT-Copy", ...);
  acc.CreateOrder(..., "PTT-Click", ...);
  acc.CreateOrder(..., "PTT-Mirror-Close", ...);
  acc.CreateOrder(..., "PTT-BE-Stop", ...);
  acc.CreateOrder(..., "PTT-Tighten", ...);

SCAN: `acc\.CreateOrder` — verify 9th argument (index 9 zero-based) starts with "PTT-"

---

### NT8-015 | P0 | `AtrSizingEngine` (Indicator SUBCLASS) MUST NOT BE `sealed`
CONFIRMED: B9 (runtime crash — NT8 Indicator infrastructure requires unsealed class)
ERROR: NinjaScript compiler rejects sealed Indicator subclasses in some contexts.
       NT8 may need to generate a proxy/wrapper subclass internally.

BANNED:
  public sealed class AtrSizingEngine : Indicator { ... }

SAFE:
  public class AtrSizingEngine : Indicator { ... }   // no sealed modifier

SCAN: `sealed class.*Indicator|sealed class.*NinjaScript`

---

### NT8-016 | P0 | `TradeCopierWindow` MUST NOT BE `sealed`
CONFIRMED: B3 (runtime — window fails to open when sealed)
ERROR: No compiler error — window simply does not appear.
CAUSE: NT8 WPF Window infrastructure (or XAML tooling) may attempt to derive from or
       proxy the Window class. Sealed breaks this.

BANNED:
  public sealed class TradeCopierWindow : Window { ... }

SAFE:
  public class TradeCopierWindow : Window { ... }

SCAN: `sealed class.*Window`

---

### NT8-017 | P1 | `volatile bool` CROSS-THREAD STATE FIELDS ARE MANDATORY (JS-023)
CONFIRMED: B7, B8, B9 (race conditions without volatile)
ERROR: No compiler error — stale cached reads on other CPU cores; non-deterministic behaviour.
CAUSE: NT8 AddOn runs on multiple threads: UI thread (WPF), market data thread
       (OnBarUpdate/MarketDataUpdate), order routing thread (OnOrderUpdate). Any bool/int
       field read on one thread and written on another MUST be volatile.

BANNED:
  private bool _clickArmed = false;   // read on market thread, written on UI thread
  private int  _copyModeValue = 0;    // read on order thread, written on UI thread

SAFE:
  private volatile bool _clickArmed    = false;   // JS-023: UI writes, market reads
  private volatile int  _copyModeValue = 0;       // JS-023: UI writes, order thread reads
  private volatile bool _atrEnabled    = false;   // JS-023

SCAN: `private bool _|private int _` in class fields — check if accessed from >1 thread;
      if yes, must be `volatile`

---

### NT8-018 | P1 | `lock()` IS BANNED — USE `volatile` + `ConcurrentDictionary`/`ConcurrentBag`
CONFIRMED: B1 (JS-021 — performance + deadlock risk in NT8 callback threads)
ERROR: No compiler error — potential deadlock; NT8 callback threads must not block.

BANNED:
  private readonly object _lock = new object();
  lock (_lock) { _state = newState; }

SAFE:
  private volatile int _stateValue = 0;   // int backing for enum
  // Write: _stateValue = (int)newState;  (single writer)
  // Read:  var s = (MyEnum)_stateValue;  (multiple readers)

  // For collections: ConcurrentDictionary or ConcurrentBag
  private ConcurrentDictionary<string, Foo> _map = new ConcurrentDictionary<string, Foo>();
  private ConcurrentBag<CopyRule> _rules = new ConcurrentBag<CopyRule>();

SCAN: `lock\s*\(` (non-comment)

---

### NT8-019 | P0 | `async void` IS BANNED IN NT8 CALLBACK METHODS
CONFIRMED: B1 (JS-033)
ERROR: No compiler error — unhandled exceptions in async void crash the NT8 process silently.
CAUSE: NT8 lifecycle methods (`OnOrderUpdate`, `OnBarUpdate`, etc.) are called synchronously
       by the NT8 runtime. Making them async void means exceptions escape the call site.

BANNED:
  protected override async void OnOrderUpdate(...) { await Task.Delay(1); }
  private async void OnBreakEvenClick(object s, RoutedEventArgs e) { await ...; }

SAFE:
  // All NT8 callback overrides must be synchronous void.
  // For deferred UI work: Dispatcher.InvokeAsync (fire-and-forget on UI thread is safe).
  protected override void OnOrderUpdate(...) { Dispatcher.InvokeAsync(() => UpdateUI()); }

SCAN: `async void`

---

### NT8-020 | P1 | `SolidColorBrush` MUST BE FROZEN BEFORE USE IN NT8 WPF
CONFIRMED: B7 (runtime InvalidOperationException across threads)
ERROR: InvalidOperationException "Cannot modify a frozen object" OR cross-thread access error.
CAUSE: WPF brushes created on one thread and used on another throw if not frozen.
       In NT8, UI updates may occur on threads other than the brush-creation thread.

BANNED:
  button.Background = new SolidColorBrush(Color.FromRgb(34, 197, 94));

SAFE:
  // Use a helper that always freezes:
  private static SolidColorBrush MakeBrush(byte r, byte g, byte b)
  {
      var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
      brush.Freeze();
      return brush;
  }
  button.Background = MakeBrush(34, 197, 94);

SCAN: `new SolidColorBrush\(` without `.Freeze()` on the next line

---

### NT8-021 | P1 | `Account.All` MUST NOT BE ACCESSED IN CONSTRUCTORS OR FIELD INITIALIZERS
CONFIRMED: B5 (NullReferenceException at startup)
ERROR: NullReferenceException — Account.All is null before NT8 initializes.
CAUSE: NT8 account infrastructure is not ready at AddOn construction time.
       Account.All is only populated after NT8 has fully loaded.

BANNED:
  public class MyAddOn : AddOnBase
  {
      private readonly IEnumerable<Account> _accs = Account.All;   // CRASH
  }

SAFE:
  // Access Account.All only inside event handlers that fire after NT8 is loaded:
  protected override void OnWindowCreated(Window w)
  {
      foreach (var acc in Account.All) { ... }   // Safe here
  }

SCAN: `Account\.All` outside of event handlers/lifecycle overrides

---

### NT8-022 | P1 | `WPF KeyBinding WITH LETTER KEYS` IS REJECTED BY NT8
CONFIRMED: B4 (KeyBinding silently ignored)
ERROR: No compiler error — keyboard shortcut simply never fires.
CAUSE: NT8 intercepts keyboard input for its own trading shortcuts. Letter-key bindings
       added via WPF KeyBinding are swallowed before they reach the AddOn's Window.

BANNED:
  new KeyBinding(command, new KeyGesture(Key.B, ModifierKeys.Shift));

SAFE:
  // Use buttons only — no keyboard shortcuts for trading actions.
  // If keyboard shortcuts are needed, use NT8's own hotkey API, not WPF KeyBinding.

SCAN: `KeyBinding|KeyGesture` (flag for review — may be intentional in non-trading contexts)

---

### NT8-023 | P1 | `NTWindow` CANNOT BE EMBEDDED AS A `UserControl`
CONFIRMED: B5 (layout broken — NTWindow is a Window, not a control)
ERROR: No compiler error — window appears as a separate floating window, not embedded.

BANNED:
  public class TradeCopierPanel : NTWindow { ... }   // for injectable panel

SAFE:
  public class TradeCopierPanel : UserControl { ... }   // injectable into Grid rows

SCAN: `: NTWindow` (check if intended as injectable panel — if yes, change to UserControl)

---

### NT8-024 | P1 | `NTWindow` AS STANDALONE WINDOW BASE CAUSES WINDOW-NOT-APPEARING
CONFIRMED: B6 (window opens but is not visible)
ERROR: No compiler error — window object exists but never renders.
CAUSE: NTWindow has internal NT8 lifecycle hooks that conflict with direct `new Window().Show()`.

BANNED:
  public class TradeCopierWindow : NTWindow { ... }

SAFE:
  public class TradeCopierWindow : System.Windows.Window { ... }

SCAN: `: NTWindow\b`

---

### NT8-025 | P1 | `NTMenuItem.Header` IS NOT ALWAYS A `string` — USE `.ToString()`
CONFIRMED: B6 (null reference / wrong comparison)
ERROR: No compiler error — deduplication check silently fails; menu item added twice.
CAUSE: NT8 may set `MenuItem.Header` to a `TextBlock` object rather than a plain string.
       `mi.Header as string` returns null when Header is TextBlock.

BANNED:
  if (mi.Header as string == "Trade Copier") return;

SAFE:
  var hdr = mi.Header != null ? mi.Header.ToString() : string.Empty;
  if (hdr == "Trade Copier") return;

  // Better: use a volatile bool guard instead of header comparison:
  private static volatile bool _menuWired = false;

SCAN: `\.Header\s+as\s+string`

---

### NT8-026 | P1 | TRAILING STOP ORDER DETECTED BY `order.TrailPrice > 0`
CONFIRMED: B9/B10 (behavioral — incorrect handling without this check)
ERROR: No compiler error — acc.Change(StopPrice) on trailing stop may freeze the trail silently.
CAUSE: NT8 trailing stop orders are `OrderType.StopMarket` orders with `TrailPrice > 0`.
       Calling acc.Change() with only a new StopPrice on such an order has UNDEFINED behaviour
       (trail may freeze). Must detect and handle separately.

BANNED:
  order.StopPrice = newStop;
  acc.Change(new Order[] { order });   // if order.TrailPrice > 0: trail may be killed

SAFE:
  bool isTrailing = order.TrailPrice > 0;
  if (isTrailing)
  {
      // Option B (safe default): skip Mode 2 relay for trailing stops
      // Option B for BE: cancel + replace with fixed StopMarket
      acc.Cancel(new Order[] { order });
      acc.CreateOrder(instr, action, OrderType.StopMarket, OrderEntry.Manual,
                      TimeInForce.Day, qty, 0, newStop, null, "PTT-BE-Stop",
                      DateTime.MaxValue, (NinjaTrader.Cbi.CustomOrder)null);
  }
  else
  {
      order.StopPrice = newStop;
      acc.Change(new Order[] { order });
  }

SCAN: `acc\.Change\(new Order` — verify IsTrailingStop guard present before call

---

### NT8-027 | P1 | `Instrument.MarketData` SUBSCRIPTION FROM ADDON CONTEXT — VERIFY BEFORE USE
CONFIRMED: B9/B10 (GAP-002 — unverified as of B10 start)
ERROR: No compiler error — MarketDataUpdate handler may never fire if subscription does not
       work in AddOnBase context (no NinjaScriptBase lifecycle).
STATUS: PENDING SIM101 VERIFICATION (GAP-002 test not yet run)
CAUSE: `NinjaTrader.Data.Instrument.GetInstrument(name).MarketData.MarketDataUpdate` event
       is documented as available but its behaviour from an AddOnBase subclass (no OnBarUpdate,
       no OnStateChange lifecycle) is unconfirmed.

SAFE PATTERN (once confirmed):
  var dataInstr = NinjaTrader.Data.Instrument.GetInstrument(instr.FullName);
  if (dataInstr != null)
      dataInstr.MarketData.MarketDataUpdate += OnPriceTick;

  // Handler fires on market data thread — volatile field reads only, Dispatcher.InvokeAsync for UI

FALLBACK (if MarketDataUpdate does NOT fire from AddOn):
  // Use Account.AccountItemUpdate (UnrealizedPnL changes proxy for price movement)
  // — coarser resolution, fires per P&L tick not per price tick

UPDATE THIS RULE: after running GAP-002 Sim101 test, fill in confirmed behaviour.
SCAN: `MarketData\.MarketDataUpdate` — check if confirmed by GAP-002 test result

---

### NT8-028 | P1 | `HEX COLOR STRING LITERALS` ARE BANNED — USE `MakeBrush(r,g,b)`
CONFIRMED: B7 (risk of NT8 WPF parsing failure + violates team scan)
ERROR: No compiler error — may cause WPF XAML parse failure in some NT8 versions.

BANNED:
  button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22c55e"));

SAFE:
  button.Background = MakeBrush(34, 197, 94);   // #22c55e expressed as decimal RGB

SCAN: `"#[0-9A-Fa-f]{6}"` in string literals (not in comments)

---

### NT8-029 | P1 | `TICK ALIGNMENT` IS MANDATORY ON ALL STOP/LIMIT PRICES
CONFIRMED: B4 (order rejection by broker/sim if price not on tick boundary)
ERROR: No compiler error — order may be rejected or silently moved by broker to nearest tick.

BANNED:
  double stopPrice = entryPrice - 0.03;   // arbitrary offset, may not be on tick

SAFE:
  double raw = entryPrice - (bufferTicks * instrument.MasterInstrument.TickSize);
  double stopPrice = Math.Round(raw / instrument.MasterInstrument.TickSize)
                   * instrument.MasterInstrument.TickSize;

SCAN: manual review — check all stop/limit price calculations for tick alignment

---

### NT8-030 | P0 | `OnWindowCreated(Window)` FIRES FOR EVERY NT8 WINDOW — GUARD WITH VOLATILE BOOL
CONFIRMED: B6 (menu item added N times — one per window open)
ERROR: No compiler error — menu items multiply; injection runs on wrong window types.

BANNED:
  protected override void OnWindowCreated(Window window)
  {
      AddMenuItems(window);   // called for EVERY window: Control Center, Chart, etc.
  }

SAFE:
  private static volatile bool _menuWired = false;

  protected override void OnWindowCreated(Window window)
  {
      if (_menuWired) return;               // idempotency guard
      var cc = window as ControlCenter;
      if (cc == null) return;               // only act on Control Center
      AddMenuItems(cc);
      _menuWired = true;
  }

SCAN: `OnWindowCreated` — verify idempotency guard present before any state mutation

---


### NT8-031 | P0 | `OrderState.PendingSubmit` DOES NOT EXIST IN NT8
CONFIRMED: B18 (CS0117 — F5 gate failure)
ERROR: CS0117 "'OrderState' does not contain a definition for 'PendingSubmit'"
CAUSE: NT8's `OrderState` enum does not include `PendingSubmit`. Standard .NET/NinjaTrader 7
       docs may list it but NT8 Roslyn build host does not expose it. Only `Initialized`,
       `Working`, `Cancelled`, `Filled`, `PartFilled`, `Rejected`, `Unknown` exist in NT8.

BANNED:
  if (order.OrderState != OrderState.Working &&
      order.OrderState != OrderState.Initialized &&
      order.OrderState != OrderState.PendingSubmit)   // CS0117 — PendingSubmit does not exist
      continue;

SAFE:
  if (order.OrderState != OrderState.Working &&
      order.OrderState != OrderState.Initialized)     // Initialized covers pre-ack state
      continue;

NOTE: `Initialized` is the NT8 pre-acknowledgement state that covers what `PendingSubmit`
      represents in other platforms. Using `Initialized` alone is sufficient.

SCAN: `OrderState\.PendingSubmit`

---


## CATEGORY: AGENT UPDATE PROTOCOL

### HOW TO ADD A NEW RULE

When a new NT8 compiler error or runtime crash is confirmed in a PTT block:

1. Assign next ID: NT8-NNN (increment from last rule above)
2. Set SEVERITY: P0 (build/crash), P1 (silent wrong behaviour), P2 (style/risk)
3. Set CONFIRMED: block where hit (B11, B12, ...)
4. Fill in ERROR, CAUSE, BANNED, SAFE, SCAN fields
5. Add rule to the bottom of the relevant CATEGORY section
6. Update the version line and date at the top of this file
7. Append a summary row to the INDEX TABLE below

---

## INDEX TABLE — ALL RULES AT A GLANCE

| ID      | Severity | Summary | Confirmed |
|---------|----------|---------|-----------|
| NT8-001 | P0 | `{ get; init; }` banned — IsExternalInit not in NT8 Roslyn | B7/B8 |
| NT8-002 | P0 | `abstract record` / `sealed record` banned — record constructors emit IsExternalInit | B8 |
| NT8-003 | P0 | `volatile double` banned — CLR only allows volatile on <=32-bit / refs | B8/B9 |
| NT8-004 | P0 | `System.Collections.Immutable` not available in NT8 NinjaScript | B8 |
| NT8-005 | P0 | `readonly struct` with `{ get; private set; }` banned (CS8341) | B8 |
| NT8-006 | P1 | `ConcurrentBag.Any()` requires explicit `using System.Linq` | B8 |
| NT8-007 | P0 | `CreateOrder` arg 12 is `CustomOrder`, not `string` | B8 |
| NT8-008 | P0 | `Chart.ChartControl` property does not exist — use FindVisualChild | B8 |
| NT8-009 | P0 | `ChartControl.GetValueByY()` absent in this NT8 build — stub it | B8 |
| NT8-010 | P0 | `State.SetDefaults` in Indicator must be fully namespace-qualified | B9 |
| NT8-011 | P0 | `Add(ATR(Period))` in headless Indicator OnStateChange is invalid | B9 |
| NT8-012 | P1 | `FrameworkElementFactory` cannot add ColumnDefinitions before instantiation | B10-UI-01 |
| NT8-013 | P0 | `DateTime.Now` for CreateOrder expiry — use `DateTime.MaxValue` | B1 |
| NT8-014 | P1 | CreateOrder signal name must start with `"PTT-"` | B7 |
| NT8-015 | P0 | `AtrSizingEngine : Indicator` must not be sealed | B9 |
| NT8-016 | P0 | `TradeCopierWindow : Window` must not be sealed | B3 |
| NT8-017 | P1 | Cross-thread bool/int fields must be `volatile` (JS-023) | B7/B9 |
| NT8-018 | P1 | `lock()` banned — use volatile + ConcurrentDictionary/ConcurrentBag | B1 |
| NT8-019 | P0 | `async void` banned in NT8 callback methods | B1 |
| NT8-020 | P1 | `SolidColorBrush` must be `.Freeze()`d before cross-thread use | B7 |
| NT8-021 | P1 | `Account.All` banned in constructors/field initializers | B5 |
| NT8-022 | P1 | WPF `KeyBinding` with letter keys silently ignored by NT8 | B4 |
| NT8-023 | P1 | `NTWindow` as UserControl base — use `UserControl` instead | B5 |
| NT8-024 | P1 | `NTWindow` as standalone Window base causes window-not-appearing | B6 |
| NT8-025 | P1 | `NTMenuItem.Header as string` returns null — use `.ToString()` | B6 |
| NT8-026 | P1 | Trailing stop `order.TrailPrice > 0` — detect before acc.Change() | B9/B10 |
| NT8-027 | P1 | `Instrument.MarketData` from AddOn context — verify before use (GAP-002 PENDING) | B9/B10 |
| NT8-028 | P1 | Hex color string literals banned — use `MakeBrush(r,g,b)` | B7 |
| NT8-029 | P1 | Tick alignment mandatory on all stop/limit prices | B4 |
| NT8-030 | P0 | `OnWindowCreated` fires for every NT8 window — guard with volatile bool | B6 |
| NT8-031 | P0 | `OrderState.PendingSubmit` does not exist in NT8 — use `Initialized` only | B18 |
| NT8-032 | P2 | `CopyEngineTests.cs` must stay co-located in PropTraderTools assembly — no separate test runner .csproj | B19 |


### NT8-032 | P2 | `CopyEngineTests.cs` MUST REMAIN CO-LOCATED IN SAME ASSEMBLY AS `CopyEngine.cs`
CONFIRMED: B19 (engineer attempted to create separate test runner — deleted after failure)
ERROR: CS0246 "The type or namespace name 'CopyRule' could not be found" when compiling
       a separate test runner `.csproj` that references `CopyEngineTests.cs`.
CAUSE: `CopyEngineTests.cs` casts `fi.GetValue(_engine)` directly to `ConcurrentBag<CopyRule>`
       where `CopyRule` is a `private readonly struct` nested inside `CopyEngine`. This is only
       valid when both files compile as the same assembly. Any separate runner `.csproj` will
       fail CS0246 on `CopyRule` because private nested types are inaccessible from outside
       the declaring assembly.

       Additionally, `CopyEngineTests.cs` uses `System.Collections.Immutable.ImmutableDictionary`
       and `System.Reflection.NullabilityInfoContext` — both .NET 6+ only, incompatible with net48.
       A separate runner targeting net48 would also fail for this reason.

BANNED:
  // Creating a separate .csproj to run CopyEngineTests.cs:
  CopyEngineTests.Runner.csproj   (any project file separate from PropTraderTools.csproj)
  NT8StubForTests.cs              (stub shim to fake NT8 types in a test project)

SAFE:
  // CopyEngineTests.cs stays inside src/PropTraderTools/ — compiled by PropTraderTools.csproj.
  // Test contract is verified via source scan, NOT via dotnet test:

  // CORRECT — count [Fact] tests by source grep:
  Select-String -Path "src\PropTraderTools\CopyEngineTests.cs" -Pattern "\[Fact\]" | Measure-Object | Select-Object -ExpandProperty Count

  // CORRECT — verify specific test method names exist:
  Select-String -Path "src\PropTraderTools\CopyEngineTests.cs" -Pattern "MyTestMethodName"

  // WRONG — do not run these for PTT test verification:
  // dotnet test src/PropTraderTools/          -- net48 target, no Test.Sdk
  // dotnet test tests/V12_Performance.Tests/  -- 331 unrelated V12 complexity tests

NOTE: The [Fact] count is a SOURCE CONTRACT ASSERTION — counted by grep, verified by F5 in NT8.
      Baseline: 111 before B19. After B19 Lane 1: 113. After B19 Lane 2: 116.

SCAN: Any new `.csproj` file under `src/PropTraderTools/` other than `PropTraderTools.csproj`
      Any `NT8Stub*.cs` or `*ForTests.cs` file in `src/PropTraderTools/`
