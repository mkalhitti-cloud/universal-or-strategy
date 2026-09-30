# B33-LaneA: 01-diff-plan.md
# DW-B33-01 — New-Stop BE Approach: SubmitBeStop + OrphanCancelGuard
# Architect: ptt-architect | Phase 1 | 2026-07-20
# Target file: CopyEngine.cs (src/PropTraderTools/CopyEngine.cs)

---

## Section 1: Rules Gate Result

```
STEP 0 — RULES CATALOG GATE: PASS
  [x] docs/standards/jane-street/RULES_CATALOG.md — UTF-8 readable, all P0 rules identified
  [x] P0 scan — JS-021 lock(): zero matches in planned changes
  [x] P0 scan — JS-033 async void: zero planned async void methods
  [x] P0 scan — JS-001 throw in hot path: zero planned exception throws
  [x] P0 scan — JS-002 return null: no return null in new methods (_pendingBeStop field init
      is a field initializer, not a return statement — acceptable)
  [x] NT8-007 CreateOrder arg12: (NinjaTrader.Cbi.CustomOrder)null — verified below
  [x] NT8-013 DateTime.MaxValue: used in CreateOrder — confirmed
  [x] NT8-014 signal name "PTT-BE": starts with "PTT-" — confirmed
  [x] NT8-018 lock() banned: no lock() in any planned change
  [x] NT8-019 async void banned: no async void in any planned change
  [x] NT8-029 tick alignment: bePrice is pre-computed with Math.Round/tickSize before SubmitBeStop call
  [x] NT8-043 null-conditional -= banned: no new subscriptions added (orphan guard uses Option B)
GATE RESULT: PASS
```

---

## Section 2: NT8 Compiler Check — CreateOrder 12-Arg Verification

**Planned call in SubmitBeStop:**
```csharp
_pendingBeStop = acc.CreateOrder(
    instr, direction, OrderType.StopMarket, OrderEntry.Manual,
    TimeInForce.Gtc, qty, 0, bePrice, null, "PTT-BE",
    DateTime.MaxValue, (NinjaTrader.Cbi.CustomOrder)null);
```

**NT8-007 12-arg signature (zero-indexed):**
```
 0  Instrument instrument          -> instr
 1  OrderAction orderAction        -> direction  (Sell or BuyToCover)
 2  OrderType orderType            -> OrderType.StopMarket
 3  OrderEntry orderEntry          -> OrderEntry.Manual
 4  TimeInForce timeInForce        -> TimeInForce.Gtc
 5  int quantity                   -> qty
 6  double limitPrice              -> 0   (no limit for StopMarket)
 7  double stopPrice               -> bePrice   (the BE price, tick-aligned)
 8  string oco                     -> null
 9  string signalName              -> "PTT-BE"  (starts with PTT- per NT8-014)
10  DateTime gtd                   -> DateTime.MaxValue  (GTC, per NT8-013)
11  CustomOrder customOrder        -> (NinjaTrader.Cbi.CustomOrder)null  (per NT8-007)
```

**Confirmation against production patterns:**
- `TrimOneAccountLimit` L1229-1232: `(NinjaTrader.Cbi.CustomOrder)null` — MATCHES
- `FlattenOneAccountLimit` L1264-1267: `(NinjaTrader.Cbi.CustomOrder)null` — MATCHES
- `TrimOneAccount` L1011-1014: `null` (not cast) — older pattern, still compiles
- `FlattenOneAccount` L1048-1051: `null` (not cast) — older pattern, still compiles

**Decision:** Use explicit cast `(NinjaTrader.Cbi.CustomOrder)null` matching the newest production pattern.

**NOTE on spec pseudocode:** The spec uses a simplified 10-arg pseudocode AND includes `acc.Submit(new[] { _pendingBeStop })` after CreateOrder. This is WRONG for NT8. `CreateOrder` creates AND submits atomically — there is no `acc.Submit()` overload in NT8 AddOn context. The Submit call is OMITTED from the implementation. `CreateOrder` return value IS the live order reference; no separate submission needed.

**NT8 Compiler Check: PASS**

---

## Section 3: Architect Decision — Orphan Guard Hook Strategy

**Decision: Option B — hook inside `TryFirePositionState`**

**Rationale:**
1. **Zero new event subscriptions**: No `acc.PositionUpdate` subscription added to `Subscribe()`/`Unsubscribe()`. Lower blast radius. No NT8-043 complications.
2. **`hasPos` is already computed**: `TryFirePositionState` (line 732) calls `HasOpenPosition(e.Order.Account, e.Order.Instrument)` and stores result in `hasPos`. Reusing this variable costs zero additional NT8 API calls.
3. **Both Account and Instrument available**: `e.Order.Account` and `e.Order.Instrument` are available in TryFirePositionState at the point of the new hook.
4. **Idempotent guard**: `OrphanCancelGuard` checks `_pendingBeStop == null` as its first line — safe against spurious fires (e.g., partial fills, multiple cancelled orders in rapid succession).
5. **Spec intent preserved**: The spec says "OnPositionUpdate fires: position.Quantity == 0 → OrphanCancelGuard()". `HasOpenPosition` returns false when qty == 0 — semantically identical to qty == 0 check.

**Hook location:** After the `PositionStateChanged` fire at line 734, add:
```csharp
if (!hasPos)
    OrphanCancelGuard(e.Order.Account, e.Order.Instrument);
```

---

## Section 4: Architect Decision — SubmitBeStop Scope (All Accounts or Leader Only)

**Decision: LEADER ONLY — separation at `BreakEven(Account leader, ...)` call site**

**Rationale:**
- The spec change table (line 12198) explicitly states: "MoveStopToBreakEven — **leader path**" → new: `SubmitBeStop(acc, instr, bePrice, qty)`.
- Followers have PTT-created bracket stops. `acc.Change()` on PTT-created orders (non-ATM-slot) still works (NT8-046 only blocks ATM slot orders Stop1/Stop2). Applying `SubmitBeStop` to followers would create a third stop (ATM-slot + PTT-bracket + PTT-BE) — incorrect.
- `MoveStopToBreakEven` is NOT modified. The `acc.Change()` loop in MoveStopToBreakEven continues to serve followers only (ATM slot names are already filtered by `IsAtmSlotName()` at line 1498).

**Implementation:**
- `BreakEven(Account leader, ...)` at line 1561: replace `MoveStopToBreakEven(leader, instrument, bufferTicks)` with inline price computation + `SubmitBeStop(leader, instrument, newStop, pos.Quantity)`.
- The follower `foreach` loop (lines 1562-1565) is unchanged — it still calls `MoveStopToBreakEven(acc, ...)`.

**Price computation (extracted from MoveStopToBreakEven lines 1466-1470, replicated at call site):**
```csharp
var pos = FindPosition(leader, instrument);
if (!IsFlat(pos))
{
    double tickSize = instrument.MasterInstrument.TickSize;
    bool isLong = pos.MarketPosition == MarketPosition.Long;
    double raw = pos.AveragePrice + (isLong ? 1.0 : -1.0) * bufferTicks * tickSize;
    double newStop = Math.Round(raw / tickSize) * tickSize;
    SubmitBeStop(leader, instrument, newStop, pos.Quantity);
}
```

---

## Section 5: Source Location Map

| Change | Method | Line Start | Line End | Notes |
|--------|--------|-----------|----------|-------|
| 1 — Build tag | PttBuild.Tag constant | 41 | 41 | Single-line string constant replacement |
| 2 — New field | CopyEngine class body | ~160 | ~160 | Insert after last field declaration |
| 3 — Leader BE path | BreakEven(Account,Instrument,int) | 1554 | 1567 | Replace line 1561 only |
| 4 — Orphan guard hook | TryFirePositionState | 718 | 735 | Insert 2 lines after line 734 |
| 5 — New method SubmitBeStop | (insert after MoveStopToBreakEven) | 1538 | — | New method after line 1537 closing brace |
| 6 — New method OrphanCancelGuard | (insert after SubmitBeStop) | ~1558 | — | New method after SubmitBeStop closing brace |

---

## Section 6: Complete Diff Plan — 6 Changes

---

### Change 1 — Build Tag Update

**File:** `src/PropTraderTools/CopyEngine.cs`
**Target:** `PttBuild.Tag` constant, line 41
**Old code (verbatim):**
```csharp
        internal const string Tag = "PTT-COPIER B33-DIAG | 2026-07-20 | DW-B32-10+11 filter+armed-fix";
```
**New code:**
```csharp
        internal const string Tag = "PTT-COPIER B33 | new-stop BE | 2026-07-20";
```
**Rationale:** Build tag update confirms B33 Phase 1 code is live in NT8 Output tab.

---

### Change 2 — New Field: `_pendingBeStop`

**File:** `src/PropTraderTools/CopyEngine.cs`
**Target:** Class field block, after last event declaration (line 160: `public event Action<bool> CopyEnabledChanged;`)
**Old code (verbatim — context lines):**
```csharp
        // B20-LANE-A T2: Copy ON/OFF sync event (DW-B17-SYNC-01)
        // Plain delegate field -- NOT lock-guarded (JS-021). Fired from SetEnabled on every toggle.
        // Lane C wires TradeCopierPanel and TradeCopierWindow subscribers.
        public event Action<bool> CopyEnabledChanged;
```
**New code (insert AFTER the existing CopyEnabledChanged line):**
```csharp
        // B20-LANE-A T2: Copy ON/OFF sync event (DW-B17-SYNC-01)
        // Plain delegate field -- NOT lock-guarded (JS-021). Fired from SetEnabled on every toggle.
        // Lane C wires TradeCopierPanel and TradeCopierWindow subscribers.
        public event Action<bool> CopyEnabledChanged;

        // B33 DW-B33-01: pending BE stop reference. volatile (NT8-017: read on order thread, written on BE arm).
        // null = no BE stop pending. Set by SubmitBeStop. Cleared by OrphanCancelGuard.
        private volatile Order _pendingBeStop = null;
```
**Rationale:** Tracks the live PTT-BE stop order for duplicate guard and orphan cancellation. `volatile` required per NT8-017 (cross-thread access from order callbacks and UI-thread BE arm).

---

### Change 3 — Leader BE Path: Replace MoveStopToBreakEven(leader) with SubmitBeStop

**File:** `src/PropTraderTools/CopyEngine.cs`
**Target:** `BreakEven(Account leader, Instrument instrument, int bufferTicks)`, line 1561
**Old code (verbatim):**
```csharp
        internal void BreakEven(Account leader, Instrument instrument, int bufferTicks)
        {
            if (leader == null)                                      // (1) null guard
            {
                StatusUpdate?.Invoke("PTT-BE: leader null -- BE skipped");
                return;
            }
            MoveStopToBreakEven(leader, instrument, bufferTicks);   // (2) DW-B32-08: standalone path
            foreach (var acc in AllAccounts(instrument))            // (3) follower fan-out
            {
                if (acc == leader) continue;                        // (4) skip leader (already done above)
                MoveStopToBreakEven(acc, instrument, bufferTicks);
            }
        }
```
**New code:**
```csharp
        // B33 DW-B33-01: leader uses new-stop BE (SubmitBeStop). Followers still use MoveStopToBreakEven (acc.Change on PTT-created stops).
        // CYC=6: null guard(1), IsFlat(2), isLong ternary(3), SubmitBeStop call(4), foreach(5), acc==leader(6).
        // NT8-046: acc.Change() silently rejected on ATM-owned stops. SubmitBeStop creates independent PTT-BE stop.
        internal void BreakEven(Account leader, Instrument instrument, int bufferTicks)
        {
            if (leader == null)                                      // (1) null guard
            {
                StatusUpdate?.Invoke("PTT-BE: leader null -- BE skipped");
                return;
            }
            // B33 DW-B33-01: leader path -- new-stop approach (NT8-046: can't Change() ATM-owned stops)
            var leaderPos = FindPosition(leader, instrument);
            if (!IsFlat(leaderPos))                                  // (2) position open
            {
                double tickSize = instrument.MasterInstrument.TickSize;
                bool isLong = leaderPos.MarketPosition == MarketPosition.Long; // (3) direction
                double raw = leaderPos.AveragePrice + (isLong ? 1.0 : -1.0) * bufferTicks * tickSize;
                double newStop = Math.Round(raw / tickSize) * tickSize;        // tick-align per NT8-029
                SubmitBeStop(leader, instrument, newStop, leaderPos.Quantity); // (4) submit
            }
            foreach (var acc in AllAccounts(instrument))            // (5) follower fan-out
            {
                if (acc == leader) continue;                        // (6) skip leader (already done above)
                MoveStopToBreakEven(acc, instrument, bufferTicks);  // followers: existing acc.Change path
            }
        }
```
**Rationale:** Leader uses `SubmitBeStop` (new independent stop order, bypasses NT8-046). Follower fan-out is unchanged — MoveStopToBreakEven with `acc.Change()` still works on PTT-created follower bracket stops.

---

### Change 4 — Orphan Guard Hook in TryFirePositionState

**File:** `src/PropTraderTools/CopyEngine.cs`
**Target:** `TryFirePositionState(OrderEventArgs e)`, line 734
**Old code (verbatim — the last 3 lines of TryFirePositionState):**
```csharp
            string instr   = e.Order.Instrument.FullName;
            bool hasPos     = HasOpenPosition(e.Order.Account, e.Order.Instrument);
            bool hasEntries = HasWorkingEntries(e.Order.Account, e.Order.Instrument);
            PositionStateChanged?.Invoke(instr, new PositionState(hasPos, hasEntries));
        }
```
**New code:**
```csharp
            string instr   = e.Order.Instrument.FullName;
            bool hasPos     = HasOpenPosition(e.Order.Account, e.Order.Instrument);
            bool hasEntries = HasWorkingEntries(e.Order.Account, e.Order.Instrument);
            PositionStateChanged?.Invoke(instr, new PositionState(hasPos, hasEntries));
            // B33 DW-B33-01: orphan guard -- if position just went flat, cancel pending BE stop
            if (!hasPos)
                OrphanCancelGuard(e.Order.Account, e.Order.Instrument);
        }
```
**Rationale:** Fires OrphanCancelGuard whenever position flattens (qty == 0) via any path — manual flatten, ATM stop filling first, or any other close. `hasPos` already computed; no extra NT8 API call. Option B chosen over PositionUpdate subscription (avoids NT8-043, zero new event registrations).

---

### Change 5 — New Method: `SubmitBeStop`

**File:** `src/PropTraderTools/CopyEngine.cs`
**Target:** Insert after `MoveStopToBreakEven` closing brace (after line 1537), before `BreakEven(Instrument, int)` at line 1539

**New code (insert between lines 1537 and 1539):**
```csharp

        // B33 DW-B33-01: SubmitBeStop -- places independent PTT-BE StopMarket order at bePrice.
        // CYC=4: flat guard(1), duplicate guard(2), direction ternary(3), try/catch(4).
        // NT8-007: 12-arg CreateOrder -- arg12 = (NinjaTrader.Cbi.CustomOrder)null.
        // NT8-013: DateTime.MaxValue for GTC. NT8-014: signal name "PTT-BE" starts with PTT-.
        // NT8-029: bePrice must be tick-aligned by caller (BreakEven computes Math.Round/tickSize).
        // JS-021: no lock(). JS-002: null field, not null return.
        private void SubmitBeStop(Account acc, Instrument instr, double bePrice, int qty)
        {
            var pos = FindPosition(acc, instr);
            if (IsFlat(pos))                                                               // (1) flat guard
            {
                NinjaTrader.Code.Output.Process("[BE] SubmitBeStop -- position flat, skip", PrintTo.OutputTab1);
                return;
            }
            if (_pendingBeStop != null && _pendingBeStop.OrderState == OrderState.Working) // (2) duplicate guard
            {
                NinjaTrader.Code.Output.Process("[BE] SubmitBeStop -- pending BE stop already live, skip", PrintTo.OutputTab1);
                return;
            }
            var direction = pos.MarketPosition == MarketPosition.Long                      // (3) direction
                ? OrderAction.Sell
                : OrderAction.BuyToCover;
            try                                                                             // (4) NT8 API
            {
                _pendingBeStop = acc.CreateOrder(
                    instr, direction, OrderType.StopMarket, OrderEntry.Manual,
                    TimeInForce.Gtc, qty, 0, bePrice, null, "PTT-BE",
                    DateTime.MaxValue, (NinjaTrader.Cbi.CustomOrder)null);
                NinjaTrader.Code.Output.Process(
                    "[BE] SubmitBeStop " + direction + " " + qty + " @ " + bePrice,
                    PrintTo.OutputTab1);
                StatusUpdate?.Invoke(acc.Name + ": BE stop submitted @ " + bePrice);
            }
            catch (Exception ex)
            {
                NinjaTrader.Code.Output.Process("[BE] SubmitBeStop EXCEPTION -- " + ex.Message, PrintTo.OutputTab1);
                StatusUpdate?.Invoke(acc.Name + ": BE SubmitBeStop failed -- " + ex.Message);
            }
        }

```
**Rationale:** Creates independent PTT-BE StopMarket order that bypasses NT8-046. ATM stop remains live; when PTT-BE fills, position = 0 and ATM auto-cancels its own stop. `CreateOrder` is atomic — no `acc.Submit()` needed (NT8 AddOn has no Submit API).

---

### Change 6 — New Method: `OrphanCancelGuard`

**File:** `src/PropTraderTools/CopyEngine.cs`
**Target:** Insert immediately after `SubmitBeStop` closing brace (above `BreakEven(Instrument, int)` at line 1539)

**New code:**
```csharp
        // B33 DW-B33-01: OrphanCancelGuard -- cancels pending PTT-BE stop if position went flat.
        // Called from TryFirePositionState when hasPos == false.
        // CYC=3: null check(1), state check + null reset(2), cancel call(3).
        // JS-021: no lock(). acc.Cancel is thread-safe NT8 API call.
        private void OrphanCancelGuard(Account acc, Instrument instr)
        {
            if (_pendingBeStop == null)                                                    // (1) null check
                return;
            if (_pendingBeStop.OrderState != OrderState.Working)                          // (2) not working
            {
                _pendingBeStop = null;
                return;
            }
            try                                                                            // (3) cancel
            {
                acc.Cancel(new Order[] { _pendingBeStop });
                NinjaTrader.Code.Output.Process("[BE] OrphanCancelGuard fired -- pending BE stop cancelled", PrintTo.OutputTab1);
                StatusUpdate?.Invoke(acc.Name + ": OrphanCancelGuard -- BE stop cancelled");
            }
            catch (Exception ex)
            {
                NinjaTrader.Code.Output.Process("[BE] OrphanCancelGuard EXCEPTION -- " + ex.Message, PrintTo.OutputTab1);
            }
            _pendingBeStop = null;
        }

```
**Rationale:** Prevents orphaned PTT-BE stop remaining live after position is already closed by another path (manual flatten, ATM stop fills first, etc.). Clears `_pendingBeStop` in all code paths to prevent stale reference on next BE arm.

---

## Section 7: Test Plan — 3 [Fact] Method Bodies

These tests use the existing reflection pattern in [`CopyEngineTests.cs`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs). No NT8 runtime required. All three are structurally verifiable — they guard against accidental removal or signature change of the new methods.

**Pattern used (from existing test file):**
```csharp
private static FieldInfo GetField(string name)
    => typeof(CopyEngine).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);

private static MethodInfo GetMethod(string name)
    => typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
```

---

**Test 1: SubmitBeStop method exists with correct signature**
```csharp
[Fact]
public void SubmitBeStop_MethodExists_And_HasFourParameters()
{
    var mi = typeof(CopyEngine).GetMethod(
        "SubmitBeStop",
        BindingFlags.NonPublic | BindingFlags.Instance);
    Assert.NotNull(mi);
    var parms = mi.GetParameters();
    Assert.Equal(4, parms.Length);
    // Verify parameter types in order: Account, Instrument, double, int
    Assert.Equal(typeof(NinjaTrader.Cbi.Account),     parms[0].ParameterType);
    Assert.Equal(typeof(NinjaTrader.Cbi.Instrument),   parms[1].ParameterType);
    Assert.Equal(typeof(double),                       parms[2].ParameterType);
    Assert.Equal(typeof(int),                          parms[3].ParameterType);
}
```

---

**Test 2: OrphanCancelGuard method exists with correct signature**
```csharp
[Fact]
public void OrphanCancelGuard_MethodExists_And_HasTwoParameters()
{
    var mi = typeof(CopyEngine).GetMethod(
        "OrphanCancelGuard",
        BindingFlags.NonPublic | BindingFlags.Instance);
    Assert.NotNull(mi);
    var parms = mi.GetParameters();
    Assert.Equal(2, parms.Length);
    // Verify parameter types in order: Account, Instrument
    Assert.Equal(typeof(NinjaTrader.Cbi.Account),     parms[0].ParameterType);
    Assert.Equal(typeof(NinjaTrader.Cbi.Instrument),   parms[1].ParameterType);
}
```

---

**Test 3: _pendingBeStop field exists and initializes to null**
```csharp
[Fact]
public void PendingBeStop_FieldExists_And_InitialValueIsNull()
{
    var fi = typeof(CopyEngine).GetField(
        "_pendingBeStop",
        BindingFlags.NonPublic | BindingFlags.Instance);
    Assert.NotNull(fi);
    // Field type must be NinjaTrader.Cbi.Order
    Assert.Equal(typeof(NinjaTrader.Cbi.Order), fi.FieldType);
    // Initial value on singleton instance must be null (no BE armed at startup)
    var value = fi.GetValue(CopyEngine.Instance);
    Assert.Null(value);
}
```

---

## Section 8: Verification Checklist for Lane C

Lane C (ptt-verifier) should confirm the following after engineer applies the diff:

```
SCAN-01 — BUILD TAG
  [ ] CopyEngine.cs line 41: Tag = "PTT-COPIER B33 | new-stop BE | 2026-07-20"
  [ ] NT8 Output tab on chart inject shows this string

SCAN-02 — FIELD
  [ ] _pendingBeStop field present in class body
  [ ] Declared volatile (NT8-017: cross-thread field)
  [ ] Type is Order (not var, not object)

SCAN-03 — LEADER BE PATH
  [ ] BreakEven(Account leader, ...) no longer calls MoveStopToBreakEven(leader, ...)
  [ ] BreakEven calls FindPosition(leader, instrument) for price calculation
  [ ] BreakEven calls SubmitBeStop(leader, instrument, newStop, leaderPos.Quantity)
  [ ] Follower foreach loop still calls MoveStopToBreakEven(acc, instrument, bufferTicks) unchanged

SCAN-04 — ORPHAN GUARD HOOK
  [ ] TryFirePositionState: `if (!hasPos) OrphanCancelGuard(e.Order.Account, e.Order.Instrument);` present
  [ ] Hook is AFTER PositionStateChanged?.Invoke(...) line (not before)

SCAN-05 — SUBMITBESTOP METHOD
  [ ] Method signature: private void SubmitBeStop(Account acc, Instrument instr, double bePrice, int qty)
  [ ] CreateOrder call: 12 args, last arg = (NinjaTrader.Cbi.CustomOrder)null
  [ ] Signal name "PTT-BE" — starts with "PTT-" (NT8-014)
  [ ] DateTime.MaxValue used (NT8-013)
  [ ] OrderType.StopMarket with 0 limitPrice, bePrice as stopPrice (arg7)
  [ ] NO acc.Submit() call (does not exist in NT8)
  [ ] _pendingBeStop assigned the return value of CreateOrder
  [ ] Print "[BE] SubmitBeStop {direction} {qty} @ {bePrice}" to Output

SCAN-06 — ORPHANCANCELGUARD METHOD
  [ ] Method signature: private void OrphanCancelGuard(Account acc, Instrument instr)
  [ ] Null guard: if (_pendingBeStop == null) return;
  [ ] State guard: if (_pendingBeStop.OrderState != OrderState.Working) { _pendingBeStop = null; return; }
  [ ] Cancel call: acc.Cancel(new Order[] { _pendingBeStop });
  [ ] _pendingBeStop = null at end of every code path
  [ ] Print "[BE] OrphanCancelGuard fired -- pending BE stop cancelled"

SCAN-07 — P0 COMPLIANCE
  [ ] grep "lock(" src/PropTraderTools/CopyEngine.cs -- zero results in new code
  [ ] grep "async void" src/PropTraderTools/CopyEngine.cs -- zero results in new code
  [ ] grep "DateTime\.Now" src/PropTraderTools/CopyEngine.cs -- zero results in new code
  [ ] grep "acc\.Submit" src/PropTraderTools/CopyEngine.cs -- zero results
  [ ] 3 new [Fact] tests in CopyEngineTests.cs passing: dotnet test
```

---

*END OF B33-LaneA DIFF PLAN*
