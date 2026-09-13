# PTT-BUGS-LIVE-0911 Lane E — Architecture Plan
## Re-arm NT8 ATM Bracket Protection After Partial QX/BE Exit

**Status**: PLAN_COMPLETE (Cycle 3)
**Phase**: 1 (Architecture)  
**Author**: ptt-architect  
**Date**: 2026-09-11  
**File scope**: `src/PropTraderTools/CopyEngine.cs` + `src/PropTraderTools/CopyEngineTests.cs`

---

## 1. Executive Summary

### What Changes
- **New field** `_laneEDispatched` (`ConcurrentDictionary<string, byte>`) in `CopyEngine` — dedup guard for Lane E re-arm events.
- **Modified method** `OnFollowerPositionUpdate` (line ~1020) — one new void call `TryLaneEHook(acc, e.Position)` appended after `TrySetFollowerSlateReady`. Zero CYC impact.
- **New method** `TryLaneEHook` — entry-point hook called from `OnFollowerPositionUpdate`. Calls `IsLaneEEligible` as first guard (absorbs all compound boolean checks). After eligibility confirmed: resolves `namedMode`, checks dedup, delegates to `TryLaneEDispatchFollower`, opens gate on success.
- **New private helper** `IsLaneEEligible(Account acc, NinjaTrader.Cbi.Position pos)` — pure eligibility predicate extracted from `TryLaneEHook`. Contains ALL compound guard logic: Clone mode check, null checks, qty>0, gate-closed check, ATM template check. Absorbs all `||` operators so `TryLaneEHook` never sees them. CYC=8.
- **New method** `TryLaneEDispatchFollower` — per-follower ATM dispatch helper. Builds `CopySignal` from the follower's current position (quantity = `pos.Quantity`, price = `pos.AveragePrice`). Calls `SendCopyWithAtm`. Commits dedup.
- **Modified method** `CancelQxBrackets` (both 2-param and 3-param overloads) — adds a single `_laneEDispatched.TryRemove(BuildLaneEKey(acc, instr), out _)` call per account to reset the dedup for the next QX/BE cycle. Zero CYC impact on callers (helper call, not a branch). The null-coalescing logic for `instr` is isolated inside `BuildLaneEKey`.
- **New private static helper** `BuildLaneEKey(Account acc, Instrument instr)` — constructs the Lane E dedup key string. CYC=2. Isolated here so `CancelQxBrackets` incurs +0 CYC.

### What Does NOT Change
- `TrySetFollowerSlateReady` — logic unchanged, signature unchanged.
- `TryMarkFollowerSlateNotReady` — logic unchanged, signature unchanged.
- `SendCopyWithAtm` — reused as-is.
- `_followerSlateReady` semantics — Lane E reads and writes using the identical ConcurrentDictionary indexer pattern already used by `TrySetFollowerSlateReady`.
- All non-Clone mode behavior — `TryLaneEHook` is a no-op for Signal/Mirror/Market modes.
- All tests in `CopyEngineTests.cs` — no existing test is modified; new tests are additive.

### What the User Sees
After a partial QX or BE exit that leaves a remaining position on the follower with no ATM brackets:
- The follower's remaining position is automatically protected by a fresh ATM bracket.
- The bracket uses the same Clone ATM template already configured (same stop/target distances).
- The bracket is placed without user interaction, within the same `OnFollowerPositionUpdate` event cycle that detects the remaining position.
- No duplicate brackets: the dedup guard prevents re-arming on subsequent position events for the same slot.
- No regressions for non-Clone modes, full-exit (flat) scenarios, or first-session entries.

---

## 2. LANE-SPLIT GATE

| Question | Answer | Rationale |
|---|---|---|
| Q1. Same method or within 50 lines? | YES | All logic is in `OnFollowerPositionUpdate` + two extracted helpers (< 50 lines from wiring point). |
| Q2. Fix B design depends on Fix A final design? | YES | There is only one fix in Lane E. |
| Q3. Each fix has standalone value if the other is blocked? | N/A | Single fix. |
| Q4. Each fix has an independent SIM verification path? | N/A | Single fix. |

**LANE-SPLIT GATE RESULT: SINGLE-PIPELINE**

Rationale: Q1 and Q2 are YES → Lane split is not warranted. Lane E is a single, coherent feature with a single wiring point, single state machine, and single SIM verification path.

---

## 3. Source Analysis

### 3.1 `OnFollowerPositionUpdate` (current, line 1020–1027)

```csharp
// CYC<=3: e/Position/Account null(1) + IsFollowerAccount(2) + TrySetFollowerSlateReady(0).
// JS-021: no lock. JS-001: no throw. JS-002: void. ASCII-only. No async/await.
private void OnFollowerPositionUpdate(object sender, PositionEventArgs e)
{
    if (e?.Position?.Account == null)           // (1)
        return;
    if (!IsFollowerAccount(e.Position.Account)) // (2)
        return;
    TrySetFollowerSlateReady(e.Position.Account);
}
```

**Key observations:**
- CYC = 3 (entry + 2 branches; void call = 0 CYC in McCabe).
- Called on NT8 account background thread (confirmed line 1013–1016 comment).
- Already has the account and the `PositionEventArgs e` — both needed by Lane E.

### 3.2 `TrySetFollowerSlateReady` (current, line 966–977)

```csharp
// CYC<=4: acc null(1) + IsFollowerAccount(2) + !IsAccountFlat(3) + HasAnyActiveOrders(4).
private void TrySetFollowerSlateReady(Account acc)
{
    if (acc == null)                    // (1)
        return;
    if (!IsFollowerAccount(acc))        // (2)
        return;
    if (!IsAccountFlat(acc))            // (3)   <-- returns early when follower has remaining position
        return;
    if (HasAnyActiveOrders(acc))        // (4)
        return;
    _followerSlateReady[acc.Name] = true;
}
```

**Key observation:** Branch (3) `!IsAccountFlat(acc)` causes early return when the follower holds a remaining position. This is the DESIGN GAP Lane E closes: `TrySetFollowerSlateReady` never opens the gate, so the follower is permanently unprotected. Lane E hooks after this call.

### 3.3 `TryMarkFollowerSlateNotReady` (current, line 951–957)

```csharp
// CYC<=2: acc null(1) + IsFollowerAccount(2).
private void TryMarkFollowerSlateNotReady(Account acc)
{
    if (acc == null)                    // (1)
        return;
    if (!IsFollowerAccount(acc))        // (2)
        return;
    _followerSlateReady[acc.Name] = false;
}
```

**Key observation:** Called by `CancelQxBrackets` (both overloads). Lane E does NOT modify this method. Instead, Lane E resets `_laneEDispatched` inside `CancelQxBrackets` itself so the dedup is cleared when a new QX/BE cycle begins.

### 3.4 `SendCopyWithAtm` (current, line 5221–5265)

```csharp
// CYC=4: (1) try/catch outer, (2) order null guard, (3) AtmObject branch, (4) string branch.
private bool SendCopyWithAtm(
    Account follower,
    Instrument instrument,
    in CopySignal signal,
    FollowerAtmMode.Named namedMode)
{
    try {
        var order = follower.CreateOrder(
            instrument, signal.Action, OrderType.Limit, OrderEntry.Manual,
            TimeInForce.Gtc, signal.Quantity, signal.LimitPrice, 0,
            string.Empty, "Entry",   // NT8: MUST be "Entry"
            DateTime.MaxValue, (NinjaTrader.Cbi.CustomOrder)null);
        if (order == null) return false;
        if (namedMode.AtmObject != null)
            NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(namedMode.AtmObject, order);
        else
            NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(namedMode.TemplateName, order);
        RecordFollowerCopy(signal.OrderId, order);
        StatusUpdate?.Invoke(follower.Name + ": PTT-ATM entry @ " + signal.LimitPrice + " atm=" + namedMode.TemplateName);
        return true;
    }
    catch (Exception ex) {
        StatusUpdate?.Invoke("PTT-ATM error: " + ex.Message);
        return false;
    }
}
```

**Key observations:**
- Signal name hardcoded to "Entry" — required by NT8 for `StartAtmStrategy`.
- Order type hardcoded to `OrderType.Limit`.
- `StartAtmStrategy` is the static AddOnBase-available form — confirmed in mission brief.
- Lane E reuses this method unchanged.

### 3.5 `TryLaneDHook` pattern (current, line 2679–2713)

```csharp
// CYC=6: (1) mode!=Clone, (2) type!=Limit, (3) !Named, (4) dispatched in loop, (5) laneDCount>0.
private void TryLaneDHook(Order order, CopyRule rule, CopySignal baseSignal, int baseQty)
{
    if (GetCopyMode() != CopyMode.Clone) return;         // (1) non-Clone no-op
    if (order.OrderType != OrderType.Limit) return;      // (2) Market skip
    FollowerAtmMode cloneAtmMode = GetCloneAtmMode(order.Instrument.FullName);
    if (!(cloneAtmMode is FollowerAtmMode.Named namedMode)) return; // (3) no template
    int laneDCount = 0;
    int idx = 0;
    foreach (Account acc in rule.FollowerAccounts)
    {
        if (TryLaneDDispatchFollower(acc, rule, idx, order, namedMode, baseSignal, baseQty))
            laneDCount++;                                 // (4)
        idx++;
    }
    if (laneDCount > 0)
    {
        string instrKey = order.Instrument.FullName + "|" + order.OrderAction.ToString();
        SetLiveEntryDispatched(instrKey, order.OrderId.ToString());
    }                                                     // (5)
}
```

**Key observation:** Lane E's `TryLaneEHook` follows this exact structural pattern with three differences:
1. Called from `OnFollowerPositionUpdate` (not `DispatchCopy`) — receives `Account acc` + `Position pos` (not `Order + CopyRule`).
2. No `order.OrderType` check (Lane E derives signal from position, not an incoming order).
3. No `SetLiveEntryDispatched` (no orderId available; dedup is handled by `_laneEDispatched` instead).

### 3.6 `_laneDDispatched` field declaration (line 406–411)

```csharp
// Lane D: dispatch record for TryLaneDHook-handled ATM followers (double-dispatch guard).
// Key: orderId + "#" + accName (ASCII-only). Value: 0 (unused placeholder).
// ConcurrentDictionary: no lock (JS-021). TryAdd: atomic. ContainsKey: lock-free O(1).
// Growth: O(followers * dispatches/session). ~60 bytes/entry. No cleanup required per session.
private readonly ConcurrentDictionary<string, byte> _laneDDispatched =
    new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
```

**Lane E analog:** `_laneEDispatched` uses identical declaration pattern. Key format: `acc.Name + "#LANE-E#" + instr.FullName`. Cleanup: `TryRemove` in `CancelQxBrackets` when the next QX/BE cycle begins.

---

## 4. Architecture Decision — Trigger Condition (Q1)

**Question:** `OnFollowerPositionUpdate` fires on every position change, including fills that INCREASE position. How does Lane E distinguish "post-QX/BE clean-slate → re-entered remaining position" from spurious fires?

**Answer — Three-Guard Sequence:**

| Guard | Check | Rationale |
|---|---|---|
| Guard A | `GetCopyMode() != CopyMode.Clone → return` | Non-Clone no-op. Zero behavior change for Signal/Mirror/Market. |
| Guard B | `pos.Quantity <= 0 → return` | No remaining position (flat or null event) — not a re-arm trigger. |
| Guard C | `_followerSlateReady.TryGetValue → ready=true → return` | Gate is already open — either first session entry or bracket-sync was never disrupted. Not a post-QX/BE event. |
| Guard D | ATM template check: `GetCloneAtmMode(instrFullName)` not `Named` → return | No ATM template configured — nothing to arm. |
| Guard E | `_laneEDispatched.ContainsKey(key) → return` | Already dispatched for this account+instrument slot — dedup prevents double-arm. |

**Precise trigger state:** `Clone mode` AND `pos.Quantity > 0` AND `gate=false` AND `Named template exists` AND `not yet dispatched`.

This state combination exclusively identifies the post-QX/BE remaining-position scenario because:
- Normal post-flat events: `pos.Quantity = 0` → Guard B fires.
- Normal first-entry events: gate is absent (treated as ready by `TryGetValue` miss) → Guard C fires.
- Post-QX/BE events: gate explicitly set to `false` by `CancelQxBrackets` → passes Guard C.

---

## 5. Architecture Decision — Position Quantity Source (Q2)

**Question:** Does `e.Position.Quantity` give the current remaining quantity reliably at the point `OnFollowerPositionUpdate` fires?

**Answer: Yes.** `PositionUpdate` is an NT8 AddOnBase account event that fires AFTER the position object is updated. The `e.Position` object is a live snapshot reflecting the current state at event-fire time. This is confirmed by the existing comment at `SubmitBeStop` (line 1336): "Changes to positions will not be reflected till at least the next OnBarUpdate() after an order fill" — this restriction applies only to `StrategyBase.Position` polling, NOT to `Account.PositionUpdate` event handlers in AddOnBase.

**Correct source:** `e.Position.Quantity` passed as `pos.Quantity` in `TryLaneEHook` → `TryLaneEDispatchFollower`.

---

## 6. Architecture Decision — ATM Template / Quantity (Q3)

**Question:** The ATM template must match the follower's current remaining quantity, not the leader's original entry size. How is the quantity determined?

**Answer:** `pos.Quantity` (from `e.Position.Quantity`) is used directly as the `CopySignal.Quantity`. This is the follower's current remaining position size — it already reflects any multiplier scaling applied at original entry time. No further multiplier is applied at re-arm time.

**ATM template resolution:** `GetCloneAtmMode(pos.Instrument.FullName)` — the engine-level per-instrument Clone ATM mode. This is the same ATM template used by `TryLaneDHook`. The template defines stop/target DISTANCES (in ticks), which are independent of position size.

**LimitPrice:** `pos.AveragePrice` — the follower's existing position average price. This is the correct anchor for the ATM bracket (same price as original entry).

**CopySignal construction:**
```csharp
OrderAction action = pos.MarketPosition == MarketPosition.Long
    ? OrderAction.Buy
    : OrderAction.SellShort;
string syntheticId = "LANE-E#" + acc.Name + "#" + pos.Instrument.FullName;
CopySignal sig = CopySignal.Create(action, OrderType.Limit, pos.Quantity, pos.AveragePrice, syntheticId);
```

---

## 7. Architecture Decision — Dedup Field Design (Q4)

**Question:** What is the dedup key? What is the `ConcurrentDictionary` field?

**Answer:**

**New field:**
```csharp
// Lane E: re-arm dedup guard. Prevents double-arm on multiple position events for the same
// post-QX/BE slot. Key: accName + "#LANE-E#" + instrFullName (ASCII-only, no lock JS-021).
// TryAdd: atomic on success. TryRemove: called from CancelQxBrackets to reset for next cycle.
// Growth: O(followers * instruments). ~60 bytes/entry. Cleaned up per QX/BE cycle.
private readonly ConcurrentDictionary<string, byte> _laneEDispatched =
    new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
```

**Key format:** `acc.Name + "#LANE-E#" + instr.FullName`

Separator `#LANE-E#` is ASCII-only. NT8 account names cannot contain `#`. Instrument full names contain spaces and contract month codes but not `#LANE-E#`. This key is unique per (account, instrument) pair.

**Write (commit):** `_laneEDispatched.TryAdd(key, 0)` in `TryLaneEDispatchFollower` on successful `SendCopyWithAtm`.

**Clear (reset for next cycle):** `_laneEDispatched.TryRemove(BuildLaneEKey(acc, instr), out _)` in both `CancelQxBrackets` overloads, immediately after `TryMarkFollowerSlateNotReady`. This ensures the dedup is reset at the start of each QX/BE cycle so that the NEXT position event for the same account+instrument can re-arm again. The null-coalescing guard for `instr` lives inside `BuildLaneEKey` — zero CYC delta on the callers.

**Why TryRemove in CancelQxBrackets (not TryMarkFollowerSlateNotReady):** `CancelQxBrackets` has both `acc` AND `instr` in scope, which are both needed for the key. `TryMarkFollowerSlateNotReady` only has `acc` — the instrument is unavailable there. Adding TryRemove to `CancelQxBrackets` keeps `TryMarkFollowerSlateNotReady` semantically pure.

---

## 8. Architecture Decision — Gate Interaction (Q5)

**Question:** Does Lane E change `TrySetFollowerSlateReady`/`TryMarkFollowerSlateNotReady` logic, or hook after them? Must not break Lane B bracket-sync-protection.

**Answer: Hook after. Zero changes to existing gate methods.**

**Gate state machine after Lane E:**
```
CancelQxBrackets → _followerSlateReady[acc] = false    (gate closes, Lane B blocks)
    └── _laneEDispatched.TryRemove(key)               (Lane E dedup reset)

OnFollowerPositionUpdate fires:
    └── TrySetFollowerSlateReady(acc)
        └── IsAccountFlat=false → returns early (gate stays false)
    └── TryLaneEHook(acc, pos)                        (NEW — fires here)
        └── Guards A-E pass → TryLaneEDispatchFollower
            └── SendCopyWithAtm → ATM re-armed
            └── _laneEDispatched.TryAdd(key, 0)       (dedup committed)
        └── if ok: _followerSlateReady[acc] = true    (gate opens)

Lane B bracket sync: _followerSlateReady[acc] = true → proceeds normally ✓
```

**Gate integrity confirmed:**
- `TrySetFollowerSlateReady`: untouched. Its flat+no-orders precondition is semantically unchanged.
- `TryMarkFollowerSlateNotReady`: untouched. Its false-write behavior is semantically unchanged.
- Lane B's `FindFollowerBracketOrder` gate check (line 4118): reads `_followerSlateReady` — after Lane E opens the gate, bracket sync proceeds. This is correct: the follower now has fresh ATM brackets and is safe for bracket sync.
- No deadlock risk: `_followerSlateReady` is a `ConcurrentDictionary` — no lock, no blocking.

---

## 9. CYC Budget Analysis (Q6)

### `OnFollowerPositionUpdate` — Before and After

| Method | Before | After | Δ | Budget |
|---|---|---|---|---|
| `OnFollowerPositionUpdate` | CYC=3 | CYC=3 | +0 | ≤8 ✓ |
| `TryLaneEHook` | — | CYC=5 | — | ≤8 ✓ (V-02 fixed: `IsLaneEEligible` extracted) |
| `IsLaneEEligible` | — | CYC=8 | — | ≤8 ✓ (absorbs all `\|\|` operators from guards) |
| `TryLaneEDispatchFollower` | — | CYC=5 | — | ≤8 ✓ |
| `BuildLaneEKey` | — | CYC=2 | — | ≤8 ✓ |
| `CancelQxBrackets` 2-param | CYC=4 | CYC=4 | +0 | ≤8 ✓ (V-01 fixed: `??` moved into `BuildLaneEKey`) |
| `CancelQxBrackets` 3-param | CYC=8 | CYC=8 | +0 | ≤8 ✓ (V-01 fixed: `BuildLaneEKey` call = 0 CYC delta) |

### `TryLaneEHook` McCabe Branch-Count (V-02 corrected)

JS-013 rule: every `if`, `||`, `&&`, `?:`, `??` = +1 independently.

```
method entry                                      +1 (baseline)
(1) if (!IsLaneEEligible(acc, pos))               +1  [if]
(2) if (_laneEDispatched.ContainsKey(key))        +1  [if]  -- dedup check
    -- TryLaneEDispatchFollower call              +0  (method call, not a branch)
(3) if (ok)                                       +1  [if]  -- gate open
                                            Total = 5 CYC ≤ 8 ✓
```

Note: `instrFullName`, `mode`, `namedMode`, and `key` are local variable assignments between guards — zero CYC each. `TryLaneEHook` is free of all `||`/`&&` operators; they are absorbed by `IsLaneEEligible`.

### `IsLaneEEligible` McCabe Branch-Count (new helper, V-02 fix)

JS-013 rule applied strictly: each `||` is +1 independently of its enclosing `if`.

```
method entry                                                          +1 (baseline)
(1) if (GetCopyMode() != CopyMode.Clone)                              +1  [if]
(2) if (pos == null || pos.Instrument == null)                        +1  [if]  +1  [||]
(3) if (pos.Quantity <= 0)                                            +1  [if]
(4) if (!_followerSlateReady.TryGetValue(acc.Name, out ready) || ready)  +1  [if]  +1  [||]
(5) if (!(GetCloneAtmMode(pos.Instrument.FullName) is FollowerAtmMode.Named))  +1  [if]
                                                                Total = 8 CYC ≤ 8 ✓
```

No `&&`, `?:`, or `??` in the helper body. The 6 `if`/`||` nodes plus entry sum exactly to 8.

Extraction into `TryLaneEDispatchFollower` keeps the dispatch body and per-follower logic separate. This follows the `TryLaneDHook` + `TryLaneDDispatchFollower` pattern exactly.

### `TryLaneEDispatchFollower` McCabe Branch-Count

```
method entry                                      +1 (baseline)
(A) acc == null                                   +1
(B) !PassesDailyCapCheck(acc)                     +1
?:  action = ... Long ? Buy : SellShort           +1   <- ternary, JS-013 §1
(C) if (ok)                                       +1
                                            Total = 5 CYC ≤ 8 ✓
```

### `CancelQxBrackets` CYC Impact

`TryRemove(BuildLaneEKey(acc, instr), out _)` is a single method call (no branch added to the caller). McCabe does not count method calls as branches. The `??` null-coalescing guard for `instr` is isolated inside `BuildLaneEKey` — it contributes +1 CYC to `BuildLaneEKey` (CYC=2 total), and +0 CYC to either `CancelQxBrackets` overload. Both overloads remain within their documented CYC bounds. V-01 resolved.

### `BuildLaneEKey` McCabe Branch-Count

```
method entry                                      +1 (baseline)
(1) instr != null ? instr.FullName : ""           +1
                                            Total = 2 CYC ≤ 8 ✓
```

---

## 10. Non-Clone Safety (Q7)

**Mechanism:** `IsLaneEEligible` check (1): `if (GetCopyMode() != CopyMode.Clone) return false;`

This is the FIRST guard inside `IsLaneEEligible`, called as the first statement in `TryLaneEHook`. For any mode other than Clone (Signal, Mirror, Market, or Inherit), `IsLaneEEligible` returns false immediately and `TryLaneEHook` returns at branch (1). There is no state read, no dictionary lookup, no allocation. Execution path: `IsLaneEEligible` → single `GetCopyMode()` call + compare + `return false` → `TryLaneEHook` `return`. Zero behavior change for non-Clone paths.

**Confirmation matrix:**

| Mode | TryLaneEHook behavior |
|---|---|
| `CopyMode.Clone` | `IsLaneEEligible` proceeds through guards — may re-arm ATM |
| `CopyMode.Signal` | `IsLaneEEligible` returns false at check (1) → `TryLaneEHook` returns — no-op |
| `CopyMode.Mirror` | `IsLaneEEligible` returns false at check (1) → `TryLaneEHook` returns — no-op |
| `CopyMode.Market` | `IsLaneEEligible` returns false at check (1) → `TryLaneEHook` returns — no-op |

---

## 11. Implementation Specification

### 11.1 New Field

**Location:** `CopyEngine.cs`, immediately after `_laneDDispatched` field declaration (~line 411).

```csharp
// Lane E: re-arm dedup guard. Prevents double-arm on multiple PositionUpdate events for the
// same post-QX/BE slot. Key: accName + "#LANE-E#" + instrFullName (ASCII-only). Value: 0 (unused).
// ConcurrentDictionary: no lock (JS-021). TryAdd: atomic. TryRemove: called from CancelQxBrackets.
// Growth: O(followers * instruments). ~60 bytes/entry. Cleaned up per QX/BE cycle.
private readonly ConcurrentDictionary<string, byte> _laneEDispatched =
    new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
```

### 11.2 Modified: `OnFollowerPositionUpdate`

**Location:** `CopyEngine.cs`, line ~1026 (after `TrySetFollowerSlateReady` call).

**Before (line 1020–1027):**
```csharp
private void OnFollowerPositionUpdate(object sender, PositionEventArgs e)
{
    if (e?.Position?.Account == null)           // (1)
        return;
    if (!IsFollowerAccount(e.Position.Account)) // (2)
        return;
    TrySetFollowerSlateReady(e.Position.Account);
}
```

**After:**
```csharp
private void OnFollowerPositionUpdate(object sender, PositionEventArgs e)
{
    if (e?.Position?.Account == null)           // (1)
        return;
    if (!IsFollowerAccount(e.Position.Account)) // (2)
        return;
    TrySetFollowerSlateReady(e.Position.Account);
    TryLaneEHook(e.Position.Account, e.Position); // Lane E: re-arm ATM after partial QX/BE
}
```

**CYC impact:** +0 (void call, no branch). CYC remains 3.

### 11.3 Modified: `CancelQxBrackets` 2-param (line 1038–1052)

**Location:** `CopyEngine.cs`, line ~1042 (after `TryMarkFollowerSlateNotReady(acc)`).

**Add one line:**
```csharp
TryMarkFollowerSlateNotReady(acc); // BUG2: bracket wipe imminent
_laneEDispatched.TryRemove(BuildLaneEKey(acc, instr), out _); // Lane E: reset re-arm dedup
```

**CYC impact:** +0 (method call to `BuildLaneEKey`, not a branch). The `??` null-coalescing guard lives inside `BuildLaneEKey` — zero CYC delta on this overload.

### 11.4 Modified: `CancelQxBrackets` 3-param (line 1153–1190)

**Location:** `CopyEngine.cs`, find the line: `TryMarkFollowerSlateNotReady(acc); // BUG2: bracket wipe imminent (3-param path)` (~line 1161).

**Add one line after it:**
```csharp
TryMarkFollowerSlateNotReady(acc); // BUG2: bracket wipe imminent (3-param path)
_laneEDispatched.TryRemove(BuildLaneEKey(acc, instr), out _); // Lane E: reset re-arm dedup
```

**CYC impact:** +0 (method call to `BuildLaneEKey`, not a branch). This overload was already at CYC=8 before this addition. Because `BuildLaneEKey` is a helper call (not an inline `??` expression), the +1 CYC from the null guard is charged to `BuildLaneEKey` (CYC=2), NOT to this overload. CYC remains 8. V-01 resolved.

### 11.5 New Method: `TryLaneEHook`

**Location:** `CopyEngine.cs`, immediately after `TryLaneDDispatchFollower` (~line 2753).

**Full method:**
```csharp
// Lane E: re-arm ATM hook. Called from OnFollowerPositionUpdate after TrySetFollowerSlateReady.
// Fires when follower has a remaining position (post-partial QX/BE) with gate closed and no ATM brackets.
// CYC=5: (1)IsLaneEEligible gate,(2)dedup check,(3)ok gate-open. All compound guards in IsLaneEEligible.
// JS-001: no throw (early returns + SendCopyWithAtm has its own try/catch). JS-021: no lock.
// JS-023: no UI update -- StatusUpdate raised inside SendCopyWithAtm (existing pattern).
// ASCII-only. Thread: NT8 account background thread (same as OnFollowerPositionUpdate).
private void TryLaneEHook(Account acc, NinjaTrader.Cbi.Position pos)
{
    // Branch (1): eligibility gate -- all compound guards delegated to IsLaneEEligible
    if (!IsLaneEEligible(acc, pos))
        return;

    // Resolve ATM template and dedup key (assignments only, +0 CYC)
    string instrFullName = pos.Instrument.FullName;
    FollowerAtmMode.Named namedMode =
        GetCloneAtmMode(instrFullName) as FollowerAtmMode.Named;
    string key = acc.Name + "#LANE-E#" + instrFullName;

    // Branch (2): dedup check -- only re-arm once per account+instrument slot
    if (_laneEDispatched.ContainsKey(key))
        return;

    // Dispatch: re-arm ATM bracket for the remaining position
    bool ok = TryLaneEDispatchFollower(acc, pos.Instrument, pos, namedMode, key);

    // Branch (3): gate open -- ATM is now armed; bracket sync is safe to proceed
    if (ok)
        _followerSlateReady[acc.Name] = true;
}
```

**CYC = 5 (entry=1, branch(1)=1, branch(2)=1, branch(3)=1; method calls +0). Well within budget ≤ 8. V-02 fixed.**

### 11.5a New Private Helper: `IsLaneEEligible`

**Location:** `CopyEngine.cs`, immediately before `TryLaneEHook` (or grouped immediately after — same region).

**Full method:**
```csharp
// Lane E: eligibility predicate for TryLaneEHook. Absorbs all compound boolean guards so
// TryLaneEHook contains zero || or && operators (JS-013 compliance).
// CYC=8: entry(1) + if(1) + if+||(2) + if(1) + if+||(2) + if(1). Exactly at budget.
// JS-001: no throw. JS-021: no lock. JS-002: returns bool, never null.
// ASCII-only. Thread: NT8 account background thread (same as TryLaneEHook).
private bool IsLaneEEligible(Account acc, NinjaTrader.Cbi.Position pos)
{
    // (1): non-Clone mode -> not eligible (Signal/Mirror/Market no-op)
    if (GetCopyMode() != CopyMode.Clone)
        return false;

    // (2): null guard -- || is +1 CYC per JS-013, isolated here intentionally
    if (pos == null || pos.Instrument == null)
        return false;

    // (3): no remaining position -> not a re-arm event (flat or zero-qty update)
    if (pos.Quantity <= 0)
        return false;

    // (4): gate must be closed (false) -- confirms post-QX/BE state.
    // Absent key (first session entry) treated as ready -> returns false -> no-op. Correct.
    // || is +1 CYC per JS-013, isolated here intentionally.
    bool ready;
    if (!_followerSlateReady.TryGetValue(acc.Name, out ready) || ready)
        return false;

    // (5): ATM template must be Named -- Inherit/unconfigured = no-op
    if (!(GetCloneAtmMode(pos.Instrument.FullName) is FollowerAtmMode.Named))
        return false;

    return true;
}
```

**CYC = 8 (entry=1, (1)if=1, (2)if=1+(||)=1, (3)if=1, (4)if=1+(||)=1, (5)if=1). Exactly at budget ≤ 8. ✓**

### 11.6 New Method: `TryLaneEDispatchFollower`

**Location:** `CopyEngine.cs`, immediately after `TryLaneEHook`.

**Full method:**
```csharp
// Lane E: per-follower ATM re-arm helper. Keeps TryLaneEHook CYC low.
// CYC=5: (A) acc null guard, (B) daily cap check, ?: action ternary, (C) SendCopyWithAtm result.
// JS-001: no throw -- returns false on all failure paths; SendCopyWithAtm has its own try/catch.
// JS-021: no lock -- ConcurrentDictionary.TryAdd is atomic. JS-002: returns bool (never null).
// ASCII-only. Thread: NT8 account background thread (same as TryLaneEHook caller).
private bool TryLaneEDispatchFollower(
    Account acc,
    NinjaTrader.Cbi.Instrument instr,
    NinjaTrader.Cbi.Position pos,
    FollowerAtmMode.Named namedMode,
    string dedupKey
)
{
    // Branch (A): null guard
    if (acc == null)
        return false;

    // Branch (B): daily position cap -- respect existing cap contract
    if (!PassesDailyCapCheck(acc))
        return false;

    // Build signal from follower's remaining position
    OrderAction action = pos.MarketPosition == NinjaTrader.Cbi.MarketPosition.Long
        ? OrderAction.Buy
        : OrderAction.SellShort;
    string syntheticOrderId = "LANE-E#" + acc.Name + "#" + instr.FullName;
    CopySignal sig = CopySignal.Create(
        action,
        OrderType.Limit,
        pos.Quantity,       // remaining quantity (already multiplier-scaled from original entry)
        pos.AveragePrice,   // ATM bracket anchored at original entry price
        syntheticOrderId
    );

    // Branch (C): dispatch ATM entry; mark dedup only on success
    bool ok = SendCopyWithAtm(acc, instr, in sig, namedMode);
    if (ok)
        // Branch (D): commit dedup so subsequent PositionUpdate events skip re-arm
        _laneEDispatched.TryAdd(dedupKey, 0);
    return ok;
}
```

**CYC = 5 (entry + 2 guards + 1 ternary ?: + 1 if(ok) branch). Within budget.**

### 11.7 New Private Static Helper: `BuildLaneEKey`

**Location:** `CopyEngine.cs`, immediately after `TryLaneEDispatchFollower` (or grouped near `_laneEDispatched` usage).

**Full method:**
```csharp
// Lane E: dedup key builder. Isolates the instr null-guard so CancelQxBrackets callers
// incur +0 CYC from the null check. Called from both CancelQxBrackets overloads.
// CYC=2: entry(1) + ternary null guard(1). ASCII-only. No lock (JS-021). static: no state.
private static string BuildLaneEKey(Account acc, NinjaTrader.Cbi.Instrument instr)
{
    return acc.Name + "#LANE-E#" + (instr != null ? instr.FullName : string.Empty);
}
```

**CYC = 2 (entry + 1 branch for null ternary). Well within budget ≤ 8. ✓**

**Note:** `acc.Name` is assumed non-null at call sites (both `CancelQxBrackets` overloads guard `acc` before calling `TryMarkFollowerSlateNotReady`, which is wired before this call). No additional null guard for `acc` is needed in this helper.

---

## 12. Test Strategy

All new tests are additive (`CopyEngineTests.cs`). No existing tests modified.

### Test Group: T_LANE_E (xUnit [Fact])

#### `T_LANE_E_01_TryLaneEHook_NonCloneMode_IsNoOp`
**Asserts:** With `CopyMode.Signal`, calling `TryLaneEHook(acc, pos)` where pos has Quantity=1 and gate is closed does NOT call `SendCopyWithAtm` and does NOT open the gate.  
**Verification:** `_followerSlateReady[acc.Name]` remains false after call.

#### `T_LANE_E_02_TryLaneEHook_CloneMode_GateAlreadyOpen_IsNoOp`
**Asserts:** With `CopyMode.Clone` and `_followerSlateReady[acc.Name] = true`, calling `TryLaneEHook` does NOT dispatch (gate already open = not a re-arm event).  
**Verification:** `_laneEDispatched` contains no new key.

#### `T_LANE_E_03_TryLaneEHook_CloneMode_GateClosed_ZeroQuantity_IsNoOp`
**Asserts:** With `CopyMode.Clone`, gate=false, but `pos.Quantity = 0`, `TryLaneEHook` returns without dispatching.  
**Verification:** `_laneEDispatched` empty; gate remains false.

#### `T_LANE_E_04_TryLaneEHook_CloneMode_GateClosed_HasPosition_NoTemplate_IsNoOp`
**Asserts:** With `CopyMode.Clone`, gate=false, `pos.Quantity=1`, but `GetCloneAtmMode` returns `Inherit` (no template), `TryLaneEHook` returns without dispatching.  
**Verification:** `_laneEDispatched` empty; gate remains false.

#### `T_LANE_E_05_TryLaneEHook_CloneMode_GateClosed_HasPosition_DispatchesOnce`
**Asserts:** With `CopyMode.Clone`, gate=false, `pos.Quantity=1`, Named template set, `TryLaneEHook` calls `SendCopyWithAtm` exactly once and opens the gate.  
**Verification:** `_followerSlateReady[acc.Name] = true`; `_laneEDispatched.ContainsKey(key) = true`.

#### `T_LANE_E_06_TryLaneEHook_CloneMode_DoubleFirePrevented`
**Asserts:** Calling `TryLaneEHook` twice for the same account+instrument (simulating two position events) dispatches `SendCopyWithAtm` exactly once (dedup fires on second call).  
**Verification:** `_laneEDispatched` key present after first call; second call returns without dispatch.

#### `T_LANE_E_07_CancelQxBrackets_ClearsLaneEDedupKey`
**Asserts:** After `CancelQxBrackets(acc, instr)` is called, `_laneEDispatched` no longer contains the key for `acc+instr`.  
**Verification:** Key absent after cancel; `TryLaneEHook` can re-arm again (simulated by second position event).

#### `T_LANE_E_08_TryLaneEDispatchFollower_NullAccount_ReturnsFalse`
**Asserts:** `TryLaneEDispatchFollower(null, instr, pos, namedMode, key)` returns false without throwing.

#### `T_LANE_E_09_TryLaneEDispatchFollower_DailyCapExceeded_ReturnsFalse`
**Asserts:** When `PassesDailyCapCheck(acc)` fails, `TryLaneEDispatchFollower` returns false and does not call `SendCopyWithAtm`.

#### `T_LANE_E_10_OnFollowerPositionUpdate_LaneEWired`
**Asserts:** `OnFollowerPositionUpdate` is a private instance method on `CopyEngine` with signature `void(object, PositionEventArgs)` — structural test confirming wiring point exists.

---

## 13. 7-Scan Checklist (Layer 1 Contract)

This checklist is the engineer's mandatory pre-commit gate. All items must pass before any PR is opened.

| Scan | Check | Required Result |
|---|---|---|
| **SCAN-01** | `grep -rn "lock(" src/PropTraderTools/CopyEngine.cs` | 0 matches |
| **SCAN-02** | Manual McCabe count: `OnFollowerPositionUpdate` | CYC = 3 ≤ 8 ✓ |
| **SCAN-02** | Manual McCabe count: `TryLaneEHook` | CYC = 5 ≤ 8 ✓ (V-02 fixed: `IsLaneEEligible` extracted) |
| **SCAN-02** | Manual McCabe count: `IsLaneEEligible` | CYC = 8 ≤ 8 ✓ (entry+if+if+\|\|+if+if+\|\|+if = 8) |
| **SCAN-02** | Manual McCabe count: `TryLaneEDispatchFollower` | CYC = 5 ≤ 8 ✓ |
| **SCAN-03** | `grep -n "async\|await" src/PropTraderTools/CopyEngine.cs` (new methods only) | 0 matches |
| **SCAN-04** | Non-ASCII byte scan on CopyEngine.cs | 0 non-ASCII bytes in new/modified lines |
| **SCAN-05** | `grep -n "AtmStrategyCreate\|AtmStrategyChangeStopTarget" src/PropTraderTools/CopyEngine.cs` | 0 matches |
| **SCAN-06** | `powershell -File .\scripts\build_readiness.ps1` | 0 errors, 0 warnings, 0 test failures |
| **SCAN-07** | Structural — `TryLaneEHook` is declared private void and wired in `OnFollowerPositionUpdate` | Confirmed in diff |
| **SCAN-07** | Structural — `IsLaneEEligible` is declared `private bool` on `CopyEngine`, called as first guard in `TryLaneEHook` | Confirmed in diff |
| **SCAN-07** | Structural — `_laneEDispatched` field declared as `ConcurrentDictionary<string, byte>` | Confirmed in diff |
| **SCAN-07** | Structural — `CancelQxBrackets` 2-param and 3-param both call `_laneEDispatched.TryRemove(BuildLaneEKey(acc, instr), out _)` | Confirmed in diff |
| **SCAN-07** | Structural — `BuildLaneEKey` is declared `private static string` and present in `CopyEngine.cs` (V-01 fix) | Confirmed in diff |
| **SCAN-07** | Structural — `IsLaneEEligible` first branch is `GetCopyMode() != CopyMode.Clone` (non-Clone no-op via eligibility helper) | Confirmed in diff |
| **SCAN-07** | Structural — gate integrity: `TrySetFollowerSlateReady` signature and body are byte-for-byte unchanged | Confirmed by diff |
| **SCAN-07** | Structural — gate integrity: `TryMarkFollowerSlateNotReady` signature and body are byte-for-byte unchanged | Confirmed by diff |

---

## 14. Spec Requirement Traceability

| Spec Requirement | Architecture Element | Status |
|---|---|---|
| Re-arm ATM on follower after partial QX/BE exit | `TryLaneEHook` + `TryLaneEDispatchFollower` | CLOSED |
| Trigger only on "post-QX/BE clean slate → remaining position" transition | Guard sequence in `TryLaneEHook` (guards A–F) | CLOSED |
| Use existing `SendCopyWithAtm` / `StartAtmStrategy` for re-arm | `TryLaneEDispatchFollower` calls `SendCopyWithAtm` | CLOSED |
| No double-arm on multiple position events | `_laneEDispatched` ConcurrentDictionary dedup | CLOSED |
| Dedup reset on next QX/BE cycle | `TryRemove` in both `CancelQxBrackets` overloads | CLOSED |
| Gate opens after successful re-arm | `_followerSlateReady[acc.Name] = true` after dispatch | CLOSED |
| Gate integrity for Lane B bracket sync | `TrySetFollowerSlateReady` + `TryMarkFollowerSlateNotReady` unchanged | CONFIRMED |
| Non-Clone no-op | `IsLaneEEligible` check (1): `GetCopyMode() != CopyMode.Clone` — first guard in eligibility predicate | CLOSED |
| No `lock()` | `ConcurrentDictionary` throughout | CONFIRMED JS-021 |
| No `throw` in dispatch path | All early-return + `SendCopyWithAtm` try/catch | CONFIRMED JS-001 |
| No WPF update off-thread | `StatusUpdate?.Invoke` inside `SendCopyWithAtm` (existing) | CONFIRMED JS-023 |
| CYC ≤ 8 per method | `OnFollowerPositionUpdate`=3, `TryLaneEHook`=5, `IsLaneEEligible`=8, `TryLaneEDispatchFollower`=5 | CONFIRMED |
| ASCII-only identifiers and strings | All string literals ASCII; no Unicode | CONFIRMED |
| `StartAtmStrategy` only (not `AtmStrategyCreate`) | `SendCopyWithAtm` uses static `StartAtmStrategy` | CONFIRMED |
| Order name "PTT-" prefix ... "Entry" for ATM | `SendCopyWithAtm` hardcodes name="Entry" | CONFIRMED NT8 |

---

## 15. Section K — Deferred Work

### DW-LD-03: Lane E re-arm ATM after QX/BE
**Status: CLOSED by this plan.**  
Lane E directly implements re-arming of NT8 ATM bracket protection after QX/BE partial exit via `TryLaneEHook` + `TryLaneEDispatchFollower`. The mechanism uses `SendCopyWithAtm` (existing, tested) and opens `_followerSlateReady` gate upon success. Full dedup, non-Clone safety, and gate integrity are designed and specified.

### DW-PTT0911B-01: SIM end-to-end verification
**Status: ADDRESSED as post-compile SIM validation step.**

This deferred item becomes the **Lane E success criterion** for the ptt-verifier:

**SIM Verification Protocol:**
1. Configure NT8 SIM with leader + 1 follower, Clone mode, Named ATM template.
2. Enter a position on the leader (e.g., long 2 contracts).
3. Confirm follower copies entry with ATM brackets (Lane D working).
4. Execute QX-ALL (partial — take 1 contract profit while remaining 1 contract).
5. **Expected:** Follower's ATM brackets cancelled (Lane B). Gate = false.
6. **Expected (Lane E fix):** Within the next `OnFollowerPositionUpdate` event, follower receives a new ATM bracket for the 1 remaining contract. `_followerSlateReady` = true. NT8 Output tab confirms: `{acc}: PTT-ATM entry @ {price} atm={template}`.
7. Confirm NO duplicate position (follower still holds 1 contract, not 2).
8. Execute a manual exit on the leader — confirm follower exit is mirrored correctly with Lane B bracket sync (gate was properly open).

**Verification outputs to confirm in NT8 Output tab:**
- `[PTT-QX] snapshot:` + cancel messages (existing)
- New: `{followerAccName}: PTT-ATM entry @ {avgPrice} atm={templateName}` (from `StatusUpdate?.Invoke` inside `SendCopyWithAtm`)

**Regression tests to confirm not broken:**
- First entry (flat → position): Lane D handles ATM. Lane E no-ops (gate absent = treated ready).
- Full exit (position → flat): Gate closes, then `IsAccountFlat=true` → `TrySetFollowerSlateReady` opens gate. Lane E no-ops (qty=0 after flat).
- Non-Clone mode: Lane E returns at branch (1). Zero behavior change.

---

## Appendix A: Confirmed NT8 API Facts Used

- `StartAtmStrategy(AtmStrategy, Order)` / `(string, Order)` — AddOnBase available (static form). USED.
- `AtmStrategyCreate()` — StrategyBase-only. BANNED. Not used.
- `AtmStrategyChangeStopTarget()` — StrategyBase-only. BANNED. Not used.
- `account.PositionUpdate` — AddOnBase available. Already subscribed by Lane B. USED.
- `Account.Change()` — silent no-op on ATM-owned brackets. NOT used.
- `Account.CreateOrder()` + `follower.Submit()` — NOT used in Lane E (delegated entirely to `SendCopyWithAtm`).

---

## Appendix B: CYC Verification for New Call Sites in CancelQxBrackets

**V-01 Fix Applied (Cycle 2):** The inline `instr?.FullName ?? string.Empty` expression has been extracted into the `BuildLaneEKey(Account acc, Instrument instr)` helper. The `??` null-coalescing operator (+1 CYC per JS-013) now lives inside `BuildLaneEKey`, not inside the `CancelQxBrackets` methods.

`CancelQxBrackets` 2-param (line 1038–1052) current CYC=4:
```
null guard(1) + foreach(2) + IsQxCancelEligible2(3) + stale.Count==0(4)
```
Adding `_laneEDispatched.TryRemove(BuildLaneEKey(acc, instr), out _)` at line 1043 is a helper call (McCabe: +0 branches). Final CYC=4. ✓

`CancelQxBrackets` 3-param (line 1153–) current CYC=8:
The comment documents CYC=8. Adding `_laneEDispatched.TryRemove(BuildLaneEKey(acc, instr), out _)` is a helper call (McCabe: +0 branches). The `??` guard is inside `BuildLaneEKey` (CYC=2, separate method). Final CYC=8. ✓

`BuildLaneEKey` (new, line ~after `TryLaneEDispatchFollower`):
```
method entry(1) + ternary null guard for instr(1) = CYC=2. ✓
```

---

*Plan written by ptt-architect. Cycle 1 reviewed by ptt-plan-reviewer (REVIEW_FAIL — V-01). Cycle 2: V-01 fixed via BuildLaneEKey helper extraction (REVIEW_FAIL — V-02). Cycle 3: V-02 fixed via IsLaneEEligible helper extraction — all ||/&& operators absorbed into helper, TryLaneEHook CYC reduced from 10 to 5, IsLaneEEligible CYC=8 exactly at budget. Pending ptt-plan-reviewer Phase 2 Cycle 3 review.*
