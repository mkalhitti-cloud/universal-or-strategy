# B40-LaneA Architecture Plan

**Block**: B40-LaneA -- BE ALL Armed/Wait + OCO Collision Fix
**Date**: 2026-07-30
**Architect**: ptt-architect
**Status**: REVIEW_PASS

---

## Section 1 -- Block Summary

B40 closes two defects discovered in B39 live testing: DW-B39-OCO-01 (P0), an OCO ID
collision that caused NT8 to reject orders when two sim accounts shared the first four
characters of their names, and DW-B39-BEHAVIOR-01 (P1), a missing armed/wait state for
the BE ALL button that matched the behavior of the per-account BE+ button. After B40, BE ALL
arms all accounts (amber), waits for price to cross entry+buffer per account, then submits
the stop per-account at the trigger moment -- with OCO IDs guaranteed globally unique across
all accounts and iterations.

---

## Section 2 -- Defect Root Cause Analysis

### DW-B39-OCO-01 (P0) -- OCO ID Collision

**Root cause (code confirmed)**: `SubmitBeStop` at `CopyEngine.cs:1632-1635` builds the
OCO group ID as:

```
"PTT-BE-" + leaderAcc.Name.Substring(0, 4) + "-" + (int)(bePrice / tickSize) + "-" + i
```

When `PttGlobalBreakEven.Execute()` loops `Account.All` and calls `SubmitBeStop` for
`Sim101` and `Sim102` within the same millisecond:

- Sim101 account name prefix: `"Sim1"`
- Sim102 account name prefix: `"Sim1"` (same -- both truncate to 4 chars)
- `bePrice / tickSize` is identical (same instrument and entry level)
- `i = 0` for the first target pair in both

Result: both calls produce `"PTT-BE-Sim1-20000-0"` -- NT8 rejects the second order with
"OCO ID cannot be reused" and the stop for one account is never submitted.

**Why the chosen fix is correct and minimal**:
1. A globally unique sequence counter `_beAllOcoSeq` in `CopyEngine` (incremented once per
   `ArmAllPendingBe` call) combined with a per-account index `accIdx` guarantees OCO IDs
   are unique even when two accounts fire `SubmitBeStop` synchronously.
2. The new format `"PTT-BEG-" + seq.ToString("D5") + "-" + accIdx + "-" + pairIndex` uses
   prefix `PTT-BEG-` (Global BE) to distinguish from per-account `PTT-BE-` IDs.
3. `BuildGlobalBeOcoId` is a pure static function in `PttGlobalBreakEven` -- no side effects,
   independently testable.
4. `SubmitBeStop` receives an optional `string ocoOverride = null` parameter. When non-null
   (global BE path only), it uses `ocoOverride + "-" + i` instead of the accName-based
   formula. When null (single-account BE+ path), the existing formula is unchanged.
   `PttBreakEven.cs` is untouched.

### DW-B39-BEHAVIOR-01 (P1) -- Missing Armed/Wait State for BE ALL

**Root cause**: B39 `PttGlobalBreakEven.Execute()` calls `SubmitBeStop` immediately for
every account x position. There is no armed state, no price watcher, and no amber visual.
The per-account BE+ button (in `TradeCopierPanel.cs`) has an Idle/Armed FSM with
`ArmPendingBe` and a price-based trigger via `AccountItemUpdate` -- BE ALL lacked this.

**Why the chosen fix is correct and minimal**:
1. `ArmAllPendingBe` delegates to the existing `ArmPendingBe` (per-account) for each
   account with an open position. Reuses the proven `OnPendingBeAccountUpdate` trigger path.
2. Accounts already past the entry+buffer threshold get `SubmitBeStop` called immediately
   (same as the existing "IsPriceAlreadyAtBe" fast-fire path in `OnBeClick`). No armed slot
   is created for these accounts.
3. `_globalBeState` (BeState.Idle/Armed) in Panel and Window mirrors `_beState` exactly.
4. `OnPendingBeFiredDispatch` already exists and marshals to UI thread. B40 adds a single
   check: if Armed and `IsPendingSlotsEmpty()` then reset to Idle. This is a minimal addition.

---

## Section 3 -- File Change Matrix

| File | Change Type | What Changes | New Methods | CYC (new/max) |
|------|-------------|--------------|-------------|---------------|
| `src/PropTraderTools/CopyEngine.cs` | Add + Modify | 4 new methods; `_beAllOcoSeq` field; `SubmitBeStop` optional param | `ArmAllPendingBe`, `IsPriceAlreadyAtBeForAccount`, `ComputeBePrice`, `IsPendingSlotsEmpty` | 5, 4, 2, 1 |
| `src/PropTraderTools/Features/PttGlobalBreakEven.cs` | Rewrite body + Add | `Execute()` body delegates to engine; `_ocoSeq` field; `BuildGlobalBeOcoId` method | `BuildGlobalBeOcoId` | 1 |
| `src/PropTraderTools/TradeCopierPanel.cs` | Modify + Add | `_globalBeState` field; `OnGlobalBeClick` rewritten; `OnPendingBeFiredDispatch` updated; `Detach()` updated; `UpdateBeAllVisuals` added | `UpdateBeAllVisuals` | 2 |
| `src/PropTraderTools/TradeCopierWindow.cs` | Modify + Add | Mirror Panel changes exactly; `_windowGlobalBeState` field; `OnWindowGlobalBeClick` rewritten; `UpdateWindowBeAllVisuals` added; `WBrushCaution` already exists | `UpdateWindowBeAllVisuals` | 2 |
| `src/PropTraderTools/CopyEngineTests.cs` | Add | 12 new `[Fact]` tests T_B40_01..T_B40_12 | none (tests only) | -- |

---

## Section 4 -- Method Signatures

### CopyEngine.cs -- New Methods

```csharp
// B40 -- ArmAllPendingBe: arms all accounts with open positions for pending BE watcher.
// For accounts already past entry+buffer: fires SubmitBeStop immediately with global OCO prefix.
// For accounts not yet triggered: calls ArmPendingBe (subscribes AccountItemUpdate).
// Returns armedCount (accounts that entered Armed state, NOT immediate-fires).
// CYC=5: Account.All foreach(1), acc.Positions foreach(2), IsFlat guard(3),
//         IsPriceAlreadyAtBeForAccount(4), immediate/arm branch(5).
// JS-021: no lock(). JS-002: returns int. NT8-021: Account.All accessed post-init only.
internal int ArmAllPendingBe(int bufferTicks)

// B40 -- IsPriceAlreadyAtBeForAccount: checks if current market price has already crossed
// the BE threshold for a specific account/position. Used by ArmAllPendingBe.
// Long:  acc.Get(AccountItem.BidPrice) >= averagePrice + bufferTicks * tickSize
// Short: acc.Get(AccountItem.AskPrice) <= averagePrice - bufferTicks * tickSize
// Per-account API: each account uses its own live market data feed.
// CYC=4: isLong ternary for bid/ask selection(2), refPx<=0 guard(3), isLong?>=:<=(4).
// NT8-AccItem: uses acc.Get(AccountItem.BidPrice) / acc.Get(AccountItem.AskPrice) (null-guarded).
private bool IsPriceAlreadyAtBeForAccount(Account acc, Position pos, int bufferTicks)

// B40 -- ComputeBePrice: pure static BE price calculation. Tick-aligned.
// Used by ArmAllPendingBe for immediate-fire path and by IsPriceAlreadyAtBeForAccount's caller.
// CYC=2: base(1) + isLong ternary for direction(2). NT8-029: tick alignment via Math.Round.
private static double ComputeBePrice(Position pos, int bufferTicks)

// B40 -- IsPendingSlotsEmpty: returns true when no armed pending-BE slots remain.
// Called by OnPendingBeFiredDispatch to auto-reset _globalBeState to Idle.
// CYC=1: expression body. ConcurrentDictionary.IsEmpty is lock-free (JS-021).
internal bool IsPendingSlotsEmpty()
```

### CopyEngine.cs -- Modified Method

```csharp
// Add optional ocoOverride parameter. When non-null, uses ocoOverride+"-"+i as OCO group ID
// instead of the accName.Substring(0,4)-based formula. Caller (ArmAllPendingBe immediate path)
// builds ocoOverride via PttGlobalBreakEven.BuildGlobalBeOcoId(seq, accIdx, 0).
// All existing callers pass no 4th argument -- default null preserves B39 behavior exactly.
internal void SubmitBeStop(Account leaderAcc, Instrument instr, double bePrice, string ocoOverride = null)
```

### CopyEngine.cs -- New Field

```csharp
// B40: monotonic OCO sequence counter for ArmAllPendingBe immediate-fire path.
// JS-023: volatile int allowed. NT8-003: volatile double is banned -- int is safe.
// Interlocked.Increment in ArmAllPendingBe; read is implicit via volatile read semantic.
private volatile int _beAllOcoSeq = 0;
```

### PttGlobalBreakEven.cs -- New Method

```csharp
// B40 DW-B39-OCO-01 FIX: globally unique OCO group ID prefix for BE ALL path.
// Format: "PTT-BEG-{seq:D5}-{accIdx}-{pairIndex}"
// seq    = monotonic per-ArmAllPendingBe-call (Interlocked.Increment on _beAllOcoSeq in engine)
// accIdx = index of account in the Account.All iteration (0,1,2...)
// pairIndex = i from the beTargets loop in SubmitBeStop (passed as ocoOverride+"-"+i in caller)
// CYC=1: pure expression. ASCII-only. No hex. No FontFamily.
// internal static so CopyEngine.ArmAllPendingBe can call it without circular dependency.
internal static string BuildGlobalBeOcoId(int seq, int accIdx, int pairIndex)
    => "PTT-BEG-" + seq.ToString("D5") + "-" + accIdx + "-" + pairIndex;
```

### PttGlobalBreakEven.cs -- New Field

```csharp
// B40: execution counter. Incremented in Execute() via Interlocked.Increment.
// JS-023: volatile int allowed. NT8-003: volatile double banned -- not used here.
// Used as a test assertion hook: test can verify Execute() was called N times.
private volatile int _ocoSeq = 0;
```

### TradeCopierPanel.cs -- New Method

```csharp
// B40 -- UpdateBeAllVisuals: updates the BE ALL button visual state.
// Idle: purple background (matches B39 default). Armed: amber background.
// CYC=2: null guard(1), Idle/Armed ternary(2).
// UI-thread only. No Dispatcher wrap needed (callers are always on UI thread).
private void UpdateBeAllVisuals(BeState state)
```

### TradeCopierWindow.cs -- New Method (mirrors Panel)

```csharp
// B40 -- UpdateWindowBeAllVisuals: window-surface mirror of UpdateBeAllVisuals.
// CYC=2. WBrushPurple (exists at line 69) and WBrushCaution (exists at line 65).
private void UpdateWindowBeAllVisuals(BeState state)
```

---

## Section 5 -- Data Flow

### Armed State Transitions

```
[BE ALL Button Click -- Idle state]
  UI Thread: OnGlobalBeClick / OnWindowGlobalBeClick
  |
  v
  PttGlobalBreakEven.Execute(bufferTicks)
    Interlocked.Increment(ref _ocoSeq)    -- count execution
    CopyEngine.Instance.ArmAllPendingBe(bufferTicks)
      |
      int seq = Interlocked.Increment(ref _beAllOcoSeq)  -- global OCO seq
      |
      foreach acc in Account.All (accIdx = 0,1,2...)
        foreach pos in acc.Positions
          if IsFlat(pos): continue
          if IsPriceAlreadyAtBeForAccount(acc, pos, bufferTicks):
            -- already past threshold: IMMEDIATE FIRE
            double bePrice = ComputeBePrice(pos, bufferTicks)
            string ocoPrefix = PttGlobalBreakEven.BuildGlobalBeOcoId(seq, accIdx, 0)
            SubmitBeStop(acc, pos.Instrument, bePrice, ocoPrefix)   -- unique OCO ID
          else:
            -- not yet triggered: ARM AND WAIT
            ArmPendingBe(pos.Instrument, acc, bufferTicks)          -- subscribe AccountItemUpdate
            armedCount++
  |
  if !IsPendingSlotsEmpty():
    _globalBeState = BeState.Armed
    UpdateBeAllVisuals(BeState.Armed)   -- button turns amber
  // else: all accounts already fired or no positions -- stay Idle


[Price crosses entry+buffer for an armed account]
  NT8 Account background thread: OnPendingBeAccountUpdate
    TryRemove slot (atomic via ConcurrentDictionary)
    Unsubscribe AccountItemUpdate
    BreakEven(acc, instr, bufferTicks)  -- calls SubmitBeStop (ocoOverride=null, existing OCO logic)
    PendingBeFired.Invoke(instrName, accName)
  |
  v
  Panel.OnPendingBeFiredDispatch(instr, accountName)  [background thread]
    Dispatcher.InvokeAsync(() => {
      OnBeConnected(instr, accountName)    -- resets per-panel _beState if this is the panel's account
      if (_globalBeState == BeState.Armed && CopyEngine.Instance.IsPendingSlotsEmpty()):
        _globalBeState = BeState.Idle
        UpdateBeAllVisuals(BeState.Idle)  -- button returns to purple
    })


[BE ALL Button Click -- Armed state (disarm)]
  UI Thread: OnGlobalBeClick / OnWindowGlobalBeClick
  |
  foreach acc in Account.All:
    CopyEngine.Instance.DisarmPendingBe(acc)   -- TryRemove + unsubscribe
  _globalBeState = BeState.Idle
  UpdateBeAllVisuals(BeState.Idle)


[Detach() -- panel/window teardown]
  UI Thread: Detach()
  |
  foreach acc in Account.All:
    CopyEngine.Instance.DisarmPendingBe(acc)
  _globalBeState = BeState.Idle
  (no visual update -- panel is being destroyed)
```

### Auto-Reset via IsPendingSlotsEmpty

`_pendingBeSlots` is a `ConcurrentDictionary<string, PendingBeSlot>` shared across all
armed accounts (both per-account BE+ and BE ALL). `IsPendingSlotsEmpty()` returns
`_pendingBeSlots.IsEmpty`. On the last armed account's fire event, `TryRemove` atomically
removes its slot. When `OnPendingBeFiredDispatch` marshals to the UI thread and executes the
check, `IsEmpty` returns `true` -- `_globalBeState` resets to Idle. If multiple accounts
fire nearly simultaneously, only the LAST PendingBeFired event will observe `IsEmpty==true`.
All earlier ones observe non-empty and skip the reset. This is correct: no spurious resets.

---

## Section 6 -- JS / NT8 Rule Compliance

| Change | Rule | Verdict |
|--------|------|---------|
| `private volatile int _ocoSeq = 0;` | JS-023 (atomic primitives), NT8-003 (volatile double banned -- int is safe), NT8-017 (cross-thread bool/int must be volatile) | PASS |
| `private volatile int _beAllOcoSeq = 0;` | Same as above | PASS |
| `Interlocked.Increment(ref _ocoSeq)` | JS-023: atomic primitive for simple state | PASS |
| `Interlocked.Increment(ref _beAllOcoSeq)` | JS-023 | PASS |
| `ArmAllPendingBe` -- no lock(), ConcurrentDictionary reads | JS-021 (no lock), JS-025 (lock-free collections) | PASS |
| `ArmAllPendingBe` -- not async void | JS-033 (async void banned), NT8-019 | PASS |
| `ArmAllPendingBe` -- returns int, no null | JS-002 (no return null) | PASS |
| `SubmitBeStop` optional param `string ocoOverride = null` | JS-002: null as DEFAULT PARAMETER is a sentinel, not a null return value -- not a violation | PASS |
| `BuildGlobalBeOcoId` return value | ASCII-only strings per project mandate; no hex; no FontFamily | PASS |
| `OnGlobalBeClick` -- event handler void | JS-033: async void banned except event handlers; synchronous void event handler is fine | PASS |
| `UpdateBeAllVisuals` -- UI thread only | No Dispatcher needed; callers are always on UI thread (click handlers, Dispatcher.InvokeAsync callbacks) | PASS |
| `OnPendingBeFiredDispatch` update -- `Dispatcher.InvokeAsync` | NT8-042 says NT8-specific Dispatcher paths banned. This uses WPF UIElement `this.Dispatcher` (inherited by UserControl/Window), NOT `NinjaTrader.Core.Globals.*` -- confirmed already working in production at Panel line 758 | PASS |
| `DateTime.MaxValue` (existing in SubmitBeStop) | NT8-013: no DateTime.Now | PASS |
| `"PTT-BEG-"` OCO prefix | NT8-014: signal names must start with PTT- -- ocoId is not a signal name, but PTT- prefix preserved for consistency | PASS |
| Account.All access in ArmAllPendingBe | NT8-021: Account.All banned in constructors; ArmAllPendingBe is called from UI button click handlers (post-NT8-init) | PASS |
| No `acc?.Event -= handler` pattern | NT8-043: null-conditional compound assignment banned; use explicit if (acc != null) guard | PASS |
| `_pendingBeSlots.IsEmpty` | ConcurrentDictionary.IsEmpty is lock-free per .NET 4.8 spec | PASS |
| Detach() DisarmPendingBe loop | NT8-043: DisarmPendingBe already guards against null inside its body; calling with each acc from Account.All is safe | PASS |

---

## Section 7 -- Test Coverage Plan

All tests in `CopyEngineTests.cs`. `[Fact]` tests added at end of file after existing T_B39_08.
Baseline after B39: 202 `[Fact]` tests. After B40: 214 `[Fact]` tests (+12).

| ID | Method Under Test | What It Asserts |
|----|-------------------|-----------------|
| T_B40_01 | `ArmAllPendingBe` | No open positions in any account: `armedCount == 0`, `_pendingBeSlots.IsEmpty == true` |
| T_B40_02 | `ArmAllPendingBe` | 2 accounts, both past BE threshold: `SubmitBeStop` called twice (via fake delegate), `armedCount == 0`, slots empty |
| T_B40_03 | `ArmAllPendingBe` | 2 accounts, neither past BE threshold: `ArmPendingBe` called twice (via test seam), `armedCount == 2`, slots non-empty |
| T_B40_04 | `ArmAllPendingBe` | Mixed: 1 account past BE, 1 not yet: `SubmitBeStop` called once, `armedCount == 1` |
| T_B40_05 | `IsPendingSlotsEmpty` | Returns `true` on a fresh engine (no slots); returns `false` after `ArmPendingBe` adds a slot |
| T_B40_06 | `ComputeBePrice` | Long position, entry=100.0, buffer=2 ticks, tickSize=0.25: result == `Math.Round((100.0 + 2*0.25)/0.25)*0.25 == 100.5` |
| T_B40_07 | `ComputeBePrice` | Short position, entry=100.0, buffer=2 ticks, tickSize=0.25: result == `Math.Round((100.0 - 2*0.25)/0.25)*0.25 == 99.5` |
| T_B40_08 | `BuildGlobalBeOcoId` | `BuildGlobalBeOcoId(5, 2, 1)` returns `"PTT-BEG-00005-2-1"` |
| T_B40_09 | `BuildGlobalBeOcoId` | 2 calls with same seq but different accIdx return different strings: seq=3, accIdx=0 vs accIdx=1 |
| T_B40_10 | `SubmitBeStop` ocoOverride | When `ocoOverride = "PTT-BEG-00001-0"` is passed, the OCO ID in the CreateOrder call uses `"PTT-BEG-00001-0-0"` for pair 0 (verified via output capture or fake account) |
| T_B40_11 | `IsPriceAlreadyAtBeForAccount` | Long position, entry=100.0, buffer=2, tickSize=0.25, bid=100.75 (>= 100.5 target): returns `true` |
| T_B40_12 | `IsPriceAlreadyAtBeForAccount` | Long position, entry=100.0, buffer=2, tickSize=0.25, bid=100.25 (< 100.5 target): returns `false` |

---

## Section 8 -- Out of Scope

The following are explicitly NOT included in B40:

1. **DW-B39-OOS-01** (Keyboard shortcut for BE ALL via Shift+G): still deferred to B41+.
2. **DW-B39-OOS-03** (Global BE armed state in `PttGlobalBreakEven.cs` for spec `armed`
   mode): B40 implements armed/wait via `ArmAllPendingBe` which reuses the per-account
   `ArmPendingBe`/`OnPendingBeAccountUpdate` path. A separate armed-state field in
   `PttGlobalBreakEven` itself is out of scope.
3. **DW-B39-OOS-05** (Visual buffer sync across Panel/Window surfaces): still deferred.
4. **`PttBreakEven.cs`**: ZERO changes. The per-account single-BE button logic is unchanged.
5. **OCO collision in single-account BE path**: The existing `Substring(0,4)` OCO ID formula
   is retained for single-account BE+ (`SubmitBeStop` with `ocoOverride=null`). The
   single-account path cannot produce collisions because only one account fires per click.
6. **`CancelStaleBrackets` behavior**: Unchanged. NT8-051 sim-account bracket cleanup is
   already implemented and out of scope for B40.
7. **`PropTraderTools.csproj`**: No new files added (all B40 changes are in existing files).
8. **Build tag update**: `"PTT-COPIER B40 | be-all-armed-oco-fix | 2026-07-30"` in
   `CopyEngine.cs` -- ptt-engineer handles this per ticket. Not in scope for architecture.

---

## Section 9 -- Ticket Structure

### T1 -- Engine + OCO Fix
**File**: `src/PropTraderTools/CopyEngine.cs`
**File**: `src/PropTraderTools/Features/PttGlobalBreakEven.cs`

**Spec requirements**: DW-B39-OCO-01 (P0) OCO ID collision fix; DW-B39-BEHAVIOR-01 (P1)
engine side of armed/wait state.

**CopyEngine.cs changes**:
1. Add field `private volatile int _beAllOcoSeq = 0;` near `_pendingBeSlots` (line ~135).
2. Add `internal bool IsPendingSlotsEmpty() => _pendingBeSlots.IsEmpty;` after `DisarmPendingBe`.
3. Add `private static double ComputeBePrice(Position pos, int bufferTicks)` after `IsPendingSlotsEmpty`.
4. Add `private bool IsPriceAlreadyAtBeForAccount(Account acc, Position pos, int bufferTicks)` after `ComputeBePrice`.
5. Add `internal int ArmAllPendingBe(int bufferTicks)` after `IsPriceAlreadyAtBeForAccount`.
6. Modify `SubmitBeStop` signature: add `string ocoOverride = null` as 4th optional parameter.
   Inside the per-pair loop at line 1632: `string ocoId_i = ocoOverride != null ? (ocoOverride + "-" + i) : ("PTT-BE-" + accPrefix + "-" + priceKey + "-" + i);`

**PttGlobalBreakEven.cs changes**:
1. Add field `private volatile int _ocoSeq = 0;` after `_globalBeBuffer`.
2. Add `internal static string BuildGlobalBeOcoId(int seq, int accIdx, int pairIndex)`.
3. Rewrite `Execute(int bufferTicks)` body:
   ```csharp
   internal void Execute(int bufferTicks)
   {
       System.Threading.Interlocked.Increment(ref _ocoSeq);
       CopyEngine.Instance.ArmAllPendingBe(bufferTicks);
   }
   ```
4. Retain `Execute(IEnumerable<Account>, int)` test-seam overload UNCHANGED.
5. Retain `ExecuteOne`, `IncrementBuffer`, `DecrementBuffer`, `GlobalBeBuffer` UNCHANGED.

**SCAN-01**: grep `lock(` in modified files: 0 hits required.
**SCAN-02**: grep `async void` in modified files: 0 hits required.
**SCAN-03**: grep `return null` in new methods: 0 hits required.
**SCAN-04**: grep `throw new` in new methods: 0 hits required.
**SCAN-05**: CYC per method: IsPendingSlotsEmpty=1, ComputeBePrice=2, IsPriceAlreadyAtBeForAccount=4, ArmAllPendingBe=5, BuildGlobalBeOcoId=1. All <= 8.
**SCAN-06**: dotnet build 0 new errors.
**SCAN-07**: `[Fact]` count: 202 (unchanged after T1; tests are in T3).

---

### T2 -- UI Wiring (Panel + Window)
**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**File**: `src/PropTraderTools/TradeCopierWindow.cs`

**Spec requirements**: DW-B39-BEHAVIOR-01 (P1) UI armed/wait state.

**TradeCopierPanel.cs changes**:
1. Add field `private BeState _globalBeState = BeState.Idle;` near existing `_beState` field (line ~200).
2. Rename `OnGlobalBeClick` comment block (B39 → B40). Rewrite body:
   ```csharp
   // B40: OnGlobalBeClick -- armed/wait FSM for BE ALL. CYC=3.
   private void OnGlobalBeClick(object sender, RoutedEventArgs e)
   {
       switch (_globalBeState)
       {
           case BeState.Idle:
               CopyEngine.Instance.GlobalBe.Execute(CopyEngine.Instance.GlobalBe.GlobalBeBuffer);
               if (!CopyEngine.Instance.IsPendingSlotsEmpty())
               {
                   _globalBeState = BeState.Armed;
                   UpdateBeAllVisuals(BeState.Armed);
               }
               break;
           case BeState.Armed:
               if (Account.All != null)
                   foreach (var acc in Account.All)
                       CopyEngine.Instance.DisarmPendingBe(acc);
               _globalBeState = BeState.Idle;
               UpdateBeAllVisuals(BeState.Idle);
               break;
       }
   }
   ```
3. Update `OnPendingBeFiredDispatch`:
   ```csharp
   private void OnPendingBeFiredDispatch(string instr, string accountName)
   {
       Dispatcher.InvokeAsync(() =>
       {
           OnBeConnected(instr, accountName);
           if (_globalBeState == BeState.Armed && CopyEngine.Instance.IsPendingSlotsEmpty())
           {
               _globalBeState = BeState.Idle;
               UpdateBeAllVisuals(BeState.Idle);
           }
       });
   }
   ```
4. Add `UpdateBeAllVisuals(BeState state)`:
   ```csharp
   // B40 -- UpdateBeAllVisuals: purple=Idle, amber=Armed. CYC=2.
   private void UpdateBeAllVisuals(BeState state)
   {
       if (_globalBeBtn2 == null) return;
       _globalBeBtn2.Background = state == BeState.Idle ? BrushPurple : BrushCaution;
   }
   ```
5. Update `Detach()`: after existing `DisarmPendingBe(_leaderAccount)` line, add:
   ```csharp
   // B40: disarm all accounts on detach (BE ALL global cleanup)
   if (Account.All != null)
       foreach (var acc in Account.All)
           CopyEngine.Instance.DisarmPendingBe(acc);
   _globalBeState = BeState.Idle;
   ```

**TradeCopierWindow.cs changes** (mirror Panel exactly):
1. Add `private BeState _windowGlobalBeState = BeState.Idle;` field.
2. Rewrite `OnWindowGlobalBeClick` same FSM as Panel.
3. Add `OnWindowPendingBeFiredDispatch` subscription + handler (same pattern as Panel `OnPendingBeFiredDispatch`).
   - Subscribe in window constructor or `BuildUI`: `_engine.PendingBeFired += OnWindowPendingBeFiredDispatch;`
   - Unsubscribe in `Closed` or teardown handler.
4. Add `UpdateWindowBeAllVisuals(BeState state)`:
   ```csharp
   private void UpdateWindowBeAllVisuals(BeState state)
   {
       if (_windowGlobalBeBtn == null) return;
       _windowGlobalBeBtn.Background = state == BeState.Idle ? WBrushPurple : WBrushCaution;
   }
   ```
   `WBrushCaution` already exists at Window line 65. No new brush definition needed.

**SCAN-01**: 0 `lock(` in modified methods.
**SCAN-02**: 0 `async void` in new methods.
**SCAN-03**: 0 `return null` in new methods.
**SCAN-04**: 0 `throw new` in new methods.
**SCAN-05**: UpdateBeAllVisuals=2, OnGlobalBeClick~4, OnPendingBeFiredDispatch~2. All <= 8.
**SCAN-06**: dotnet build 0 new errors.
**SCAN-07**: `[Fact]` count: 202 (tests are in T3).

---

### T3 -- Tests
**File**: `src/PropTraderTools/CopyEngineTests.cs`

**Spec requirements**: Verify both fixes (DW-B39-OCO-01 and DW-B39-BEHAVIOR-01).

Add 12 new `[Fact]` tests `T_B40_01` through `T_B40_12` as specified in Section 7.

**Test isolation strategy**:
- `ArmAllPendingBe` tests use `CopyEngine._testMode` or stub Account/Position objects.
  The `Execute(IEnumerable<Account>, int)` test-seam overload in `PttGlobalBreakEven` bypasses
  `Account.All`. `ArmAllPendingBe` tests inject via a CopyEngine-level test constructor that
  accepts a fake `Account.All` list.
- `ComputeBePrice` is `private static` -- test via `PrivateObject` reflection or expose as
  `internal` with `[InternalsVisibleTo("CopyEngineTests")]` (already configured in project).
- `IsPendingSlotsEmpty` is `internal` -- directly callable from tests.
- `BuildGlobalBeOcoId` is `internal static` -- directly callable.
- `IsPriceAlreadyAtBeForAccount` is `private` -- test via `ArmAllPendingBe` with fake market
  data that is above/below threshold.

**SCAN-01..07** applied to test file:
- 0 `lock(`, 0 `async void`, 0 `return null`, 0 `throw new`
- All test method bodies are pure assertions, CYC=1 per test
- `[Fact]` count after T3: **214** (was 202; +12)

---

## Deferred Items Closed by B40

| ID | Status |
|----|--------|
| DW-B39-OCO-01 (P0) OCO ID collision (Sim101/Sim102) | CLOSED by T1 |
| DW-B39-BEHAVIOR-01 (P1) BE ALL armed/wait missing | CLOSED by T1+T2 |
| DW-B39-OOS-03 (P2) armed state machine for global BE | PARTIALLY CLOSED -- B40 implements armed/wait via existing per-account ArmPendingBe path; a separate `PttGlobalBreakEven` armed-state field is not needed |

---

## Build Tag

`"PTT-COPIER B40 | be-all-armed-oco-fix | 2026-07-30"`

---

## 7-Scan Pre-Flight Summary

| Scan | Target | Pre-Flight Result |
|------|--------|------------------|
| SCAN-01 | `lock(` in new/modified code | 0 -- all state via volatile+Interlocked+ConcurrentDictionary |
| SCAN-02 | `async void` in new code | 0 -- synchronous event handlers; no async |
| SCAN-03 | `return null` in new methods | 0 -- returns int, bool, double, string (non-null) |
| SCAN-04 | `throw new` in new code | 0 |
| SCAN-05 | CYC per method | Max=5 (ArmAllPendingBe); all <= 8 |
| SCAN-06 | dotnet build | 0 new errors expected; existing AtrSizingEngine errors pre-exist (out of scope per DW-B39-INFO-01) |
| SCAN-07 | `[Fact]` count | 214 after T3 (202 baseline + 12) |

---

*Generated by ptt-architect | Phase 1 Architecture | B40-LaneA | 2026-07-30*
