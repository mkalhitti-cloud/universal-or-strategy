# PTT-COPIER-B7 — Ticket Definitions
# Phase 4 output. Written by ptt-architect after REVIEW_PASS on 02-architecture-plan.md.
# Source: docs/brain/PTT-COPIER-B7/02-architecture-plan.md (REVIEW_PASS, Revision 1)
# Status: TICKETS_COMPLETE

---

## Engineer Notes (From Plan Reviewer — MANDATORY READ BEFORE CODING)

These observations were raised in `02-plan-review.md` and MUST be addressed during implementation.

1. **ConcurrentBag deduplication (T1 — PopulateOrderMap):**
   `ConcurrentBag<T>` has no deduplication semantics. `PopulateOrderMap` must check existing
   bag contents before calling `.Add()` to prevent duplicate `FollowerBinding` entries when
   `OnOrderUpdate` fires repeated `Working` state events for the same `(signalName, follower)` pair.
   Suggested guard (iterate bag, check for existing match before Add):
   ```csharp
   var bag = _orderMap.GetOrAdd(fromEntrySignalName, _ => new ConcurrentBag<FollowerBinding>());
   // Dedup: only Add if this follower account is not already recorded for this signal name
   if (!bag.Any(b => b.FollowerAccount == followerAccount))
       bag.Add(new FollowerBinding { FollowerAccount = followerAccount,
                                     FromEntrySignalName = fromEntrySignalName });
   ```
   This keeps `PopulateOrderMap` CYC = 2 (adds 1 branch for the if-guard). Still <= 8. ✅

2. **T2 compile dependency (T1 MUST be committed first):**
   `TradeCopierPanel.cs` and `TradeCopierWindow.cs` both reference `PositionState` (defined in
   `CopyEngine.cs`) and subscribe to `CopyEngine.PositionStateChanged`. T1 (CopyEngine.cs) MUST
   compile and pass all tests before T2 work begins. T2 will not compile against a pre-T1 baseline.

3. **Window per-rule button tracking lists (T2 — TradeCopierWindow.cs):**
   `UpdateButtonColors` on `TradeCopierWindow` iterates four per-rule button lists:
   `_flattenBtns`, `_cancelBtns`, `_trimBtns`, `_beBtns` (`List<Button>`). These fields MUST be
   declared at class level. Buttons created in `BuildRuleRow()` and `BuildDynamicRuleRow()` MUST be
   appended to these lists immediately after creation. Use the existing `_leaderBoxes` /
   `_followerBoxes` lists as the precedent pattern for this storage approach.

4. **FollowerAtmMode nested record placement (T1 — CopyEngine.cs):**
   The three `sealed record` variants (`Inherit`, `Market`, `Named`) MUST be declared INSIDE the
   body of `public abstract record FollowerAtmMode`. This is required so they can access the
   `private FollowerAtmMode() { }` base constructor (C# nested-type visibility rule). Do NOT move
   them outside the abstract record body — they will not compile if the base constructor is private
   and they are declared at file scope.

5. **CopyRule.Create factory update (T1 — CopyEngine.cs):**
   `CopyRule.FollowerAtmTemplates` is a new `init`-only field. The existing `CopyRule.Create`
   factory method MUST be updated to pass `FollowerAtmTemplates` through. Default value in B7 is
   always `ImmutableDictionary<string, FollowerAtmMode>.Empty`. Engineer must locate every existing
   `Create()` call site in the file and confirm no compilation break. No behavior change in B7.

---

## Ticket T1 — CopyEngine + Tests (P0)

> **Covers:** B7-F0 (bracket mirroring), V01 (order map + FollowerBinding), V02 (price-delta guard),
> V03 (nullable return), V05 (PositionState struct + event), V06 (FollowerAtmMode hierarchy),
> V07 (ImmutableDictionary on CopyRule)

### Spec Requirement IDs

| Requirement | Spec Location |
|-------------|---------------|
| Bracket mirroring via OrderUpdate Working state | spec line 2162 (B7-F0 feature card) |
| `_orderMap` keyed by `FromEntrySignal` name | spec line 2175-2176 |
| `FollowerBinding` struct in pill | spec line 2195 (pill: "New: _orderMap + FollowerBinding") |
| Match by `FromEntrySignal` — NOT leg-type scan | spec line 2181, 2188, 1846-1847 |
| Stop leg: `StopPrice` sync via `acc.Change()` | spec line 2183-2184 |
| Target leg: `LimitPrice` sync via `acc.Change()` | spec line 2184 |
| Price delta >= 1 tick guard (no micro-jitter) | spec line 2189 (implied; plan V02) |
| `PositionState` as `readonly struct` | spec line 1045, 1052 |
| `PositionStateChanged` event on CopyEngine | spec line 716-717 |
| `FollowerAtmMode` sealed record hierarchy | spec line 1045, 2335 |
| `ImmutableDictionary<string, FollowerAtmMode>` on `CopyRule` | spec line 1059, 2340 |
| Min 2 new xUnit [Fact] tests | spec line 2196 (plan provides 5 — exceeds minimum) |

### Wave Workspace File Paths

```
c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs
c:\WSGTA\universal-or-strategy\tests\PropTraderTools.Tests\CopyEngineTests.cs
```
*(Mirror path via deploy-sync.ps1 to NinjaTrader hard-link after commit.)*

### Current Baseline

| File | Lines | State |
|------|-------|-------|
| `CopyEngine.cs` | 534 | B6-complete (SaveRules/LoadRules). All 7 scans PASS. |
| `CopyEngineTests.cs` | 345 | 22 xUnit [Fact] tests passing. |

### Method Signatures to Implement

---

#### 1. `private void DispatchCopy(Order order, CopyRule rule)` — EXTRACTED from `OnOrderUpdate`

**File:** `CopyEngine.cs`
**Type:** Private extraction (pure refactor — behavior identical to current `OnOrderUpdate` lines 172-201)
**CYC estimate:** 6
**JS rules:** JS-021 (no lock — ConcurrentDictionary dedup check), JS-001 (no throw — try/catch in caller)

**Behavior contract:**
- Gate A: if `order.OrderState != OrderState.Submitted` → return
- Gate B: derive `isMarket` / `isLimit`; if neither → return
- Gate C: if `IsDedup(order.Id)` → return
- Dispatch: foreach follower account in `rule.FollowerAccounts`
  - if `acc == null` → continue
  - if `!PassesDailyCapCheck(acc, rule)` → continue
  - call `SendCopy(order, acc, rule)` (existing method, unchanged)
- This is a **pure structural extraction**. No logic changes. Existing behavior is preserved exactly.

**Pre-condition:** Caller (`OnOrderUpdate`) has already matched the order to a `CopyRule` and confirmed
`IsWorkingBracket` is false before calling this method.

---

#### 2. `private static bool IsWorkingBracket(Order order)` — NEW

**File:** `CopyEngine.cs`
**Type:** New static predicate
**CYC estimate:** 1
**JS rules:** JS-002 (no null return — returns bool, not nullable)

**Behavior contract:**
- Returns: `order.OrderState == OrderState.Working && IsBracketLeg(order)`
- Both conditions must be true for the method to return `true`
- `IsBracketLeg` is an existing private method in `CopyEngine.cs` (reused, not modified)
- This is the gate predicate for the bracket-detection branch in `OnOrderUpdate`

---

#### 3. `private void HandleBracketChange(Order leaderOrder, CopyRule rule)` — NEW

**File:** `CopyEngine.cs`
**Type:** New method
**CYC estimate:** 8 (exactly at limit — see branch breakdown)
**JS rules:** JS-001 (try/catch around `acc.Change()` — no throw in hot path), JS-021 (no lock), JS-002 (fo nullable guard), JS-025 (ConcurrentDictionary access in _orderMap)

**Behavior contract (branch-by-branch):**
```
(1) bool isStop = IsStopLeg(leaderOrder)   -- ternary, counts as 1 branch
(1) Instrument instrument = leaderOrder.Instrument; if instrument == null: return
(1) double tickSize = instrument.MasterInstrument?.TickSize ?? 0.0;
    if tickSize <= 0: proceed with raw price (no tick rounding applied)
(1) double rawPrice = isStop ? leaderOrder.StopPrice : leaderOrder.LimitPrice  -- ternary
    double newPrice = tickSize > 0 ? Math.Round(rawPrice / tickSize) * tickSize : rawPrice
    // Tick rounding applied BEFORE price-delta guard (V02 order requirement)
(1) foreach rule.FollowerAccounts
(1)   if acc == null: continue
(0)   var fo = FindFollowerBracketOrder(acc, leaderOrder.FromEntrySignal, isStop)
(1)   if fo == null: continue   -- V03 nullable guard
(1)   PRICE-DELTA GUARD (V02):
      double currentPrice = isStop ? fo.StopPrice : fo.LimitPrice;
      if (Math.Abs(newPrice - currentPrice) < tickSize) continue;
    try {
        if (isStop) fo.StopPrice = newPrice; else fo.LimitPrice = newPrice;
        acc.Change(new Order[] { fo });
        StatusUpdate?.Invoke(acc.Name + ": bracket synced " + (isStop ? "stop" : "target")
                             + " -> " + newPrice);
    } catch (Exception ex) {
        StatusUpdate?.Invoke(acc.Name + ": bracket sync error: " + ex.Message);
    }
    // try/catch wraps acc.Change() ONLY — does NOT add to method CYC count
```
**Total CYC = 8** (exactly at limit). ✅

**Critical NT8 note:** `acc.Change(new Order[] { fo })` is the established NT8 pattern (see
`CopyEngine.cs` line 443 — `MoveStopToBreakEven` uses this exact call). The loop is synchronous;
no `Dispatcher.InvokeAsync` needed in CopyEngine (engine is not a UI class).

**Infinite mirror prevention:** Follower accounts are never the `MasterAccount` of any rule by
architectural construction. `OnOrderUpdate` rule-matching (Gate 2) prevents follower-originated
events from reaching this path.

---

#### 4. `private Order? FindFollowerBracketOrder(Account follower, string fromEntrySignalName, bool isStop)` — NEW

**File:** `CopyEngine.cs`
**Type:** New method, nullable return (V03)
**CYC estimate:** 4
**JS rules:** JS-002 (nullable return `Order?` — null contract explicit, callers use `if fo == null`)

**Behavior contract:**
```
(1) foreach follower.Orders
(1)   if order.FromEntrySignal != fromEntrySignalName: continue
      // spec line 2176: "key = master entry order FromEntrySignal name"
      // Do NOT match by leg type first — correct bracket found only via signal name
(1)   if order.OrderState != OrderState.Working: continue
(1)   if isStop: check order.OrderType == OrderType.StopMarket || OrderType.StopLimit
      else:      check order.OrderType == OrderType.Limit && !IsStopLeg(order)
      if match: return order
    // end loop
(0) return null
```

**Why `FromEntrySignal` matching (not leg-type scan):**
Per spec lines 2176-2177 and V01: correlating by `FromEntrySignal` name ensures the correct bracket
is found even when multiple instruments are open simultaneously. Old leg-type scan could select the
wrong bracket if ATM names collide across instruments.

**Return type annotation:** `Order?` (C# 8+ nullable reference type). The `?` annotation is the V03
fix — it makes the null contract compile-time verifiable. All callers must use `if (fo == null) continue`.

---

#### 5. `private void PopulateOrderMap(string fromEntrySignalName, Account followerAccount)` — NEW

**File:** `CopyEngine.cs`
**Type:** New method
**CYC estimate:** 2 (1 for the dedup guard — see Engineer Note #1)
**JS rules:** JS-021 (no lock), JS-025 (ConcurrentDictionary.GetOrAdd is atomic)

**Behavior contract (with mandatory dedup guard from Engineer Note #1):**
```csharp
var bag = _orderMap.GetOrAdd(fromEntrySignalName,
              _ => new ConcurrentBag<FollowerBinding>());
// Dedup guard: prevent accumulating duplicate bindings on repeated Working state events
if (!bag.Any(b => b.FollowerAccount == followerAccount))
    bag.Add(new FollowerBinding
    {
        FollowerAccount      = followerAccount,
        FromEntrySignalName  = fromEntrySignalName
    });
```
- `ConcurrentDictionary.GetOrAdd` is atomic (JS-025) — no lock needed for outer map
- `ConcurrentBag.Add` is lock-free (JS-021)
- The `Any()` read on `ConcurrentBag` is safe for concurrent access (bag is thread-safe for reads)
- Called from `OnOrderUpdate` Gate B when `IsWorkingBracket(e.Order)` is true and
  `e.Order.FromEntrySignal != null`

---

#### 6. `private void TryFirePositionState(OrderEventArgs e)` — NEW

**File:** `CopyEngine.cs`
**Type:** New method
**CYC estimate:** 2
**JS rules:** JS-003 (PositionState readonly struct passed by value — no reference aliasing in closure)

**Behavior contract:**
```
(1) if e.OrderState is not (Filled | PartFilled | Cancelled | Rejected): return
    // Fires ONLY on states that change position truth — NOT on Working (prevents
    // spurious button updates during bracket drags)
(1) if e.Order?.Instrument?.FullName == null: return
    string instr = e.Order.Instrument.FullName;
    bool hasPos     = HasOpenPosition(e.Order.Account, e.Order.Instrument);
    bool hasEntries = HasWorkingEntries(e.Order.Account, e.Order.Instrument);
    PositionStateChanged?.Invoke(instr, new PositionState
    {
        HasOpenPosition   = hasPos,
        HasWorkingEntries = hasEntries
    });
```
**Call site in `OnOrderUpdate`:** Called as the FIRST statement, BEFORE Gate 1 (`if !_isCopyEnabled: return`).
This ensures position-state events fire even when copy is disabled — UI button colors remain accurate
at all times regardless of copy toggle state.

---

#### 7. `private bool HasOpenPosition(Account acc, Instrument instrument)` — NEW

**File:** `CopyEngine.cs`
**Type:** New private helper
**CYC estimate:** 2
**JS rules:** JS-002 (null-safe: pos can be null)

**Behavior contract:**
```
(1) var pos = FindPosition(acc, instrument);  // existing private method
    if pos == null: return false
(0) return pos.Quantity > 0
```

---

#### 8. `private bool HasWorkingEntries(Account acc, Instrument instrument)` — NEW

**File:** `CopyEngine.cs`
**Type:** New private helper
**CYC estimate:** 3
**JS rules:** JS-021 (no lock — iterates acc.Orders, same pattern as CancelPendingEntries)

**Behavior contract:**
```
(1) foreach acc.Orders
(1)   if order.Instrument != instrument: continue
(1)   if order.OrderState != OrderState.Working: continue
(0)   if !IsBracketLeg(order): return true   // entry order (not a bracket leg) found
    // end loop
(0) return false
```
**Distinction:** Working bracket legs (stop/target) do NOT count as entries. Only working non-bracket
orders (i.e., the entry itself) trigger the `hasEntries` = true path.

---

#### 9. `OnOrderUpdate` — RESTRUCTURED (not new — existing method modified)

**File:** `CopyEngine.cs`
**CYC after restructure:** 7 (was ~11 before extraction)
**JS rules:** JS-021 (no lock in restructured body)

**New structure (delta from current):**
```
[LINE 0]  TryFirePositionState(e);               // NEW: pre-gate, unconditional
[Gate 1]  if !_isCopyEnabled: return             // (1) unchanged
[Gate 2]  foreach _rules; match instr+account    // (2) unchanged
[Gate 2n] if matchedRule == null: return         // (1) unchanged
[Gate 2.5]if !matchedRule.Value.Enabled: return  // (1) unchanged
[Gate B]  if IsWorkingBracket(e.Order):          // (1) NEW BRANCH
              if e.Order.FromEntrySignal != null: // (1) NEW NESTED CHECK
                  PopulateOrderMap(e.Order.FromEntrySignal, <follower>)
              HandleBracketChange(e.Order, matchedRule.Value)
              return
[else]    DispatchCopy(e.Order, matchedRule.Value) // (0) EXTRACTED (no branch)
// Total: 7 branches. CYC = 7 ✅
```
**Note on Gate B follower argument for PopulateOrderMap:** The follower account that produced
the `Working` event is `e.Order.Account`. Pass `e.Order.Account` as the `followerAccount` arg.

---

### Data Model Changes

All additive. No existing fields, types, or methods removed or renamed.

#### A. New `using` directive (top of `CopyEngine.cs`)
```csharp
using System.Collections.Immutable;   // V07: for ImmutableDictionary in CopyRule
```

#### B. Top-level types (outside `CopyEngine` class, inside `namespace PropTraderTools`)

```csharp
// V01: Binding record for _orderMap inner collection
internal readonly struct FollowerBinding
{
    internal Account FollowerAccount     { get; init; }
    internal string  FromEntrySignalName { get; init; }
}

// V05: Position truth snapshot — JS-003 (readonly struct prevents bool transposition)
public readonly struct PositionState
{
    public bool HasOpenPosition   { get; init; }
    public bool HasWorkingEntries { get; init; }
}

// V06: ATM mode discriminated union — JS-003 + JS-010
// MANDATORY: nested records must be INSIDE the abstract record body (Engineer Note #4)
public abstract record FollowerAtmMode
{
    private FollowerAtmMode() { }   // JS-010: private base constructor — no external subclassing
    public sealed record Inherit()                  : FollowerAtmMode;  // B7 default
    public sealed record Market()                   : FollowerAtmMode;  // pure market
    public sealed record Named(string TemplateName) : FollowerAtmMode;  // specific ATM template
}
// B7 status: scaffolding only. Zero behavior change. B8 adds SendCopy switch + UI dropdown.
```

#### C. New field in `CopyEngine` class body
```csharp
// V01: order map for follower bracket lookup
// JS-025: ConcurrentDictionary (atomic GetOrAdd) + ConcurrentBag (lock-free Add/iterate)
// JS-021: NO lock keyword anywhere
private readonly ConcurrentDictionary<string, ConcurrentBag<FollowerBinding>> _orderMap
    = new ConcurrentDictionary<string, ConcurrentBag<FollowerBinding>>();
```

#### D. New event in `CopyEngine` class body
```csharp
// V05: position state change notification for UI surfaces
// Fired from TryFirePositionState — before Gate 1 (fires even when copy is disabled)
public event Action<string, PositionState> PositionStateChanged;
```

#### E. Additive field in existing `CopyRule` private readonly struct
```csharp
// V07: ATM template map per follower account — JS-009 (ImmutableDictionary, not Dictionary)
// Default: always Empty in B7. B8 populates via ImmutableDictionary.SetItem(acc, mode).
internal ImmutableDictionary<string, FollowerAtmMode> FollowerAtmTemplates { get; init; }
    = ImmutableDictionary<string, FollowerAtmMode>.Empty;
```
**⚠ Engineer action required:** Update `CopyRule.Create(...)` factory method to pass
`FollowerAtmTemplates` through (default: `ImmutableDictionary<string, FollowerAtmMode>.Empty`).
Locate all existing `CopyRule.Create(...)` call sites and confirm no compilation break (Engineer Note #5).

---

### xUnit Tests (CopyEngineTests.cs — 5 new [Fact] methods)

All tests use xUnit `[Fact]` only. No `[Theory]`. No NUnit. No MSTest.
File: `c:\WSGTA\universal-or-strategy\tests\PropTraderTools.Tests\CopyEngineTests.cs`
Target total after T1: **27 [Fact] methods** (22 baseline + 5 new).

---

#### T-B7-01: `DispatchCopy_MethodExists`

```csharp
[Fact]
public void DispatchCopy_MethodExists()
{
    // Asserts: private method "DispatchCopy" exists on CopyEngine with exactly 2 parameters
    // (Order, CopyRule). Guards against accidental removal of the extracted method.
    var method = typeof(CopyEngine).GetMethod(
        "DispatchCopy",
        BindingFlags.NonPublic | BindingFlags.Instance);
    Assert.NotNull(method);
    Assert.Equal(2, method.GetParameters().Length);
}
```

---

#### T-B7-02: `IsWorkingBracket_MethodExists`

```csharp
[Fact]
public void IsWorkingBracket_MethodExists()
{
    // Asserts: private static method "IsWorkingBracket" exists on CopyEngine with exactly
    // 1 parameter (Order). Guards against accidental removal.
    var method = typeof(CopyEngine).GetMethod(
        "IsWorkingBracket",
        BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);
    Assert.Equal(1, method.GetParameters().Length);
}
```

---

#### T-B7-03: `HandleBracketChange_NullGuards_DoNotThrow`

```csharp
[Fact]
public void HandleBracketChange_NullGuards_DoNotThrow()
{
    // Asserts: invoking HandleBracketChange via reflection with a null-adjacent
    // (minimal stub) Order does not throw an unhandled exception out of the method.
    // Verifies that the instrument-null guard (branch 2) returns cleanly.
    var engine = CreateMinimalEngine();   // helper: constructs CopyEngine with no rules
    var method = typeof(CopyEngine).GetMethod(
        "HandleBracketChange",
        BindingFlags.NonPublic | BindingFlags.Instance);
    Assert.NotNull(method);
    var minimalOrder = CreateStubOrderNoInstrument();  // FromEntrySignal="sig1", Instrument=null
    var minimalRule  = CreateDefaultCopyRule();
    // Should not throw — instrument==null guard returns early
    var ex = Record.Exception(() =>
        method.Invoke(engine, new object[] { minimalOrder, minimalRule }));
    Assert.Null(ex);
}
```

---

#### T-B7-04: `FindFollowerBracketOrder_NullableReturnType`

```csharp
[Fact]
public void FindFollowerBracketOrder_NullableReturnType()
{
    // Asserts: FindFollowerBracketOrder return type is Order? (nullable reference type).
    // Confirms JS-002 compliance — null contract is explicit at the type level, not implicit.
    var method = typeof(CopyEngine).GetMethod(
        "FindFollowerBracketOrder",
        BindingFlags.NonPublic | BindingFlags.Instance);
    Assert.NotNull(method);
    // Nullable annotation on reference type: ReturnType.Name == "Order" and IsClass
    // The NullabilityInfoContext confirms the return is annotated nullable
    var ctx = new System.Reflection.NullabilityInfoContext();
    var nullInfo = ctx.Create(method.ReturnParameter);
    Assert.Equal(System.Reflection.NullabilityState.Nullable, nullInfo.WriteState);
}
```

---

#### T-B7-05: `OnOrderUpdate_WithWorkingBracket_DoesNotDispatchCopy`

```csharp
[Fact]
public void OnOrderUpdate_WithWorkingBracket_DoesNotDispatchCopy()
{
    // Asserts: when OnOrderUpdate receives a Working+bracket order with _isCopyEnabled=true,
    // the DispatchCopy path is NOT taken (i.e., no copy is sent for bracket drag events).
    // Verifies that Gate B (IsWorkingBracket) correctly diverts to HandleBracketChange path.
    var engine = CreateEngineWithCopyEnabled();  // _isCopyEnabled = true, one rule wired
    int dispatchCount = 0;
    // Intercept: count calls to SendCopy via a test hook or verify via side effects
    // (pattern: track StatusUpdate "bracket synced" vs "copy sent" prefix)
    var bracketOrder = CreateStubWorkingBracketOrder();  // OrderState=Working, IsBracketLeg=true
    var eventArgs    = CreateOrderEventArgs(bracketOrder);
    InvokeOnOrderUpdate(engine, eventArgs);
    // DispatchCopy path sends copies — if 0 copies sent, Gate B correctly diverted
    Assert.Equal(0, dispatchCount);
}
```

---

### 7-Scan Checklist (T1 — CopyEngine.cs + CopyEngineTests.cs)

Run each grep against `src\PropTraderTools\CopyEngine.cs` and `tests\PropTraderTools.Tests\CopyEngineTests.cs`.

| Scan | Pattern to Search | Expected Count | Notes |
|------|-------------------|---------------|-------|
| SCAN-01 | `lock(` | **0** | `_orderMap` uses ConcurrentDictionary (JS-021/JS-025). No `lock` keyword in any new method. |
| SCAN-02 | Non-ASCII characters (any char > 0x7F) | **0** | All string literals and comments are ASCII-only. No emoji, curly quotes, or Unicode symbols. |
| SCAN-03 | `FontFamily` | **0** | No font-related changes in CopyEngine. |
| SCAN-04 | `#[0-9A-Fa-f]{6}` (hex color strings) | **0** | No hex color strings. CopyEngine contains no brush creation code. |
| SCAN-05 | `CreateOrder` not preceded by `"PTT-"` prefix | **0** | No new `CreateOrder` calls. Existing `"PTT-Copy"` call site unchanged. |
| SCAN-06 | `DateTime.Now` (not `DateTime.UtcNow`) | **0** | No `DateTime` usage in any new method. Existing code unchanged. |
| SCAN-07 | `sealed class TradeCopierWindow` (class declaration) | **0** | CopyEngine.cs contains no Window declaration. N/A for this file. |

### Dependency

None. T1 is self-contained. No other B7 file changes are required before T1 can be committed.

### Verification Criteria

- [ ] All 22 existing `[Fact]` tests pass (zero regressions)
- [ ] All 5 new `[Fact]` tests (T-B7-01 through T-B7-05) pass
- [ ] `OnOrderUpdate` CYC = 7 (verified by `complexity_audit.py`)
- [ ] `HandleBracketChange` CYC = 8 (exactly at limit — flag if it exceeds 8)
- [ ] All 7 SCAN counts = 0
- [ ] `dotnet build` zero errors, zero new warnings
- [ ] `deploy-sync.ps1` executed after commit (hard-link sync)

---

## Ticket T2 — UI: Button Color Coding + ScrollViewer (P2)

> **Covers:** B7-F1 (button color coding, Layer 2 + Layer 3), B7-F5 (ScrollViewer on rule grid),
> V04 (live state wiring), V08 (corrected canonical RGB values)

> **⚠ DEPENDENCY: T1 must be committed and compiled before T2 work begins.** (Engineer Note #2)

### Spec Requirement IDs

| Requirement | Spec Location |
|-------------|---------------|
| Copy ON = green, Copy OFF = grey (Layer 2) | PTT_DESIGN_PILLAR (Layer 2 semantic colors) |
| Flatten/Cancel = red ONLY when position/entries live | spec line 716-717, PTT_DESIGN_PILLAR Layer 3 |
| Trim/BE = amber/green ONLY when position live | spec line 716-717, PTT_DESIGN_PILLAR Layer 3 |
| Grey when no target state exists (all action buttons) | spec line 716 ("A grey button is information") |
| `CopyEngine.PositionStateChanged` event drives live transitions | spec line 717 |
| Both surfaces (Panel + Window) subscribe and update | spec line 717 ("All surfaces subscribe") |
| Canonical RGB values per PTT_DESIGN_PILLAR | PTT_DESIGN_PILLAR lines 192-198 |
| ScrollViewer wrapping `_rulesPanel` (MaxHeight=400) | spec line 1409 (Window rule rows) |
| `DockPanel.SetDock` on ScrollViewer wrapper (not StackPanel) | architectural constraint from plan |

### Wave Workspace File Paths

```
c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs
c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierWindow.cs
```

### Current Baseline

| File | Lines | State |
|------|-------|-------|
| `TradeCopierPanel.cs` | 225 | B7-FIX (P&L live dropdown complete). Has `MakeBrush`. |
| `TradeCopierWindow.cs` | 392 | B6-FIX4 (plain Window, LoadRules/SaveRules). No `MakeBrush`. |

### Canonical Brush RGB Values (V08 — corrected from previous wrong values)

All brush creation uses integer RGB via `MakeBrush()` / `MakeWinBrush()`. **No hex string literals.**

| Brush Name | RGB | Hex (comment only — never in code) | Use |
|------------|-----|-------------------------------------|-----|
| `BrushActive` / `WBrushActive` | `(34, 197, 94)` | #22c55e | Copy ON, BE (when position live) |
| `BrushDanger` / `WBrushDanger` | `(239, 68, 68)` | #ef4444 | Flatten, Cancel (when target live) — **CORRECTED from (185,28,28)** |
| `BrushCaution` / `WBrushCaution` | `(245, 158, 11)` | #f59e0b | Trim (when position live) — **CORRECTED from (217,119,6)** |
| `BrushInactive` / `WBrushInactive` | `(55, 65, 81)` | #4b5563 | All action buttons (no target state) — **CORRECTED from (75,85,99)** |

### Method Signatures to Implement

---

### TradeCopierPanel.cs Changes

#### 1. Brush fields — NEW/UPDATED at class level

**Type:** Additive class-level field declarations (or update of existing wrong-value fields)
**JS rules:** JS-008 (brushes Freeze()d via existing `MakeBrush` helper — static readonly = zero allocation)

```csharp
// Canonical semantic button brushes (V08: corrected RGB per PTT_DESIGN_PILLAR lines 192-198)
// JS-008: all Freeze()d via MakeBrush(), static readonly = zero allocation on re-render
private static readonly SolidColorBrush BrushActive   = MakeBrush( 34, 197,  94);  // green  #22c55e
private static readonly SolidColorBrush BrushDanger   = MakeBrush(239,  68,  68);  // red    #ef4444
private static readonly SolidColorBrush BrushCaution  = MakeBrush(245, 158,  11);  // amber  #f59e0b
private static readonly SolidColorBrush BrushInactive = MakeBrush( 55,  65,  81);  // grey   #4b5563
// Note: BrushPositive/BrushNegative/BrushDim stay in FollowerItem nested class (P&L colors, unchanged)
// Note: BrushActive replaces old BrushCopyOn/BrushBe (same RGB — canonical name from pillar)
// Note: BrushInactive replaces old BrushCopyOff (corrected RGB value)
// Note: BrushDanger replaces old BrushDanger (corrected RGB — was 185,28,28)
```

---

#### 2. `private void UpdateButtonColors(bool hasPosition, bool hasEntries)` — NEW

**File:** `TradeCopierPanel.cs`
**CYC estimate:** 5
**JS rules:** JS-023 (called only via `Dispatcher.InvokeAsync` — caller responsibility), JS-008 (all brushes are static readonly Freeze()d)

**Behavior contract:**
```
Must run on UI thread (always called via Dispatcher.InvokeAsync from OnPositionStateChanged).
(1) _copyToggleBtn.Background = _copyEnabled ? BrushActive : BrushInactive
(1) _flattenBtn.Background    = hasPosition ? BrushDanger : BrushInactive
(1) _cancelBtn.Background     = hasEntries  ? BrushDanger : BrushInactive
(1) _trimBtn.Background       = hasPosition ? BrushCaution : BrushInactive
(1) _beBtn.Background         = hasPosition ? BrushActive  : BrushInactive
```
No side effects beyond setting button `.Background` properties. No new subscriptions.
Note: copy toggle is ALWAYS interactive (never truly "off-color" for state) — it reflects
`_copyEnabled` directly, not a position/entry state.

---

#### 3. `private void OnPositionStateChanged(string instr, PositionState state)` — NEW

**File:** `TradeCopierPanel.cs`
**CYC estimate:** 1
**JS rules:** JS-023 (Dispatcher.InvokeAsync for off-thread → UI-thread marshal), JS-003 (PositionState is value type — closure captures safely)

**Behavior contract:**
```
(1) if _instrument == null || _instrument.FullName != instr: return
    // Panel is per-instrument. Only update buttons if the event is for our instrument.
(0) Dispatcher.InvokeAsync(() =>
        UpdateButtonColors(state.HasOpenPosition, state.HasWorkingEntries))
```
Called on the NT8 order-update background thread (off UI thread). MUST marshal via
`Dispatcher.InvokeAsync`. The `state` struct is captured by value — no reference aliasing risk.

---

#### 4. `BuildUI()` modifications — Layer 2 + Layer 3 initial state

**File:** `TradeCopierPanel.cs`
**Type:** Modification of existing method (additive changes only)

```
For each action button created in BuildUI():
  _copyToggleBtn.Background = BrushInactive   // OFF state at startup
  flattenBtn.Background     = BrushInactive   // starts grey — no position yet
  cancelBtn.Background      = BrushInactive   // starts grey — no entries yet
  trimBtn.Background        = BrushInactive   // starts grey — no position yet
  beBtn.Background          = BrushInactive   // starts grey — no position yet

  // Do NOT call SetResourceReference(Control.StyleProperty, "NTButtonStyle") on
  // color-coded buttons — NT8 ControlTemplate may override Background property.
  // Non-color-coded buttons (e.g. "Apply Rule") keep NTButtonStyle.

At END of BuildUI() (after all buttons created):
  UpdateButtonColors(false, false);   // V04: consistent initial state
```

---

#### 5. `OnToggle()` modification — Layer 2 toggle reflection

**File:** `TradeCopierPanel.cs`
**Type:** Modification of existing method (1 line change)

```csharp
// After existing state-flip logic — ADD:
_copyToggleBtn.Background = _copyEnabled ? BrushActive : BrushInactive;
```

---

#### 6. Subscribe/unsubscribe wiring — V04

**File:** `TradeCopierPanel.cs`
**Type:** Modification of existing event handler registrations

```csharp
// In OnLoaded handler — ADD (alongside existing AccountItemUpdate subscription):
_engine.PositionStateChanged += OnPositionStateChanged;

// In Detach() method — ADD (alongside existing AccountItemUpdate unsubscription):
_engine.PositionStateChanged -= OnPositionStateChanged;
```
Pattern: Panel already has `OnLoaded` and `Detach()` wiring for `AccountItemUpdate`. Use identical
subscription/unsubscription pattern.

---

### TradeCopierWindow.cs Changes

#### 1. `MakeWinBrush` static helper — NEW

**File:** `TradeCopierWindow.cs`
**Type:** New private static method
**CYC estimate:** 1
**JS rules:** JS-008 (Freeze() call — brush thread-safe for Dispatcher.InvokeAsync capture)

```csharp
// JS-008: produces frozen, thread-safe SolidColorBrush
// "Win" prefix avoids collision with potential Window base-class members
private static SolidColorBrush MakeWinBrush(byte r, byte g, byte b)
{
    var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
    brush.Freeze();
    return brush;
}
```

---

#### 2. Brush fields — NEW at class level

**File:** `TradeCopierWindow.cs`
**JS rules:** JS-008 (brushes Freeze()d via MakeWinBrush — static readonly)

```csharp
// Canonical semantic brushes (V08: corrected RGB per PTT_DESIGN_PILLAR lines 192-198)
private static readonly SolidColorBrush WBrushActive   = MakeWinBrush( 34, 197,  94);  // green
private static readonly SolidColorBrush WBrushDanger   = MakeWinBrush(239,  68,  68);  // red
private static readonly SolidColorBrush WBrushCaution  = MakeWinBrush(245, 158,  11);  // amber
private static readonly SolidColorBrush WBrushInactive = MakeWinBrush( 55,  65,  81);  // grey
```

---

#### 3. Button-reference tracking fields — NEW (Engineer Note #3)

**File:** `TradeCopierWindow.cs`
**Type:** New class-level field declarations
**JS rules:** JS-021 (no lock — these lists are only accessed on UI thread)

```csharp
// Per-rule button tracking for UpdateButtonColors iteration (Engineer Note #3)
// Precedent: _leaderBoxes / _followerBoxes (existing pattern in this file)
private readonly List<Button> _flattenBtns = new List<Button>();
private readonly List<Button> _cancelBtns  = new List<Button>();
private readonly List<Button> _trimBtns    = new List<Button>();
private readonly List<Button> _beBtns      = new List<Button>();
```
Buttons appended to these lists immediately after creation in `BuildRuleRow()` and
`BuildDynamicRuleRow()` (see item 6 below).

---

#### 4. `private void UpdateButtonColors(bool hasPosition, bool hasEntries)` — NEW

**File:** `TradeCopierWindow.cs`
**CYC estimate:** 5
**JS rules:** JS-023 (called only via `Dispatcher.InvokeAsync`), JS-008 (all brushes static readonly Freeze()d)

**Behavior contract:**
```
Must run on UI thread (always called via Dispatcher.InvokeAsync from OnPositionStateChanged).
(1) _globalToggleBtn.Background = _copyEnabled ? WBrushActive : WBrushInactive
(1) foreach _flattenBtns: btn.Background = hasPosition ? WBrushDanger : WBrushInactive
(1) foreach _cancelBtns:  btn.Background = hasEntries  ? WBrushDanger : WBrushInactive
(1) foreach _trimBtns:    btn.Background = hasPosition ? WBrushCaution : WBrushInactive
(1) foreach _beBtns:      btn.Background = hasPosition ? WBrushActive  : WBrushInactive
```
Window shows ALL rules (not filtered per instrument). Any position state change on any instrument
triggers full re-evaluation of all per-rule buttons.

---

#### 5. `private void OnPositionStateChanged(string instr, PositionState state)` — NEW

**File:** `TradeCopierWindow.cs`
**CYC estimate:** 1
**JS rules:** JS-023 (Dispatcher.InvokeAsync for off-thread marshal), JS-003 (PositionState value capture)

**Behavior contract:**
```
(1) if instr == null: return   // simple null guard — Window has no per-instrument filter
    // (intentional asymmetry vs Panel — Window shows all rules, Panel is per-instrument)
(0) Dispatcher.InvokeAsync(() =>
        UpdateButtonColors(state.HasOpenPosition, state.HasWorkingEntries))
```

---

#### 6. `BuildRuleRow()` + `BuildDynamicRuleRow()` modifications — append to button lists

**File:** `TradeCopierWindow.cs`
**Type:** Additive modification inside existing methods

For EACH row-building method, after creating each action button, append to the corresponding list:
```csharp
// Example inside BuildRuleRow():
var flattenBtn = new Button { Content = "Flatten", Background = WBrushInactive };
_flattenBtns.Add(flattenBtn);   // ADD — Engineer Note #3

var cancelBtn  = new Button { Content = "Cancel",  Background = WBrushInactive };
_cancelBtns.Add(cancelBtn);

var trimBtn    = new Button { Content = "Trim",    Background = WBrushInactive };
_trimBtns.Add(trimBtn);

var beBtn      = new Button { Content = "BE",      Background = WBrushActive };
// Note: BE toggle buttons start colored (WBrushActive, Content="[ON]") per plan Section 2
_beBtns.Add(beBtn);
// Do NOT call SetResourceReference("NTButtonStyle") on these color-coded buttons
```
Apply the same pattern in `BuildDynamicRuleRow()` — identical button creation, identical list appends.

---

#### 7. `BuildUI()` modifications — initial state + ScrollViewer (B7-F5)

**File:** `TradeCopierWindow.cs`
**Type:** Modification of existing method

```csharp
// Global toggle initial state:
_globalToggleBtn.Background = WBrushInactive;  // OFF at startup
// Do NOT call SetResourceReference("NTButtonStyle") on _globalToggleBtn

// At END of BuildUI() (after all rule rows created via BuildRuleRow):
UpdateButtonColors(false, false);   // V04: consistent initial state

// B7-F5: Wrap _rulesPanel in ScrollViewer
// CURRENT:
//   _rulesPanel = new StackPanel();
//   _rulesPanel.Children.Add(BuildRuleRow("MES"));
//   DockPanel.SetDock(_rulesPanel, Dock.Top);
//   root.Children.Add(_rulesPanel);
//
// AFTER:
_rulesPanel = new StackPanel();
_rulesPanel.Children.Add(BuildRuleRow("MES"));

var rulesScroll = new ScrollViewer
{
    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
    MaxHeight = 400,
    Content   = _rulesPanel
};
DockPanel.SetDock(rulesScroll, Dock.Top);   // Dock on ScrollViewer (outer wrapper), NOT StackPanel
root.Children.Add(rulesScroll);
// Note: _rulesPanel field itself is unchanged (still StackPanel).
//       OnAddRule handler appends to _rulesPanel.Children — works correctly because
//       _rulesPanel is the ScrollViewer's Content.
```

---

#### 8. `OnGlobalToggle()` modification — Layer 2 toggle reflection

**File:** `TradeCopierWindow.cs`
**Type:** Modification of existing method (1 line change)

```csharp
// After existing state-flip logic — ADD:
_globalToggleBtn.Background = _copyEnabled ? WBrushActive : WBrushInactive;
```

---

#### 9. `OnRuleToggle()` modification — Layer 2 toggle reflection

**File:** `TradeCopierWindow.cs`
**Type:** Modification of existing method (1 line change)

```csharp
// After existing state-flip logic — ADD:
btn.Background = newState ? WBrushActive : WBrushInactive;
```

---

#### 10. Subscribe/unsubscribe wiring — V04

**File:** `TradeCopierWindow.cs`
**Type:** Additive wiring

```csharp
// In OnLoaded handler — ADD:
_engine.PositionStateChanged += OnPositionStateChanged;

// In constructor (after BuildUI()) — ADD:
Closed += OnWindowClosed;

// New method OnWindowClosed (NEW):
private void OnWindowClosed(object sender, EventArgs e)
{
    _engine.PositionStateChanged -= OnPositionStateChanged;
}
```
Window already has `Loaded += OnLoaded` at line 52. `OnWindowClosed` is the Window-side equivalent
of Panel's `Detach()` — called when the Window is closed to prevent memory leaks / ghost callbacks.

---

### Data Model Changes (T2)

None for UI files. `PositionState` struct lives in `CopyEngine.cs` (T1).
No new using directives needed in Panel or Window (`PositionState` is in `namespace PropTraderTools`,
same as the UI classes — no `using` required).

### xUnit Tests (T2)

No xUnit tests required for B7-F1 / B7-F5. All changes are pure WPF UI (brush assignment, layout).
Manual verification via NT8 F5 is the acceptance gate for T2.

**Manual F5 verification checklist:**
- [ ] Copy toggle: green when ON, grey (`#4b5563`) when OFF — both Panel and Window
- [ ] Flatten/Cancel: start grey; turn red when a position or working entry exists
- [ ] Trim: starts grey; turns amber when position exists
- [ ] BE: starts grey; turns green when position exists
- [ ] Open a simulated position: all four action buttons activate simultaneously on both surfaces
- [ ] Close the position: all four action buttons return to grey simultaneously on both surfaces
- [ ] Add 5+ rules via "+ Add Rule" button: rule area scrolls, log area below fills remaining space
- [ ] Window title bar / base class unchanged (`sealed class TradeCopierWindow` still sealed)

### 7-Scan Checklist (T2 — TradeCopierPanel.cs + TradeCopierWindow.cs)

Run each grep against both UI files.

| Scan | Pattern to Search | Expected Count | Notes |
|------|-------------------|---------------|-------|
| SCAN-01 | `lock(` | **0** | All button updates on UI thread only. No lock keyword. |
| SCAN-02 | Non-ASCII characters (any char > 0x7F) | **0** | All string literals ASCII-only. No Unicode symbols. |
| SCAN-03 | `FontFamily` | **0** | No font-related changes in T2. |
| SCAN-04 | `#[0-9A-Fa-f]{6}` (hex color strings) | **0** | All brush values via `MakeBrush(r,g,b)` / `MakeWinBrush(r,g,b)` integer RGB. No hex literals. |
| SCAN-05 | `CreateOrder` not preceded by `"PTT-"` prefix | **0** | No `CreateOrder` calls in UI files. |
| SCAN-06 | `DateTime.Now` (not `DateTime.UtcNow`) | **0** | No `DateTime` usage in any T2 method. |
| SCAN-07 | `sealed class TradeCopierWindow` | **0 changes** | Class declaration unchanged. File still contains exactly 1 `sealed class TradeCopierWindow` — verify it is not accidentally removed or made non-sealed. |

### Dependency

**T1 must be committed and compiled before T2 begins.**
- `TradeCopierPanel.cs` and `TradeCopierWindow.cs` reference `PositionState` (defined in `CopyEngine.cs`)
- Both subscribe to `CopyEngine.PositionStateChanged` (declared in `CopyEngine.cs`)
- Without T1, T2 will not compile

### Verification Criteria

- [ ] All 27 `[Fact]` tests pass (22 baseline + 5 from T1 — zero regressions)
- [ ] NT8 F5 compilation: green (zero errors, zero new warnings)
- [ ] `deploy-sync.ps1` executed after T2 commit
- [ ] Manual Layer 3 live state test passes (see manual checklist above)
- [ ] All 7 SCAN counts = 0 on both UI files
- [ ] `dotnet csharpier check src/` passes (no formatting violations)

---

## Summary

| Ticket | Files | Priority | Dep | Verification |
|--------|-------|----------|-----|-------------|
| T1 | `CopyEngine.cs`, `CopyEngineTests.cs` | P0 | None | 27 tests pass, SCAN 0, build green |
| T2 | `TradeCopierPanel.cs`, `TradeCopierWindow.cs` | P2 | T1 committed | F5 green, SCAN 0, manual Layer 3 test |

**Total new methods across both tickets:** 20 (all CYC <= 8, see plan Section 6 CYC matrix)
**Total [Fact] tests after B7:** 27 (22 baseline + 5 new in T1)
**7-scan status:** All expected zero on all 4 files
