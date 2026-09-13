# PTT-BUGS-LIVE-0911 Lane C — Architecture Plan (Cycle 2)

**Status:** PLAN_COMPLETE
**Phase:** 1 (Architecture)
**Author:** ptt-architect
**Epic:** PTT-BUGS-LIVE-0911
**Cycle:** 2 — built on current source, all Cycle 1 violations resolved

---

## 1. Source Investigation — what the code actually does today

All line numbers cite `src/PropTraderTools/CopyEngine.cs` unless noted.

### 1.1 FollowerAtmMode discriminated union (lines 80–113)

```
public abstract class FollowerAtmMode          // line 80 — private base ctor, no subclassing
  public sealed class Inherit : FollowerAtmMode  // line 83 — default: use leader order params
  public sealed class Market  : FollowerAtmMode  // line 88 — force market order
  public sealed class Named   : FollowerAtmMode  // line 93 — native ATM bracket
      string TemplateName { get; }               // line 95
      NinjaTrader.NinjaScript.AtmStrategy AtmObject { get; }  // line 100 — live object preferred
      Named(string templateName)                  // line 102 — string-only ctor
      Named(string templateName, AtmStrategy atmObj)  // line 108 — object overload ctor (B66 hotfix)
```

### 1.2 _cloneAtmObjectByInstr (line 151) — V-02 resolved

```csharp
// Line 151-152
private readonly ConcurrentDictionary<string, NinjaTrader.NinjaScript.AtmStrategy>
    _cloneAtmObjectByInstr =
        new ConcurrentDictionary<string, NinjaTrader.NinjaScript.AtmStrategy>(StringComparer.Ordinal);
```

- Written by: `SetCloneAtmObjectCache(string instrFullName, AtmStrategy atmObj)` (line 740)
- Read by: `GetCloneAtmMode(string instrFullName)` (line 751)
- Companion dict: `_cloneAtmCacheByInstr` (line 145, `ConcurrentDictionary<string, string>`) stores template name string for display.

### 1.3 SetCloneAtmObjectCache (line 740)

```csharp
// Line 740
internal void SetCloneAtmObjectCache(string instrFullName, NinjaTrader.NinjaScript.AtmStrategy atmObj)
{
    if (atmObj != null)                               // branch (1)
        _cloneAtmObjectByInstr[instrFullName] = atmObj;
    else
        _cloneAtmObjectByInstr.TryRemove(instrFullName, out _);
}
```

CYC=2. Called from `TradeCopierPanel.OnCloneModeClick` (TradeCopierPanel.cs line 1860).

### 1.4 GetCloneAtmMode (line 751) — V-02 resolved

```csharp
// Line 751
internal FollowerAtmMode GetCloneAtmMode(string instrFullName)
{
    NinjaTrader.NinjaScript.AtmStrategy atmObj;
    if (_cloneAtmObjectByInstr.TryGetValue(instrFullName, out atmObj) && atmObj != null) // branch (1)
        return new FollowerAtmMode.Named(
            _cloneAtmCacheByInstr.TryGetValue(instrFullName, out var tpl) ? tpl : string.Empty,
            atmObj
        );
    string cache;
    if (_cloneAtmCacheByInstr.TryGetValue(instrFullName, out cache) && cache.Length > 0) // branch (2)
        return new FollowerAtmMode.Named(cache);
    return new FollowerAtmMode.Inherit();
}
```

CYC=4. Never returns null (JS-002). Falls back to Inherit if no ATM object or string found.

### 1.5 ResolveAtmMode (line 5183)

```csharp
// Line 5183
private FollowerAtmMode ResolveAtmMode(CopyRule rule, string accountName, string instrFullName)
{
    if (GetCopyMode() == CopyMode.Clone)   // branch (1)
        return GetCloneAtmMode(instrFullName);
    return GetAtmMode(rule, accountName);
}
```

CYC=2. Clone mode -> `GetCloneAtmMode` (per-instrument from _cloneAtmObjectByInstr).
Signal/Mirror mode -> `GetAtmMode(rule, accountName)` (from `CopyRule.FollowerAtmTemplates` dict).

### 1.6 OnOrderUpdate (line 1642) — entry point for ALL copy dispatch

```csharp
// Line 1642
private void OnOrderUpdate(object sender, OrderEventArgs e)
```

Subscribed at: `acc.OrderUpdate += OnOrderUpdate` (line 1601), in `Subscribe()` (line 1597), foreach over `Account.All`.

**Pre-gate block** (unconditional, before rule gates):
- EvictDedup (line 1645)
- TryLogDragTrace (line 1646)
- TryFireFollowerBeDisarm (line 1650)
- TryFireFollowerBeRetry (line 1654)
- TryEvictFollowerBeSlot (line 1657)
- TrySweptPttDragOrphans (line 1660)
- TryRecordBeTargetFill (line 1670)
- TryTriggerBeRecovery (line 1671)
- TryCleanupReArmedAtmBracket (line 1676)
- **TryReplaceOnAtmCancel** (line 1680) — fires ReplaceFollowerCopyOnAtmCancel for "PTT-Copy"/"Entry" Cancelled orders
- TryNakedDetect (line 1683)
- HandleDrainTerminalState (line 1688)
- TryDrainWatchdog (line 1692)

**Gate block** (post-TryResolveEnabledRule):
- TryResolveEnabledRule (line 1695): enabled + rule match + rule enabled. Returns if any fails.
- TryFirePositionState (line 1699)
- TryMirrorOrderUpdate (line 1702)
- TryCancelFollowerEntries (line 1706): returns true (+ return) if leader order Cancelled
- TryDispatchLeaderFlat (line 1710): returns true (+ return) if leader went flat
- TryHandleDrag (line 1725): returns true (+ return) if bracket drag or entry drag detected

**Single entry copy dispatch call:**
```csharp
// Line 1729 (ONLY call site for entry copy dispatch in the entire file)
DispatchCopy(e.Order, matchedRule);
```

### 1.7 DispatchCopy (line 2561)

```csharp
// Line 2561
private void DispatchCopy(Order order, CopyRule rule)
```

ONLY called from OnOrderUpdate line 1729. **Two total references in the file:** line 1729 (call) and line 2561 (definition).

Gate chain:
- **Gate 0.5** (line 2565): `IsExitSignalNameOrAnonClose(order.Name, order.OrderType)` — blocks PTT-* cascade AND known NT8 exit signal names
- **Gate 3** (line 2578): `IsDispatchTriggerState(order.OrderState, order.OrderType)` — Submitted (market) or Accepted (limit AddOn)
- **Gate 4** (line 2591): `IsDispatchableOrderType(order.OrderType)` — market or limit only
- **Gate 5** (line 2608): `IsLiveEntryBlocked_Check(instrKey, orderId, order.LimitPrice)` — price-keyed dedup

After all gates:
```csharp
// Lines 2619-2661
var baseSignal = CopySignal.Create(order.OrderAction, order.OrderType, order.Quantity, order.LimitPrice, orderId);
int baseQty = ResolveBaseQty(order.Instrument, baseSignal.Quantity);  // line 2629
// Line 2630: gap — Lane D wiring point (see Section 4)
// B119: snapshot instrument and last direction
int dispatched = 0;
foreach (var acc in rule.FollowerAccounts)
{
    if (ShouldSkipFollower(...)) { idx++; continue; }
    DispatchToFollower(acc, order, rule, idx, baseSignal, baseQty);  // line 2651
    idx++; dispatched++;
}
if (dispatched > 0) SetLiveEntryDispatched(instrKey, orderId);  // dedup commit
_lastLeaderDirection[instr.FullName] = currentAction;           // direction tracking
```

Current CYC=8 (at limit). Zero branch budget remaining.

### 1.8 DispatchToFollower (line 2760) — V-01 / V-04 resolved

```csharp
// Line 2760
private void DispatchToFollower(
    Account acc,
    Order order,
    CopyRule rule,
    int idx,
    CopySignal baseSignal,
    int baseQty
)
{
    int mult = GetMultiplier(rule, idx);
    var scaledSignal = CopySignal.Create(...baseQty * mult...);
    var mode = ResolveAtmMode(rule, acc.Name, order.Instrument.FullName);
    // diagnostic log (lines 2778-2793)
    if (mode is FollowerAtmMode.Named namedAtm)   // line 2795 — Named ONLY goes to SendCopyWithAtm
        SendCopyWithAtm(acc, order.Instrument, in scaledSignal, namedAtm);
    else                                           // line 2798 — Inherit/Market goes to SendCopy
        SendCopy(acc, order.Instrument, in scaledSignal, mode);
}
```

CYC<=3. **Named mode NEVER reaches SendCopy.** SendCopy NEVER sees Named mode. V-04 resolved.

### 1.9 SendCopy (line 970 in stale index; body confirmed at current ~5060-5113)

```csharp
private bool SendCopy(Account follower, Instrument instrument, in CopySignal signal, FollowerAtmMode mode)
```

Handles Inherit and Market modes only (by design — Named routes to SendCopyWithAtm). Uses:
```csharp
var order = follower.CreateOrder(instrument, signal.Action, orderType, OrderEntry.Manual,
    TimeInForce.Gtc, signal.Quantity, limitPrice, 0, null,
    "PTT-Copy",   // signal name — PTT- prefix invariant
    DateTime.MaxValue, (NinjaTrader.Cbi.CustomOrder)null);
if (order != null)
    follower.Submit(new[] { order });
```

### 1.10 SendCopyWithAtm (line 5123) — V-01 / V-03 resolved

```csharp
// Line 5123
private bool SendCopyWithAtm(
    Account follower,
    Instrument instrument,
    in CopySignal signal,
    FollowerAtmMode.Named namedMode
)
{
    try {
        var order = follower.CreateOrder(
            instrument, signal.Action, OrderType.Limit, OrderEntry.Manual, TimeInForce.Gtc,
            signal.Quantity, signal.LimitPrice, 0, string.Empty,
            "Entry",    // line 5142: MUST be "Entry" for StartAtmStrategy to arm brackets
            DateTime.MaxValue, (NinjaTrader.Cbi.CustomOrder)null
        );
        if (order == null) return false;            // branch (2)
        if (namedMode.AtmObject != null)            // branch (3) — object overload preferred
            NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(namedMode.AtmObject, order); // line 5149 STATIC
        else                                        // branch (4) — string fallback
            NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(namedMode.TemplateName, order); // line 5151 STATIC
        RecordFollowerCopy(signal.OrderId, order);
        StatusUpdate?.Invoke(follower.Name + ": PTT-ATM entry @ " + ...);
        return true;
    }
    catch (Exception ex) { StatusUpdate?.Invoke("PTT-ATM error: " + ex.Message); return false; }
}
```

CYC=4. **STATIC call confirmed at lines 5149 and 5151.** V-03 resolved.
Called from: DispatchToFollower line 2796, ReplaceFollowerCopyOnAtmCancel line 4522.

### 1.11 ReplaceFollowerCopyOnAtmCancel (line 4502)

```csharp
// Line 4502
private void ReplaceFollowerCopyOnAtmCancel(Order cancelledOrder)
```

Called from TryReplaceOnAtmCancel (line 915) which is called from pre-Gate-1 block (line 1680) in OnOrderUpdate.
Named mode -> SendCopyWithAtm (line 4522); other modes -> SendCopy (line 4524).
This is the cancel/replace flow — separate from the entry fill path.

### 1.12 OnCloneModeClick (TradeCopierPanel.cs line 1848)

```csharp
// TradeCopierPanel.cs line 1848
private void OnCloneModeClick(object sender, RoutedEventArgs e)
{
    CopyEngine.Instance.SetCopyMode(CopyMode.Clone);
    string instrKey = _instrument?.FullName ?? string.Empty;
    NinjaTrader.NinjaScript.AtmStrategy atmObj = null;
    if (_currentChart != null)
        atmObj = TradeCopierAddOn.FindVisualChild<ChartTrader>(_currentChart)?.AtmStrategy;
    CopyEngine.Instance.SetCloneAtmObjectCache(instrKey, atmObj);  // live object -> _cloneAtmObjectByInstr
    string tpl = GetLeaderAtmTemplateName(_currentChart);           // string for display
    CopyEngine.Instance.SetCloneAtmCache(instrKey, tpl);            // string -> _cloneAtmCacheByInstr
    UpdateAtmComboVisibility(Visibility.Collapsed);
}
```

CYC=2. Runs on WPF UI thread (RoutedEventHandler). Captures AtmStrategy live object at click time.
Writes to _cloneAtmObjectByInstr and _cloneAtmCacheByInstr via ConcurrentDictionary (thread-safe).

---

## 2. Lane-Split Gate Result

| Question | Finding |
|---|---|
| **Q1. Same method or within 50 lines?** | YES. Both the "Push/OnOrderUpdate is correct" finding and the "Lane D wiring point" are about `OnOrderUpdate` (line 1642) and `DispatchCopy` (line 2561) — the same dispatch chain, within 88 lines of each other. |
| **Q2. Fix B depends on Fix A final design?** | YES. The Lane D wiring point is predicated on first confirming that OnOrderUpdate is the single, correct entry (Fix A finding). Without Fix A, Lane D has no determined home. |
| **Q3. Each fix has standalone value if other blocked?** | Fix A (finding that OnOrderUpdate is correct) has standalone value as documentation. Fix B alone without Fix A is meaningless — it requires the A finding to be settled. |
| **Q4. Independent SIM verification paths?** | YES — A verifiable by code review; B verifiable by adding diagnostic log at wiring point and confirming it fires once per master entry in SIM. |

**RESULT: SINGLE-PIPELINE** — Fix A and Fix B are sequential and dependent. Do not split into separate lanes.

---

## 3. Push Entry Dispatch Design

### Finding: OnOrderUpdate is the correct single entry point. No Push path should be added.

**Why a Push path is architecturally impossible before OnOrderUpdate:**

NT8's `Order` object is created by NT8 when the trader (or strategy) submits an order. The `OrderId`, `LimitPrice`, `OrderAction`, `OrderType`, and final `OrderState` (Submitted/Accepted) are all properties that only exist on the `Order` object *after* NT8 has processed the submission. There is no NT8 AddOnBase hook that fires before `Account.OrderUpdate` with a valid `Order` object for a just-submitted entry.

**Why Initialized state is NOT a viable earlier hook:**
- `IsDispatchTriggerState` explicitly rejects `OrderState.Initialized` — it only allows `Submitted` (market) and `Accepted` (AddOn limit). This is intentional: Initialized does not mean the order has been confirmed by the exchange or the sim engine.
- Firing dispatch on Initialized would produce duplicate dispatch attempts (Submitted event arrives immediately after Initialized for market orders). The dedup guard in Gate 5 would block the second attempt, but this wastes cycles and violates the minimal-change principle.

**Why there is no external "PushCopy" API today:**
- `grep "Push|PushEntry|PushCopy|TriggerCopy|ForceCopy" src/PropTraderTools/CopyEngine.cs` — zero matches.
- `DispatchCopy` has exactly 2 references in the file: definition (line 2561) and single call site (OnOrderUpdate line 1729). It is not exposed publicly or internally as a Push API.

**Conclusion:** `OnOrderUpdate -> DispatchCopy(e.Order, matchedRule)` at line 1729 is the **architecturally correct, deterministic, and necessary single entry point** for entry copy dispatch. No Push design should be introduced.

The existing gate chain is already deterministic:
1. Gate 0.5 blocks cascade and exit signals
2. Gate 3 ensures only Submitted/Accepted states fire (exact trigger states, no noise)
3. Gate 4 ensures only market/limit order types fire
4. Gate 5 provides price-keyed dedup (no double-fire)
5. The post-loop `SetLiveEntryDispatched` commits the dedup key only if at least one follower received the dispatch (no phantom lock)

---

## 4. Lane D Wiring Point

**Exact location:**

| Field | Value |
|---|---|
| File | `src/PropTraderTools/CopyEngine.cs` |
| Method | `DispatchCopy(Order order, CopyRule rule)` |
| Method start line | 2561 |
| **Wiring line** | **Between line 2629 and line 2631** |
| Context | After all 5 gates have passed, after `ResolveBaseQty`, before the B119 instrument/direction snapshot |

**Exact current code at wiring location:**

```csharp
// Line 2629
int baseQty = ResolveBaseQty(order.Instrument, baseSignal.Quantity);

// << Lane D wiring point >> -- new unconditional void call inserted here
// e.g.: TryLaneD_OnEntryGatesPassed(order, rule, baseSignal, baseQty);

// Line 2631 (B119 comment)
// B119: DW-B128 -- snapshot instrument and last direction once before the loop.
OrderAction currentAction = order.OrderAction;
```

**Why this location is correct:**
- All 5 gates have passed: the order is a confirmed, non-cascade, trigger-state, dedup-clear entry signal from a matched master account.
- `baseSignal` and `baseQty` are resolved — Lane D has full signal context.
- CYC impact: an unconditional void call costs **0 branches** in the parent method. DispatchCopy is currently at CYC=8 (limit). Adding an unconditional call does not increase CYC.
- Threading: still on NT8 account background thread. No Dispatcher.InvokeAsync needed for a sync hook.
- The wiring is **before** the follower loop, so Lane D sees the leader order once, not N times per follower.

**What Lane D can wire here:**
- Diagnostic logging hook (print leader order details to Output tab)
- Latency measurement (record timestamp before follower loop begins)
- Pre-flight validation (e.g., check follower position state before dispatching)
- A future "delay dispatch" mechanism (queue the (order, rule) for deferred processing — requires ConcurrentQueue, not lock)

**Method signature for the Lane D hook method (when implemented):**
```csharp
// CYC budget: <= 8. JS-001: no throw. JS-021: no lock.
private void TryLaneDHook(Order order, CopyRule rule, CopySignal baseSignal, int baseQty)
{
    // implementation TBD by Lane D ticket
}
```

---

## 5. Jane Street / NT8 Constraint Checklist

| Rule | Status | Evidence |
|---|---|---|
| **JS-021 (P0)**: No lock/Monitor/Mutex/SemaphoreSlim | PASS | `_cloneAtmObjectByInstr` is `ConcurrentDictionary` (line 151). All dispatch path methods use `ConcurrentDictionary` or `volatile`. Zero `lock(` in reviewed dispatch chain. |
| **JS-001 (P0)**: No throw in dispatch/gate/event-handler | PASS | `SendCopyWithAtm` try/catch logs and returns false (line 5162). `SendCopy` try/catch logs and returns false. `ReplaceFollowerCopyOnAtmCancel` no throw. `DispatchCopy` no throw. |
| **JS-002 (P0)**: No unexpected null return | PASS | `GetCloneAtmMode` returns `Inherit` as fallback (never null, line 762). `ResolveAtmMode` never returns null. All predicates return bool. |
| **JS-025**: ConcurrentDictionary for shared state | PASS | `_cloneAtmObjectByInstr`, `_cloneAtmCacheByInstr`, `_dedupCache`, `_lastLeaderDirection` all `ConcurrentDictionary`. |
| **CYC <= 8 per method (P1)** | PASS | `DispatchCopy` CYC=8 (at limit, no room). `DispatchToFollower` CCN<=3. `SendCopyWithAtm` CYC=4. `GetCloneAtmMode` CYC=4. Any Lane D method must target CYC<=8. |
| **ASCII-only** | PASS | All reviewed string literals and comments are ASCII-only. |
| **No async/await in NT8 lifecycle/event handlers** | PASS | `OnOrderUpdate` and all dispatch-chain methods are synchronous. |
| **NT8: StartAtmStrategy is STATIC** | PASS | Lines 5149, 5151 confirm `NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(...)` — static call. |
| **NT8: Order name "Entry" for ATM arming** | PASS | Line 5142: `"Entry"` is the hardcoded signal name in `SendCopyWithAtm`. |
| **NT8: AtmStrategyCreate() banned in AddOnBase** | PASS | Zero occurrences in `CopyEngine.cs`. |
| **NT8: AtmStrategyChangeStopTarget() banned in AddOnBase** | PASS | Zero occurrences in `CopyEngine.cs`. |
| **NT8: Account.Change() for bracket sync (not ATM arming)** | PASS | `Account.Change()` used only in `SyncFollowerBracket`. Not in ATM path. |
| **"PTT-" prefix on all entry order names** | PASS | `SendCopy` uses `"PTT-Copy"` (signalName). `SendCopyWithAtm` uses `"Entry"` — required by NT8 for ATM arming; PTT prefix not applicable for the `"Entry"` name (NT8 constraint supersedes). |
| **No DateTime.Now** | PASS | Not observed in reviewed dispatch chain. `DateTime.MaxValue` used in CreateOrder args (correct). |

---

## 6. Tickets Required

### T1 — Lane C Finding Documentation + Lane D Wiring Point Addition

**Type:** Documentation + minimal code addition (zero behavior change)
**Files:** `src/PropTraderTools/CopyEngine.cs`, `docs/brain/PTT-BUGS-LIVE-0911/02-architecture-plan-lane-c.md` (this file)

**Spec requirement IDs satisfied:**
- Lane C mission items 1, 2, 3, 4 (all four are addressed by this plan)

**What the code ticket does (minimal):**
Add a single comment block at the Lane D wiring point in `DispatchCopy` (lines 2629-2631) that:
1. Documents the wiring point explicitly in source
2. Adds a no-op stub `TryLaneDHook(order, rule, baseSignal, baseQty)` method with empty body and correct JS rule annotations, called unconditionally from `DispatchCopy` at the wiring location

This is the minimum required to establish the wiring contract for Lane D without changing any behavior.

**Method signatures:**

```csharp
// In DispatchCopy, after line 2629 (ResolveBaseQty):
TryLaneDHook(order, rule, baseSignal, baseQty);  // unconditional void call -- 0 CYC cost

// New method (stub for Lane D):
// Lane D wiring stub. CYC=1 (base only -- empty body). JS-001: no throw. JS-021: no lock.
// PTT-BUGS-LIVE-0911 Lane D: implement here.
private void TryLaneDHook(Order order, CopyRule rule, CopySignal baseSignal, int baseQty)
{
    // Lane D implementation slot -- body added by Lane D ticket.
}
```

**xUnit test:**
```csharp
[Fact]
public void TryLaneDHook_MethodExists_WithCorrectSignature()
// Asserts: TryLaneDHook is private, has 4 parameters (Order, CopyRule, CopySignal, int),
//          returns void, and does not throw when called with a null Order.
```

**7-scan checklist (SCAN-01 through SCAN-07):**
- SCAN-01 (lock scan): `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` — zero matches required.
- SCAN-02 (throw scan): no `throw` statements in `TryLaneDHook` body or its callers in the dispatch path.
- SCAN-03 (null return): `TryLaneDHook` returns void — JS-002 N/A. Callers that call it do not expect a return value.
- SCAN-04 (CYC): `DispatchCopy` CYC after adding unconditional void call = still 8 (no branch added). `TryLaneDHook` CYC=1 (empty body). Verify with Lizard.
- SCAN-05 (PTT- prefix): `TryLaneDHook` adds no order names or string literals. N/A.
- SCAN-06 (DateTime): `TryLaneDHook` uses no DateTime. N/A.
- SCAN-07 (ASCII): Comment and method name `TryLaneDHook` are ASCII-only. Verify no Unicode in stub.

---

## 7. Open Questions / Deferred Items

### 7.1 What does Lane D actually implement?

Lane C's charter is to identify the wiring point and confirm OnOrderUpdate is the correct entry. Lane D's charter (not defined yet) would specify the actual diagnostic, delay, or pre-flight logic to wire at that point. Lane C produces no opinion on what Lane D does — only where it hooks.

### 7.2 DispatchCopy CYC=8 headroom

`DispatchCopy` is at CYC=8 (limit). The Lane D unconditional void call costs 0 branches. However, if Lane D needs conditional logic (e.g., "only fire hook if flag enabled"), that condition MUST live inside `TryLaneDHook` (keeping parent at CYC=8), not in the calling site in `DispatchCopy`.

### 7.3 Named-mode order type in SendCopyWithAtm

`SendCopyWithAtm` hardcodes `OrderType.Limit` regardless of the leader's order type (line 5135). If the leader places a market order in Named/Clone mode, the follower's entry is still placed as a Limit order at the leader's current limit price (0 for market orders — this may produce unexpected behavior). This is a pre-existing design decision; Lane C does not resolve it. Flag for a separate investigation.

### 7.4 _cloneAtmCacheByInstr vs _cloneAtmObjectByInstr consistency

If `SetCloneAtmObjectCache` is called with a non-null `atmObj` but `SetCloneAtmCache` is not called (or vice versa), the two dicts can be inconsistent. Currently `OnCloneModeClick` calls both in sequence. A race is not possible (both calls are on UI thread). However, if `SetCloneAtmObjectCache` is called from any non-UI path in the future, this consistency must be re-examined. Lane D should not split these calls.

### 7.5 Index staleness confirmed

The `jcodemunch-mcp` index (`indexed_at: 2026-08-10`) is stale vs. current source. It does not contain `DispatchToFollower`, `SendCopyWithAtm`, `ReplaceFollowerCopyOnAtmCancel`, or the per-instrument `GetCloneAtmMode(instrFullName)` signature. All architectural claims in this plan are sourced from direct `read_file` + `grep` reads of the current file, not from the index. Re-index is needed before any jcodemunch symbol search is trusted.

---

*Return: PLAN_COMPLETE*
